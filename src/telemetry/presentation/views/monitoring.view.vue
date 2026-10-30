<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import ConsumptionChart from '../components/consumption-chart.component.vue';

const props = defineProps({
  defaultTab: {
    type: String,
    default: 'monitoring'
  }
});

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();

const telemetry = useTelemetryStore();
const devices = useDeviceStore();
const condoStore = useCondominiumStore();

const condominiumId = ref(null);
const selected = ref(null);
const granularity = ref('live');
const granOptions = computed(() => [
  { value: 'live', label: t('telemetry.live') },
  { value: 'day', label: t('telemetry.daily') },
  { value: 'month', label: t('telemetry.monthly') }
]);

// Tab synchronization
const validTabs = ['monitoring', 'alerts'];
const initialTab = (route.query.tab && validTabs.includes(route.query.tab))
  ? route.query.tab
  : (props.defaultTab || 'monitoring');

const activeTab = ref(initialTab);

watch(() => props.defaultTab, (val) => {
  if (val && validTabs.includes(val)) {
    activeTab.value = val;
  }
});

watch(() => route.query.tab, (val) => {
  if (val && validTabs.includes(val)) {
    activeTab.value = val;
  }
});

function onTabChange(tab) {
  activeTab.value = tab;
  router.replace({ query: { ...route.query, tab } }).catch(() => {});
}

const activeTitle = computed(() => {
  return activeTab.value === 'alerts' ? t('telemetry.alertsTitle') : t('telemetry.monitoringTitle');
});

// Telemetry Points
const points = computed(() => devices.devices.filter(d => d.type !== 'gateway'));
const unitOf = (d) => d.type === 'water_pulse_meter' ? 'L/min' : 'A';
const valueOf = (d) => telemetry.latestByDevice[d.id]?.value;

// Alerts filter and state
const alertStatus = ref(null);
const statusOptions = computed(() => ['open', 'acknowledged'].map(v => ({ value: v, label: t(`telemetry.alertStatus.${v}`) })));
const alertRows = computed(() => telemetry.alerts.filter(a => !alertStatus.value || a.status === alertStatus.value));
const deviceName = (id) => devices.devices.find(d => d.id === id)?.deviceId ?? `#${id}`;

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});

watch(condominiumId, async (id) => {
  if (!id) return;
  telemetry.stopPolling();
  await devices.fetchByCondominium(id);
  await telemetry.fetchLive(id);
  await telemetry.fetchAlerts(id);
  telemetry.startPolling(id);
  selected.value = points.value[0] ?? null;
}, { immediate: true });

onUnmounted(() => telemetry.stopPolling());

