import { defineStore } from 'pinia';

import { getHealth, getRuntimeStatus } from '@/api/localApiClient';
import type { HealthResponse, RuntimeStatusResponse } from '@/types/runtime';

type ConnectionState = 'checking' | 'connected' | 'disconnected';

interface RuntimeState {
  connectionState: ConnectionState;
  loading: boolean;
  error: string;
  health: HealthResponse | null;
  status: RuntimeStatusResponse | null;
}

export const useRuntimeStore = defineStore('runtime', {
  state: (): RuntimeState => ({
    connectionState: 'checking',
    loading: false,
    error: '',
    health: null,
    status: null,
  }),
  actions: {
    async refresh(): Promise<void> {
      this.loading = true;
      this.connectionState = 'checking';
      this.error = '';

      try {
        const [health, status] = await Promise.all([getHealth(), getRuntimeStatus()]);
        this.health = health;
        this.status = status;
        this.connectionState = 'connected';
      } catch (error) {
        this.connectionState = 'disconnected';
        this.error = error instanceof Error ? error.message : 'Local runtime is unavailable.';
      } finally {
        this.loading = false;
      }
    },
  },
});