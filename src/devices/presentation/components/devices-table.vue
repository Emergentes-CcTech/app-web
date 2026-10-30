<script setup>
import { useI18n } from 'vue-i18n';
import DeviceActionsMenu from './device-actions-menu.vue';

defineProps({ devices: Array, loading: Boolean });
defineEmits(['edit', 'unlink']);
const { t } = useI18n();
const TYPE_ICONS = { water_pulse_meter: 'pi pi-filter', current_sensor: 'pi pi-bolt', gateway: 'pi pi-wifi' };
function since(iso) {
  if (!iso) return t('devices.neverSeen');
  const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  if (minutes < 1) return t('dashboard.admin.justNow');
  if (minutes < 120) return t('dashboard.admin.minutesAgo', { n: minutes });
  return new Date(iso).toLocaleString();
}
</script>

<template>
  <div class="table-card">
    <pv-data-table :value="devices" :loading="loading" :paginator="devices.length > 10" :rows="10" dataKey="id" rowHover>
      <template #empty><p class="empty">{{ t('devices.empty') }}</p></template>
      <pv-column field="deviceId" :header="t('devices.fields.deviceId')" sortable>
        <template #body="{ data }">
          <div class="device">
            <span class="icon" :class="data.type"><i :class="TYPE_ICONS[data.type] || 'pi pi-microchip'" aria-hidden="true" /></span>
            <span class="device-text"><strong>{{ data.deviceId }}</strong><small>{{ t(`devices.types.${data.type}`) }}</small></span>
          </div>
        </template>
      </pv-column>
      <pv-column :header="t('devices.fields.measurementPoint')">
        <template #body="{ data }">
          <span class="point">{{ t(`condominiums.commonAreas.types.${data.measurementPoint}`, data.measurementPoint) }}</span>
          <small v-if="data.location" class="muted block">{{ data.location }}</small>
        </template>
      </pv-column>
      <pv-column field="macAddress" :header="t('devices.fields.macAddress')"><template #body="{ data }"><code class="mac">{{ data.macAddress || '—' }}</code></template></pv-column>
      <pv-column :header="t('devices.fields.lastSeenAt')"><template #body="{ data }"><span class="muted">{{ since(data.lastSeenAt) }}</span></template></pv-column>
      <pv-column field="status" :header="t('devices.fields.status')" sortable>
        <template #body="{ data }"><pv-tag :severity="data.isOnline ? 'success' : 'danger'" :value="data.isOnline ? t('devices.status.online') : t('devices.status.offline')" /></template>
      </pv-column>
      <pv-column style="width: 4rem"><template #body="{ data }"><device-actions-menu :device="data" @edit="$emit('edit', $event)" @unlink="$emit('unlink', $event)" /></template></pv-column>
    </pv-data-table>
  </div>
</template>

<style scoped>
.table-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden; }
.device { display: flex; align-items: center; gap: .7rem; }
.icon { width: 34px; height: 34px; border-radius: 8px; display: grid; place-items: center; background: #ECFDF5; color: #059669; flex-shrink: 0; }
.icon.current_sensor { background: #FEF3C7; color: #b45309; }
.icon.gateway { background: #EFF6FF; color: #1d4ed8; }
.device-text { display: flex; flex-direction: column; }
.device-text small { color: #6B7280; }
.point { font-weight: 500; }
.block { display: block; }
.mac { font-size: .85rem; background: #f3f4f6; border-radius: 4px; padding: .1rem .35rem; }
.muted { color: #6B7280; }
.empty { text-align: center; color: #6B7280; padding: 1.5rem 0; margin: 0; }
</style>
