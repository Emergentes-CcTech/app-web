<script setup>
import { computed, reactive, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { loadCloudinaryWidget } from '../../../shared/infrastructure/cloudinary-loader.js';
import { getReceiptUploadConfig } from '../../../shared/infrastructure/cloudinary-config.js';

const props = defineProps({ period: String, loading: Boolean });
const emit = defineEmits(['submit']);
const { t } = useI18n();

const cloudinaryName = import.meta.env.VITE_CLOUDINARY_CLOUD_NAME;
const cloudinaryPreset = import.meta.env.VITE_CLOUDINARY_UPLOAD_PRESET;
const hasCloudinary = Boolean(cloudinaryName?.trim() && cloudinaryPreset?.trim());
const uploading = ref(false);
const uploadError = ref('');

// Foto o PDF del recibo matriz subido directo a Cloudinary; al backend solo viaja la URL segura
async function uploadReceipt() {
  uploadError.value = '';
  uploading.value = true;
  try {
    const cloudinary = await loadCloudinaryWidget();
    cloudinary.openUploadWidget(getReceiptUploadConfig(cloudinaryName, cloudinaryPreset), (error, result) => {
      if (error) { uploadError.value = t('billing.bills.uploadError'); uploading.value = false; return; }
      if (result?.event === 'success') form.attachmentUrl = result.info.secure_url;
      if (result?.event === 'close' || result?.event === 'success') uploading.value = false;
    });
  } catch (_) {
    uploadError.value = t('billing.bills.uploadError');
    uploading.value = false;
  }
}

const form = reactive({ service: 'water', billedConsumption: null, billedAmount: null, dueDate: '', measuredConsumption: null, attachmentUrl: '' });
const errors = reactive({});
const services = computed(() => [{ value: 'water', label: t('billing.bills.water') }, { value: 'electricity', label: t('billing.bills.electricity') }]);
const unit = computed(() => form.service === 'water' ? 'm3' : 'kWh');
watch(() => form.service, () => { form.measuredConsumption = null; });

function submit() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!props.period) errors.period = t('billing.validation.period');
  if (!(Number(form.billedConsumption) > 0)) errors.billedConsumption = t('billing.validation.positive');
  if (!(Number(form.billedAmount) > 0)) errors.billedAmount = t('billing.validation.positive');
  if (!form.dueDate) errors.dueDate = t('devices.validation.required');
  if (Object.keys(errors).length) return;
  emit('submit', { ...form, period: props.period, consumptionUnit: unit.value });
}
</script>

<template>
  <form class="bill-form" @submit.prevent="submit">
    <div class="field"><label>{{ t('billing.bills.service') }}</label><pv-select v-model="form.service" :options="services" optionLabel="label" optionValue="value" class="w-full" /></div>
    <div class="field"><label>{{ t('billing.bills.billedConsumption') }} ({{ unit }})</label><pv-input-number v-model="form.billedConsumption" :min="0" :maxFractionDigits="2" :invalid="!!errors.billedConsumption" class="w-full" /><small v-if="errors.billedConsumption" class="p-error">{{ errors.billedConsumption }}</small></div>
    <div class="field"><label>{{ t('billing.bills.amount') }}</label><pv-input-number v-model="form.billedAmount" mode="currency" currency="PEN" locale="es-PE" :invalid="!!errors.billedAmount" class="w-full" /><small v-if="errors.billedAmount" class="p-error">{{ errors.billedAmount }}</small></div>
    <div class="field"><label>{{ t('billing.bills.dueDate') }}</label><pv-input-text v-model="form.dueDate" type="date" :invalid="!!errors.dueDate" class="w-full" /></div>
    <div class="field"><label>{{ t('billing.bills.measuredConsumption') }} ({{ unit }})</label><pv-input-number v-model="form.measuredConsumption" :min="0" :maxFractionDigits="2" class="w-full" /></div>
    <div class="field">
      <label>{{ t('billing.bills.attachment') }}</label>
      <div v-if="hasCloudinary" class="upload">
        <pv-button type="button" :label="form.attachmentUrl ? t('billing.bills.replaceReceipt') : t('billing.bills.uploadReceipt')"
                   icon="pi pi-upload" severity="secondary" outlined :loading="uploading" @click="uploadReceipt" />
        <a v-if="form.attachmentUrl" :href="form.attachmentUrl" target="_blank" rel="noopener" class="receipt-link"><i class="pi pi-file" aria-hidden="true"></i> {{ t('billing.bills.viewReceipt') }}</a>
      </div>
      <pv-input-text v-else v-model="form.attachmentUrl" placeholder="https://" class="w-full" />
      <small v-if="uploadError" class="p-error">{{ uploadError }}</small>
    </div>
    <pv-message v-if="errors.period" severity="error" :closable="false">{{ errors.period }}</pv-message>
    <pv-button type="submit" :label="t('common.save')" icon="pi pi-check" :loading="loading" />
  </form>
</template>

<style scoped>
.bill-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; align-items: end; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
.upload { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; }
.receipt-link { color: var(--iobuilt-primary-hover); text-decoration: none; font-size: .9rem; }
.receipt-link:hover { text-decoration: underline; }
</style>
