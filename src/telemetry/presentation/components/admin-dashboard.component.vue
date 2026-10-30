<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { Line, Bar } from 'vue-chartjs';
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler } from 'chart.js';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { usePaymentStore } from '../../../payments/application/payment.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { useSubscriptionStore } from '../../../subscriptions/application/subscription.store.js';
import { DeviceType } from '../../../devices/domain/model/device.entity.js';
import { projectToMonthEnd } from '../../application/projection.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import StatCard from './stat-card.component.vue';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler);

// RF-010 + RF-011: panel único del administrador. Reúne KPIs del mes, equipos en vivo y alertas,
// que antes vivían en pantallas separadas (Monitoreo y Alertas).
const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const telemetry = useTelemetryStore();
const payments = usePaymentStore();
const devices = useDeviceStore();
const condoStore = useCondominiumStore();
const subscriptions = useSubscriptionStore();

const condominiumId = ref(null);
const condominium = computed(() => condoStore.activeCondominiums.find(c => c.id === condominiumId.value) || null);
const subscription = computed(() => condominiumId.value ? subscriptions.subscriptionFor(condominiumId.value) : null);
const planName = computed(() => subscriptions.planFor(subscription.value)?.name || null);
const hasActiveSubscription = computed(() => !!subscription.value?.isActive());
const lastUpdated = ref(null);

// ── KPIs del mes a partir del consumo diario (lecturas acumuladas → consumo real) ──
const monthKey = computed(() => new Date().toISOString().slice(0, 7));
const currentMonthDays = computed(() => telemetry.dailyConsumption.filter(d => d.date.startsWith(monthKey.value)));
const round2 = (n) => Math.round(n * 100) / 100;
const waterMonth = computed(() => round2(currentMonthDays.value.reduce((a, d) => a + d.waterM3, 0)));
const energyMonth = computed(() => round2(currentMonthDays.value.reduce((a, d) => a + d.energyKwh, 0)));
const criticalCount = computed(() => telemetry.openAlerts.filter(a => a.isCritical).length);
const formatNumber = (n, digits = 1) => Number(n).toLocaleString(locale.value === 'es' ? 'es-PE' : 'en-US', { maximumFractionDigits: digits });

// ── Equipos del kit HaaS ──
const gateway = computed(() => devices.devices.find(d => d.type === DeviceType.GATEWAY) || null);
const meters = computed(() => devices.devices.filter(d => d.type !== DeviceType.GATEWAY));
const selectedId = ref(null);
const selected = computed(() => meters.value.find(d => d.id === selectedId.value) || null);
const latest = (d) => telemetry.latestByDevice[d.id] || null;
const isWater = (d) => d.type === DeviceType.WATER_PULSE_METER;
function sinceLabel(iso) {
  if (!iso) return '—';
  const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  return minutes < 1 ? t('dashboard.admin.justNow') : t('dashboard.admin.minutesAgo', { n: minutes });
}

// ── Gráficos ──
const metric = ref('water');
const metricOptions = computed(() => [{ value: 'water', label: t('dashboard.admin.water') }, { value: 'energy', label: t('dashboard.admin.energy') }]);
const dailyChart = computed(() => {
  const days = currentMonthDays.value;
  const water = metric.value === 'water';
  return {
    labels: days.map(d => d.date.slice(8, 10)),
    datasets: [{
      label: water ? 'm³' : 'kWh',
      data: days.map(d => water ? d.waterM3 : d.energyKwh),
      backgroundColor: water ? '#10B981' : '#f59e0b',
      borderRadius: 4,
      maxBarThickness: 32,
    }],
  };
});
const liveChart = computed(() => {
  const list = selected.value ? (telemetry.readingsByDevice[selected.value.id] || []) : [];
  return {
    labels: list.map(r => new Date(r.timestamp).toLocaleTimeString(locale.value, { hour: '2-digit', minute: '2-digit' })),
    datasets: [{ label: list[0]?.rateUnit || '', data: list.map(r => r.rate), borderColor: '#059669', backgroundColor: 'rgba(16,185,129,.12)', fill: true, tension: .3, pointRadius: 0 }],
  };
});
const chartOpts = { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true } } };
const liveOpts = { ...chartOpts, scales: { ...chartOpts.scales, x: { ticks: { maxTicksLimit: 8, maxRotation: 0 }, grid: { display: false } } } };

