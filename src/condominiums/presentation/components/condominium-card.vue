<script setup>
import { useI18n } from 'vue-i18n';

defineProps({
  condominium: { type: Object, required: true },
  // Suscripción del condominio (null si aún no tiene plan) y nombre del plan
  subscription: { type: Object, default: null },
  planName: { type: String, default: '' },
});
defineEmits(['open']);
const { t } = useI18n();
</script>

<template>
  <article class="condo-card" tabindex="0" @click="$emit('open', condominium)" @keyup.enter="$emit('open', condominium)">
    <header>
      <h3>{{ condominium.name }}</h3>
      <pv-tag :severity="condominium.isActive ? 'success' : 'secondary'"
              :value="condominium.isActive ? t('condominiums.status.active') : t('condominiums.status.inactive')" />
    </header>
    <p class="muted"><i class="pi pi-map-marker" /> {{ condominium.address }}<span v-if="condominium.district">, {{ condominium.district }}</span></p>
    <div class="facts">
      <span><i class="pi pi-building" /> {{ t('condominiums.apartmentsCount', { n: condominium.totalUnits }) }}</span>
      <span><i class="pi pi-th-large" /> {{ t('condominiums.commonAreas.count', { n: condominium.commonAreasCount }, condominium.commonAreasCount) }}</span>
    </div>
    <footer>
      <span class="muted">{{ t('condominiums.subscription') }}:</span>
      <template v-if="subscription">
        <strong>{{ planName }}</strong>
        <pv-tag :severity="subscription.isActive() ? 'success' : 'warn'"
                :value="subscription.isActive() ? t('subscriptions.status-active') : t('subscriptions.status-cancelled')" />
      </template>
      <pv-tag v-else severity="secondary" :value="t('condominiums.noSubscription')" />
    </footer>
  </article>
</template>

<style scoped>
.condo-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.1rem; cursor: pointer; transition: box-shadow .15s, border-color .15s; display: flex; flex-direction: column; gap: .5rem; }
.condo-card:hover, .condo-card:focus { box-shadow: 0 4px 12px rgba(0,0,0,.08); border-color: #10B981; outline: none; }
header { display: flex; justify-content: space-between; align-items: center; gap: .5rem; }
h3 { margin: 0; font-size: 1.1rem; }
.muted { color: #6B7280; }
.facts { display: flex; flex-wrap: wrap; gap: .4rem 1rem; color: #111827; }
.facts .pi { color: #059669; margin-right: .25rem; }
footer { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; border-top: 1px solid #f3f4f6; padding-top: .6rem; font-size: .9rem; }
</style>
