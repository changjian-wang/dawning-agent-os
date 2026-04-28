import type { ChildProcessWithoutNullStreams } from 'node:child_process';
import { app, BrowserWindow } from 'electron';
import { startBackend } from './backend.js';
import { createMainWindow } from './window.js';

let backendProcess: ChildProcessWithoutNullStreams | undefined;

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('before-quit', () => {
  backendProcess?.kill();
});

await app.whenReady();

const backend = await startBackend();
backendProcess = backend.process;
await createMainWindow({ apiBaseUrl: backend.apiBaseUrl, startupToken: backend.startupToken });

app.on('activate', async () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    await createMainWindow({ apiBaseUrl: backend.apiBaseUrl, startupToken: backend.startupToken });
  }
});