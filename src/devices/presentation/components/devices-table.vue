<script setup>
import { useI18n } from 'vue-i18n';
import DeviceActionsMenu from './device-actions-menu.vue';

defineProps({ devices: Array, loading: Boolean });
defineEmits(['edit', 'unlink']);
const { t } = useI18n();
const fmt = (d) => d ? new Date(d).toLocaleString() : '—';
</script>

<template>
  <pv-data-table :value="devices" :loading="loading" paginator :rows="10" dataKey="id" stripedRows>
    <template #empty>{{ t('devices.empty') }}</template>
    <pv-column field="deviceId" :header="t('devices.fields.deviceId')" sortable />
    <pv-column :header="t('devices.fields.type')"><template #body="{ data }">{{ t(`devices.types.${data.type}`) }}</template></pv-column>
    <pv-column :header="t('devices.fields.measurementPoint')"><template #body="{ data }">{{ t(`devices.points.${data.measurementPoint}`) }}</template></pv-column>
    <pv-column field="macAddress" :header="t('devices.fields.macAddress')" />
    <pv-column field="installedAt" :header="t('devices.fields.installedAt')" sortable />
    <pv-column :header="t('devices.fields.lastSeenAt')"><template #body="{ data }">{{ fmt(data.lastSeenAt) }}</template></pv-column>
    <pv-column :header="t('devices.fields.status')">
      <template #body="{ data }"><pv-tag :severity="data.isOnline ? 'success' : 'danger'" :value="data.isOnline ? t('devices.status.online') : t('devices.status.offline')" /></template>
    </pv-column>
    <pv-column :header="t('common.actions')"><template #body="{ data }"><device-actions-menu :device="data" @edit="$emit('edit', $event)" @unlink="$emit('unlink', $event)" /></template></pv-column>
  </pv-data-table>
</template>