// ── Alertas (antes en su propia vista) ──
const alertFilter = ref('open');
const alertFilterOptions = computed(() => [{ value: 'open', label: t('dashboard.admin.openAlerts') }, { value: 'all', label: t('dashboard.admin.allAlerts') }]);
const alertRows = computed(() => (alertFilter.value === 'open' ? telemetry.alerts.filter(a => a.isOpen) : telemetry.alerts));
const deviceName = (id) => devices.devices.find(d => d.id === id)?.deviceId ?? `#${id}`;
const alertStatusLabel = (s) => t(`telemetry.alertStatus.${s}`, s);
async function ack(a) {
  await telemetry.acknowledge(a.id);
  toast.add({ severity: 'success', summary: t('common.success'), detail: t('telemetry.acknowledged'), life: 2000 });
}

async function load() {
  const id = condominiumId.value;
  if (!id) return;
  telemetry.stopPolling();
  await Promise.all([
    devices.fetchByCondominium(id),
    telemetry.fetchLive(id),
    telemetry.fetchAlerts(id),
    telemetry.fetchDailyConsumption(id).catch(() => []),
    payments.fetchByCondominium(id),
  ]);
  if (!selected.value) selectedId.value = meters.value[0]?.id ?? null;
  lastUpdated.value = new Date();
  telemetry.startPolling(id);
}

