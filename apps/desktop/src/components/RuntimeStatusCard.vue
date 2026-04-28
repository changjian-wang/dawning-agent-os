<template>
  <a-card class="runtime-card" :bordered="false">
    <template #title>Local Runtime</template>
    <template #extra>
      <a-button size="mini" :loading="runtime.loading" @click="runtime.refresh">Refresh</a-button>
    </template>

    <a-space direction="vertical" size="small" fill>
      <a-tag :color="statusColor">{{ runtime.connectionState }}</a-tag>
      <a-typography-text v-if="runtime.status">Data: {{ runtime.status.dataDirectory }}</a-typography-text>
      <a-typography-text v-if="runtime.error" type="danger">{{ runtime.error }}</a-typography-text>
    </a-space>
  </a-card>
</template>

<script lang="ts" setup>
import { computed, onMounted } from 'vue';

import { useRuntimeStore } from '@/stores/runtime';

const runtime = useRuntimeStore();

const statusColor = computed(() => {
  if (runtime.connectionState === 'connected') {
    return 'green';
  }

  if (runtime.connectionState === 'checking') {
    return 'blue';
  }

  return 'red';
});

onMounted(() => runtime.refresh());
</script>

<style scoped>
.runtime-card {
  width: 100%;
}
</style>