/**
 * Readiness probe.
 *
 * Per ADR-025 §4 the supervisor polls `/api/runtime/status` with the
 * startup token at 250ms intervals up to 30s. Success requires:
 *
 *   - HTTP 200
 *   - body.healthy === true
 *   - body.database.ready === true
 *   - body.database.schemaVersion >= 1
 *
 * The shape of the response is owned by ADR-024 and ADR-023; this module
 * only describes the subset it needs to validate readiness.
 */

import { HEADER_NAME } from "./spawn-backend.js";

export interface RuntimeStatusResponse {
  healthy?: boolean;
  database?: {
    ready?: boolean;
    schemaVersion?: number | null;
    filePath?: string | null;
  };
}

export interface ProbeOptions {
  baseUrl: string;
  token: string;
  /** Polling interval in milliseconds. Defaults to 250. */
  intervalMs?: number;
  /** Total timeout in milliseconds. Defaults to 30_000. */
  timeoutMs?: number;
}

export interface ProbeSuccess {
  status: RuntimeStatusResponse;
  durationMs: number;
}

/**
 * Poll until the API reports a fully ready state, or reject after the
 * timeout. Each individual fetch failure is swallowed and retried; only
 * the wrapping timeout can fail the probe.
 */
export async function probeUntilReady(options: ProbeOptions): Promise<ProbeSuccess> {
  const interval = options.intervalMs ?? 250;
  const timeout = options.timeoutMs ?? 30_000;
  const url = `${options.baseUrl.replace(/\/+$/u, "")}/api/runtime/status`;
  const start = Date.now();

  let lastError: unknown = undefined;
  while (Date.now() - start < timeout) {
    try {
      const response = await fetch(url, {
        headers: { [HEADER_NAME]: options.token },
      });
      if (response.ok) {
        const body = (await response.json()) as RuntimeStatusResponse;
        if (
          body.healthy === true &&
          body.database?.ready === true &&
          typeof body.database.schemaVersion === "number" &&
          body.database.schemaVersion >= 1
        ) {
          return { status: body, durationMs: Date.now() - start };
        }
      }
    } catch (err) {
      lastError = err;
    }
    await sleep(interval);
  }

  const reason = lastError instanceof Error ? `: ${lastError.message}` : "";
  throw new Error(
    `runtime status did not become ready within ${timeout}ms${reason}`,
  );
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
