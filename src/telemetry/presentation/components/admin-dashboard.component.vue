<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { Line, Bar } from 'vue-chartjs';
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler } from 'chart.js';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { usePaymentStore } from '../../../payments/application/payment.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { useSubscriptionStore } from '../../../subscriptions/application/subscription.store.js';
import { projectToMonthEnd } from '../../application/projection.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import StatCard from './stat-card.component.vue';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler);

// RF-010: panel del condominio para el administrador
const { t } = useI18n();
const router = useRouter();
const telemetry = useTelemetryStore();
const payments = usePaymentStore();
const devices = useDeviceStore();
const condoStore = useCondominiumStore();
const subscriptions = useSubscriptionStore();

// Resumen de condominios: departamentos, áreas comunes y estado de la suscripción de cada uno
const portfolio = computed(() => condoStore.activeCondominiums.map(c => {
  const sub = subscriptions.subscriptionFor(c.id);
  return { id: c.id, name: c.name, district: c.district, totalUnits: c.totalUnits, commonAreas: c.commonAreasCount,
    plan: subscriptions.planFor(sub)?.name || '—', active: !!sub?.isActive(), hasSub: !!sub, nextBilling: sub?.nextBillingDate || '—' };
}));
const totalApartments = computed(() => portfolio.value.reduce((a, c) => a + c.totalUnits, 0));
const totalCommonAreas = computed(() => portfolio.value.reduce((a, c) => a + c.commonAreas, 0));
const activeSubs = computed(() => portfolio.value.filter(c => c.active).length);

const condominiumId = ref(null);
const metric = ref('water');
const metricOptions = computed(() => [{ value: 'water', label: t('dashboard.admin.water') }, { value: 'energy', label: t('dashboard.admin.energy') }]);

const water = computed(() => telemetry.consumptionOf(telemetry.monthReadings, 'L/min'));
const energy = computed(() => telemetry.consumptionOf(telemetry.monthReadings, 'A'));
const projection = computed(() => projectToMonthEnd(water.value));
const criticalCount = computed(() => telemetry.openAlerts.filter(a => a.isCritical).length);
const recentAlerts = computed(() => telemetry.openAlerts.slice(0, 5));

// Consumo diario del mes (agua o energía)
const daily = computed(() => {
  const unit = metric.value === 'water' ? 'L/min' : 'A';
  const byDay = {};
  telemetry.monthReadings.filter(r => r.unit === unit).forEach(r => { (byDay[r.timestamp.slice(8, 10)] ||= []).push(r); });
  const labels = Object.keys(byDay).sort();
  return { labels, values: labels.map(d => telemetry.consumptionOf(byDay[d], unit)) };
});
const lineData = computed(() => ({ labels: daily.value.labels, datasets: [{ label: metric.value === 'water' ? 'm³' : 'kWh', data: daily.value.values, borderColor: '#059669', backgroundColor: 'rgba(16,185,129,.15)', fill: true, tension: .3 }] }));
const barData = computed(() => ({ labels: [t('dashboard.admin.currentMonth')], datasets: [{ label: 'm³', data: [water.value], backgroundColor: '#10B981' }, { label: 'kWh', data: [energy.value], backgroundColor: '#f59e0b' }] }));
const chartOpts = { responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true } } };

const summaries = computed(() => condoStore.activeCondominiums.map(c => ({
  ...c, online: c.id === condominiumId.value ? devices.online : null, total: c.id === condominiumId.value ? devices.total : null,
  alerts: c.id === condominiumId.value ? telemetry.openAlertsCount : null,
})));

