<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useBillingStore } from '../../application/billing.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { usePaymentStore } from '../../../payments/application/payment.store.js';
import { useResidentStore } from '../../../residents/application/resident.store.js';
import { formatPen } from '../../../shared/infrastructure/session.js';
import BillForm from '../components/bill-form.vue';
import ReconciliationCard from '../components/reconciliation-card.vue';
import RegisterPaymentDialog from '../../../payments/presentation/components/register-payment-dialog.vue';

const props = defineProps({
  defaultTab: {
    type: String,
    default: 'bills'
  }
});

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();

const store = useBillingStore();
const condoStore = useCondominiumStore();
const paymentStore = usePaymentStore();
const residentStore = useResidentStore();

const condominiumId = ref(null);
const period = ref(new Date().toISOString().slice(0, 7));
const saving = ref(false);
const calculating = ref(false);

// State for active tab synchronized with route query or props
const validTabs = ['bills', 'settlements', 'delinquency'];
const initialTab = (route.query.tab && validTabs.includes(route.query.tab))
  ? route.query.tab
  : (props.defaultTab || 'bills');

const activeTab = ref(initialTab);

watch(() => props.defaultTab, (val) => {
  if (val && validTabs.includes(val)) {
    activeTab.value = val;
  }
});

watch(() => route.query.tab, (val) => {
  if (val && validTabs.includes(val)) {
    activeTab.value = val;
  }
});

function onTabChange(tab) {
  activeTab.value = tab;
  router.replace({ query: { ...route.query, tab } }).catch(() => {});
}

// El título nombra la sección del sidebar; las pestañas ya indican la subvista activa
const activeTitle = computed(() => t('option.billing'));

// Conciliation bill for current period
const periodBill = computed(() => store.bills.find(b => b.period === period.value) || null);

// Delinquency overdue rows
const overdueRows = computed(() => paymentStore.overdueUnitIds.map(unitId => {
  const list = paymentStore.overduePayments.filter(p => p.unitId === unitId);
  const resident = residentStore.residents.find(r => r.unitId === unitId);
  return {
    unitId,
    unitNumber: resident?.unitNumber ?? unitId,
    resident: resident?.fullName || '—',
    periods: list.map(p => p.period).sort().join(', '),
    amount: Math.round(list.reduce((a, p) => a + p.amount * 100, 0)) / 100,
    daysOverdue: Math.max(...list.map(p => p.daysOverdue)),
    first: [...list].sort((a, b) => String(a.dueDate).localeCompare(String(b.dueDate)))[0],
  };
}));

const paymentDialog = ref(false);
const selectedPayment = ref(null);

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});

watch(condominiumId, (id) => {
  if (id) {
    store.fetchBills(id);
    store.fetchSettlements(id);
    paymentStore.fetchByCondominium(id);
    residentStore.fetchByCondominium(id);
  }
}, { immediate: true });

async function submitBill(bill) {
  saving.value = true;
  try {
    await store.addBill({ ...bill, condominiumId: condominiumId.value });
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('billing.bills.saved'), life: 2500 });
  } catch (_) {
    toast.add({ severity: 'error', summary: 'Error', detail: t('billing.messages.error'), life: 3000 });
  } finally {
    saving.value = false;
  }
}

async function calculateSettlement() {
  calculating.value = true;
  try {
    const id = await store.calculate(condominiumId.value, period.value);
    router.push({ name: 'settlement-detail', params: { id } });
  } catch (e) {
    const key = { participation_not_100: 'billing.messages.notBalanced', no_bill: 'billing.messages.noBill' }[e.message] || 'billing.messages.error';
    toast.add({ severity: 'warn', summary: t('billing.settlements.calculate'), detail: t(key), life: 4500 });
  } finally {
    calculating.value = false;
  }
}

function openRegister(row) {
  selectedPayment.value = row.first;
  paymentDialog.value = true;
}

