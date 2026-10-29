<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { usePaymentStore } from '../../application/payment.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { useResidentStore } from '../../../residents/application/resident.store.js';
import { formatPen } from '../../../shared/infrastructure/session.js';
import RegisterPaymentDialog from '../components/register-payment-dialog.vue';

const { t } = useI18n();
const toast = useToast();
const store = usePaymentStore();
const condoStore = useCondominiumStore();
const residentStore = useResidentStore();

const condominiumId = ref(null);
const dialog = ref(false);
const selected = ref(null);

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, (id) => { if (id) { store.fetchByCondominium(id); residentStore.fetchByCondominium(id); } }, { immediate: true });

// Una fila por unidad con deuda vencida
const rows = computed(() => store.overdueUnitIds.map(unitId => {
  const list = store.overduePayments.filter(p => p.unitId === unitId);
  const resident = residentStore.residents.find(r => r.unitId === unitId);
  return {
    unitId, unitNumber: resident?.unitNumber ?? unitId, resident: resident?.fullName || '—',
    periods: list.map(p => p.period).sort().join(', '), amount: Math.round(list.reduce((a, p) => a + p.amount * 100, 0)) / 100,
    daysOverdue: Math.max(...list.map(p => p.daysOverdue)), first: [...list].sort((a, b) => String(a.dueDate).localeCompare(String(b.dueDate)))[0],
  };
}));

function openRegister(row) { selected.value = row.first; dialog.value = true; }
async function confirmPayment(data) {
  await store.register(selected.value.id, data);
  dialog.value = false;
  toast.add({ severity: 'success', summary: t('common.success'), detail: t('payments.register.saved'), life: 2500 });
  residentStore.fetchByCondominium(condominiumId.value);
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('payments.delinquency.title') }}</h1>
      <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
    </div>
    <div class="stats">
      <div class="stat" :class="store.delinquencyRate > 0 ? 'bad' : 'ok'"><span>{{ t('payments.delinquency.rate') }}</span><strong>{{ store.delinquencyRate }} %</strong></div>
      <div class="stat"><span>{{ t('payments.delinquency.overdueAmount') }}</span><strong>{{ formatPen(store.overdueAmount) }}</strong></div>
      <div class="stat"><span>{{ t('payments.delinquency.units') }}</span><strong>{{ store.overdueUnitIds.length }} / {{ store.unitsTotal }}</strong></div>
    </div>
    <pv-data-table :value="rows" :loading="store.isLoading" dataKey="unitId" stripedRows>
      <template #empty>{{ t('payments.delinquency.empty') }}</template>
      <pv-column field="unitNumber" :header="t('residents.fields.unit')" />
      <pv-column field="resident" :header="t('payments.delinquency.resident')" />
      <pv-column field="periods" :header="t('payments.delinquency.periods')" />
      <pv-column :header="t('payments.register.amount')"><template #body="{ data }">{{ formatPen(data.amount) }}</template></pv-column>
      <pv-column field="daysOverdue" :header="t('payments.delinquency.daysOverdue')" />
      <pv-column :header="t('common.actions')"><template #body="{ data }"><pv-button :label="t('payments.delinquency.register')" icon="pi pi-check-circle" size="small" @click="openRegister(data)" /></template></pv-column>
    </pv-data-table>
    <register-payment-dialog v-model:visible="dialog" :payment="selected" @confirm="confirmPayment" />
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
.filter { min-width: 16rem; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin: 1rem 0; }
.stat { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1rem; display: flex; flex-direction: column; gap: 4px; }
.stat span { color: #6B7280; font-size: .85rem; }
.stat strong { font-size: 1.6rem; }
.stat.ok { background: #ECFDF5; border-color: #10B981; }
.stat.bad { background: #FEF2F2; border-color: #ef4444; }
</style>
