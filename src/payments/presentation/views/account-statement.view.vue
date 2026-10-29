<script setup>
import { computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { usePaymentStore } from '../../application/payment.store.js';
import { PaymentStatus } from '../../domain/model/payment.entity.js';
import { getSessionUserId, formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = usePaymentStore();

const pending = computed(() => store.pendingForResident);
const history = computed(() => [...store.payments].sort((a, b) => String(b.period).localeCompare(String(a.period))));
const severity = (s) => ({ paid: 'success', overdue: 'danger', pending: 'warn' }[s]);

onMounted(async () => {
  await store.fetchForResident(getSessionUserId());
  if (route.query.paid === '1') toast.add({ severity: 'success', summary: t('common.success'), detail: t('payments.statement.paidToast'), life: 3500 });
});

async function payWithCard() {
  if (!pending.value) return;
  try {
    const { url } = await store.createCheckout(pending.value.id);
    if (url) window.location.href = url;
  } catch (_) {
    toast.add({ severity: 'error', summary: 'Error', detail: t('payments.statement.checkoutError'), life: 3500 });
  }
}

// Comprobante simple descargable (texto plano)
function downloadReceipt(p) {
  const text = [`IoBuild`, t('payments.statement.receipt'), '',
    `${t('payments.statement.period')}: ${formatPeriod(p.period, locale.value)}`,
    `${t('payments.statement.amount')}: ${formatPen(p.amount)}`,
    `${t('payments.register.paidAt')}: ${p.paidAt}`,
    `${t('payments.register.reference')}: ${p.reference || '—'}`].join('\n');
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
  a.download = `comprobante-${p.period}.txt`;
  a.click();
  URL.revokeObjectURL(a.href);
}
// RNF-006: el desglose se abre en un clic desde el estado de cuenta
const viewBreakdown = (p) => router.push({ path: ROUTES.AUDIT, query: { line: p.settlementLineId } });
</script>

<template>
  <div class="page">
    <h1>{{ t('payments.statement.title') }}</h1>
    <div v-if="store.isLoading" class="center"><pv-progress-spinner /></div>
    <template v-else>
      <section v-if="pending" class="hero" :class="pending.effectiveStatus">
        <div>
          <span>{{ t('payments.statement.pending') }} · {{ formatPeriod(pending.period, locale) }}</span>
          <strong>{{ formatPen(pending.amount) }}</strong>
          <small>{{ t('payments.statement.dueDate') }}: {{ pending.dueDate }}
            <template v-if="pending.daysOverdue"> · {{ t('payments.delinquency.daysOverdue') }}: {{ pending.daysOverdue }}</template></small>
        </div>
        <pv-button :label="t('payments.statement.payNow')" icon="pi pi-credit-card" size="large" @click="payWithCard" />
      </section>
      <section v-else class="hero ok"><strong>✓ {{ t('payments.statement.upToDate') }}</strong></section>

      <h3>{{ t('payments.statement.history') }}</h3>
      <pv-data-table :value="history" dataKey="id" stripedRows paginator :rows="10">
        <template #empty>{{ t('payments.statement.empty') }}</template>
        <pv-column :header="t('payments.statement.period')"><template #body="{ data }">{{ formatPeriod(data.period, locale) }}</template></pv-column>
        <pv-column :header="t('payments.statement.concept')"><template #body>{{ t('payments.statement.conceptValue') }}</template></pv-column>
        <pv-column :header="t('payments.statement.amount')"><template #body="{ data }">{{ formatPen(data.amount) }}</template></pv-column>
        <pv-column :header="t('payments.statement.status')"><template #body="{ data }"><pv-tag :severity="severity(data.effectiveStatus)" :value="t(`payments.status.${data.effectiveStatus}`)" /></template></pv-column>
        <pv-column :header="t('common.actions')">
          <template #body="{ data }">
            <pv-button :label="t('payments.statement.viewBreakdown')" icon="pi pi-search" text size="small" @click="viewBreakdown(data)" />
            <pv-button v-if="data.effectiveStatus === PaymentStatus.PAID" :label="t('payments.statement.download')" icon="pi pi-download" text size="small" @click="downloadReceipt(data)" />
          </template>
        </pv-column>
      </pv-data-table>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
.hero { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 1.5rem; border-radius: 14px; margin-bottom: 1.5rem; border: 1px solid #e5e7eb; background: #fff; }
.hero div { display: flex; flex-direction: column; gap: 4px; }
.hero strong { font-size: 2rem; }
.hero.ok { background: #ECFDF5; border-color: #10B981; color: #065f46; }
.hero.overdue { background: #FEF2F2; border-color: #ef4444; }
.center { text-align: center; padding: 2rem; }
</style>
