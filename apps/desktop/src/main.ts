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
    // Per ADR-032 §决策 A1 the renderer is now a 65/35 split with a
    // chat pane that needs a comfortable minimum width (~480px) plus
    // an inbox pane that needs ≥320px. The 1100×720 default leaves
    // both panes above their min-width with room to spare.
    width: 1100,
    height: 720,
    minWidth: 880,
    minHeight: 560,
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

// Per ADR-031 §G1 a sister "tag" button POSTs to
// /api/inbox/items/{id:guid}/tags and surfaces a 1-5-element tag
// array. Same token / non-idempotency story as summarize.
ipcMain.handle(
  "agentos:inbox:suggestTags",
  async (_event, itemId: unknown) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    if (typeof itemId !== "string" || itemId.length === 0) {
      throw new Error("itemId must be a non-empty string");
    }
    const response = await fetch(
      `${runtime.baseUrl}/api/inbox/items/${encodeURIComponent(itemId)}/tags`,
      {
        method: "POST",
        headers: { [HEADER_NAME]: runtime.token },
      },
    );
    return readBodyAsResult(response);
  },
);

// =====================================================================
// Chat V0 IPC handlers — per ADR-032 §决策 G1 / H1.
//
// `list-sessions` / `create-session` / `list-messages` are plain
// fetch-and-forward handlers. `send-message` is special: the server
// returns an SSE stream, so main parses each `event: NAME\ndata: JSON`
// frame and forwards them to the renderer via webContents.send so the
// renderer can render deltas as they arrive.
// =====================================================================

ipcMain.handle(
  "agentos:chat:list-sessions",
  async (_event, query: { limit?: unknown; offset?: unknown }) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    const limit =
      typeof query?.limit === "number" && Number.isFinite(query.limit)
        ? query.limit
        : 50;
    const offset =
      typeof query?.offset === "number" && Number.isFinite(query.offset)
        ? query.offset
        : 0;
    const url = `${runtime.baseUrl}/api/chat/sessions?limit=${limit}&offset=${offset}`;
    const response = await fetch(url, {
      headers: { [HEADER_NAME]: runtime.token },
    });
    return readBodyAsResult(response);
  },
);

ipcMain.handle("agentos:chat:create-session", async () => {
  if (!runtime) {
    throw new Error("runtime not initialized");
  }
  const response = await fetch(`${runtime.baseUrl}/api/chat/sessions`, {
    method: "POST",
    headers: { [HEADER_NAME]: runtime.token },
  });
  return readBodyAsResult(response);
});

ipcMain.handle(
  "agentos:chat:list-messages",
  async (_event, sessionId: unknown) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    if (typeof sessionId !== "string" || sessionId.length === 0) {
      throw new Error("sessionId must be a non-empty string");
    }
    const response = await fetch(
      `${runtime.baseUrl}/api/chat/sessions/${encodeURIComponent(sessionId)}/messages`,
      { headers: { [HEADER_NAME]: runtime.token } },
    );
    return readBodyAsResult(response);
  },
);

interface ChatStreamFrame {
  kind: "chunk" | "done" | "error";
  [field: string]: unknown;
}

/**
 * Parses an SSE byte stream coming from
 * `POST /api/chat/sessions/{id}/messages` and forwards each frame to
 * <paramref name="onFrame"/>. Per ADR-032 §决策 H1 each frame is
 * exactly one `event: NAME\n` line followed by one `data: JSON\n` line
 * and a blank-line terminator. We support
 * <list type="bullet">
///   <item><description>CRLF or LF line endings (we normalize early).</description></item>
///   <item><description>SSE comment lines (lines starting with ":") which we skip.</description></item>
///   <item><description>Multi-line `data:` segments (joined with \n) — defensive only; the server emits single-line JSON.</description></item>
/// </list>
 * Frames whose `data:` body fails JSON parsing are dropped silently
 * (logged); the next frame still ships.
 */