async function ack(a) {
  await telemetry.acknowledge(a.id);
  toast.add({ severity: 'success', summary: t('common.success'), detail: t('telemetry.acknowledged'), life: 2000 });
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ activeTitle }}</h1>
      <div class="flex gap-2">
        <pv-select v-model="condominiumId" :options="condoStore.activeCondominiums" optionLabel="name" optionValue="id" class="filter" />
        <pv-select v-if="activeTab === 'alerts'" v-model="alertStatus" :options="statusOptions" optionLabel="label" optionValue="value" showClear :placeholder="t('payments.statement.status')" class="filter" />
      </div>
    </div>

    <pv-tabs :value="activeTab" @update:value="onTabChange" class="monitoring-tabs">
      <pv-tab-list class="tabs-header mb-4">
        <pv-tab value="monitoring">
          <i class="pi pi-chart-line mr-2"></i>{{ t('telemetry.monitoringTitle') }}
        </pv-tab>
        <pv-tab value="alerts">
          <i class="pi pi-bell mr-2"></i>{{ t('telemetry.alertsTitle') }}
          <pv-tag v-if="telemetry.openAlertsCount > 0" severity="danger" class="ml-2" :value="telemetry.openAlertsCount" />
        </pv-tab>
      </pv-tab-list>

      <pv-tab-panels>
        <!-- Tab 1: Monitoreo en vivo -->
        <pv-tab-panel value="monitoring">
          <div v-if="activeTab === 'monitoring'">
            <div class="cards">
              <article v-for="d in points" :key="d.id" class="point card" :class="{ active: selected?.id === d.id, off: !d.isOnline }" tabindex="0"
                       @click="selected = d" @keyup.enter="selected = d">
                <header>
                  <strong>{{ t(`devices.points.${d.measurementPoint}`) }}</strong>
                  <pv-tag :severity="d.isOnline ? 'success' : 'danger'" :value="d.isOnline ? t('devices.status.online') : t('devices.status.offline')" />
                </header>
                <div class="value reading-value">{{ valueOf(d) ?? '—' }} <small>{{ unitOf(d) }}</small></div>
                <small class="muted">{{ d.deviceId }}</small>
              </article>
            </div>
            <p v-if="!points.length" class="muted">{{ t('devices.empty') }}</p>
            <section v-if="selected" class="detail card">
              <div class="detail-head">
                <h3>{{ t(`devices.points.${selected.measurementPoint}`) }} · {{ selected.deviceId }}</h3>
                <pv-select-button v-model="granularity" :options="granOptions" optionLabel="label" optionValue="value" :allowEmpty="false" />
              </div>
              <consumption-chart :condominiumId="condominiumId" :deviceId="selected.id" :granularity="granularity" :unit="unitOf(selected)" />
            </section>
          </div>
        </pv-tab-panel>

        <!-- Tab 2: Alertas -->
        <pv-tab-panel value="alerts">
          <div v-if="activeTab === 'alerts'">
            <pv-data-table :value="alertRows" dataKey="id" stripedRows paginator :rows="10" class="mt-2">
              <template #empty>{{ t('telemetry.noAlerts') }}</template>
              <pv-column :header="t('telemetry.severity')">
                <template #body="{ data }">
                  <pv-tag :severity="data.isCritical ? 'danger' : 'warn'" :value="t(`telemetry.severities.${data.severity}`)" />
                </template>
              </pv-column>
              <pv-column :header="t('telemetry.type')">
                <template #body="{ data }">{{ t(`telemetry.alertTypes.${data.type}`) }}</template>
              </pv-column>
              <pv-column :header="t('devices.fields.deviceId')">
                <template #body="{ data }">{{ deviceName(data.deviceId) }}</template>
              </pv-column>
              <pv-column :header="t('telemetry.date')">
                <template #body="{ data }">{{ new Date(data.createdAt).toLocaleString() }}</template>
              </pv-column>
              <pv-column :header="t('payments.statement.status')">
                <template #body="{ data }">
                  <pv-tag :severity="data.isOpen ? 'warn' : 'secondary'" :value="t(`telemetry.alertStatus.${data.status}`)" />
                </template>
              </pv-column>
              <pv-column :header="t('common.actions')">
                <template #body="{ data }">
                  <pv-button v-if="data.isOpen" :label="t('telemetry.acknowledge')" icon="pi pi-check" size="small" @click="ack(data)" />
                </template>
              </pv-column>
            </pv-data-table>
          </div>
        </pv-tab-panel>
      </pv-tab-panels>
    </pv-tabs>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.5rem; }
.filter { min-width: 14rem; }
.monitoring-tabs { background: #ffffff; border-radius: 12px; padding: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
.tabs-header { border-bottom: 1px solid #e2e8f0; }
.cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 1rem; margin: 1rem 0; }
.point { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1rem; cursor: pointer; transition: all 0.2s ease; }
.point.active { border-color: #10B981; box-shadow: 0 0 0 2px #ECFDF5; }
.point.off { background: #FEF2F2; }
.point header { display: flex; justify-content: space-between; align-items: center; gap: .5rem; }
.value { font-size: 2rem; font-weight: 600; margin: .5rem 0; }
.muted { color: #6B7280; }
.detail { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; margin-top: 1rem; }
.detail-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem; }
.mb-4 { margin-bottom: 1rem; }
.mr-2 { margin-right: 0.5rem; }
.ml-2 { margin-left: 0.5rem; }
.mt-2 { margin-top: 0.5rem; }
</style>
