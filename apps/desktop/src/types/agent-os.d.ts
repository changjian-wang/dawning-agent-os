export interface AgentOSRuntimeBridge {
  apiBaseUrl: string;
  startupToken: string;
}

declare global {
  interface Window {
    agentOS?: {
      runtime: AgentOSRuntimeBridge;
    };
  }
}

export {};