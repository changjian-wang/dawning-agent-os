import { createRouter, createWebHashHistory } from 'vue-router';

export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/',
      redirect: '/chat',
    },
    {
      path: '/chat',
      name: 'chat',
      component: () => import('@/pages/chat/ChatPage.vue'),
    },
    {
      path: '/inbox',
      name: 'inbox',
      component: () => import('@/pages/inbox/InboxPage.vue'),
    },
    {
      path: '/memory',
      name: 'memory',
      component: () => import('@/pages/memory/MemoryPage.vue'),
    },
  ],
});