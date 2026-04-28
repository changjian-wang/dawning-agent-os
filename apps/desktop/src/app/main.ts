import '@arco-design/web-vue/dist/arco.css';
import '@/styles/global.css';

import ArcoVue from '@arco-design/web-vue';
import { createPinia } from 'pinia';
import { createApp } from 'vue';

import App from './App.vue';
import { router } from './router';

createApp(App).use(createPinia()).use(router).use(ArcoVue).mount('#app');