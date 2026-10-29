<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import ConsumptionChart from '../components/consumption-chart.component.vue';

const { t } = useI18n();
const telemetry = useTelemetryStore();
const devices = useDeviceStore();
const condoStore = useCondominiumStore();

const condominiumId = ref(null);
const selected = ref(null);
const granularity = ref('live');
const granOptions = computed(() => [{ value: 'live', label: t('telemetry.live') }, { value: 'day', label: t('telemetry.daily') }, { value: 'month', label: t('telemetry.monthly') }]);

// Una tarjeta por punto de medición (el gateway no mide consumo)
const points = computed(() => devices.devices.filter(d => d.type !== 'gateway'));
const unitOf = (d) => d.type === 'water_pulse_meter' ? 'L/min' : 'A';
const valueOf = (d) => telemetry.latestByDevice[d.id]?.value;

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, async (id) => {
  if (!id) return;
  telemetry.stopPolling();
  await devices.fetchByCondominium(id);
  await telemetry.fetchLive(id);
  telemetry.startPolling(id);
  selected.value = points.value[0] ?? null;
}, { immediate: true });
onUnmounted(() => telemetry.stopPolling());
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('telemetry.monitoringTitle') }}</h1>
      <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
    </div>
    <div class="cards">
      <article v-for="d in points" :key="d.id" class="point" :class="{ active: selected?.id === d.id, off: !d.isOnline }" tabindex="0"
               @click="selected = d" @keyup.enter="selected = d">
        <header><strong>{{ t(`devices.points.${d.measurementPoint}`) }}</strong>
          <pv-tag :severity="d.isOnline ? 'success' : 'danger'" :value="d.isOnline ? t('devices.status.online') : t('devices.status.offline')" /></header>
        <div class="value">{{ valueOf(d) ?? '—' }} <small>{{ unitOf(d) }}</small></div>
        <small class="muted">{{ d.deviceId }}</small>
      </article>
    </div>
    <p v-if="!points.length" class="muted">{{ t('devices.empty') }}</p>
    <section v-if="selected" class="detail">
      <div class="detail-head">
        <h3>{{ t(`devices.points.${selected.measurementPoint}`) }} · {{ selected.deviceId }}</h3>
        <pv-select-button v-model="granularity" :options="granOptions" optionLabel="label" optionValue="value" :allowEmpty="false" />
      </div>
      <consumption-chart :condominiumId="condominiumId" :deviceId="selected.id" :granularity="granularity" :unit="unitOf(selected)" />
    </section>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
.filter { min-width: 16rem; }
.cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 1rem; margin: 1rem 0; }
.point { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1rem; cursor: pointer; }
.point.active { border-color: #10B981; box-shadow: 0 0 0 2px #ECFDF5; }
.point.off { background: #FEF2F2; }
.point header { display: flex; justify-content: space-between; align-items: center; gap: .5rem; }
.value { font-size: 2rem; font-weight: 600; margin: .5rem 0; }
.muted { color: #6B7280; }
.detail { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; }
.detail-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem; }
</style>
