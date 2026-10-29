<script setup>
import { useI18n } from 'vue-i18n';
import { formatPen } from '../../../shared/infrastructure/session.js';

// RF-013: desglose trazable hasta el recibo, el porcentaje y las lecturas usadas
defineProps({ line: { type: Object, required: true }, bills: { type: Array, default: () => [] } });
const { t } = useI18n();
</script>

<template>
  <div class="breakdown">
    <p class="formula">
      {{ t('billing.breakdown.formula') }}:
      <strong>{{ formatPen(line.breakdown?.commonAreaAmount) }} × {{ Number(line.breakdown?.participationPct ?? line.participationPct).toFixed(2) }} % = {{ formatPen(line.amount) }}</strong>
    </p>
    <p v-for="b in bills" :key="b.id">{{ t('billing.breakdown.sourceBill') }}: {{ t(`billing.bills.${b.service}`) }} · {{ b.period }} · {{ formatPen(b.billedAmount) }}</p>
    <p v-if="line.breakdown?.readingsFrom">{{ t('billing.breakdown.readings') }}: {{ line.breakdown.readingsFrom }} → {{ line.breakdown.readingsTo }}</p>
  </div>
</template>

<style scoped>
.breakdown { padding: .5rem 1rem; background: #ECFDF5; border-radius: 8px; }
.formula { margin: .25rem 0; }
p { margin: .2rem 0; }
</style>
