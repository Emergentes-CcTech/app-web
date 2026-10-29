<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useAuditStore } from '../../application/audit.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { isAdmin } from '../../../shared/infrastructure/roles.js';
import { getSessionUserId } from '../../../shared/infrastructure/session.js';
import { BillingApi } from '../../../billing/infrastructure/billing-api.js';
import { BillingAssembler } from '../../../billing/infrastructure/billing.assembler.js';
import { CondominiumApi } from '../../../condominiums/infrastructure/condominium-api.js';
import LineBreakdown from '../../../billing/presentation/components/line-breakdown.vue';
import AuditTable from '../components/audit-table.vue';

const { t } = useI18n();
const route = useRoute();
const store = useAuditStore();
const condoStore = useCondominiumStore();
const iam = useIamStore();
const billingApi = new BillingApi();
const condoApi = new CondominiumApi();

const admin = computed(() => isAdmin(iam.currentUser?.role));
// administrador
const condominiumId = ref(null);
const type = ref(null);
const from = ref('');
const to = ref('');
const typeOptions = computed(() => ['reading', 'proration', 'manual_adjustment', 'participation_changed'].map(v => ({ value: v, label: t(`audit.types.${v}`) })));
// residente
const lines = ref([]);
const selectedLine = ref(null);
const bills = ref([]);
const myUnits = ref([]);

async function loadAdmin() { if (condominiumId.value) await store.fetchEvents(condominiumId.value, { type: type.value, from: from.value && new Date(from.value).toISOString(), to: to.value && new Date(`${to.value}T23:59:59`).toISOString() }); }

async function selectLine(line) {
  selectedLine.value = line;
  const settlement = (await billingApi.getSettlement(line.settlementId)).data;
  bills.value = BillingAssembler.toBills((await billingApi.getBills(settlement.condominiumId, settlement.period)).data);
  // cambios de porcentaje de su unidad
  await store.fetchEvents(settlement.condominiumId, { entity: `unit:${line.unitId}` });
}

onMounted(async () => {
  if (admin.value) {
    if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
    condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
  } else {
    myUnits.value = await condoApi.getUnitsByResident(getSessionUserId());
    const all = await Promise.all(myUnits.value.map(u => billingApi.getLinesByUnit(u.id).then(r => r.data.map(BillingAssembler.toLine))));
    lines.value = all.flat();
    const wanted = Number(route.query.line);   // ?line=ID abre un desglose directamente
    const initial = lines.value.find(l => l.id === wanted) || lines.value[0];
    if (initial) await selectLine(initial);
  }
});
watch([condominiumId, type, from, to], () => { if (admin.value) loadAdmin(); });

function exportCsv() {
  const rows = [['fecha', 'tipo', 'entidad', 'usuario', 'anterior', 'nuevo'], ...store.events.map(e => [e.at, e.type, e.entity, e.actorUserId, e.previousValue, e.newValue])];
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([rows.map(r => r.join(',')).join('\n')], { type: 'text/csv' }));
  a.download = 'auditoria.csv'; a.click(); URL.revokeObjectURL(a.href);
}
</script>

<template>
  <div class="page">
    <template v-if="admin">
      <div class="head"><h1>{{ t('audit.title') }}</h1><pv-button :label="t('audit.exportCsv')" icon="pi pi-download" severity="secondary" @click="exportCsv" /></div>
      <div class="filters">
        <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
        <pv-select v-model="type" :options="typeOptions" optionLabel="label" optionValue="value" showClear :placeholder="t('telemetry.type')" class="filter" />
        <pv-input-text v-model="from" type="date" :aria-label="t('audit.from')" /><pv-input-text v-model="to" type="date" :aria-label="t('audit.to')" />
      </div>
      <audit-table :events="store.events" :loading="store.isLoading" />
    </template>
    <template v-else>
      <h1>{{ t('audit.residentTitle') }}</h1>
      <p v-if="!lines.length" class="muted">{{ t('payments.statement.empty') }}</p>
      <div v-else class="split">
        <ul class="lines">
          <li v-for="l in lines" :key="l.id" :class="{ active: selectedLine?.id === l.id }" tabindex="0" @click="selectLine(l)" @keyup.enter="selectLine(l)">
            #{{ l.id }} · {{ l.breakdown?.readingsFrom?.slice(0, 7) }} · S/ {{ l.amount.toFixed(2) }}</li>
        </ul>
        <div v-if="selectedLine">
          <line-breakdown :line="selectedLine" :bills="bills" />
          <h3>{{ t('audit.participationChanges') }}</h3>
          <audit-table :events="store.events" :loading="store.isLoading" />
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.head { display: flex; justify-content: space-between; align-items: center; }
.filters { display: flex; gap: .75rem; flex-wrap: wrap; margin-bottom: 1rem; }
.filter { min-width: 14rem; }
.split { display: grid; grid-template-columns: 260px 1fr; gap: 1.5rem; }
@media (max-width: 800px) { .split { grid-template-columns: 1fr; } }
.lines { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: .5rem; }
.lines li { padding: .75rem; background: #fff; border: 1px solid #e5e7eb; border-radius: 8px; cursor: pointer; }
.lines li.active { border-color: #10B981; background: #ECFDF5; }
.muted { color: #6B7280; }
</style>
