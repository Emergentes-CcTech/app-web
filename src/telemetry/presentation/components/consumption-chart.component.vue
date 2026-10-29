<script setup>
import { computed, ref, watch, onMounted } from 'vue';
import { Line } from 'vue-chartjs';
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler } from 'chart.js';
import { useTelemetryStore } from '../../application/telemetry.store.js';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler);

const props = defineProps({
  condominiumId: { type: Number, required: true },
  deviceId: { type: Number, required: true },
  granularity: { type: String, default: 'live', validator: (v) => ['live', 'day', 'month'].includes(v) },
  unit: { type: String, default: 'L/min' },   // "L/min" o "A"
});

const store = useTelemetryStore();
const series = ref([]);

async function load() {
  if (props.granularity === 'live') {
    const list = store.readingsByDevice[props.deviceId] || [];
    series.value = list.map(r => ({ label: new Date(r.timestamp).toLocaleTimeString(), value: r.value }));
  } else {
    series.value = await store.fetchHistory(props.condominiumId, props.deviceId, props.granularity);
  }
}
onMounted(load);
watch(() => [props.deviceId, props.granularity, props.condominiumId], load);
watch(() => store.readingsByDevice[props.deviceId], () => { if (props.granularity === 'live') load(); }, { deep: true });

const chartData = computed(() => ({
  labels: series.value.map(p => p.label),
  datasets: [{ label: props.unit, data: series.value.map(p => p.value), borderColor: '#059669', backgroundColor: 'rgba(16,185,129,.15)', fill: true, tension: .3 }],
}));
const options = computed(() => ({
  responsive: true, maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: { y: { beginAtZero: true, title: { display: true, text: props.unit } } },
}));
</script>

<template>
  <div class="chart-wrap">
    <Line v-if="series.length" :data="chartData" :options="options" />
    <p v-else class="empty">—</p>
  </div>
</template>

<style scoped>
.chart-wrap { height: 260px; position: relative; }
.empty { text-align: center; color: #6B7280; padding-top: 5rem; }
</style>
