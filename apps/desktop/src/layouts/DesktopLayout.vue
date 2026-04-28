<template>
  <a-layout class="desktop-shell">
    <a-layout-sider :width="220" class="desktop-shell__sider">
      <div class="desktop-shell__brand">Dawning Agent OS</div>
      <a-menu :selected-keys="[$route.name?.toString() ?? 'chat']" @menu-item-click="handleMenuClick">
        <a-menu-item key="chat">Chat</a-menu-item>
        <a-menu-item key="inbox">Agent Inbox</a-menu-item>
        <a-menu-item key="memory">Memory Ledger</a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="desktop-shell__header">
        <RuntimeStatusCard />
      </a-layout-header>
      <a-layout-content class="desktop-shell__content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script lang="ts" setup>
import { useRouter } from 'vue-router';

import RuntimeStatusCard from '@/components/RuntimeStatusCard.vue';

const router = useRouter();

function handleMenuClick(key: string): void {
  void router.push({ name: key });
}
</script>