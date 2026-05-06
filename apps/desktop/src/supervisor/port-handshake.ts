/**
 * Stdout port handshake.
 *
 * Per ADR-025 §3 the supervisor extracts the runtime port from the
 * standard ASP.NET Core hosting line:
 *
 *   info: Microsoft.Hosting.Lifetime[14]
 *         Now listening on: http://127.0.0.1:51234
 *
 * The port appears in the second physical line of the log entry, so we
 * scan stdout buffer-by-buffer and match the `Now listening on` substring
 * regardless of where in the buffer it lands.
 */

import type { BackendChildProcess } from "./spawn-backend.js";

const LISTENING_PATTERN = /Now listening on:\s+(https?:\/\/[^\s]+)/i;

export interface ListeningInfo {
  /** The full base URL announced by Kestrel, e.g. `http://127.0.0.1:51234`. */
  baseUrl: string;
  /** The numeric port extracted from `baseUrl`. */
  port: number;
}

/**
 * Wait for the child to print its `Now listening on:` line. Resolves with
 * the parsed base URL or rejects if the child exits / errors / the
 * timeout elapses without a match.
 */
export function awaitListening(
  child: BackendChildProcess,
  timeoutMs: number = 30_000,
): Promise<ListeningInfo> {
  return new Promise((resolve, reject) => {
    let buffer = "";
    let settled = false;

    const finish = (
      action: () => void,
      cleanup: () => void,
    ): void => {
      if (settled) {
        return;
      }
      settled = true;
      cleanup();
      action();
    };

    const onData = (chunk: Buffer): void => {
      buffer += chunk.toString();
      const match = LISTENING_PATTERN.exec(buffer);
      if (match && match[1]) {
        const baseUrl = match[1].replace(/\/+$/u, "");
        const url = new URL(baseUrl);
        const port = Number(url.port);
        if (Number.isFinite(port) && port > 0) {
          finish(() => resolve({ baseUrl, port }), cleanup);
        }
      }
    };

    const onExit = (code: number | null, signal: NodeJS.Signals | null): void => {
      finish(
        () =>
          reject(
            new Error(
              `dotnet exited before announcing a port (code=${code ?? "null"}, signal=${
                signal ?? "null"
              })`,
            ),
          ),
        cleanup,
      );
    };

    const onError = (err: Error): void => {
      finish(() => reject(err), cleanup);
    };

    const timer = setTimeout(() => {
      finish(
        () =>
          reject(
            new Error(`timed out after ${timeoutMs}ms waiting for "Now listening on:" line`),
          ),
        cleanup,
      );
    }, timeoutMs);

    const cleanup = (): void => {
      clearTimeout(timer);
      child.stdout.off("data", onData);
      child.off("exit", onExit);
      child.off("error", onError);
    };

    child.stdout.on("data", onData);
    child.once("exit", onExit);
    child.once("error", onError);
  });
}
