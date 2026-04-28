import { BrowserWindow } from 'electron';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export interface RuntimeWindowOptions {
  apiBaseUrl: string;
  startupToken: string;
}

export async function createMainWindow(options: RuntimeWindowOptions): Promise<BrowserWindow> {
  const currentDirectory = dirname(fileURLToPath(import.meta.url));
  const window = new BrowserWindow({
    width: 1180,
    height: 760,
    minWidth: 960,
    minHeight: 640,
    title: 'Dawning Agent OS',
    webPreferences: {
      preload: join(currentDirectory, '../preload/index.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      additionalArguments: [
        `--agentos-api-url=${options.apiBaseUrl}`,
        `--agentos-startup-token=${options.startupToken}`,
      ],
    },
  });

  const rendererUrl = process.env.ELECTRON_RENDERER_URL;
  if (rendererUrl) {
    await window.loadURL(rendererUrl);
  } else {
    await window.loadFile(join(currentDirectory, '../../dist/index.html'));
  }

  return window;
}