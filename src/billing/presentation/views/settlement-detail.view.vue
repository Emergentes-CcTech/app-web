<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import { useBillingStore } from '../../application/billing.store.js';
import { BillingApi } from '../../infrastructure/billing-api.js';
import { BillingAssembler } from '../../infrastructure/billing.assembler.js';
import { CondominiumApi } from '../../../condominiums/infrastructure/condominium-api.js';
import { useResidentStore } from '../../../residents/application/resident.store.js';
import { formatPeriod } from '../../../shared/infrastructure/session.js';
import SettlementTable from '../components/settlement-table.vue';

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const confirm = useConfirm();
const store = useBillingStore();
const residentStore = useResidentStore();

const units = ref([]);
const bills = ref([]);
const loading = ref(true);
const s = computed(() => store.currentSettlement);
const unitsById = computed(() => Object.fromEntries(units.value.map(u => [u.id, u])));
const residentsByUnit = computed(() => Object.fromEntries(residentStore.residents.map(r => [r.unitId, r.fullName])));

onMounted(async () => {
  try {
    const settlement = await store.fetchSettlement(route.params.id);
    units.value = await new CondominiumApi().getUnits(settlement.condominiumId);
    residentStore.fetchByCondominium(settlement.condominiumId);
    bills.value = BillingAssembler.toBills((await new BillingApi().getBills(settlement.condominiumId, settlement.period)).data);
  } finally { loading.value = false; }
});

function issue() {
  confirm.require({
    message: t('billing.settlements.confirmIssue'), header: t('billing.settlements.issue'), icon: 'pi pi-send',
    acceptLabel: t('billing.settlements.issue'), rejectLabel: t('common.cancel'),
    accept: async () => { await store.issue(s.value.id); toast.add({ severity: 'success', summary: t('common.success'), detail: t('billing.settlements.issued'), life: 2500 }); },
  });
}
</script>

<template>
  <div class="page">
    <div v-if="loading" class="center"><pv-progress-spinner /></div>
    <template v-else-if="s">
      <div class="head">
        <h1>{{ t('billing.settlements.detailTitle') }} · {{ formatPeriod(s.period, locale) }}</h1>
        <div class="flex gap-2">
          <pv-button :label="t('common.back')" icon="pi pi-arrow-left" severity="secondary" @click="router.push({ name: 'settlements' })" />
          <pv-button v-if="!s.isIssued" :label="t('billing.settlements.issue')" icon="pi pi-send" @click="issue" />
        </div>
      </div>
      <pv-message v-if="s.isIssued" severity="info" :closable="false">{{ t('billing.settlements.readOnly') }}</pv-message>
      <settlement-table :settlement="s" :unitsById="unitsById" :residentsByUnit="residentsByUnit" :bills="bills" />
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
.center { text-align: center; padding: 2rem; }
</style>
