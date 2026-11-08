<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { usePaymentStore } from '../../../payments/application/payment.store.js';
import { getSessionUserId, formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import AiExplainPanel from './ai-explain-panel.component.vue';
import CommonAreasReport from './common-areas-report.component.vue';

// Mi consumo: la cuota del residente y, en una sola pantalla, el consumo de las áreas comunes de su condominio
// RNF-006: desglose de la liquidación en ≤ 3 interacciones desde el inicio de sesión
const { t, locale } = useI18n();
const router = useRouter();
const payments = usePaymentStore();
const loading = ref(true);

const sortedPayments = computed(() => [...payments.payments].sort((a, b) => String(b.period).localeCompare(String(a.period))));
const latest = computed(() => sortedPayments.value[0] || null);
const lastThree = computed(() => sortedPayments.value.slice(0, 3));
const myUnit = computed(() => payments.myUnits[0] || null);
const sev = (s) => ({ paid: 'success', overdue: 'danger', pending: 'warn' }[s]);

onMounted(async () => {
  try {
    await payments.fetchForResident(getSessionUserId());
  } finally { loading.value = false; }
});
const openBreakdown = () => router.push({ path: ROUTES.AUDIT, query: { line: latest.value.settlementLineId } });
</script>

<template>
  <div class="page">
    <div>
      <h1>{{ t('option.myConsumption') }}</h1>
      <p v-if="myUnit" class="muted subtitle">{{ t('analytics.myUnit', { unit: myUnit.number }) }}</p>
    </div>
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

      <common-areas-report v-if="myUnit" :condominium-id="Number(myUnit.condominiumId)">
        <template #insights>
          <ai-explain-panel :condominium-id="Number(myUnit.condominiumId)" :unit-id="Number(myUnit.id)" audience="resident" />
        </template>
      </common-areas-report>

      <section v-if="lastThree.length">
        <div class="head"><h2>{{ t('dashboard.resident.recent') }}</h2><pv-button :label="t('option.accountStatement')" text size="small" @click="router.push(ROUTES.ACCOUNT_STATEMENT)" /></div>
        <ul class="list"><li v-for="p in lastThree" :key="p.id"><span>{{ formatPeriod(p.period, locale) }}</span><span>{{ formatPen(p.amount) }}</span><pv-tag :severity="sev(p.effectiveStatus)" :value="t(`payments.status.${p.effectiveStatus}`)" /></li></ul>
      </section>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; display: flex; flex-direction: column; gap: 1.25rem; }
h1 { margin: 0; }
h2 { margin: 0; font-size: 1.25rem; }
.muted { color: #6B7280; }
.subtitle { margin: .25rem 0 0; }
.hero { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 1.5rem; border-radius: 14px; background: #fff; border: 1px solid #e5e7eb; }
.hero div { display: flex; flex-direction: column; gap: 6px; }
.hero strong { font-size: 2.2rem; }
.hero.paid { background: #ECFDF5; border-color: #10B981; }
.hero.overdue { background: #FEF2F2; border-color: #ef4444; }
.head { display: flex; justify-content: space-between; align-items: center; }
.list { list-style: none; padding: 0; margin: .5rem 0 0; }
.list li { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: .6rem 0; border-bottom: 1px solid #f3f4f6; }
.center { text-align: center; padding: 2rem; }
</style>
