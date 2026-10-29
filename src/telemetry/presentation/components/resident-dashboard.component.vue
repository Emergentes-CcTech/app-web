<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { usePaymentStore } from '../../../payments/application/payment.store.js';
import { getSessionUserId, formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import StatCard from './stat-card.component.vue';

// RNF-006: desglose de la liquidación en ≤ 3 interacciones desde el inicio de sesión
const { t, locale } = useI18n();
const router = useRouter();
const telemetry = useTelemetryStore();
const payments = usePaymentStore();
const loading = ref(true);

const latest = computed(() => [...payments.payments].sort((a, b) => String(b.period).localeCompare(String(a.period)))[0] || null);
const lastThree = computed(() => [...payments.payments].sort((a, b) => String(b.period).localeCompare(String(a.period))).slice(0, 3));
const water = computed(() => telemetry.consumptionOf(telemetry.monthReadings, 'L/min'));
const energy = computed(() => telemetry.consumptionOf(telemetry.monthReadings, 'A'));
const sev = (s) => ({ paid: 'success', overdue: 'danger', pending: 'warn' }[s]);

onMounted(async () => {
  try {
    await payments.fetchForResident(getSessionUserId());
    const condoId = payments.myUnits[0]?.condominiumId;
    if (condoId) await telemetry.fetchMonth(condoId).catch(() => {});
  } finally { loading.value = false; }
});
const openBreakdown = () => router.push({ path: ROUTES.AUDIT, query: { line: latest.value.settlementLineId } });
</script>

<template>
  <div class="page">
    <h1>{{ t('option.myConsumption') }}</h1>
    <div v-if="loading" class="center"><pv-progress-spinner /></div>
    <template v-else>
      <section class="hero" :class="latest?.effectiveStatus">
        <template v-if="latest">
          <div>
            <span>{{ t('dashboard.resident.myStatement', { month: formatPeriod(latest.period, locale) }) }}</span>
            <strong>{{ formatPen(latest.amount) }}</strong>
            <pv-tag :severity="sev(latest.effectiveStatus)" :value="t(`payments.status.${latest.effectiveStatus}`)" />
          </div>
          <pv-button :label="t('dashboard.resident.viewBreakdown')" icon="pi pi-search" size="large" @click="openBreakdown" />
        </template>
        <span v-else>{{ t('payments.statement.empty') }}</span>
      </section>

      <h3>{{ t('dashboard.resident.commonConsumption') }}</h3>
      <div class="stats">
        <stat-card :title="t('dashboard.admin.stats.waterMonth')" :value="`${water} m³`" icon="pi-wallet" iconBgColor="bg-green-100" iconTextColor="text-green-600" />
        <stat-card :title="t('dashboard.admin.stats.pumpEnergy')" :value="`${energy} kWh`" icon="pi-bolt" iconBgColor="bg-yellow-100" iconTextColor="text-yellow-600" />
      </div>

      <div class="head"><h3>{{ t('dashboard.resident.recent') }}</h3><pv-button :label="t('option.accountStatement')" text size="small" @click="router.push(ROUTES.ACCOUNT_STATEMENT)" /></div>
      <ul class="list"><li v-for="p in lastThree" :key="p.id"><span>{{ formatPeriod(p.period, locale) }}</span><span>{{ formatPen(p.amount) }}</span><pv-tag :severity="sev(p.effectiveStatus)" :value="t(`payments.status.${p.effectiveStatus}`)" /></li></ul>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1000px; margin: 0 auto; }
.hero { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 1.5rem; border-radius: 14px; margin-bottom: 1.5rem; background: #fff; border: 1px solid #e5e7eb; }
.hero div { display: flex; flex-direction: column; gap: 6px; }
.hero strong { font-size: 2.2rem; }
.hero.paid { background: #ECFDF5; border-color: #10B981; }
.hero.overdue { background: #FEF2F2; border-color: #ef4444; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem; }
.head { display: flex; justify-content: space-between; align-items: center; }
.list { list-style: none; padding: 0; }
.list li { display: flex; justify-content: space-between; align-items: center; padding: .6rem 0; border-bottom: 1px solid #f3f4f6; }
.center { text-align: center; padding: 2rem; }
</style>
