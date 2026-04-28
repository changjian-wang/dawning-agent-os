import { contextBridge } from 'electron';

function readArgument(name: string): string {
  const prefix = `${name}=`;
  return process.argv.find((argument) => argument.startsWith(prefix))?.slice(prefix.length) ?? '';
}

contextBridge.exposeInMainWorld('agentOS', {
  runtime: {
    apiBaseUrl: readArgument('--agentos-api-url'),
    startupToken: readArgument('--agentos-startup-token'),
  },
});