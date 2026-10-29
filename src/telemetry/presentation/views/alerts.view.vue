<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';

const { t } = useI18n();
const toast = useToast();
const store = useTelemetryStore();
const devices = useDeviceStore();
const condoStore = useCondominiumStore();

const condominiumId = ref(null);
const status = ref(null);
const statusOptions = computed(() => ['open', 'acknowledged'].map(v => ({ value: v, label: t(`telemetry.alertStatus.${v}`) })));
const rows = computed(() => store.alerts.filter(a => !status.value || a.status === status.value));
const deviceName = (id) => devices.devices.find(d => d.id === id)?.deviceId ?? `#${id}`;

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, async (id) => {
  if (!id) return;
  await devices.fetchByCondominium(id);
  await store.fetchAlerts(id);
  store.startPolling(id);
}, { immediate: true });
onUnmounted(() => store.stopPolling());

async function ack(a) {
  await store.acknowledge(a.id);
  toast.add({ severity: 'success', summary: t('common.success'), detail: t('telemetry.acknowledged'), life: 2000 });
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('telemetry.alertsTitle') }}</h1>
      <div class="flex gap-2">
        <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
        <pv-select v-model="status" :options="statusOptions" optionLabel="label" optionValue="value" showClear :placeholder="t('payments.statement.status')" class="filter" />
      </div>
    </div>
    <pv-data-table :value="rows" dataKey="id" stripedRows paginator :rows="10">
      <template #empty>{{ t('telemetry.noAlerts') }}</template>
      <pv-column :header="t('telemetry.severity')"><template #body="{ data }"><pv-tag :severity="data.isCritical ? 'danger' : 'warn'" :value="t(`telemetry.severities.${data.severity}`)" /></template></pv-column>
      <pv-column :header="t('telemetry.type')"><template #body="{ data }">{{ t(`telemetry.alertTypes.${data.type}`) }}</template></pv-column>
      <pv-column :header="t('devices.fields.deviceId')"><template #body="{ data }">{{ deviceName(data.deviceId) }}</template></pv-column>
      <pv-column :header="t('telemetry.date')"><template #body="{ data }">{{ new Date(data.createdAt).toLocaleString() }}</template></pv-column>
      <pv-column :header="t('payments.statement.status')"><template #body="{ data }"><pv-tag :severity="data.isOpen ? 'warn' : 'secondary'" :value="t(`telemetry.alertStatus.${data.status}`)" /></template></pv-column>
      <pv-column :header="t('common.actions')"><template #body="{ data }"><pv-button v-if="data.isOpen" :label="t('telemetry.acknowledge')" icon="pi pi-check" size="small" @click="ack(data)" /></template></pv-column>
    </pv-data-table>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem; }
.filter { min-width: 14rem; }
</style>
