<script setup>
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { Bar, Line } from 'vue-chartjs';
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler } from 'chart.js';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import { buildCommonAreaReport, dailySeries, sumTotals, changePct } from '../../application/common-area-report.js';
import { projectToMonthEnd } from '../../application/projection.js';
import { commonAreaIcon } from '../../../condominiums/domain/model/common-area.entity.js';
import { getSessionUserId } from '../../../shared/infrastructure/session.js';
import StatCard from '../components/stat-card.component.vue';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler);

// Analytics del dueño de departamento: reporte de consumo de las áreas comunes de su condominio
const { t, locale } = useI18n();
const telemetry = useTelemetryStore();
const devices = useDeviceStore();
const condoStore = useCondominiumStore();

const loading = ref(true);
const failed = ref(false);
const condominium = ref(null);
const unitNumber = ref('');
const currentTotals = ref([]);   // consumo por medidor del mes en curso
const previousTotals = ref([]);  // mismo tramo del mes anterior
const metric = ref('water');
const metricOptions = computed(() => [{ value: 'water', label: t('analytics.water') }, { value: 'energy', label: t('analytics.energy') }]);

const monthLabel = computed(() => new Date().toLocaleDateString(locale.value === 'en' ? 'en-US' : 'es-PE', { month: 'long', year: 'numeric' }));

const report = computed(() => buildCommonAreaReport(condominium.value?.commonAreas || [], devices.devices, currentTotals.value));
const previous = computed(() => sumTotals(previousTotals.value));
const waterChange = computed(() => changePct(report.value.totals.waterM3, previous.value.waterM3));
const energyChange = computed(() => changePct(report.value.totals.energyKwh, previous.value.energyKwh));
const projection = computed(() => projectToMonthEnd(metric.value === 'water' ? report.value.totals.waterM3 : report.value.totals.energyKwh));
const unitLabel = computed(() => metric.value === 'water' ? 'm³' : 'kWh');

const changeText = (pct) => pct === null ? t('analytics.noPrevious') : t('analytics.vsPrevious', { pct: `${pct > 0 ? '+' : ''}${pct}` });

const monitoredRows = computed(() => report.value.rows.filter(r => r.monitored));
const byAreaData = computed(() => ({
  labels: monitoredRows.value.map(r => r.name),
  datasets: [{
    label: unitLabel.value,
    data: monitoredRows.value.map(r => metric.value === 'water' ? r.waterM3 : r.energyKwh),
    backgroundColor: metric.value === 'water' ? '#10B981' : '#f59e0b',
    borderRadius: 6,
  }],
}));
const daily = computed(() => dailySeries(telemetry.dailyConsumption, new Date().toISOString().slice(0, 7), metric.value));
const dailyData = computed(() => ({
  labels: daily.value.labels,
  datasets: [{ label: unitLabel.value, data: daily.value.values, borderColor: '#059669', backgroundColor: 'rgba(16,185,129,.15)', fill: true, tension: .3 }],
}));
const chartOpts = { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true } } };

function meterStatus(row) {
  if (!row.monitored) return { severity: 'secondary', label: t('analytics.status.noMeter') };
  if (row.metersOnline === row.metersTotal) return { severity: 'success', label: t('analytics.status.online') };
  return { severity: 'danger', label: t('analytics.status.offline') };
}
const formatDate = (iso) => iso ? new Date(iso).toLocaleString(locale.value === 'en' ? 'en-US' : 'es-PE') : '—';

