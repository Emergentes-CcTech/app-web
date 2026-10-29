<script setup>
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { formatPen } from '../../../shared/infrastructure/session.js';
import LineBreakdown from './line-breakdown.vue';

defineProps({ settlement: Object, unitsById: { type: Object, default: () => ({}) }, residentsByUnit: { type: Object, default: () => ({}) }, bills: Array });
const { t } = useI18n();
const expanded = ref({});
</script>

<template>
  <pv-data-table v-model:expandedRows="expanded" :value="settlement.lines" dataKey="id" stripedRows>
    <pv-column expander style="width: 3rem" />
    <pv-column :header="t('residents.fields.unit')"><template #body="{ data }">{{ unitsById[data.unitId]?.number ?? data.unitId }}</template></pv-column>
    <pv-column :header="t('payments.delinquency.resident')"><template #body="{ data }">{{ residentsByUnit[data.unitId] || '—' }}</template></pv-column>
    <pv-column :header="t('condominiums.units.participation')"><template #body="{ data }">{{ Number(data.participationPct).toFixed(2) }} %</template></pv-column>
    <pv-column :header="t('billing.settlements.share')"><template #body="{ data }">{{ formatPen(data.amount) }}</template></pv-column>
    <pv-column :header="t('billing.settlements.issuance')"><template #body>
      <pv-tag :severity="settlement.isIssued ? 'success' : 'secondary'" :value="t(`billing.settlements.status.${settlement.status}`)" /></template></pv-column>
    <template #expansion="{ data }"><line-breakdown :line="data" :bills="bills" /></template>
    <template #footer>
      <div class="foot">
        <span>{{ t('billing.settlements.total') }}: <strong>{{ formatPen(settlement.linesTotal) }}</strong></span>
        <pv-tag :severity="settlement.linesMatchTotal ? 'success' : 'danger'"
                :value="`${t('billing.settlements.check')}: ${settlement.linesMatchTotal ? '✓' : '✗'} (${formatPen(settlement.commonAreaAmount)})`" />
      </div>
    </template>
  </pv-data-table>
</template>

<style scoped>
.foot { display: flex; justify-content: space-between; align-items: center; }
</style>
