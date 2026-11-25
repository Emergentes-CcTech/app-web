<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { useTelemetryStore } from '../../../telemetry/application/telemetry.store.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import { DemoToolsApi } from '../../infrastructure/demo-tools-api.js';

// Herramientas para presentar IoBuild sin esperar eventos reales: alertas al instante y adelanto de un mes
const { t, locale } = useI18n();
const toast = useToast();
const confirm = useConfirm();
const condoStore = useCondominiumStore();
const telemetryStore = useTelemetryStore();
const api = new DemoToolsApi();

const condominiumId = ref(null);
const snapshot = ref(null);
const busy = ref('');

const alertTypes = [
  { type: 'leak', icon: 'pi pi-filter' },
  { type: 'overload', icon: 'pi pi-bolt' },
  { type: 'gateway', icon: 'pi pi-wifi' },
];

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});

async function load() {
  snapshot.value = null;
  if (!condominiumId.value) return;
  try { snapshot.value = await api.snapshot(condominiumId.value); } catch (_) { snapshot.value = null; }
}
watch(condominiumId, load);

const errorDetail = (e) => e?.response?.data?.error || t('demoTools.error');
const pen = (n) => `S/ ${Number(n || 0).toFixed(2)}`;
const date = (iso) => iso ? new Date(`${iso}T12:00:00`).toLocaleDateString(locale.value, { day: 'numeric', month: 'short', year: 'numeric' }) : '';

async function createAlert(type) {
  busy.value = type;
  try {
    const alert = await api.createAlert(condominiumId.value, type);
    toast.add({ severity: alert.severity === 'Critical' ? 'error' : 'warn', summary: alert.title,
      detail: Number(alert.estimatedOvercostSoles) > 0 ? t('demoTools.alerts.created', { amount: pen(alert.estimatedOvercostSoles) }) : t('demoTools.alerts.createdNoCost'), life: 5000 });
    telemetryStore.fetchOpenAlerts();
    await load();
  } catch (e) {
    toast.add({ severity: 'error', summary: t('demoTools.alerts.title'), detail: errorDetail(e), life: 4000 });
  } finally { busy.value = ''; }
}

async function resolveAll() {
  busy.value = 'resolve';
  try {
    const { resolved } = await api.resolveAll(condominiumId.value);
    toast.add({ severity: 'success', summary: t('demoTools.alerts.title'), detail: t('demoTools.alerts.resolved', { n: resolved }), life: 3000 });
    telemetryStore.fetchOpenAlerts();
    await load();
  } catch (e) {
    toast.add({ severity: 'error', summary: t('demoTools.alerts.title'), detail: errorDetail(e), life: 4000 });
  } finally { busy.value = ''; }
}

function advanceMonth() {
  confirm.require({
    header: t('demoTools.time.title'),
    message: t('demoTools.time.confirm'),
    icon: 'pi pi-forward',
    acceptLabel: t('demoTools.time.advance'),
    rejectLabel: t('common.cancel'),
    accept: async () => {
      busy.value = 'time';
      try {
        snapshot.value = await api.advanceMonth(condominiumId.value);
        const overdue = snapshot.value.periods.reduce((s, p) => s + p.overdue, 0);
        toast.add({ severity: 'success', summary: t('demoTools.time.title'), detail: t('demoTools.time.done', { n: overdue }), life: 5000 });
      } catch (e) {
        toast.add({ severity: 'error', summary: t('demoTools.time.title'), detail: errorDetail(e), life: 5000 });
      } finally { busy.value = ''; }
    },
  });
}

const totalOverdue = computed(() => (snapshot.value?.periods || []).reduce((s, p) => s + p.overdue, 0));
</script>

