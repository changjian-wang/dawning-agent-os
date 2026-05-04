/**
 * Preload bridge — runs in the renderer's isolated world.
 *
 * Per ADR-025 §3 / ADR-027 §3 the renderer never sees the startup token
 * directly; instead it asks the main process via IPC for runtime data
 * and inbox operations, and main attaches the token on its way out.
 * This keeps the token in the main process and out of the BrowserWindow
 * JS context.
 *
 * Per ADR-027 §2 this file is precompiled via `tsconfig.preload.json`
 * to `dist/preload.cjs.js` and main.ts loads it from there. tsx does
 * NOT participate in Electron's preload load chain — Electron's
 * BrowserWindow uses its own loader, which expects a real CommonJS
 * file on disk.
 */

import { contextBridge, ipcRenderer } from "electron";

interface RuntimeStatus {
  healthy?: boolean;
  database?: {
    ready?: boolean;
    schemaVersion?: number | null;
    filePath?: string | null;
  };
  [key: string]: unknown;
}

export interface InboxItemSnapshot {
  id: string;
  content: string;
  source: string | null;
  capturedAtUtc: string;
  createdAt: string;
}

export interface InboxListPage {
  items: InboxItemSnapshot[];
  total: number;
  limit: number;
  offset: number;
}

/**
 * Server-side projection of the ADR-030 InboxItemSummary record. Field
 * names mirror Api `InboxItemSummaryResponse` (camelCase by JSON
 * convention). `promptTokens` / `completionTokens` are nullable per
 * ADR-028 §G1 (some providers don't return usage).
 */
export interface InboxItemSummary {
  itemId: string;
  summary: string;
  model: string;
  promptTokens: number | null;
  completionTokens: number | null;
  durationMs: number;
}

export interface InboxProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export type InboxIpcResult<T> =
  | { ok: true; value: T }
  | { ok: false; status: number; problem: InboxProblem };

const api = {
  runtime: {
    /** Returns the current /api/runtime/status JSON; main attaches the token. */
    getStatus: async (): Promise<RuntimeStatus> => {
      return (await ipcRenderer.invoke("agentos:runtime:get-status")) as RuntimeStatus;
    },
    /** Returns the base URL without exposing the token. */
    getBaseUrl: async (): Promise<string> => {
      return (await ipcRenderer.invoke("agentos:runtime:get-base-url")) as string;
    },
  },
  inbox: {
    /** POST /api/inbox via main; token attached by main, never seen here. */
    capture: async (req: {
      content: string;
      source?: string;
    }): Promise<InboxIpcResult<InboxItemSnapshot>> => {
      return (await ipcRenderer.invoke(
        "agentos:inbox:capture",
        req,
      )) as InboxIpcResult<InboxItemSnapshot>;
    },
    /** GET /api/inbox via main; token attached by main, never seen here. */
    list: async (
      query: { limit?: number; offset?: number } = {},
    ): Promise<InboxIpcResult<InboxListPage>> => {
      return (await ipcRenderer.invoke(
        "agentos:inbox:list",
        query,
      )) as InboxIpcResult<InboxListPage>;
    },
    /**
     * POST /api/inbox/items/{id}/summarize via main; per ADR-030 each
     * call invokes the active LLM provider. Non-idempotent — calling
     * twice may return different summaries.
     */
    summarize: async (itemId: string): Promise<InboxIpcResult<InboxItemSummary>> => {
      return (await ipcRenderer.invoke(
        "agentos:inbox:summarize",
        itemId,
      )) as InboxIpcResult<InboxItemSummary>;
    },
  },
};

contextBridge.exposeInMainWorld("agentos", api);

export type AgentOsBridge = typeof api;
