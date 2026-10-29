<script setup>
import { onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useBillingStore } from '../../application/billing.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { formatPen } from '../../../shared/infrastructure/session.js';

const { t } = useI18n();
const router = useRouter();
const toast = useToast();
const store = useBillingStore();
const condoStore = useCondominiumStore();

const condominiumId = ref(null);
const period = ref(new Date().toISOString().slice(0, 7));
const calculating = ref(false);

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, (id) => { if (id) store.fetchSettlements(id); }, { immediate: true });

async function calculate() {
  calculating.value = true;
  try {
    const id = await store.calculate(condominiumId.value, period.value);
    router.push({ name: 'settlement-detail', params: { id } });
  } catch (e) {
    const key = { participation_not_100: 'billing.messages.notBalanced', no_bill: 'billing.messages.noBill' }[e.message] || 'billing.messages.error';
    toast.add({ severity: 'warn', summary: t('billing.settlements.calculate'), detail: t(key), life: 4500 });
  } finally { calculating.value = false; }
}
</script>

<template>
  <div class="page">
    <h1>{{ t('billing.settlements.title') }}</h1>
    <div class="filters">
      <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
      <div><label class="lbl">{{ t('billing.bills.period') }}</label><pv-input-text v-model="period" type="month" /></div>
      <pv-button :label="t('billing.settlements.calculate')" icon="pi pi-calculator" :loading="calculating" @click="calculate" />
    </div>
    <pv-data-table :value="store.settlements" :loading="store.isLoading" dataKey="id" stripedRows>
      <template #empty>{{ t('billing.settlements.empty') }}</template>
      <pv-column field="period" :header="t('billing.bills.period')" sortable />
      <pv-column :header="t('billing.settlements.total')"><template #body="{ data }">{{ formatPen(data.commonAreaAmount) }}</template></pv-column>
      <pv-column :header="t('payments.statement.status')"><template #body="{ data }"><pv-tag :severity="data.isIssued ? 'success' : 'secondary'" :value="t(`billing.settlements.status.${data.status}`)" /></template></pv-column>
      <pv-column :header="t('common.actions')"><template #body="{ data }"><pv-button :label="t('common.view')" icon="pi pi-eye" text size="small" @click="router.push({ name: 'settlement-detail', params: { id: data.id } })" /></template></pv-column>
    </pv-data-table>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.filters { display: flex; gap: 1rem; flex-wrap: wrap; align-items: end; margin-bottom: 1rem; }
.filter { min-width: 16rem; }
.lbl { display: block; font-size: .85rem; color: #6B7280; margin-bottom: 4px; }
</style>
