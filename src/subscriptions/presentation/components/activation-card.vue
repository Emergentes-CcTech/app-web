<script setup>
import { useI18n } from "vue-i18n";

const { t } = useI18n();

// Cotización del backend (/subscriptions/activation-quote): el plan se asigna por el número de departamentos
const props = defineProps({
  quote: { type: Object, default: null },
  condominiumName: { type: String, default: '' },
  loading: { type: Boolean, default: false },
  isProcessing: { type: Boolean, default: false }
});

defineEmits(["activate"]);

const money = (value) => `S/ ${Number(value || 0).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
</script>

<template>
  <section class="activation-card" aria-labelledby="activation-title">
    <div v-if="loading" class="activation-loading">
      <pv-progress-spinner style="width: 32px; height: 32px" />
    </div>

    <template v-else-if="props.quote">
      <div class="activation-main">
        <span class="activation-pill"><i class="pi pi-bolt" aria-hidden="true"></i> {{ t('subscriptions.activation.pill') }}</span>
        <h2 id="activation-title" class="activation-title">{{ t('subscriptions.activation.title', { name: condominiumName }) }}</h2>
        <p class="activation-desc">
          {{ t('subscriptions.activation.assigned', { plan: props.quote.planName === 'GranTorre' ? 'Gran Torre' : props.quote.planName, units: props.quote.totalUnits }) }}
        </p>

        <div class="activation-monthly">
          <span class="monthly-amount">{{ money(props.quote.monthlyPrice) }}</span>
          <span class="monthly-period">{{ t('subscriptions.plusIgv') }} / {{ t('subscriptions.month') }}</span>
        </div>
        <p class="activation-note">
          {{ t('subscriptions.activation.monthlyDetail', { amount: money(props.quote.monthlyPriceWithIgv), perUnit: money(props.quote.pricePerUnitWithIgv) }) }}
        </p>

        <ul class="activation-terms">
          <li><i class="pi pi-calendar" aria-hidden="true"></i> {{ t('subscriptions.activation.contract', { months: props.quote.contractMonths }) }}</li>
          <li><i class="pi pi-wrench" aria-hidden="true"></i> {{ t('subscriptions.activation.prepaidTerm', { months: props.quote.contractMonths }) }}</li>
          <li><i class="pi pi-sign-out" aria-hidden="true"></i> {{ t('subscriptions.activation.earlyExit') }}</li>
        </ul>
      </div>

      <div class="activation-breakdown">
        <h3 class="breakdown-title">{{ t('subscriptions.activation.todayTitle') }}</h3>
        <dl class="breakdown-list">
          <div>
            <dt>{{ t('subscriptions.activation.firstFee') }}</dt>
            <dd>{{ money(props.quote.monthlyPriceWithIgv) }}</dd>
          </div>
          <div>
            <dt>{{ t('subscriptions.activation.lastFee', { month: props.quote.contractMonths }) }}</dt>
            <dd>{{ money(props.quote.prepaidLastMonthWithIgv) }}</dd>
          </div>
          <div>
            <dt>{{ t('subscriptions.activation.installation') }}</dt>
            <dd class="included">{{ t('subscriptions.activation.included') }}</dd>
          </div>
          <div class="breakdown-total">
            <dt>{{ t('subscriptions.activation.total') }}</dt>
            <dd>{{ money(props.quote.total) }}</dd>
          </div>
        </dl>
        <p class="breakdown-hint">{{ t('subscriptions.activation.payerHint') }}</p>
        <button type="button" class="activation-cta" :disabled="isProcessing" @click="$emit('activate', props.quote)">
          <i v-if="isProcessing" class="pi pi-spin pi-spinner" aria-hidden="true"></i>
          <span>{{ t('subscriptions.activation.cta') }}</span>
          <i v-if="!isProcessing" class="pi pi-arrow-right" aria-hidden="true"></i>
        </button>
      </div>
    </template>

    <p v-else class="activation-empty">{{ t('subscriptions.activation.unavailable') }}</p>
  </section>
</template>

<style scoped>
.activation-card {
  display: grid;
  grid-template-columns: minmax(0, 1.3fr) minmax(0, 1fr);
  gap: 2rem;
  background: linear-gradient(135deg, #059669 0%, #0f766e 100%);
  color: #ffffff;
  border-radius: 1.25rem;
  padding: 2.25rem 2.5rem;
  margin-bottom: 2.5rem;
  box-shadow: 0 10px 25px -5px rgba(5, 150, 105, 0.25);
}

.activation-loading,
.activation-empty {
  grid-column: 1 / -1;
  display: flex;
  justify-content: center;
  margin: 0;
}

.activation-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  background: rgba(255, 255, 255, 0.2);
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.3rem 0.85rem;
  border-radius: 9999px;
  margin-bottom: 0.75rem;
}

.activation-title {
  font-size: 1.65rem;
  font-weight: 800;
  margin: 0 0 0.5rem 0;
  letter-spacing: -0.02em;
}

.activation-desc,
.activation-note {
  font-size: 0.88rem;
  color: #e6fffa;
  margin: 0;
  line-height: 1.5;
}

.activation-monthly {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  margin: 1.25rem 0 0.25rem;
}

.monthly-amount {
  font-size: 2.25rem;
  font-weight: 800;
  letter-spacing: -0.03em;
}

.monthly-period {
  font-size: 0.9rem;
  color: #d1fae5;
}

.activation-terms {
  list-style: none;
  padding: 0;
  margin: 1.25rem 0 0;
  display: grid;
  gap: 0.5rem;
  font-size: 0.84rem;
}

.activation-terms i {
  margin-right: 0.4rem;
  color: #a7f3d0;
}

.activation-breakdown {
  background: #ffffff;
  color: #0f172a;
  border-radius: 1rem;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
}

.breakdown-title {
  font-size: 0.95rem;
  font-weight: 800;
  margin: 0 0 0.75rem;
}

.breakdown-list {
  margin: 0;
  display: grid;
  gap: 0.6rem;
}

.breakdown-list > div {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  font-size: 0.85rem;
}

.breakdown-list dt {
  color: #475569;
}

.breakdown-list dd {
  margin: 0;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.breakdown-list dd.included {
  color: #059669;
}

.breakdown-total {
  border-top: 1px solid #e2e8f0;
  padding-top: 0.6rem;
  font-size: 0.95rem !important;
}

.breakdown-total dt,
.breakdown-total dd {
  color: #0f172a;
  font-weight: 800;
}

.breakdown-hint {
  font-size: 0.75rem;
  color: #64748b;
  margin: 0.75rem 0 1rem;
  line-height: 1.45;
}

.activation-cta {
  margin-top: auto;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  background: #059669;
  color: #ffffff;
  border: none;
  font-size: 0.9rem;
  font-weight: 800;
  padding: 0.85rem 1.35rem;
  border-radius: 0.75rem;
  cursor: pointer;
  font-family: inherit;
  transition: background 0.2s ease;
}

.activation-cta:hover:not(:disabled) {
  background: #047857;
}

.activation-cta:disabled {
  opacity: 0.7;
  cursor: progress;
}

@media (max-width: 900px) {
  .activation-card {
    grid-template-columns: 1fr;
    padding: 1.75rem 1.25rem;
  }
}
</style>
