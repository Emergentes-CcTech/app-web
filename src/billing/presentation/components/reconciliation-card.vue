<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { formatPen } from '../../../shared/infrastructure/session.js';
import { RECONCILIATION_WARNING_PCT } from '../../../shared/infrastructure/constants.js';

const props = defineProps({ bill: { type: Object, default: null } });
const { t } = useI18n();
const warn = computed(() => (props.bill?.differencePct ?? 0) > RECONCILIATION_WARNING_PCT);
</script>

<template>
  <section v-if="bill" class="recon">
    <h3>{{ t('billing.reconciliation.title') }}</h3>
    <div class="grid">
      <div><span>{{ t('billing.reconciliation.billed') }}</span><strong>{{ bill.billedConsumption }} {{ bill.consumptionUnit }}</strong></div>
      <div><span>{{ t('billing.reconciliation.measured') }}</span><strong>{{ bill.measuredConsumption ?? '—' }} {{ bill.measuredConsumption !== null ? bill.consumptionUnit : '' }}</strong></div>
      <div><span>{{ t('billing.reconciliation.differenceUnits') }}</span><strong>{{ bill.differenceUnits ?? '—' }} {{ bill.differenceUnits !== null ? bill.consumptionUnit : '' }}</strong></div>
      <div><span>{{ t('billing.reconciliation.differenceAmount') }}</span><strong>{{ bill.differenceAmount !== null ? formatPen(bill.differenceAmount) : '—' }}</strong></div>
    </div>
    <pv-message v-if="warn" severity="warn" :closable="false">{{ t('billing.reconciliation.warning', { n: RECONCILIATION_WARNING_PCT }) }}</pv-message>
  </section>
</template>

<style scoped>
.recon { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; margin: 1rem 0; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 1rem; margin-bottom: .75rem; }
.grid div { display: flex; flex-direction: column; gap: 4px; }
.grid span { color: #6B7280; font-size: .85rem; }
.grid strong { font-size: 1.25rem; }
</style>
