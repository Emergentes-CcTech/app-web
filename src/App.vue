<script setup>
import { useI18n } from "vue-i18n";
import { computed } from "vue";
import { useRoute } from "vue-router";
import Layout from "./shared/presentation/components/layout.vue";
import { useIamStore } from "./iam/application/iam.store.js";

const { t } = useI18n();
const route = useRoute();
const iamStore = useIamStore();

// User is hydrated from localStorage at store init (iam.store.js), synchronously,
// before any component mounts. Re-running it here in onMounted only fires on in-app
// login (not on reload), creating the login-vs-reload asymmetry that left the sidebar
// in a stale/frozen state on first login. Removed — store init is the single source.

const showLayout = computed(() => {
  return route.meta?.public !== true && iamStore.isAuthenticated;
});
</script>

<template>

  <layout v-if="showLayout" />
  

  <router-view v-else />
</template>

<style scoped>

</style>
