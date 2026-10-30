<script setup lang="js">
import { useI18n } from 'vue-i18n';
import { ROUTES } from '../../infrastructure/paths.js';

const { t } = useI18n();
</script>

<template>
  <main class="not-found">
    <img src="/IoBuild-Logo.png" alt="IoBuild" class="logo" />
    <p class="code">404</p>
    <h1>{{ t('notFound.title') }}</h1>
    <p class="text">{{ t('notFound.text') }}</p>
    <router-link :to="ROUTES.DASHBOARD" class="home">
      <i class="pi pi-home" aria-hidden="true" /> {{ t('notFound.home') }}
    </router-link>
  </main>
</template>

<style scoped>
.not-found { min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: .5rem; padding: 2rem 1rem; text-align: center; background: linear-gradient(135deg, #f0fdf4 0%, #e6fffa 100%); }
.logo { height: 40px; margin-bottom: 1.5rem; }
.code { margin: 0; font-size: 3.5rem; font-weight: 800; color: #10B981; line-height: 1; }
h1 { margin: 0; font-size: 1.5rem; color: #111827; }
.text { margin: 0 0 1rem; color: #6B7280; max-width: 28rem; }
.home { display: inline-flex; align-items: center; gap: .5rem; background: #059669; color: #fff; text-decoration: none; padding: .65rem 1.25rem; border-radius: 8px; font-weight: 600; }
.home:hover { background: #047857; }
.home:focus-visible { outline: 3px solid #a7f3d0; outline-offset: 2px; }
</style>
