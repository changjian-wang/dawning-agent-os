/**
 * Graceful shutdown of the dotnet child.
 *
 * Per ADR-025 §6 the supervisor sends SIGTERM, waits up to 10s, then
 * escalates to SIGKILL. The returned promise resolves when the child has
 * exited (regardless of signal) so callers can sequence `app.quit()`
 * after shutdown.
 */

import type { BackendChildProcess } from "./spawn-backend.js";

const DEFAULT_GRACE_MS = 10_000;

/**
 * Send SIGTERM to the child; if still alive after `graceMs`, escalate
 * to SIGKILL. Returns when the child has exited. Safe to call multiple
 * times.
 */
export function shutdownBackend(
  child: BackendChildProcess,
  graceMs: number = DEFAULT_GRACE_MS,
): Promise<void> {
  if (child.exitCode !== null || child.signalCode !== null) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    let resolved = false;
    const finish = (): void => {
      if (resolved) {
        return;
      }
      resolved = true;
      clearTimeout(killTimer);
      resolve();
    };

    child.once("exit", finish);

    try {
      child.kill("SIGTERM");
    } catch {
      // already dead — let the exit handler / timer wrap up
    }

    const killTimer = setTimeout(() => {
      if (resolved) {
        return;
      }
      try {
        child.kill("SIGKILL");
      } catch {
        // already dead
      }
    }, graceMs);
  });
}
