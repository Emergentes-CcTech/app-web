<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useAuditStore } from '../../application/audit.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { isAdmin } from '../../../shared/infrastructure/roles.js';
import { getSessionUserId, formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';
import { BillingApi } from '../../../billing/infrastructure/billing-api.js';
import { BillingAssembler } from '../../../billing/infrastructure/billing.assembler.js';
import { CondominiumApi } from '../../../condominiums/infrastructure/condominium-api.js';
import AuditTable from '../components/audit-table.vue';
import FeeExplanation from '../components/fee-explanation.vue';

const { t, locale } = useI18n();
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
const typeOptions = computed(() => ['proration', 'payment', 'alert'].map(v => ({ value: v, label: t(`audit.types.${v}`) })));
// residente
const lines = ref([]);
const selectedLineId = ref(null);
const explanation = ref(null);
const explanationLoading = ref(false);
const explanationFailed = ref(false);
const residentLoading = ref(true);
const lineOptions = computed(() => lines.value.map(l => ({ value: l.id, label: `${formatPeriod(l.period, locale.value)} · ${formatPen(l.amount)}` })));

async function loadAdmin() { if (condominiumId.value) await store.fetchEvents(condominiumId.value, { type: type.value, from: from.value && new Date(from.value).toISOString(), to: to.value && new Date(`${to.value}T23:59:59`).toISOString() }); }

async function loadExplanation(lineId) {
  if (!lineId) return;
  explanationLoading.value = true;
  explanationFailed.value = false;
  try {
    explanation.value = (await billingApi.getLineExplanation(lineId)).data;
  } catch (_) {
    explanation.value = null;
    explanationFailed.value = true;
  } finally {
    explanationLoading.value = false;
  }
}

onMounted(async () => {
  if (admin.value) {
    if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
    condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
  } else {
    try {
      const units = await condoApi.getUnitsByResident(getSessionUserId());
      const all = await Promise.all(units.map(u => billingApi.getLinesByUnit(u.id).then(r => r.data.map(BillingAssembler.toLine))));
      // La cuota más reciente primero
      lines.value = all.flat().sort((a, b) => String(b.period).localeCompare(String(a.period)));
      const wanted = Number(route.query.line);   // ?line=ID abre un desglose directamente
      selectedLineId.value = (lines.value.find(l => l.id === wanted) || lines.value[0])?.id ?? null;
    } catch (_) {
      explanationFailed.value = true;
    } finally {
      residentLoading.value = false;
    }
  }
});
watch([condominiumId, type, from, to], () => { if (admin.value) loadAdmin(); });
watch(selectedLineId, loadExplanation);

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
      <div class="head wrap">
        <div>
          <h1>{{ t('audit.residentTitle') }}</h1>
          <p class="muted subtitle">{{ t('audit.residentSubtitle') }}</p>
        </div>
        <pv-select v-if="lines.length > 1" v-model="selectedLineId" :options="lineOptions" optionLabel="label" optionValue="value"
                   :aria-label="t('audit.fee.selectPeriod')" class="filter" />
      </div>
      <div v-if="residentLoading || explanationLoading" class="center"><pv-progress-spinner /></div>
      <pv-message v-else-if="explanationFailed" severity="error" :closable="false">{{ t('audit.fee.loadError') }}</pv-message>
      <p v-else-if="!lines.length" class="muted">{{ t('payments.statement.empty') }}</p>
      <fee-explanation v-else-if="explanation" :explanation="explanation" />
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.head { display: flex; justify-content: space-between; align-items: center; }
.filters { display: flex; gap: .75rem; flex-wrap: wrap; margin-bottom: 1rem; }
.filter { min-width: 14rem; }
.wrap { flex-wrap: wrap; gap: .75rem 1rem; align-items: flex-end; margin-bottom: 1rem; }
.head h1 { margin: 0; }
.subtitle { margin: .25rem 0 0; }
.center { text-align: center; padding: 2rem; }
.muted { color: #6B7280; }
</style>
