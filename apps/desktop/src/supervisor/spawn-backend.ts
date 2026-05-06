/**
 * Cross-platform `dotnet` spawn helper for the Api host.
 *
 * Per ADR-025 §3 the dev path is `dotnet run --project <repo>/src/Dawning.AgentOS.Api`.
 * Per ADR-025 §3 the supervisor injects `Api__StartupToken__ExpectedToken`,
 * `ASPNETCORE_URLS=http://127.0.0.1:0` and `ASPNETCORE_ENVIRONMENT=Development`
 * via env so the child uses an OS-allocated port and the same token the
 * Electron renderer will later carry on every fetch.
 */

import { spawn, type ChildProcessByStdio } from "node:child_process";
import { existsSync } from "node:fs";
import type { Readable } from "node:stream";
import path from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Concrete child shape: we use `["ignore", "pipe", "pipe"]` so stdin is
 * null and stdout/stderr are readable streams. Aliased so callers don't
 * need to spell the generic out.
 */
export type BackendChildProcess = ChildProcessByStdio<null, Readable, Readable>;

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Resolve the absolute path to `src/Dawning.AgentOS.Api` relative to
 * this file. Hard-coded for the V0 monorepo layout (apps/desktop/src/supervisor/).
 */
export function resolveApiProjectPath(): string {
  // apps/desktop/src/supervisor/ → repo root is four levels up.
  const repoRoot = path.resolve(__dirname, "..", "..", "..", "..");
  return path.join(repoRoot, "src", "Dawning.AgentOS.Api");
}

/**
 * Resolve a runnable `dotnet` binary. On macOS / Linux the canonical
 * install path is `/usr/local/share/dotnet/dotnet` and is often missing
 * from GUI-launched processes' PATH; fall back through standard install
 * locations and then to the bare command name.
 */
export function resolveDotnetCommand(): string {
  const fromEnv = process.env["DOTNET_HOST_PATH"];
  if (fromEnv && existsSync(fromEnv)) {
    return fromEnv;
  }
  const dotnetRoot = process.env["DOTNET_ROOT"];
  if (dotnetRoot) {
    const candidate = path.join(dotnetRoot, "dotnet");
    if (existsSync(candidate)) {
      return candidate;
    }
  }
  const candidates = [
    "/usr/local/share/dotnet/dotnet",
    "/usr/share/dotnet/dotnet",
    "/opt/homebrew/share/dotnet/dotnet",
  ];
  for (const candidate of candidates) {
    if (existsSync(candidate)) {
      return candidate;
    }
  }
  // Fall back to PATH lookup; will fail loudly at spawn time if missing.
  return "dotnet";
}

/**
 * Per ADR-023 the API host reads its expected startup token from
 * configuration key `Api:StartupToken:ExpectedToken`. ASP.NET Core
 * configuration maps env-var double underscores to colons, so the env
 * key is `Api__StartupToken__ExpectedToken`.
 */
export const HEADER_NAME = "X-Startup-Token";

export interface SpawnOptions {
  /** The startup token the API host will require on every request. */
  token: string;
  /** When true, attaches stdout / stderr to the parent for visibility. Defaults to true. */
  inheritStdio?: boolean;
}

/**
 * Spawn `dotnet run --project <api>` as a child process. The child binds
 * `127.0.0.1:0` so the OS picks a free port; the actual port is reported
 * later through stdout (parsed by `port-handshake.ts`).
 */
export function spawnBackend(options: SpawnOptions): BackendChildProcess {
  const apiProject = resolveApiProjectPath();
  const dotnetCommand = resolveDotnetCommand();
  const args = ["run", "--project", apiProject, "--no-launch-profile"];
  const env: NodeJS.ProcessEnv = {
    ...process.env,
    Api__StartupToken__HeaderName: HEADER_NAME,
    Api__StartupToken__ExpectedToken: options.token,
    ASPNETCORE_URLS: "http://127.0.0.1:0",
    ASPNETCORE_ENVIRONMENT: "Development",
    DOTNET_NOLOGO: "true",
  };

  const child = spawn(dotnetCommand, args, {
    env,
    stdio: ["ignore", "pipe", "pipe"],
  });

  if (options.inheritStdio !== false) {
    // Mirror the child's logs so failures are diagnosable; the
    // port-handshake reads its own stream view in parallel.
    child.stdout.on("data", (chunk: Buffer) => {
      process.stdout.write(`[dotnet] ${chunk.toString()}`);
    });
    child.stderr.on("data", (chunk: Buffer) => {
      process.stderr.write(`[dotnet] ${chunk.toString()}`);
    });
  }

  return child;
}
