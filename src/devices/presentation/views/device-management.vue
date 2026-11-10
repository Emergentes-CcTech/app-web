<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import { useDeviceStore } from '../../application/device.store.js';
import { useCondominiumStore } from '../../../condominiums/application/condominium.store.js';
import DeviceListHeader from '../components/device-list-header.vue';
import DevicesTable from '../components/devices-table.vue';
import DeviceEditDialog from '../components/device-edit-dialog.vue';
import { SubscriptionApi } from '../../../subscriptions/infrastructure/subscription-api.js';

const { t } = useI18n();
const toast = useToast();
const confirm = useConfirm();
const store = useDeviceStore();
const condoStore = useCondominiumStore();

const condominiumId = ref(null);
const search = ref('');
const type = ref(null);
const showDialog = ref(false);
const editing = ref(null);
// Kit incluido en el plan: los equipos por encima se cobran aparte en la cuota mensual
const subscriptionApi = new SubscriptionApi();
const allowance = ref(null);
async function loadAllowance(id) {
  allowance.value = null;
  if (!id) return;
  try { allowance.value = (await subscriptionApi.getDeviceAllowance(id)).data; } catch (_) { /* sin datos del plan no se muestra el kit */ }
}
const pen = (n) => `S/ ${Number(n || 0).toFixed(2)}`;

onMounted(async () => {
  if (!condoStore.condominiums.length) await condoStore.fetchAll().catch(() => {});
  condominiumId.value = condoStore.activeCondominiums[0]?.id ?? null;
});
watch(condominiumId, (id) => { if (id) store.fetchByCondominium(id); loadAllowance(id); }, { immediate: true });

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  return store.devices.filter(d => (!type.value || d.type === type.value) && (!q || `${d.deviceId} ${d.macAddress}`.toLowerCase().includes(q)));
});

function add() { editing.value = null; showDialog.value = true; }
function edit(d) { editing.value = d; showDialog.value = true; }
async function persist(entity) {
  try {
    await store.save(entity);
    showDialog.value = false;
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('devices.messages.saved'), life: 2500 });
    loadAllowance(condominiumId.value);
  } catch (e) {
    toast.add({ severity: 'error', summary: t('common.error'), detail: e.message === 'idTaken' ? t('devices.validation.idTaken') : t('devices.messages.error'), life: 3000 });
  }
}
function save(entity) {
  const a = allowance.value;
  // Un equipo nuevo por encima del kit del plan se confirma mostrando lo que suma a la cuota mensual
  if (!editing.value && a && a.communalDevices >= a.includedDevices) {
    confirm.require({
      header: t('devices.extra.title'),
      message: t('devices.extra.confirm', { plan: a.planName === 'GranTorre' ? 'Gran Torre' : a.planName, included: a.includedDevices, price: pen(a.extraDevicePrice), priceIgv: pen(a.extraDevicePriceWithIgv) }),
      icon: 'pi pi-plus-circle',
      acceptLabel: t('devices.extra.accept'),
      rejectLabel: t('common.cancel'),
      accept: () => persist(entity),
    });
    return;
  }
  persist(entity);
}
function unlink(d) {
  confirm.require({
    message: t('devices.confirmUnlink', { id: d.deviceId }), header: t('devices.actions.unlink'), icon: 'pi pi-exclamation-triangle',
    acceptLabel: t('devices.actions.unlink'), rejectLabel: t('common.cancel'), acceptProps: { severity: 'danger' },
    accept: async () => { await store.unlink(d.id); loadAllowance(condominiumId.value); toast.add({ severity: 'success', summary: t('common.success'), detail: t('devices.messages.unlinked'), life: 2500 }); },
  });
}
</script>

<template>
  <div class="page">
    <device-list-header v-model:search="search" v-model:type="type" v-model:condominium="condominiumId"
                        :devices="store.devices" :condominiums="condoStore.activeCondominiums" @add="add">
      <div class="stats">
        <div class="stat"><span>{{ t('devices.stats.total') }}</span><strong>{{ store.total }}</strong></div>
        <div class="stat"><span><i class="dot on" />{{ t('devices.stats.online') }}</span><strong>{{ store.online }}</strong></div>
        <div class="stat" :class="{ bad: store.offline }"><span><i class="dot down" />{{ t('devices.stats.offline') }}</span><strong>{{ store.offline }}</strong></div>
        <div class="stat"><span>{{ t('devices.stats.sampling') }}</span><strong>1 min</strong></div>
        <div v-if="allowance" class="stat"><span>{{ t('devices.extra.kit', { plan: allowance.planName === 'GranTorre' ? 'Gran Torre' : allowance.planName }) }}</span>
          <strong>{{ Math.min(allowance.communalDevices, allowance.includedDevices) }} / {{ allowance.includedDevices }}</strong>
          <small v-if="allowance.extraDevices">{{ t('devices.extra.summary', { n: allowance.extraDevices, amount: pen(allowance.extrasMonthly) }) }}</small></div>
      </div>
    </device-list-header>
    <devices-table :devices="filtered" :loading="store.isLoading" @edit="edit" @unlink="unlink" />
    <device-edit-dialog v-model:visible="showDialog" :device="editing" :condominiumId="condominiumId" @save="save" />
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 1rem; }
.stat { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1rem; display: flex; flex-direction: column; gap: 4px; }
.stat span { color: #6B7280; font-size: .85rem; }
.stat strong { font-size: 1.5rem; }
.stat span { display: inline-flex; align-items: center; gap: .4rem; }
.stat.bad { background: #FEF2F2; border-color: #fca5a5; }
.dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
.dot.on { background: #10B981; }
.dot.down { background: #ef4444; }
</style>
