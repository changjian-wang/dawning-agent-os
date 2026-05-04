/**
 * Electron main process entry.
 *
 * Per ADR-025 §1 / §3 / §4 / §5 / §6 this file:
 *
 *   1. Generates a startup token (crypto.randomUUID()).
 *   2. Spawns dotnet as a child process with the token + ASPNETCORE_URLS env.
 *   3. Waits for the child's `Now listening on:` line to learn the port.
 *   4. HTTP-polls /api/runtime/status until healthy + database.ready.
 *   5. Opens the BrowserWindow only after readiness; renderer fetches
 *      its status through the preload bridge.
 *   6. On window-all-closed, sends SIGTERM (10s grace, then SIGKILL) and
 *      quits Electron. process.exit / crash also runs the SIGTERM path.
 */

import { app, BrowserWindow, ipcMain } from "electron";
import { randomUUID } from "node:crypto";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { BackendChildProcess } from "./supervisor/spawn-backend.js";

import { spawnBackend, HEADER_NAME } from "./supervisor/spawn-backend.js";
import { awaitListening } from "./supervisor/port-handshake.js";
import { probeUntilReady } from "./supervisor/readiness-probe.js";
import { shutdownBackend } from "./supervisor/shutdown.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

interface RuntimeContext {
  child: BackendChildProcess;
  baseUrl: string;
  token: string;
}

let runtime: RuntimeContext | undefined;

async function startBackend(): Promise<RuntimeContext> {
  const token = randomUUID();
  const child = spawnBackend({ token });

  const onceProcessDies = () => {
    if (runtime?.child === child) {
      void shutdownBackend(child).then(() => {
        // best-effort; process is exiting anyway
      });
    }
  };
  process.once("exit", onceProcessDies);
  process.once("SIGINT", onceProcessDies);
  process.once("SIGTERM", onceProcessDies);

  const listening = await awaitListening(child, 30_000);
  const ready = await probeUntilReady({
    baseUrl: listening.baseUrl,
    token,
    intervalMs: 250,
    timeoutMs: 30_000,
  });
  console.log(
    `[main] api ready in ${ready.durationMs}ms (schemaVersion=${ready.status.database?.schemaVersion ?? "?"})`,
  );

  return { child, baseUrl: listening.baseUrl, token };
}

function createWindow(context: RuntimeContext): BrowserWindow {
  const window = new BrowserWindow({
    width: 720,
    height: 600,
    title: "Dawning Agent OS — V0",
    webPreferences: {
      // Per ADR-027 §2 preload is precompiled via tsconfig.preload.json
      // to dist/preload.cjs.js. main.ts is also precompiled to dist/main.js
      // via tsconfig.electron.json, so the preload sits in the same dir.
      preload: path.join(__dirname, "preload.cjs.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      additionalArguments: [
        `--agentos-base-url=${context.baseUrl}`,
        `--agentos-token=${context.token}`,
      ],
    },
  });

  void window.loadFile(path.join(__dirname, "renderer", "index.html"));
  return window;
}

// Register an IPC handler so the preload bridge can ask main for a
// fresh runtime status without exposing the token to the renderer
// process directly. The token lives in the main process only.
ipcMain.handle("agentos:runtime:get-status", async () => {
  if (!runtime) {
    throw new Error("runtime not initialized");
  }
  const response = await fetch(`${runtime.baseUrl}/api/runtime/status`, {
    headers: { [HEADER_NAME]: runtime.token },
  });
  return response.json();
});

ipcMain.handle("agentos:runtime:get-base-url", () => {
  return runtime?.baseUrl ?? "";
});

// Per ADR-027 §4 / §5 the renderer calls these IPC channels via the
// preload bridge `window.agentos.inbox.{capture,list}`. The token
// stays in main; renderer never sees it.

interface InboxIpcOk<T> {
  ok: true;
  value: T;
}

interface InboxIpcErr {
  ok: false;
  status: number;
  problem: unknown;
}

type InboxIpcResult<T> = InboxIpcOk<T> | InboxIpcErr;

async function readBodyAsResult<T>(response: Response): Promise<InboxIpcResult<T>> {
  if (response.ok) {
    const value = (await response.json()) as T;
    return { ok: true, value };
  }
  let problem: unknown = null;
  try {
    problem = await response.json();
  } catch {
    problem = { title: response.statusText };
  }
  return { ok: false, status: response.status, problem };
}

ipcMain.handle(
  "agentos:inbox:capture",
  async (_event, req: { content?: unknown; source?: unknown }) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    const body = JSON.stringify({
      content: typeof req?.content === "string" ? req.content : "",
      source: typeof req?.source === "string" ? req.source : null,
    });
    const response = await fetch(`${runtime.baseUrl}/api/inbox`, {
      method: "POST",
      headers: {
        [HEADER_NAME]: runtime.token,
        "Content-Type": "application/json",
      },
      body,
    });
    return readBodyAsResult(response);
  },
);

ipcMain.handle(
  "agentos:inbox:list",
  async (_event, query: { limit?: unknown; offset?: unknown }) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    const limit = typeof query?.limit === "number" && Number.isFinite(query.limit) ? query.limit : 50;
    const offset = typeof query?.offset === "number" && Number.isFinite(query.offset) ? query.offset : 0;
    const url = `${runtime.baseUrl}/api/inbox?limit=${limit}&offset=${offset}`;
    const response = await fetch(url, {
      headers: { [HEADER_NAME]: runtime.token },
    });
    return readBodyAsResult(response);
  },
);

// Per ADR-030 §G1 the renderer surfaces a per-item "summarize" button
// that POSTs to /api/inbox/items/{id:guid}/summarize. The token stays
// in the main process; the renderer only ships an itemId string.
ipcMain.handle(
  "agentos:inbox:summarize",
  async (_event, itemId: unknown) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    if (typeof itemId !== "string" || itemId.length === 0) {
      throw new Error("itemId must be a non-empty string");
    }
    // GUID route constraint on the server already rejects malformed
    // ids; we only screen for empty / non-string here so the IPC layer
    // doesn't forward obviously broken inputs.
    const response = await fetch(
      `${runtime.baseUrl}/api/inbox/items/${encodeURIComponent(itemId)}/summarize`,
      {
        method: "POST",
        headers: { [HEADER_NAME]: runtime.token },
      },
    );
    return readBodyAsResult(response);
  },
);

app.whenReady().then(async () => {
  try {
    runtime = await startBackend();
  } catch (err) {
    console.error("[main] failed to start backend:", err);
    app.exit(1);
    return;
  }
  createWindow(runtime);
});

app.on("window-all-closed", () => {
  void (async () => {
    if (runtime) {
      await shutdownBackend(runtime.child, 10_000);
    }
    app.quit();
  })();
});
