<script setup>
import { onMounted, ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useCondominiumStore } from '../../application/condominium.store.js';
import { useSubscriptionStore } from '../../../subscriptions/application/subscription.store.js';
import CondominiumCard from '../components/condominium-card.vue';

const { t } = useI18n();
const router = useRouter();
const store = useCondominiumStore();
const subscriptions = useSubscriptionStore();
const search = ref('');

onMounted(() => {
  store.fetchAll().catch(() => {});
  subscriptions.fetchAvailablePlans();
  subscriptions.fetchSubscriptions();
});

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  return store.condominiums.filter(c => !q || `${c.name} ${c.district} ${c.address}`.toLowerCase().includes(q));
});
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('condominiums.title') }}</h1>
      <pv-button :label="t('condominiums.add')" icon="pi pi-plus" @click="router.push({ name: 'condominium-new' })" />
    </div>
    <pv-icon-field class="mb-3">
      <pv-input-icon class="pi pi-search" />
      <pv-input-text v-model="search" :placeholder="t('condominiums.search')" class="w-full" />
    </pv-icon-field>
    <div v-if="store.isLoading" class="center"><pv-progress-spinner /></div>
    <pv-message v-else-if="store.errors.length" severity="error">{{ t('common.loadError') }}</pv-message>
    <p v-else-if="!filtered.length" class="muted">{{ t('condominiums.empty') }}</p>
    <div v-else class="grid-cards">
      <condominium-card v-for="c in filtered" :key="c.id" :condominium="c"
                        :subscription="subscriptions.subscriptionFor(c.id)"
                        :planName="subscriptions.planFor(subscriptions.subscriptionFor(c.id))?.name || ''"
                        @open="router.push({ name: 'condominium-details', params: { id: c.id } })" />
    </div>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.page-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
.grid-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 1rem; }
.center { text-align: center; padding: 2rem; }
.muted { color: #6B7280; }
</style>