async function load() {
  const id = condominiumId.value;
  if (!id) return;
  await Promise.all([telemetry.fetchMonth(id), telemetry.fetchAlerts(id), devices.fetchByCondominium(id), payments.fetchByCondominium(id)]);
}
onMounted(async () => {
  subscriptions.fetchAvailablePlans();
  subscriptions.fetchSubscriptions();
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, load, { immediate: true });
onUnmounted(() => telemetry.stopPolling());
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('dashboard.admin.title') }}</h1>
      <div class="flex gap-2">
        <pv-select v-if="condoStore.activeCondominiums.length > 1" v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
        <pv-button :label="t('dashboard.admin.refresh')" icon="pi pi-refresh" severity="secondary" @click="load" />
      </div>
    </div>
    <p v-if="!condoStore.activeCondominiums.length" class="muted">{{ t('condominiums.empty') }}</p>

    <div class="stats">
      <stat-card :title="t('dashboard.admin.portfolio.condominiums')" :value="portfolio.length" icon="pi-building" iconBgColor="bg-teal-100" iconTextColor="text-teal-600" />
      <stat-card :title="t('dashboard.admin.portfolio.apartments')" :value="totalApartments" icon="pi-home" iconBgColor="bg-blue-100" iconTextColor="text-blue-600" />
      <stat-card :title="t('dashboard.admin.portfolio.commonAreas')" :value="totalCommonAreas" icon="pi-th-large" iconBgColor="bg-purple-100" iconTextColor="text-purple-600" />
      <stat-card :title="t('dashboard.admin.portfolio.subscriptions')" :value="`${activeSubs} / ${portfolio.length}`" icon="pi-credit-card"
                 :iconBgColor="activeSubs === portfolio.length ? 'bg-green-100' : 'bg-orange-100'" :iconTextColor="activeSubs === portfolio.length ? 'text-green-600' : 'text-orange-600'" />
    </div>

    <section class="card">
      <div class="head"><h3>{{ t('dashboard.admin.portfolio.title') }}</h3>
        <pv-button :label="t('option.subscription')" text size="small" icon="pi pi-credit-card" @click="router.push(ROUTES.SUBSCRIPTION_DETAIL)" /></div>
      <pv-data-table :value="portfolio" dataKey="id" size="small" stripedRows>
        <pv-column field="name" :header="t('condominiums.fields.name')">
          <template #body="{ data }"><router-link :to="{ name: 'condominium-details', params: { id: data.id } }">{{ data.name }}</router-link><br><small class="muted">{{ data.district }}</small></template>
        </pv-column>
        <pv-column field="totalUnits" :header="t('dashboard.admin.portfolio.apartments')" />
        <pv-column field="commonAreas" :header="t('dashboard.admin.portfolio.commonAreas')" />
        <pv-column field="plan" :header="t('dashboard.admin.portfolio.plan')" />
        <pv-column :header="t('dashboard.admin.portfolio.status')">
          <template #body="{ data }">
            <pv-tag v-if="data.hasSub" :severity="data.active ? 'success' : 'warn'" :value="data.active ? t('subscriptions.status-active') : t('subscriptions.status-cancelled')" />
            <pv-tag v-else severity="secondary" :value="t('condominiums.noSubscription')" />
          </template>
        </pv-column>
        <pv-column field="nextBilling" :header="t('subscriptions.nextBilling')" />
      </pv-data-table>
    </section>

    <h2 class="section-title">{{ t('dashboard.admin.consumptionTitle') }}</h2>
    <div class="stats">
      <stat-card :title="t('dashboard.admin.stats.waterMonth')" :value="`${water} m³`" icon="pi-wallet" iconBgColor="bg-green-100" iconTextColor="text-green-600" />
      <stat-card :title="t('dashboard.admin.stats.pumpEnergy')" :value="`${energy} kWh`" icon="pi-bolt" iconBgColor="bg-yellow-100" iconTextColor="text-yellow-600" />
      <stat-card :title="t('dashboard.admin.stats.projection')" :value="`${projection} m³`" icon="pi-chart-line" />
      <stat-card :title="t('dashboard.admin.stats.alerts')" :value="telemetry.openAlertsCount" icon="pi-bell"
                 :iconBgColor="criticalCount ? 'bg-red-100' : 'bg-green-100'" :iconTextColor="criticalCount ? 'text-red-600' : 'text-green-600'" />
      <stat-card :title="t('dashboard.admin.stats.delinquency')" :value="`${payments.delinquencyRate} %`" icon="pi-exclamation-circle"
                 :iconBgColor="payments.delinquencyRate > 20 ? 'bg-red-100' : 'bg-green-100'" :iconTextColor="payments.delinquencyRate > 20 ? 'text-red-600' : 'text-green-600'" />
    </div>

    <div class="charts">
      <section class="card"><div class="head"><h3>{{ t('dashboard.admin.charts.daily') }}</h3>
        <pv-select-button v-model="metric" :options="metricOptions" optionLabel="label" optionValue="value" :allowEmpty="false" /></div>
        <div class="chart"><Line :data="lineData" :options="chartOpts" /></div></section>
      <section class="card"><h3>{{ t('dashboard.admin.charts.comparison') }}</h3><div class="chart"><Bar :data="barData" :options="chartOpts" /></div></section>
    </div>

    <section class="card">
      <div class="head"><h3>{{ t('dashboard.admin.recentAlerts') }}</h3><pv-button :label="t('common.view')" text size="small" @click="router.push(ROUTES.ALERTS)" /></div>
      <p v-if="!recentAlerts.length" class="muted">{{ t('telemetry.noAlerts') }}</p>
      <ul v-else class="alerts"><li v-for="a in recentAlerts" :key="a.id"><pv-tag :severity="a.isCritical ? 'danger' : 'warn'" :value="t(`telemetry.severities.${a.severity}`)" /> {{ t(`telemetry.alertTypes.${a.type}`) }} <small>{{ new Date(a.createdAt).toLocaleString() }}</small></li></ul>
    </section>

    <div v-if="condoStore.activeCondominiums.length > 1" class="condos">
      <article v-for="c in summaries" :key="c.id" class="card"><strong>{{ c.name }}</strong><small>{{ c.district }}</small>
        <small v-if="c.total !== null">{{ t('dashboard.admin.devicesOnline', { online: c.online, total: c.total }) }} · {{ t('dashboard.admin.stats.alerts') }}: {{ c.alerts }}</small></article>
    </div>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1280px; margin: 0 auto; display: flex; flex-direction: column; gap: 1rem; }
.page-head, .head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
.filter { min-width: 14rem; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 1rem; }
.charts { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; }
@media (max-width: 900px) { .charts { grid-template-columns: 1fr; } }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: .5rem; }
.chart { height: 260px; }
.alerts { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: .5rem; }
.condos { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 1rem; }
.muted { color: #6B7280; }
.section-title { font-size: 1.15rem; margin: .5rem 0 0; }
</style>