onMounted(async () => {
  try {
    const units = await condoStore.fetchUnitsByResident(getSessionUserId());
    const unit = units[0];
    if (!unit) return;
    unitNumber.value = unit.number;
    const condominiumId = unit.condominiumId;
    // Mismo tramo del mes anterior (del día 1 al día de hoy) para comparar periodos equivalentes
    const now = new Date();
    const prevFrom = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const prevMonthEnd = new Date(now.getFullYear(), now.getMonth(), 0);
    const prevTo = new Date(now.getFullYear(), now.getMonth() - 1, Math.min(now.getDate(), prevMonthEnd.getDate()),
      now.getHours(), now.getMinutes(), now.getSeconds());
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);
    const [condo, , current, prev] = await Promise.all([
      condoStore.fetchById(condominiumId),
      devices.fetchByCondominium(condominiumId),
      telemetry.fetchDeviceConsumption(condominiumId, monthStart, now),
      telemetry.fetchDeviceConsumption(condominiumId, prevFrom, prevTo),
      telemetry.fetchDailyConsumption(condominiumId),
    ]);
    condominium.value = condo;
    currentTotals.value = current;
    previousTotals.value = prev;
  } catch (_) {
    failed.value = true;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="page">
    <div class="page-head">
      <div>
        <h1>{{ t('analytics.title') }}</h1>
        <p v-if="condominium" class="muted">
          {{ t('analytics.subtitle', { condominium: condominium.name, month: monthLabel }) }}
          <span v-if="unitNumber"> · {{ t('analytics.myUnit', { unit: unitNumber }) }}</span>
        </p>
      </div>
      <pv-select-button v-if="condominium" v-model="metric" :options="metricOptions" optionLabel="label" optionValue="value" :allowEmpty="false" />
    </div>

    <div v-if="loading" class="center"><pv-progress-spinner /></div>
    <pv-message v-else-if="failed" severity="error" :closable="false">{{ t('analytics.loadError') }}</pv-message>
    <pv-message v-else-if="!condominium" severity="info" :closable="false">{{ t('analytics.noUnit') }}</pv-message>

    <template v-else>
      <div class="stats">
        <stat-card :title="t('analytics.stats.water')" :value="`${report.totals.waterM3} m³`" :subtitle="changeText(waterChange)"
                   icon="pi-filter" iconBgColor="bg-green-100" iconTextColor="text-green-600" />
        <stat-card :title="t('analytics.stats.energy')" :value="`${report.totals.energyKwh} kWh`" :subtitle="changeText(energyChange)"
                   icon="pi-bolt" iconBgColor="bg-yellow-100" iconTextColor="text-yellow-600" />
        <stat-card :title="t('analytics.stats.projection')" :value="`${projection} ${unitLabel}`" :subtitle="t('analytics.stats.projectionHint')"
                   icon="pi-chart-line" />
        <stat-card :title="t('analytics.stats.monitored')" :value="`${report.monitoredCount} / ${report.rows.length}`" :subtitle="t('analytics.stats.monitoredHint')"
                   icon="pi-th-large" iconBgColor="bg-teal-100" iconTextColor="text-teal-600" />
      </div>

      <pv-message v-if="!report.rows.length" severity="info" :closable="false">{{ t('analytics.noAreas') }}</pv-message>

      <template v-else>
        <div class="charts">
          <section class="card">
            <h3>{{ t('analytics.charts.byArea', { unit: unitLabel }) }}</h3>
            <div class="chart"><Bar v-if="monitoredRows.length" :data="byAreaData" :options="chartOpts" /><p v-else class="muted empty">{{ t('analytics.noData') }}</p></div>
          </section>
          <section class="card">
            <h3>{{ t('analytics.charts.daily', { unit: unitLabel }) }}</h3>
            <div class="chart"><Line v-if="daily.labels.length" :data="dailyData" :options="chartOpts" /><p v-else class="muted empty">{{ t('analytics.noData') }}</p></div>
          </section>
        </div>

        <section class="card">
          <h3>{{ t('analytics.table.title') }}</h3>
          <pv-data-table :value="report.rows" dataKey="id" stripedRows size="small" class="report-table">
            <pv-column :header="t('analytics.table.area')">
              <template #body="{ data }"><span class="area"><i :class="commonAreaIcon(data.type)" /> {{ data.name }}</span></template>
            </pv-column>
            <pv-column :header="t('analytics.table.water')" sortField="waterM3" sortable>
              <template #body="{ data }">{{ data.monitored ? `${data.waterM3} m³` : '—' }}<small v-if="data.waterShare" class="muted"> ({{ data.waterShare }} %)</small></template>
            </pv-column>
            <pv-column :header="t('analytics.table.energy')" sortField="energyKwh" sortable>
              <template #body="{ data }">{{ data.monitored ? `${data.energyKwh} kWh` : '—' }}<small v-if="data.energyShare" class="muted"> ({{ data.energyShare }} %)</small></template>
            </pv-column>
            <pv-column :header="t('analytics.table.meter')">
              <template #body="{ data }"><pv-tag :severity="meterStatus(data).severity" :value="meterStatus(data).label" /></template>
            </pv-column>
            <pv-column :header="t('analytics.table.lastReading')">
              <template #body="{ data }">{{ formatDate(data.lastReadingAt) }}</template>
            </pv-column>
          </pv-data-table>
          <small class="muted">{{ t('analytics.table.note') }}</small>
        </section>
      </template>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; display: flex; flex-direction: column; gap: 1rem; }
.page-head { display: flex; justify-content: space-between; align-items: flex-end; flex-wrap: wrap; gap: 1rem; }
.page-head h1 { margin: 0; }
.muted { color: #6B7280; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
.charts { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
@media (max-width: 900px) { .charts { grid-template-columns: 1fr; } }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: .75rem; }
.card h3 { margin: 0; font-size: 1rem; }
.chart { height: 260px; position: relative; }
.empty { text-align: center; padding-top: 6rem; }
.area { display: inline-flex; align-items: center; gap: .45rem; font-weight: 500; }
.area .pi { color: #059669; }
.report-table { overflow-x: auto; }
.center { text-align: center; padding: 2rem; }
</style>
