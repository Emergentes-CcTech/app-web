<script setup>
import { computed, reactive, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Device, MeasurementPoint } from '../../domain/model/device.entity.js';
import { isValidMacAddress, isValidName } from '../../../shared/presentation/validators.js';
import { useDeviceStore } from '../../application/device.store.js';

const props = defineProps({ visible: Boolean, device: Object, condominiumId: [Number, String] });
const emit = defineEmits(['update:visible', 'save']);
const { t } = useI18n();
const store = useDeviceStore();

const form = reactive(new Device({}));
const errors = reactive({});
const typeOptions = computed(() => ['water_pulse_meter', 'current_sensor', 'gateway'].map(v => ({ value: v, label: t(`devices.types.${v}`) })));
const pointOptions = computed(() => Object.values(MeasurementPoint).map(v => ({ value: v, label: t(`devices.points.${v}`) })));

watch(() => props.visible, (v) => {
  if (!v) return;
  Object.keys(errors).forEach(k => delete errors[k]);
  Object.assign(form, new Device(props.device || { condominiumId: Number(props.condominiumId), installedAt: new Date().toISOString().slice(0, 10) }));
});

function save() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!isValidName(form.deviceId, 3, 40)) errors.deviceId = t('devices.validation.deviceId');
  else if (store.isDeviceIdTaken(Number(form.condominiumId), form.deviceId, form.id)) errors.deviceId = t('devices.validation.idTaken');
  if (!form.type) errors.type = t('devices.validation.required');
  if (!form.measurementPoint) errors.measurementPoint = t('devices.validation.required');
  if (!form.macAddress || !isValidMacAddress(form.macAddress)) errors.macAddress = t('devices.validation.mac');
  if (!form.installedAt) errors.installedAt = t('devices.validation.required');
  if (Object.keys(errors).length) return;
  emit('save', { ...form, condominiumId: Number(form.condominiumId) });
}
</script>

<template>
  <pv-dialog :visible="visible" modal :header="device ? t('devices.edit') : t('devices.add')" :style="{ width: '32rem' }" @update:visible="$emit('update:visible', $event)">
    <div class="field"><label>{{ t('devices.fields.deviceId') }} *</label><pv-input-text v-model="form.deviceId" :invalid="!!errors.deviceId" class="w-full" /><small v-if="errors.deviceId" class="p-error">{{ errors.deviceId }}</small></div>
    <div class="field"><label>{{ t('devices.fields.type') }} *</label><pv-select v-model="form.type" :options="typeOptions" optionLabel="label" optionValue="value" :invalid="!!errors.type" class="w-full" /><small v-if="errors.type" class="p-error">{{ errors.type }}</small></div>
    <div class="field"><label>{{ t('devices.fields.measurementPoint') }} *</label><pv-select v-model="form.measurementPoint" :options="pointOptions" optionLabel="label" optionValue="value" :invalid="!!errors.measurementPoint" class="w-full" /><small v-if="errors.measurementPoint" class="p-error">{{ errors.measurementPoint }}</small></div>
    <div class="field"><label>{{ t('devices.fields.macAddress') }} *</label><pv-input-text v-model="form.macAddress" placeholder="AA:BB:CC:00:00:01" :invalid="!!errors.macAddress" class="w-full" /><small v-if="errors.macAddress" class="p-error">{{ errors.macAddress }}</small></div>
    <div class="field"><label>{{ t('devices.fields.installedAt') }} *</label><pv-input-text v-model="form.installedAt" type="date" :invalid="!!errors.installedAt" class="w-full" /></div>
    <div v-if="form.type === 'water_pulse_meter'" class="field"><label>{{ t('devices.fields.nightFlowThresholdLpm') }}</label><pv-input-number v-model="form.nightFlowThresholdLpm" :min="0" :maxFractionDigits="2" suffix=" L/min" class="w-full" /></div>
    <div v-if="form.type === 'current_sensor'" class="field"><label>{{ t('devices.fields.baselineAmps') }}</label><pv-input-number v-model="form.baselineAmps" :min="0" :maxFractionDigits="2" suffix=" A" class="w-full" /></div>
    <template #footer>
      <pv-button :label="t('common.cancel')" severity="secondary" @click="$emit('update:visible', false)" />
      <pv-button :label="t('common.save')" icon="pi pi-check" @click="save" />
    </template>
  </pv-dialog>
</template>

<style scoped>
.field { margin-bottom: 12px; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
</style>