async function streamSseFrames(
  body: ReadableStream<Uint8Array>,
  onFrame: (frame: ChatStreamFrame) => void,
): Promise<void> {
  const reader = body.getReader();
  const decoder = new TextDecoder("utf-8");
  let buffer = "";
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }
      // Stream-decode preserves multi-byte UTF-8 boundaries across reads.
      buffer += decoder.decode(value, { stream: true });
      buffer = buffer.replace(/\r\n/gu, "\n");

      // Process complete frames terminated by a blank line. Anything
      // after the last terminator stays in the buffer for the next read.
      let separatorIndex: number;
      while ((separatorIndex = buffer.indexOf("\n\n")) !== -1) {
        const rawFrame = buffer.slice(0, separatorIndex);
        buffer = buffer.slice(separatorIndex + 2);
        const parsed = parseSseFrame(rawFrame);
        if (parsed !== null) {
          onFrame(parsed);
        }
      }
    }
    // Flush any remaining decoder state and process a final frame
    // that may have arrived without a trailing blank line. Servers
    // that close the stream cleanly usually do emit it, but we don't
    // want to drop the `done` payload if a buggy proxy strips it.
    buffer += decoder.decode();
    buffer = buffer.replace(/\r\n/gu, "\n");
    if (buffer.trim().length > 0) {
      const parsed = parseSseFrame(buffer);
      if (parsed !== null) {
        onFrame(parsed);
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function parseSseFrame(rawFrame: string): ChatStreamFrame | null {
  let eventName: string | null = null;
  const dataLines: string[] = [];
  for (const line of rawFrame.split("\n")) {
    if (line.length === 0 || line.startsWith(":")) {
      // SSE: blank lines inside a frame are impossible (\n\n already
      // ended the frame above), and ":"-prefixed lines are comments.
      continue;
    }
    if (line.startsWith("event:")) {
      eventName = line.slice("event:".length).trimStart();
    } else if (line.startsWith("data:")) {
      dataLines.push(line.slice("data:".length).trimStart());
    }
    // `id:` and `retry:` lines are unused in V0 (ADR-032 §决策 H1) —
    // we deliberately drop them.
  }

  if (eventName === null || dataLines.length === 0) {
    return null;
  }
  if (eventName !== "chunk" && eventName !== "done" && eventName !== "error") {
    return null;
  }

  let payload: unknown;
  try {
    payload = JSON.parse(dataLines.join("\n"));
  } catch (err) {
    console.warn(`[main] failed to parse SSE frame data for event '${eventName}':`, err);
    return null;
  }
  if (typeof payload !== "object" || payload === null) {
    return null;
  }
  return { kind: eventName, ...(payload as Record<string, unknown>) };
}

ipcMain.handle(
  "agentos:chat:send-message",
  async (
    event,
    req: { sessionId?: unknown; content?: unknown; requestId?: unknown },
  ): Promise<InboxIpcResult<null>> => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    if (typeof req?.sessionId !== "string" || req.sessionId.length === 0) {
      throw new Error("sessionId must be a non-empty string");
    }
    if (typeof req?.requestId !== "string" || req.requestId.length === 0) {
      throw new Error("requestId must be a non-empty string");
    }
    const sessionId = req.sessionId;
    const requestId = req.requestId;
    const content = typeof req.content === "string" ? req.content : "";

    const response = await fetch(
      `${runtime.baseUrl}/api/chat/sessions/${encodeURIComponent(sessionId)}/messages`,
      {
        method: "POST",
        headers: {
          [HEADER_NAME]: runtime.token,
          "Content-Type": "application/json",
          // Hint to the server we only speak SSE on this route; harmless
          // if ignored, useful for debugging via curl.
          Accept: "text/event-stream",
        },
        body: JSON.stringify({ content }),
      },
    );

    if (!response.ok) {
      // Pre-stream failure (validation / 404 / auth). Body is JSON
      // ProblemDetails; reuse the inbox helper to keep error shape
      // consistent across surfaces.
      return (await readBodyAsResult<null>(response)) as InboxIpcResult<null>;
    }
    if (response.body === null) {
      return {
        ok: false,
        status: 502,
        problem: { title: "Empty response body from streaming endpoint." },
      };
    }

    const channel = `agentos:chat:frame:${requestId}`;
    try {
      await streamSseFrames(response.body, (frame) => {
        if (event.sender.isDestroyed()) {
          // BrowserWindow was closed mid-stream; further sends would
          // throw. Silently stop forwarding; the await foreach above
          // will keep draining so the server connection closes cleanly.
          return;
        }
        event.sender.send(channel, frame);
      });
    } catch (err) {
      console.error("[main] chat SSE forwarding failed:", err);
      return {
        ok: false,
        status: 502,
        problem: {
          title: "SSE stream interrupted.",
          detail: err instanceof Error ? err.message : String(err),
        },
      };
    }

    return { ok: true, value: null };
  },
);

// =====================================================================
// Memory Ledger V0 IPC handlers — per ADR-033 §决策 J1 / K1.
//
// Six fetch-and-forward handlers mirroring the inbox layout. The
// renderer ships request payloads; main attaches the startup token and
// returns the parsed JSON Result. <c>restore</c> is sugar for "PATCH
// status=Active" so the renderer's "show soft-deleted" UI can call a
// dedicated channel and keep its branching readable.
// =====================================================================

ipcMain.handle(
  "agentos:memory:create",
  async (
    _event,
    req: { content?: unknown; scope?: unknown; sensitivity?: unknown },
  ) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    const body = JSON.stringify({
      content: typeof req?.content === "string" ? req.content : "",
      scope: typeof req?.scope === "string" ? req.scope : null,
      sensitivity: typeof req?.sensitivity === "string" ? req.sensitivity : null,
    });
    const response = await fetch(`${runtime.baseUrl}/api/memory`, {
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
  "agentos:memory:list",
  async (
    _event,
    query: {
      limit?: unknown;
      offset?: unknown;
      status?: unknown;
      includeSoftDeleted?: unknown;
    },
  ) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    const limit =
      typeof query?.limit === "number" && Number.isFinite(query.limit)
        ? query.limit
        : 50;
    const offset =
      typeof query?.offset === "number" && Number.isFinite(query.offset)
        ? query.offset
        : 0;
    const params = new URLSearchParams();
    params.set("limit", String(limit));
    params.set("offset", String(offset));
    if (typeof query?.status === "string" && query.status.length > 0) {
      params.set("status", query.status);
    }
    if (query?.includeSoftDeleted === true) {
      params.set("includeSoftDeleted", "true");
    }
    const url = `${runtime.baseUrl}/api/memory?${params.toString()}`;
    const response = await fetch(url, {
      headers: { [HEADER_NAME]: runtime.token },
    });
    return readBodyAsResult(response);
  },
);

ipcMain.handle("agentos:memory:get-by-id", async (_event, id: unknown) => {
  if (!runtime) {
    throw new Error("runtime not initialized");
  }
  if (typeof id !== "string" || id.length === 0) {
    throw new Error("id must be a non-empty string");
  }
  const response = await fetch(
    `${runtime.baseUrl}/api/memory/${encodeURIComponent(id)}`,
    { headers: { [HEADER_NAME]: runtime.token } },
  );
  return readBodyAsResult(response);
});

ipcMain.handle(
  "agentos:memory:update",
  async (
    _event,
    payload: {
      id?: unknown;
      request?: {
        content?: unknown;
        scope?: unknown;
        sensitivity?: unknown;
        status?: unknown;
      };
    },
  ) => {
    if (!runtime) {
      throw new Error("runtime not initialized");
    }
    if (typeof payload?.id !== "string" || payload.id.length === 0) {
      throw new Error("id must be a non-empty string");
    }
    const req = payload.request ?? {};
    const body = JSON.stringify({
      content: typeof req?.content === "string" ? req.content : null,
      scope: typeof req?.scope === "string" ? req.scope : null,
      sensitivity: typeof req?.sensitivity === "string" ? req.sensitivity : null,
      status: typeof req?.status === "string" ? req.status : null,
    });
    const response = await fetch(
      `${runtime.baseUrl}/api/memory/${encodeURIComponent(payload.id)}`,
      {
        method: "PATCH",
        headers: {
          [HEADER_NAME]: runtime.token,
          "Content-Type": "application/json",
        },
        body,
      },
    );
    return readBodyAsResult(response);
  },
);

ipcMain.handle("agentos:memory:soft-delete", async (_event, id: unknown) => {
  if (!runtime) {
    throw new Error("runtime not initialized");
  }
  if (typeof id !== "string" || id.length === 0) {
    throw new Error("id must be a non-empty string");
  }
  const response = await fetch(
    `${runtime.baseUrl}/api/memory/${encodeURIComponent(id)}`,
    {
      method: "DELETE",
      headers: { [HEADER_NAME]: runtime.token },
    },
  );
  return readBodyAsResult(response);
});

// Restore is "PATCH status=Active". The server enforces the
// SoftDeleted → Active transition rule (ADR-033 §决策 E1); a restore
// against a non-soft-deleted entry no-ops on the server side.
ipcMain.handle("agentos:memory:restore", async (_event, id: unknown) => {
  if (!runtime) {
    throw new Error("runtime not initialized");
  }
  if (typeof id !== "string" || id.length === 0) {
    throw new Error("id must be a non-empty string");
  }
  const response = await fetch(
    `${runtime.baseUrl}/api/memory/${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: {
        [HEADER_NAME]: runtime.token,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        content: null,
        scope: null,
        sensitivity: null,
        status: "Active",
      }),
    },
  );
  return readBodyAsResult(response);
});

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
