<script setup>
import { computed, onMounted, ref } from 'vue';
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

// Solo la unidad del residente en sesión: la API ya filtra por sus claims, aquí no hay selector de unidades
const myUnit = computed(() => store.myUnits[0] || null);
const unitNumber = computed(() => myUnit.value?.unitNumber || myUnit.value?.number || '—');
const condominiumName = computed(() => store.myCondominium?.name || '—');
const aliquotFormatted = computed(() => {
  const val = myUnit.value?.aliquotPercentage ?? myUnit.value?.participationPct;
  return val == null ? '—' : Number(val).toFixed(4);
});
const residentName = computed(() => myUnit.value?.residentFullName || '—');
const paying = ref(false);

onMounted(async () => {
  // Retorno desde Stripe: se confirma con el backend antes de anunciar el pago
  if (route.query.paid === '1' && route.query.payment_id && route.query.session_id) {
    try {
      const result = await store.confirmCheckout(Number(route.query.payment_id), String(route.query.session_id));
      const isPaid = String(result?.status).toLowerCase() === 'paid';
      toast.add(isPaid
        ? { severity: 'success', summary: t('common.success'), detail: t('payments.statement.paidToast'), life: 3500 }
        : { severity: 'warn', summary: t('payments.statement.pending'), detail: t('payments.statement.paymentProcessing'), life: 4500 });
    } catch (_) {
      toast.add({ severity: 'error', summary: 'Error', detail: t('payments.statement.confirmError'), life: 4500 });
    }
    router.replace({ query: {} });
  } else if (route.query.canceled === '1') {
    toast.add({ severity: 'info', summary: t('payments.statement.canceledTitle'), detail: t('payments.statement.canceledToast'), life: 3500 });
    router.replace({ query: {} });
  }
  await store.fetchForResident(getSessionUserId());
});

async function payWithCard() {
  if (!pending.value || paying.value) return;
  paying.value = true;
  try {
    const data = await store.createCheckout(pending.value.id);
    const url = data?.url || data?.checkoutUrl;
    if (url) window.location.href = url;
    else paying.value = false;
  } catch (_) {
    paying.value = false;
    toast.add({ severity: 'error', summary: 'Error', detail: t('payments.statement.checkoutError'), life: 3500 });
  }
}

// Comprobante simple descargable (texto plano)
function downloadReceipt(p) {
  const text = [`IoBuild`, t('payments.statement.receipt'), '',
    `${t('payments.statement.unit')}: ${unitNumber.value} - ${condominiumName.value}`,
    `${t('payments.statement.holder')}: ${residentName.value} (${t('payments.statement.aliquot')}: ${aliquotFormatted.value}%)`,
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
    <div class="header-container">
      <div>
        <h1 class="page-title">{{ t('payments.statement.title') }}</h1>
        <p class="page-subtitle">{{ t('payments.statement.subtitle') }}</p>
      </div>
    </div>

    <div v-if="store.isLoading" class="center"><pv-progress-spinner /></div>
    <template v-else>
      <!-- Lo primero que busca el residente es cuánto debe y cómo pagarlo; los datos de su unidad acompañan -->
      <section v-if="pending" class="hero" :class="pending.effectiveStatus">
        <div class="hero-info">
          <span class="hero-period">{{ t('payments.statement.pending') }} · {{ formatPeriod(pending.period, locale) }}</span>
          <strong class="hero-amount">{{ formatPen(pending.amount) }}</strong>
          <small class="hero-due">{{ t('payments.statement.dueDate') }}: {{ pending.dueDate }}
            <template v-if="pending.daysOverdue"> · {{ t('payments.delinquency.daysOverdue') }}: {{ pending.daysOverdue }}</template></small>
        </div>
        <div class="hero-actions">
          <pv-button :label="t('payments.statement.payNow')" icon="pi pi-credit-card" size="large" :loading="paying" @click="payWithCard" />
        </div>
      </section>
      <section v-else class="hero ok"><strong><i class="pi pi-check-circle" aria-hidden="true"></i> {{ t('payments.statement.upToDate') }}</strong></section>

      <dl class="unit-facts">
        <div><dt>{{ t('payments.statement.unit') }}</dt><dd>{{ unitNumber }}</dd></div>
        <div><dt>{{ t('payments.statement.condominium') }}</dt><dd>{{ condominiumName }}</dd></div>
        <div><dt>{{ t('payments.statement.aliquot') }}</dt><dd class="num">{{ aliquotFormatted }}%</dd></div>
        <div><dt>{{ t('payments.statement.holder') }}</dt><dd>{{ residentName }}</dd></div>
      </dl>

      <div class="history-section">
        <h3>{{ t('payments.statement.history') }}</h3>
        <pv-data-table :value="history" dataKey="id" stripedRows :paginator="history.length > 10" :rows="10">
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
      </div>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
.header-container { margin-bottom: 1.5rem; }
.page-title { font-size: 1.75rem; font-weight: 700; color: #111827; margin: 0; }
.page-subtitle { color: #6b7280; font-size: 0.95rem; margin-top: 0.25rem; }

/* Datos de la unidad: franja sobria, secundaria al monto */
.unit-facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; margin: 0 0 1.75rem; padding: 1rem 1.25rem; border: 1px solid #e5e7eb; border-radius: 12px; background: #fff; }
.unit-facts dt { font-size: .8rem; color: #6b7280; margin-bottom: 2px; }
.unit-facts dd { margin: 0; font-weight: 600; color: #111827; }
.unit-facts .num { font-variant-numeric: tabular-nums; }

/* Hero Section */
.hero { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 1.5rem; border-radius: 14px; margin-bottom: 1rem; border: 1px solid #e5e7eb; background: #fff; }
.hero-info { display: flex; flex-direction: column; gap: 4px; }
.hero-period { font-size: 0.95rem; color: #4b5563; font-weight: 500; }
.hero-amount { font-size: 2.2rem; font-weight: 800; color: #111827; font-variant-numeric: tabular-nums; }
.hero-due { color: #6b7280; font-size: 0.85rem; }
.hero.ok { background: #ECFDF5; border-color: #10B981; color: #065f46; font-size: 1.2rem; }
.hero.overdue { background: #FEF2F2; border-color: #ef4444; }
.hero.pending { background: #FFFBEB; border-color: #FCD34D; }

.history-section h3 { margin-bottom: 1rem; font-size: 1.25rem; font-weight: 600; color: #111827; }
.center { text-align: center; padding: 2rem; }
</style>