onMounted(async () => {
  subscriptions.fetchAvailablePlans();
  subscriptions.fetchSubscriptions();
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, load, { immediate: true });
watch(() => telemetry.liveReadings, () => { lastUpdated.value = new Date(); });
// Las rutas antiguas /telemetry/alerts y la campana del encabezado llegan con ?focus=alerts
watch(() => route.query.focus, (focus) => {
  if (focus === 'alerts') requestAnimationFrame(() => document.getElementById('alerts')?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
}, { immediate: true });
onUnmounted(() => telemetry.stopPolling());
</script>

<template>
  <div class="page">
    <header class="page-head">
      <div>
        <h1>{{ t('dashboard.admin.title') }}</h1>
        <p v-if="condominium" class="subtitle">
          {{ condominium.name }}
          <template v-if="planName"> · {{ t('dashboard.admin.plan', { plan: planName }) }}</template>
          · {{ t('dashboard.admin.devicesOnline', { online: devices.online, total: devices.total }) }}
          <template v-if="lastUpdated"> · {{ t('dashboard.admin.updatedAt', { time: lastUpdated.toLocaleTimeString(locale) }) }}</template>
        </p>
      </div>
      <div class="head-actions">
        <pv-select v-if="condoStore.activeCondominiums.length > 1" v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
        <pv-button :label="t('dashboard.admin.refresh')" icon="pi pi-refresh" severity="secondary" outlined @click="load" />
      </div>
    </header>

    <p v-if="!condoStore.activeCondominiums.length" class="muted">{{ t('condominiums.empty') }}</p>
    <pv-message v-else-if="subscription && !hasActiveSubscription" severity="warn" :closable="false">
      {{ t('dashboard.admin.subscriptionInactive') }}
      <router-link :to="ROUTES.SUBSCRIPTION_DETAIL">{{ t('option.subscription') }}</router-link>
    </pv-message>

    <div class="stats">
      <stat-card :title="t('dashboard.admin.stats.waterMonth')" :value="`${formatNumber(waterMonth)} m³`" icon="pi-filter" iconBgColor="bg-green-100" iconTextColor="text-green-600"
                 :subtitle="t('dashboard.admin.projectionTo', { value: `${formatNumber(projectToMonthEnd(waterMonth))} m³` })" />
      <stat-card :title="t('dashboard.admin.stats.pumpEnergy')" :value="`${formatNumber(energyMonth)} kWh`" icon="pi-bolt" iconBgColor="bg-yellow-100" iconTextColor="text-yellow-600"
                 :subtitle="t('dashboard.admin.projectionTo', { value: `${formatNumber(projectToMonthEnd(energyMonth))} kWh` })" />
      <stat-card :title="t('dashboard.admin.stats.alerts')" :value="telemetry.openAlertsCount" icon="pi-bell"
                 :iconBgColor="criticalCount ? 'bg-red-100' : 'bg-green-100'" :iconTextColor="criticalCount ? 'text-red-600' : 'text-green-600'"
                 :subtitle="t('dashboard.admin.criticalCount', { n: criticalCount })" />
      <stat-card :title="t('dashboard.admin.stats.delinquency')" :value="`${payments.delinquencyRate} %`" icon="pi-wallet"
                 :iconBgColor="payments.delinquencyRate > 20 ? 'bg-red-100' : 'bg-green-100'" :iconTextColor="payments.delinquencyRate > 20 ? 'text-red-600' : 'text-green-600'"
                 :subtitle="t('dashboard.admin.overdueUnits', { n: payments.overdueUnitIds.length })" />
    </div>

    <section class="card">
      <div class="head">
        <h2>{{ t('dashboard.admin.equipmentTitle') }}</h2>
        <span v-if="gateway" class="gateway" :class="{ off: !gateway.isOnline }">
          <i class="pi pi-wifi" aria-hidden="true"></i>
          {{ gateway.deviceId }} · {{ gateway.isOnline ? t('devices.status.online') : t('devices.status.offline') }} · {{ sinceLabel(gateway.lastSeenAt) }}
        </span>
      </div>
      <p v-if="!meters.length" class="muted">{{ t('devices.empty') }}</p>
      <div class="meters">
        <button v-for="d in meters" :key="d.id" type="button" class="meter" :class="{ active: selectedId === d.id, off: !d.isOnline }" @click="selectedId = d.id">
          <span class="meter-head">
            <span class="meter-point">{{ t(`devices.points.${d.measurementPoint}`) }}</span>
            <span class="dot" :class="d.isOnline ? 'on' : 'down'" :title="d.isOnline ? t('devices.status.online') : t('devices.status.offline')"></span>
          </span>
          <span class="meter-location">{{ d.location || d.deviceId }}</span>
          <span class="meter-value">
            {{ latest(d)?.rate != null ? formatNumber(latest(d).rate, 2) : '—' }}
            <small>{{ isWater(d) ? 'm³/h' : 'kW' }}</small>
          </span>
          <span class="meter-meta">{{ d.deviceId }} · {{ t('dashboard.admin.cumulative') }} {{ latest(d) ? formatNumber(latest(d).value, 1) : '—' }} {{ isWater(d) ? 'm³' : 'kWh' }}</span>
        </button>
      </div>
      <div v-if="selected" class="live">
        <div class="head">
          <h3>{{ t('dashboard.admin.liveTitle', { name: selected.deviceId }) }}</h3>
          <small class="muted">{{ selected.location }}</small>
        </div>
        <div class="chart"><Line v-if="liveChart.labels.length" :data="liveChart" :options="liveOpts" /><p v-else class="muted empty">{{ t('dashboard.admin.noLiveData') }}</p></div>
      </div>
    </section>

    <div class="split">
      <section class="card">
        <div class="head">
          <h2>{{ t('dashboard.admin.charts.daily') }}</h2>
          <pv-select-button v-model="metric" :options="metricOptions" optionLabel="label" optionValue="value" :allowEmpty="false" />
        </div>
        <div class="chart"><Bar v-if="dailyChart.labels.length" :data="dailyChart" :options="chartOpts" /><p v-else class="muted empty">{{ t('dashboard.admin.noDailyData') }}</p></div>
      </section>

      <section id="alerts" class="card">
        <div class="head">
          <h2>{{ t('telemetry.alertsTitle') }}</h2>
          <pv-select-button v-model="alertFilter" :options="alertFilterOptions" optionLabel="label" optionValue="value" :allowEmpty="false" size="small" />
        </div>
        <p v-if="!alertRows.length" class="muted">{{ t('telemetry.noAlerts') }}</p>
        <ul v-else class="alerts">
          <li v-for="a in alertRows" :key="a.id" :class="{ critical: a.isCritical && a.isOpen }">
            <div class="alert-main">
              <strong>{{ t(`telemetry.alertTypes.${a.type}`, a.title || a.type) }}</strong>
              <small class="muted">{{ deviceName(a.deviceId) }} · {{ new Date(a.createdAt).toLocaleString(locale) }}</small>
              <small v-if="a.estimatedOvercostSoles" class="overcost">{{ t('dashboard.admin.overcost', { amount: `S/ ${a.estimatedOvercostSoles.toFixed(2)}` }) }}</small>
            </div>
            <pv-button v-if="a.isOpen" :label="t('telemetry.acknowledge')" size="small" text @click="ack(a)" />
            <pv-tag v-else severity="secondary" :value="alertStatusLabel(a.status)" />
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1280px; margin: 0 auto; display: flex; flex-direction: column; gap: 1.25rem; }
.page-head, .head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: .75rem 1rem; }
.page-head h1 { margin: 0; }
.subtitle { margin: .25rem 0 0; color: #6B7280; }
.head-actions { display: flex; gap: .5rem; flex-wrap: wrap; }
.filter { min-width: 14rem; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
.card h2 { font-size: 1.1rem; margin: 0; }
.card h3 { font-size: 1rem; margin: 0; }
.muted { color: #6B7280; }
.empty { text-align: center; padding-top: 5rem; margin: 0; }

.gateway { display: inline-flex; align-items: center; gap: .4rem; font-size: .85rem; color: #047857; }
.gateway.off { color: #b91c1c; }

.meters { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: .75rem; }
.meter { display: flex; flex-direction: column; gap: .35rem; text-align: left; font: inherit; color: inherit; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .9rem 1rem; cursor: pointer; transition: border-color .15s ease, box-shadow .15s ease; }
.meter:hover { border-color: #a7f3d0; }
.meter:focus-visible { outline: 2px solid #10B981; outline-offset: 2px; }
.meter.active { border-color: #10B981; box-shadow: 0 0 0 3px #ECFDF5; }
.meter.off { background: #FEF2F2; }
.meter-head { display: flex; justify-content: space-between; align-items: center; gap: .5rem; }
.meter-point { font-weight: 600; }
.meter-location { font-size: .8rem; color: #6B7280; margin-top: -.25rem; }
.dot { width: 8px; height: 8px; border-radius: 50%; }
.dot.on { background: #10B981; }
.dot.down { background: #ef4444; }
.meter-value { font-size: 1.6rem; font-weight: 700; font-variant-numeric: tabular-nums; }
.meter-value small { font-size: .85rem; font-weight: 500; color: #6B7280; }
.meter-meta { font-size: .8rem; color: #6B7280; font-variant-numeric: tabular-nums; }

.live { border-top: 1px solid #f1f5f9; padding-top: 1rem; display: flex; flex-direction: column; gap: .75rem; }
.chart { height: 240px; position: relative; }

.split { display: grid; grid-template-columns: 3fr 2fr; gap: 1rem; align-items: start; }
@media (max-width: 960px) { .split { grid-template-columns: 1fr; } }

.alerts { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; max-height: 320px; overflow-y: auto; }
.alerts li { display: flex; justify-content: space-between; align-items: center; gap: .75rem; padding: .7rem .25rem; border-bottom: 1px solid #f1f5f9; }
.alerts li.critical { border-left: 3px solid #ef4444; padding-left: .6rem; }
.alert-main { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.overcost { color: #b91c1c; font-size: .8rem; }
</style>
