<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useBillingStore } from '../../application/billing.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { formatPen } from '../../../shared/infrastructure/session.js';
import BillForm from '../components/bill-form.vue';
import ReconciliationCard from '../components/reconciliation-card.vue';

const { t } = useI18n();
const toast = useToast();
const store = useBillingStore();
const condoStore = useCondominiumStore();

const condominiumId = ref(null);
const period = ref(new Date().toISOString().slice(0, 7));
const saving = ref(false);

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, (id) => { if (id) store.fetchBills(id); }, { immediate: true });

// La conciliación se muestra antes de calcular el prorrateo (RF-012)
const periodBill = computed(() => store.bills.find(b => b.period === period.value) || null);

async function submit(bill) {
  saving.value = true;
  try {
    await store.addBill({ ...bill, condominiumId: condominiumId.value });
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('billing.bills.saved'), life: 2500 });
  } catch (_) {
    toast.add({ severity: 'error', summary: t('common.error'), detail: t('billing.messages.error'), life: 3000 });
  } finally { saving.value = false; }
}
</script>

<template>
  <div class="page">
    <h1>{{ t('billing.bills.title') }}</h1>
    <div class="filters">
      <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
      <div><label class="lbl">{{ t('billing.bills.period') }}</label><pv-input-text v-model="period" type="month" /></div>
    </div>
    <bill-form :period="period" :loading="saving" @submit="submit" />
    <reconciliation-card :bill="periodBill" />
    <pv-data-table :value="store.bills" :loading="store.isLoading" dataKey="id" stripedRows>
      <template #empty>{{ t('billing.bills.empty') }}</template>
      <pv-column field="period" :header="t('billing.bills.period')" sortable />
      <pv-column :header="t('billing.bills.service')"><template #body="{ data }">{{ t(`billing.bills.${data.service}`) }}</template></pv-column>
      <pv-column :header="t('billing.bills.billedConsumption')"><template #body="{ data }">{{ data.billedConsumption }} {{ data.consumptionUnit }}</template></pv-column>
      <pv-column :header="t('billing.bills.measuredConsumption')"><template #body="{ data }">{{ data.measuredConsumption ?? '—' }}</template></pv-column>
      <pv-column :header="t('billing.bills.amount')"><template #body="{ data }">{{ formatPen(data.billedAmount) }}</template></pv-column>
      <pv-column field="dueDate" :header="t('billing.bills.dueDate')" />
    </pv-data-table>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.filters { display: flex; gap: 1rem; flex-wrap: wrap; align-items: end; margin-bottom: 1rem; }
.filter { min-width: 16rem; }
.lbl { display: block; font-size: .85rem; color: #6B7280; margin-bottom: 4px; }
</style>