async function confirmPayment(data) {
  await paymentStore.register(selectedPayment.value.id, data);
  paymentDialog.value = false;
  toast.add({ severity: 'success', summary: t('common.success'), detail: t('payments.register.saved'), life: 2500 });
  residentStore.fetchByCondominium(condominiumId.value);
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ activeTitle }}</h1>
      <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
    </div>

    <pv-tabs :value="activeTab" @update:value="onTabChange" class="billing-tabs">
      <pv-tab-list class="tabs-header mb-4">
        <pv-tab value="bills">
          <i class="pi pi-file mr-2"></i>{{ t('billing.bills.title') }}
        </pv-tab>
        <pv-tab value="settlements">
          <i class="pi pi-calculator mr-2"></i>{{ t('billing.settlements.title') }}
        </pv-tab>
        <pv-tab value="delinquency">
          <i class="pi pi-exclamation-circle mr-2"></i>{{ t('payments.delinquency.title') }}
          <pv-tag v-if="paymentStore.overdueUnitIds.length > 0" severity="danger" class="ml-2" :value="paymentStore.overdueUnitIds.length" />
        </pv-tab>
      </pv-tab-list>

      <pv-tab-panels>
        <!-- Tab 1: Recibos matriz -->
        <pv-tab-panel value="bills">
          <div v-if="activeTab === 'bills'">
            <div class="filters">
              <div>
                <label class="lbl">{{ t('billing.bills.period') }}</label>
                <pv-input-text v-model="period" type="month" />
              </div>
            </div>
            <bill-form :period="period" :loading="saving" @submit="submitBill" />
            <reconciliation-card :bill="periodBill" />
            <pv-data-table :value="store.bills" :loading="store.isLoading" dataKey="id" stripedRows class="mt-4">
              <template #empty>{{ t('billing.bills.empty') }}</template>
              <pv-column field="period" :header="t('billing.bills.period')" sortable />
              <pv-column :header="t('billing.bills.service')">
                <template #body="{ data }">{{ t(`billing.bills.${data.service}`) }}</template>
              </pv-column>
              <pv-column :header="t('billing.bills.billedConsumption')">
                <template #body="{ data }">{{ data.billedConsumption }} {{ data.consumptionUnit }}</template>
              </pv-column>
              <pv-column :header="t('billing.bills.measuredConsumption')">
                <template #body="{ data }">{{ data.measuredConsumption ?? '—' }}</template>
              </pv-column>
              <pv-column :header="t('billing.bills.amount')">
                <template #body="{ data }">{{ formatPen(data.billedAmount) }}</template>
              </pv-column>
              <pv-column field="dueDate" :header="t('billing.bills.dueDate')" />
              <pv-column :header="t('billing.bills.attachment')">
                <template #body="{ data }">
                  <a v-if="data.attachmentUrl" :href="data.attachmentUrl" target="_blank" rel="noopener" class="receipt-link"><i class="pi pi-file" aria-hidden="true"></i> {{ t('billing.bills.viewReceipt') }}</a>
                  <span v-else>—</span>
                </template>
              </pv-column>
            </pv-data-table>
          </div>
        </pv-tab-panel>

        <!-- Tab 2: Prorrateo y liquidación -->
        <pv-tab-panel value="settlements">
          <div v-if="activeTab === 'settlements'">
            <div class="filters">
              <div>
                <label class="lbl">{{ t('billing.bills.period') }}</label>
                <pv-input-text v-model="period" type="month" />
              </div>
              <pv-button :label="t('billing.settlements.calculate')" icon="pi pi-calculator" :loading="calculating" @click="calculateSettlement" />
            </div>
            <pv-data-table :value="store.settlements" :loading="store.isLoading" dataKey="id" stripedRows class="mt-4">
              <template #empty>{{ t('billing.settlements.empty') }}</template>
              <pv-column field="period" :header="t('billing.bills.period')" sortable />
              <pv-column :header="t('billing.settlements.total')">
                <template #body="{ data }">{{ formatPen(data.commonAreaAmount) }}</template>
              </pv-column>
              <pv-column :header="t('payments.statement.status')">
                <template #body="{ data }">
                  <pv-tag :severity="data.isIssued ? 'success' : 'secondary'" :value="t(`billing.settlements.status.${data.status}`)" />
                </template>
              </pv-column>
              <pv-column :header="t('common.actions')">
                <template #body="{ data }">
                  <pv-button :label="t('common.view')" icon="pi pi-eye" text size="small" @click="router.push({ name: 'settlement-detail', params: { id: data.id } })" />
                </template>
              </pv-column>
            </pv-data-table>
          </div>
        </pv-tab-panel>

        <!-- Tab 3: Morosidad -->
        <pv-tab-panel value="delinquency">
          <div v-if="activeTab === 'delinquency'">
            <div class="stats">
              <div class="stat" :class="paymentStore.delinquencyRate > 0 ? 'bad' : 'ok'">
                <span>{{ t('payments.delinquency.rate') }}</span>
                <strong>{{ paymentStore.delinquencyRate }} %</strong>
              </div>
              <div class="stat">
                <span>{{ t('payments.delinquency.overdueAmount') }}</span>
                <strong>{{ formatPen(paymentStore.overdueAmount) }}</strong>
              </div>
              <div class="stat">
                <span>{{ t('payments.delinquency.units') }}</span>
                <strong>{{ paymentStore.overdueUnitIds.length }} / {{ paymentStore.unitsTotal }}</strong>
              </div>
            </div>
            <pv-data-table :value="overdueRows" :loading="paymentStore.isLoading" dataKey="unitId" stripedRows class="mt-4">
              <template #empty>{{ t('payments.delinquency.empty') }}</template>
              <pv-column field="unitNumber" :header="t('residents.fields.unit')" />
              <pv-column field="resident" :header="t('payments.delinquency.resident')" />
              <pv-column field="periods" :header="t('payments.delinquency.periods')" />
              <pv-column :header="t('payments.register.amount')">
                <template #body="{ data }">{{ formatPen(data.amount) }}</template>
              </pv-column>
              <pv-column field="daysOverdue" :header="t('payments.delinquency.daysOverdue')" />
              <pv-column :header="t('common.actions')">
                <template #body="{ data }">
                  <pv-button :label="t('payments.delinquency.register')" icon="pi pi-check-circle" size="small" @click="openRegister(data)" />
                </template>
              </pv-column>
            </pv-data-table>
            <register-payment-dialog v-model:visible="paymentDialog" :payment="selectedPayment" @confirm="confirmPayment" />
          </div>
        </pv-tab-panel>
      </pv-tab-panels>
    </pv-tabs>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.receipt-link { color: var(--iobuilt-primary-hover); text-decoration: none; white-space: nowrap; }
.receipt-link:hover { text-decoration: underline; }
.page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.5rem; }
.filter { min-width: 16rem; }
.billing-tabs { background: #ffffff; border-radius: 12px; padding: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
.tabs-header { border-bottom: 1px solid #e2e8f0; }
.filters { display: flex; gap: 1rem; flex-wrap: wrap; align-items: end; margin-bottom: 1rem; }
.lbl { display: block; font-size: .85rem; color: #6B7280; margin-bottom: 4px; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin: 1rem 0; }
.stat { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1rem; display: flex; flex-direction: column; gap: 4px; }
.stat span { color: #6B7280; font-size: .85rem; }
.stat strong { font-size: 1.6rem; }
.stat.ok { background: #ECFDF5; border-color: #10B981; }
.stat.bad { background: #FEF2F2; border-color: #ef4444; }
.mt-4 { margin-top: 1rem; }
.mb-4 { margin-bottom: 1rem; }
.mr-2 { margin-right: 0.5rem; }
.ml-2 { margin-left: 0.5rem; }
</style>
