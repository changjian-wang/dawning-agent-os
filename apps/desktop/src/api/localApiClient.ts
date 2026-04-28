import type { HealthResponse, RuntimeStatusResponse } from '@/types/runtime';

interface RuntimeBridge {
  apiBaseUrl: string;
  startupToken: string;
}

const fallbackRuntime: RuntimeBridge = {
  apiBaseUrl: import.meta.env.VITE_AGENTOS_API_URL ?? 'http://127.0.0.1:5144',
  startupToken: import.meta.env.VITE_AGENTOS_STARTUP_TOKEN ?? '',
};

function getRuntime(): RuntimeBridge {
  return window.agentOS?.runtime ?? fallbackRuntime;
}

async function getJson<TResponse>(path: string): Promise<TResponse> {
  const runtime = getRuntime();
  const response = await fetch(`${runtime.apiBaseUrl}${path}`, {
    headers: {
      'X-Dawning-AgentOS-Startup-Token': runtime.startupToken,
    },
  });

  if (!response.ok) {
    throw new Error(`Local API request failed: ${response.status}`);
  }

  return (await response.json()) as TResponse;
}

export function getHealth(): Promise<HealthResponse> {
  return getJson<HealthResponse>('/health');
}

export function getRuntimeStatus(): Promise<RuntimeStatusResponse> {
  return getJson<RuntimeStatusResponse>('/runtime/status');
}