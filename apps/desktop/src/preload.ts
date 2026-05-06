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

/**
 * Server-side projection of the ADR-031 InboxItemTags record. Field
 * names mirror Api `InboxItemTagsResponse`. `tags` is the post
 * AppService normalization list (1-5 open-set Chinese tags, 2-12 chars
 * each, deduped); the renderer can render verbatim.
 */
export interface InboxItemTags {
  itemId: string;
  tags: string[];
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

/**
 * Read-side projection of the ADR-032 ChatSession aggregate. Field
 * names mirror Api `ChatSessionDto` (`createdAt` / `updatedAt` —
 * not `*Utc`; the JSON serializer uses the C# property name verbatim).
 */
export interface ChatSession {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChatSessionListPage {
  items: ChatSession[];
  limit: number;
  offset: number;
}

export interface ChatMessage {
  id: string;
  sessionId: string;
  /** "user" or "assistant" — see ADR-032 §C1. */
  role: "user" | "assistant";
  content: string;
  createdAt: string;
  model: string | null;
  promptTokens: number | null;
  completionTokens: number | null;
}

export interface ChatCreateSessionResponse {
  session: ChatSession;
}

/**
 * Frame shape forwarded from main's SSE parser to the renderer via
 * `webContents.send`. Per ADR-032 §决策 H1 there are exactly three
 * frame kinds — `chunk`, `done`, `error`. The discriminator is the
 * SSE `event:` line; the per-frame fields are extracted from the
 * `data:` JSON payload.
 */
/**
 * Server-side projection of the ADR-033 MemoryLedgerEntry aggregate.
 * Per ADR-033 §决策 J1 / ADR-023 layering rule the Api ships
 * <c>source / sensitivity / status</c> as PascalCase strings (not
 * Domain enums) so the wire contract is decoupled from CLR int
 * values. The renderer renders these strings verbatim. <c>deletedAt</c>
 * is non-null exactly when <c>status === "SoftDeleted"</c>.
 */
export interface MemoryEntry {
  id: string;
  content: string;
  scope: string;
  /** ADR-033 §决策 B1: V0 only writes "UserExplicit"; future revisions may add Conversation / InboxAction / Correction. */
  source: string;
  isExplicit: boolean;
  confidence: number;
  /** "Normal" / "Sensitive" / "HighSensitive" — ADR-033 §决策 D1. */
  sensitivity: string;
  /** "Active" / "Corrected" / "Archived" / "SoftDeleted" — ADR-033 §决策 E1. */
  status: string;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
}

export interface MemoryListPage {
  items: MemoryEntry[];
  total: number;
  limit: number;
  offset: number;
}

export type ChatStreamFrame =
  | { kind: "chunk"; delta: string }
  | {
      kind: "done";
      model: string | null;
      promptTokens: number | null;
      completionTokens: number | null;
      durationMs: number;
    }
  | { kind: "error"; code: string; message: string };

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
    /**
     * POST /api/inbox/items/{id}/tags via main; per ADR-031 each call
     * invokes the active LLM provider with a JSON-array prompt. Non-
     * idempotent — calling twice may return different tag sets. The
     * value's `tags` array is already normalized (deduped, length-
     * filtered, capped at 5) by the server.
     */
    suggestTags: async (itemId: string): Promise<InboxIpcResult<InboxItemTags>> => {
      return (await ipcRenderer.invoke(
        "agentos:inbox:suggestTags",
        itemId,
      )) as InboxIpcResult<InboxItemTags>;
    },
  },
  chat: {
    /** GET /api/chat/sessions via main; ordered by updated_at DESC. */
    listSessions: async (
      query: { limit?: number; offset?: number } = {},
    ): Promise<InboxIpcResult<ChatSessionListPage>> => {
      return (await ipcRenderer.invoke(
        "agentos:chat:list-sessions",
        query,
      )) as InboxIpcResult<ChatSessionListPage>;
    },
    /** POST /api/chat/sessions via main; creates an empty session. */
    createSession: async (): Promise<InboxIpcResult<ChatCreateSessionResponse>> => {
      return (await ipcRenderer.invoke(
        "agentos:chat:create-session",
      )) as InboxIpcResult<ChatCreateSessionResponse>;
    },
    /** GET /api/chat/sessions/{id}/messages via main; full history in send order. */
    listMessages: async (sessionId: string): Promise<InboxIpcResult<ChatMessage[]>> => {
      return (await ipcRenderer.invoke(
        "agentos:chat:list-messages",
        sessionId,
      )) as InboxIpcResult<ChatMessage[]>;
    },
    /**
     * POST /api/chat/sessions/{id}/messages via main; opens an SSE
     * stream that surfaces here as a sequence of <see cref="ChatStreamFrame"/>
     * delivered through the supplied <paramref name="onFrame"/>
     * callback. The promise resolves when the stream ends; an `ok=false`
     * result indicates a pre-stream failure (validation / 404 / auth)
     * with no frames delivered. Mid-stream LLM failures arrive as a
     * single <c>kind: "error"</c> frame and the promise still resolves
     * <c>ok=true</c> — callers should track stream state from the frame
     * sequence, not from the resolved Result.
     */
    sendMessage: async (
      sessionId: string,
      content: string,
      onFrame: (frame: ChatStreamFrame) => void,
    ): Promise<InboxIpcResult<void>> => {
      // The correlation id keeps multiple concurrent streams independent
      // even though V0 only ever drives one at a time — it costs us
      // nothing and removes a "if you ever introduce parallelism here
      // you'll regret not having ids" footgun.
      const requestId =
        typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
          ? crypto.randomUUID()
          : `chat-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      const channel = `agentos:chat:frame:${requestId}`;
      const handler = (_event: unknown, frame: ChatStreamFrame): void => {
        try {
          onFrame(frame);
        } catch (err) {
          // Swallow renderer-side errors so a buggy callback can't
          // freeze the IPC channel; the next frame still arrives.
          console.error("[preload] chat onFrame handler threw:", err);
        }
      };
      ipcRenderer.on(channel, handler);
      try {
        return (await ipcRenderer.invoke("agentos:chat:send-message", {
          sessionId,
          content,
          requestId,
        })) as InboxIpcResult<void>;
      } finally {
        ipcRenderer.off(channel, handler);
      }
    },
  },
  /**
   * ADR-033 Memory Ledger V0 — six fetch-and-forward methods that
   * mirror the inbox / chat surface. The startup token never leaves
   * main; renderer only ships the request payload. <c>create / list /
   * getById / update / softDelete</c> wrap the matching REST verb
   * one-to-one; <c>restore</c> is sugar for <c>update(id, { status:
   * "Active" })</c> so the renderer can keep its "show soft-deleted"
   * code path readable.
   */
  memory: {
    /** POST /api/memory via main; only Source=UserExplicit is accepted server-side. */
    create: async (req: {
      content: string;
      scope?: string;
      sensitivity?: string;
    }): Promise<InboxIpcResult<MemoryEntry>> => {
      return (await ipcRenderer.invoke(
        "agentos:memory:create",
        req,
      )) as InboxIpcResult<MemoryEntry>;
    },
    /**
     * GET /api/memory via main; ordered by updated_at_utc DESC, id DESC.
     * <c>status</c> filter is "Active" / "Corrected" / "Archived" /
     * "SoftDeleted" or omitted; <c>includeSoftDeleted</c> defaults to
     * false, so the renderer must opt in to see deleted rows.
     */
    list: async (
      query: {
        limit?: number;
        offset?: number;
        status?: string;
        includeSoftDeleted?: boolean;
      } = {},
    ): Promise<InboxIpcResult<MemoryListPage>> => {
      return (await ipcRenderer.invoke(
        "agentos:memory:list",
        query,
      )) as InboxIpcResult<MemoryListPage>;
    },
    /** GET /api/memory/{id} via main; 404 surfaces as ok=false / status=404. */
    getById: async (id: string): Promise<InboxIpcResult<MemoryEntry>> => {
      return (await ipcRenderer.invoke(
        "agentos:memory:get-by-id",
        id,
      )) as InboxIpcResult<MemoryEntry>;
    },
    /**
     * PATCH /api/memory/{id} via main. All four fields are optional;
     * passing all-null returns <c>memory.update.empty</c> (HTTP 400)
     * via the AppService.
     */
    update: async (
      id: string,
      req: {
        content?: string;
        scope?: string;
        sensitivity?: string;
        status?: string;
      },
    ): Promise<InboxIpcResult<MemoryEntry>> => {
      return (await ipcRenderer.invoke("agentos:memory:update", {
        id,
        request: req,
      })) as InboxIpcResult<MemoryEntry>;
    },
    /** DELETE /api/memory/{id} via main; soft-delete (status → SoftDeleted, deletedAt stamped). */
    softDelete: async (id: string): Promise<InboxIpcResult<MemoryEntry>> => {
      return (await ipcRenderer.invoke(
        "agentos:memory:soft-delete",
        id,
      )) as InboxIpcResult<MemoryEntry>;
    },
    /**
     * Sugar over <c>update(id, { status: "Active" })</c>; the server
     * enforces the SoftDeleted → Active transition rule. Useful so the
     * renderer can keep "Restore" button code separate from generic
     * edit code.
     */
    restore: async (id: string): Promise<InboxIpcResult<MemoryEntry>> => {
      return (await ipcRenderer.invoke(
        "agentos:memory:restore",
        id,
      )) as InboxIpcResult<MemoryEntry>;
    },
  },
};

contextBridge.exposeInMainWorld("agentos", api);

export type AgentOsBridge = typeof api;
