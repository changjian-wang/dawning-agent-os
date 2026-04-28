import { ChildProcessWithoutNullStreams, spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { createServer } from 'node:net';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { app } from 'electron';

export interface BackendRuntime {
  apiBaseUrl: string;
  startupToken: string;
  process: ChildProcessWithoutNullStreams;
}

export async function startBackend(): Promise<BackendRuntime> {
  const port = await getAvailablePort();
  const startupToken = randomBytes(32).toString('hex');
  const apiBaseUrl = `http://127.0.0.1:${port}`;
  const apiProjectPath = resolveRepositoryPath('src/Dawning.AgentOS.Api/Dawning.AgentOS.Api.csproj');
  const dotnetExecutable = process.env.DAWNING_AGENT_OS_DOTNET_PATH ?? 'dotnet';
  const childProcess = spawn(
    dotnetExecutable,
    ['run', '--project', apiProjectPath, '--no-launch-profile', '--urls', apiBaseUrl],
    {
      env: {
        ...process.env,
        DAWNING_AGENT_OS_STARTUP_TOKEN: startupToken,
        Storage__DataDirectory: app.getPath('userData'),
      },
      stdio: 'pipe',
      windowsHide: true,
    },
  );

  childProcess.stdout.on('data', (data: Buffer) => console.info(`[agentos-api] ${data.toString().trim()}`));
  childProcess.stderr.on('data', (data: Buffer) => console.error(`[agentos-api] ${data.toString().trim()}`));

  return { apiBaseUrl, startupToken, process: childProcess };
}

function resolveRepositoryPath(relativePath: string): string {
  if (app.isPackaged) {
    return resolve(process.resourcesPath, relativePath);
  }

  const currentDirectory = dirname(fileURLToPath(import.meta.url));
  return resolve(currentDirectory, '../..', relativePath);
}

function getAvailablePort(): Promise<number> {
  return new Promise((resolvePort, reject) => {
    const server = createServer();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (address === null || typeof address === 'string') {
        server.close(() => reject(new Error('Could not allocate a local API port.')));
        return;
      }

      const { port } = address;
      server.close(() => resolvePort(port));
    });
  });
}