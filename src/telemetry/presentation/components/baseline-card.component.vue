<script setup>
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { TelemetryApi } from '../../infrastructure/telemetry-api.js';

// Detección estadística: cada edificio aprende su propio patrón nocturno (01:00–04:00) y se compara contra él
const props = defineProps({ condominiumId: { type: Number, default: null } });

const { t, locale } = useI18n();
const api = new TelemetryApi();
const rows = ref([]);
const loading = ref(false);

const severity = (s) => ({ normal: 'success', anomaly: 'danger', learning: 'info', 'no-data': 'secondary' }[s] || 'secondary');
const fmt = (v, unit) => v === null || v === undefined ? '—' : `${Number(v).toLocaleString(locale.value, { maximumFractionDigits: unit === 'kWh' ? 2 : 3 })} ${unit === 'm3/h' ? 'm³/h' : unit}`;
const nightLabel = (d) => d ? new Date(`${d}T12:00:00`).toLocaleDateString(locale.value, { day: 'numeric', month: 'short' }) : '—';

async function load() {
  rows.value = [];
  if (!props.condominiumId) return;
  loading.value = true;
  try {
    const { data } = await api.getBaselines(props.condominiumId);
    rows.value = Array.isArray(data) ? data : [];
  } catch (_) {
    rows.value = [];
  } finally {
    loading.value = false;
  }
}
watch(() => props.condominiumId, load, { immediate: true });
</script>

<template>
  <section class="card" aria-labelledby="baseline-title">
    <div class="head">
      <div>
        <h2 id="baseline-title"><i class="pi pi-chart-scatter" aria-hidden="true" /> {{ t('baselines.title') }}</h2>
        <small class="muted">{{ t('baselines.subtitle') }}</small>
      </div>
    </div>

    <div v-if="loading" class="center"><pv-progress-spinner style="width: 28px; height: 28px" /></div>
    <p v-else-if="!rows.length" class="muted">{{ t('baselines.empty') }}</p>
    <ul v-else class="rows">
      <li v-for="r in rows" :key="`${r.deviceId}-${r.metric}`">
        <div class="row-main">
          <strong>{{ t(`baselines.metrics.${r.metric}`) }}</strong>
          <small class="muted">{{ r.deviceName }}</small>
        </div>
        <div class="row-values">
          <template v-if="r.status === 'learning'">
            <small>{{ t('baselines.learning', { n: r.nightsInBaseline, total: r.nightsRequired }) }}</small>
          </template>
          <template v-else-if="r.status === 'no-data'">
            <small class="muted">{{ t('baselines.noData') }}</small>
          </template>
          <template v-else>
            <small>{{ t('baselines.lastNight', { night: nightLabel(r.lastNight), value: fmt(r.lastNightValue, r.unit) }) }}</small>
            <small class="muted">{{ t('baselines.normal', { median: fmt(r.median, r.unit), threshold: fmt(r.threshold, r.unit), n: r.nightsInBaseline }) }}</small>
          </template>
        </div>
        <pv-tag :severity="severity(r.status)" :value="t(`baselines.status.${r.status}`)" />
      </li>
    </ul>
  </section>
</template>

<style scoped>
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
.card h2 { font-size: 1.1rem; margin: 0 0 .15rem; display: flex; align-items: center; gap: .5rem; }
.card h2 i { color: #10B981; }
.muted { color: #6B7280; }
.center { display: flex; justify-content: center; }
.rows { list-style: none; margin: 0; padding: 0; display: grid; gap: .25rem; }
.rows li { display: grid; grid-template-columns: minmax(10rem, 1fr) minmax(0, 2fr) auto; gap: .75rem; align-items: center; padding: .6rem 0; border-bottom: 1px solid #f3f4f6; }
.row-main, .row-values { display: flex; flex-direction: column; gap: .1rem; }
@media (max-width: 640px) { .rows li { grid-template-columns: 1fr auto; } .row-values { grid-column: 1 / -1; } }
</style>
