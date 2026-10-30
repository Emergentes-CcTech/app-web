<script setup>
import { computed, reactive, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Device, DeviceType, DEFAULT_NIGHT_FLOW_M3H } from '../../domain/model/device.entity.js';
import { COMMON_AREA_TYPES } from '../../../condominiums/domain/model/common-area.entity.js';
import { isValidMacAddress, isValidName } from '../../../shared/presentation/validators.js';
import { useDeviceStore } from '../../application/device.store.js';

const props = defineProps({ visible: Boolean, device: Object, condominiumId: [Number, String] });
const emit = defineEmits(['update:visible', 'save']);
const { t } = useI18n();
const store = useDeviceStore();

const form = reactive(new Device({}));
const errors = reactive({});
const TYPE_ICONS = { water_pulse_meter: 'pi pi-filter', current_sensor: 'pi pi-bolt', gateway: 'pi pi-wifi' };
const typeOptions = computed(() => Object.values(DeviceType).map(v => ({ value: v, label: t(`devices.types.${v}`), icon: TYPE_ICONS[v] })));
// Solo los subsistemas que el kit HaaS monitorea; el caudalímetro va en la cisterna
const pointOptions = computed(() => COMMON_AREA_TYPES
  .filter(a => form.type !== DeviceType.WATER_PULSE_METER || a.meter === 'water')
  .map(a => ({ value: a.type, label: t(`condominiums.commonAreas.types.${a.type}`) })));

watch(() => props.visible, (v) => {
  if (!v) return;
  Object.keys(errors).forEach(k => delete errors[k]);
  Object.assign(form, new Device(props.device || { condominiumId: Number(props.condominiumId), type: DeviceType.WATER_PULSE_METER, measurementPoint: 'cistern', nightFlowThresholdM3h: DEFAULT_NIGHT_FLOW_M3H }));
});
watch(() => form.type, (type) => {
  if (type === DeviceType.WATER_PULSE_METER) {
    form.measurementPoint = 'cistern';
    if (form.nightFlowThresholdM3h == null) form.nightFlowThresholdM3h = DEFAULT_NIGHT_FLOW_M3H;
  }
});

function save() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!isValidName(form.deviceId, 3, 40)) errors.deviceId = t('devices.validation.deviceId');
  else if (store.isDeviceIdTaken(Number(form.condominiumId), form.deviceId, form.id)) errors.deviceId = t('devices.validation.idTaken');
  if (!form.type) errors.type = t('devices.validation.required');
  if (!form.measurementPoint) errors.measurementPoint = t('devices.validation.required');
  if (!form.macAddress || !isValidMacAddress(form.macAddress)) errors.macAddress = t('devices.validation.mac');
  if (!isValidName(form.location, 3, 120)) errors.location = t('devices.validation.location');
  if (Object.keys(errors).length) return;
  emit('save', Object.assign(new Device({}), form, { condominiumId: Number(form.condominiumId) }));
}
</script>

<template>
  <pv-dialog :visible="visible" modal :header="device ? t('devices.edit') : t('devices.add')" :style="{ width: '34rem' }" :breakpoints="{ '640px': '95vw' }"
             @update:visible="$emit('update:visible', $event)">
    <p v-if="!device" class="intro">{{ t('devices.dialog.intro') }}</p>
    <div class="field">
      <label><i class="pi pi-microchip" />{{ t('devices.fields.type') }}<span class="req">*</span></label>
      <pv-select-button v-model="form.type" :options="typeOptions" optionLabel="label" optionValue="value" :allowEmpty="false" class="types">
        <template #option="{ option }"><i :class="option.icon" /> {{ option.label }}</template>
      </pv-select-button>
    </div>
    <div class="grid-2">
      <div class="field">
        <label for="deviceId"><i class="pi pi-tag" />{{ t('devices.fields.deviceId') }}<span class="req">*</span></label>
        <pv-input-text id="deviceId" v-model="form.deviceId" placeholder="WFS-CIS-IN" :invalid="!!errors.deviceId" class="w-full" />
        <small v-if="errors.deviceId" class="p-error">{{ errors.deviceId }}</small>
      </div>
      <div class="field">
        <label for="mac"><i class="pi pi-sitemap" />{{ t('devices.fields.macAddress') }}<span class="req">*</span></label>
        <pv-input-text id="mac" v-model="form.macAddress" placeholder="AA:BB:CC:00:00:01" :invalid="!!errors.macAddress" class="w-full mono" />
        <small v-if="errors.macAddress" class="p-error">{{ errors.macAddress }}</small>
      </div>
    </div>
    <div class="field">
      <label for="point"><i class="pi pi-map" />{{ t('devices.fields.measurementPoint') }}<span class="req">*</span></label>
      <pv-select inputId="point" v-model="form.measurementPoint" :options="pointOptions" optionLabel="label" optionValue="value" :invalid="!!errors.measurementPoint" class="w-full" />
      <small v-if="errors.measurementPoint" class="p-error">{{ errors.measurementPoint }}</small>
    </div>
    <div class="field">
      <label for="location"><i class="pi pi-map-marker" />{{ t('devices.fields.location') }}<span class="req">*</span></label>
      <pv-input-text id="location" v-model="form.location" :placeholder="t('devices.dialog.locationPlaceholder')" :invalid="!!errors.location" class="w-full" />
      <small v-if="errors.location" class="p-error">{{ errors.location }}</small>
    </div>
    <div v-if="form.type === 'water_pulse_meter'" class="field">
      <label for="threshold"><i class="pi pi-moon" />{{ t('devices.fields.nightFlowThreshold') }}</label>
      <pv-input-number inputId="threshold" v-model="form.nightFlowThresholdM3h" :min="0" :maxFractionDigits="3" suffix=" m³/h" class="w-full" />
      <small class="hint">{{ t('devices.dialog.thresholdHint') }}</small>
    </div>
    <template #footer>
      <pv-button :label="t('common.cancel')" severity="secondary" outlined @click="$emit('update:visible', false)" />
      <pv-button :label="t('common.save')" icon="pi pi-check" @click="save" />
    </template>
  </pv-dialog>
</template>

<style scoped>
.intro { margin: 0 0 1rem; color: #6b7280; font-size: .875rem; }
.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0 1rem; }
@media (max-width: 560px) { .grid-2 { grid-template-columns: 1fr; } }
.field { display: flex; flex-direction: column; gap: .4rem; margin-bottom: 1rem; }
.field label { display: flex; align-items: center; gap: .5rem; font-size: .875rem; font-weight: 600; color: #374151; }
.field label .pi { color: #059669; }
.req { color: #ef4444; margin-left: -.25rem; }
.hint { color: #6b7280; font-size: .8rem; }
.types { display: flex; flex-wrap: wrap; }
.mono :deep(input), .mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
</style>