<template>
  <div class="page">
    <header class="head">
      <div>
        <h1>{{ t('demoTools.title') }}</h1>
        <p class="muted">{{ t('demoTools.subtitle') }}</p>
      </div>
      <pv-select v-if="condoStore.activeCondominiums.length" v-model="condominiumId" :options="condoStore.activeCondominiums"
                 optionLabel="name" optionValue="id" :aria-label="t('demoTools.condominium')" class="condo" />
    </header>

    <p v-if="!condoStore.activeCondominiums.length" class="muted">{{ t('demoTools.noCondominium') }}</p>

    <template v-else>
      <section class="card">
        <div class="card-head">
          <div>
            <h2><i class="pi pi-bell" aria-hidden="true" /> {{ t('demoTools.alerts.title') }}</h2>
            <p class="muted">{{ t('demoTools.alerts.lead') }}</p>
          </div>
          <span class="badge" :class="{ hot: snapshot?.openAlerts }">{{ t('demoTools.alerts.open', { n: snapshot?.openAlerts ?? 0 }) }}</span>
        </div>
        <div class="alert-grid">
          <button v-for="a in alertTypes" :key="a.type" type="button" class="alert-option" :disabled="!!busy" @click="createAlert(a.type)">
            <i :class="busy === a.type ? 'pi pi-spin pi-spinner' : a.icon" aria-hidden="true" />
            <strong>{{ t(`demoTools.alerts.types.${a.type}.label`) }}</strong>
            <small>{{ t(`demoTools.alerts.types.${a.type}.hint`) }}</small>
          </button>
        </div>
        <div class="actions">
          <router-link :to="ROUTES.ALERTS" class="link">{{ t('demoTools.alerts.view') }}</router-link>
          <pv-button :label="t('demoTools.alerts.resolveAll')" icon="pi pi-check-square" severity="secondary" outlined
                     :loading="busy === 'resolve'" :disabled="!snapshot?.openAlerts || (!!busy && busy !== 'resolve')" @click="resolveAll" />
        </div>
      </section>

      <section class="card">
        <div class="card-head">
          <div>
            <h2><i class="pi pi-forward" aria-hidden="true" /> {{ t('demoTools.time.title') }}</h2>
            <p class="muted">{{ t('demoTools.time.lead') }}</p>
          </div>
          <span class="badge" :class="{ hot: totalOverdue }">{{ t('demoTools.time.overdue', { n: totalOverdue }) }}</span>
        </div>
        <pv-data-table v-if="snapshot?.periods?.length" :value="snapshot.periods" dataKey="period" size="small" class="periods">
          <pv-column field="period" :header="t('demoTools.time.period')" />
          <pv-column :header="t('demoTools.time.due')"><template #body="{ data }">{{ date(data.dueDate) }}</template></pv-column>
          <pv-column field="statements" :header="t('demoTools.time.fees')" />
          <pv-column field="paid" :header="t('demoTools.time.paid')" />
          <pv-column field="pending" :header="t('demoTools.time.pending')" />
          <pv-column :header="t('demoTools.time.overdueCol')">
            <template #body="{ data }"><span :class="{ danger: data.overdue }">{{ data.overdue }}</span></template>
          </pv-column>
        </pv-data-table>
        <p v-else class="muted">{{ t('demoTools.time.empty') }}</p>
        <div class="actions">
          <router-link to="/payments/delinquency" class="link">{{ t('demoTools.time.viewDelinquency') }}</router-link>
          <pv-button :label="t('demoTools.time.advance')" icon="pi pi-forward" :loading="busy === 'time'"
                     :disabled="!snapshot?.periods?.length || (!!busy && busy !== 'time')" @click="advanceMonth" />
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1100px; margin: 0 auto; display: flex; flex-direction: column; gap: 1.25rem; }
.head { display: flex; justify-content: space-between; align-items: flex-end; gap: 1rem; flex-wrap: wrap; }
.head h1 { margin: 0; }
.head p { margin: .25rem 0 0; }
.condo { min-width: 16rem; }
.muted { color: #6B7280; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
.card-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
.card-head h2 { margin: 0; font-size: 1.1rem; display: flex; gap: .5rem; align-items: center; }
.card-head p { margin: .3rem 0 0; max-width: 46rem; }
.badge { white-space: nowrap; background: #f3f4f6; color: #374151; border-radius: 999px; padding: .25rem .7rem; font-size: .8rem; font-weight: 600; }
.badge.hot { background: #fef2f2; color: #b91c1c; }
.alert-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); gap: .75rem; }
.alert-option { text-align: left; display: flex; flex-direction: column; gap: .3rem; border: 1px solid #e5e7eb; border-radius: 10px; padding: .9rem; background: #fafafa; cursor: pointer; font: inherit; }
.alert-option:hover:not(:disabled) { border-color: #10B981; background: #ecfdf5; }
.alert-option:disabled { opacity: .6; cursor: progress; }
.alert-option i { color: #10B981; font-size: 1.1rem; }
.alert-option small { color: #6B7280; }
.actions { display: flex; justify-content: space-between; align-items: center; gap: .75rem; flex-wrap: wrap; }
.link { color: #059669; font-weight: 600; text-decoration: none; }
.danger { color: #b91c1c; font-weight: 700; }
</style>
