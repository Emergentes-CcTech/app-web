<script setup>
import { computed, reactive, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { loadCloudinaryWidget } from '../../../shared/infrastructure/cloudinary-loader.js';
import { getReceiptUploadConfig } from '../../../shared/infrastructure/cloudinary-config.js';
import { BillingApi } from '../../infrastructure/billing-api.js';

const props = defineProps({ period: String, loading: Boolean, condominiumId: [Number, String] });
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

const form = reactive({ service: 'water', billedConsumption: null, billedAmount: null, dueDate: '', attachmentUrl: '', readingFrom: '', readingTo: '' });
const errors = reactive({});
const services = computed(() => [{ value: 'water', label: t('billing.bills.water') }, { value: 'electricity', label: t('billing.bills.electricity') }]);
const unit = computed(() => form.service === 'water' ? 'm3' : 'kWh');
// El consumo medido no se escribe: lo calculan los sensores del kit para el periodo elegido
const api = new BillingApi();
const measured = ref(null);
const measuring = ref(false);
const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const today = iso(new Date());
// Por defecto el mes del periodo; se corrige con las fechas de lectura impresas en el recibo
function defaultReading() {
  if (!/^\d{4}-\d{2}$/.test(props.period || '')) return;
  const [y, m] = props.period.split('-').map(Number);
  const last = iso(new Date(y, m, 0));
  form.readingFrom = `${props.period}-01`;
  form.readingTo = last > today ? today : last;
}
watch(() => props.period, defaultReading, { immediate: true });
const validReading = computed(() => !!form.readingFrom && !!form.readingTo && form.readingFrom < form.readingTo && form.readingTo <= today);

let measureTimer = null;
async function loadMeasured() {
  measured.value = null;
  if (!props.condominiumId || !/^\d{4}-\d{2}$/.test(props.period || '') || !validReading.value) return;
  measuring.value = true;
  try { measured.value = await api.getMeasured(props.condominiumId, props.period, form.readingFrom, form.readingTo); } catch (_) { measured.value = null; }
  finally { measuring.value = false; }
}
// Se recalcula al cambiar el condominio, el periodo o las fechas de lectura (con una breve espera al escribir)
watch(() => [props.condominiumId, props.period, form.readingFrom, form.readingTo], () => {
  clearTimeout(measureTimer);
  measureTimer = setTimeout(loadMeasured, 250);
}, { immediate: true });
const measuredValue = computed(() => measured.value ? Number(form.service === 'water' ? measured.value.waterM3 : measured.value.energyKwh) : null);
// Lo facturado que no pasó por los medidores (pérdida o error del recibo)
const gap = computed(() => {
  const billed = Number(form.billedConsumption);
  if (measuredValue.value === null || !(billed > 0)) return null;
  const diff = Math.round((billed - measuredValue.value) * 100) / 100;
  return { diff, pct: Math.round((diff / billed) * 1000) / 10 };
});
const fmt = (n) => Number(n).toLocaleString('es-PE', { maximumFractionDigits: 2 });
const rangeText = computed(() => measured.value ? t('billing.bills.measuredRange', { from: measured.value.from, to: measured.value.to }) : '');

function submit() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!props.period) errors.period = t('billing.validation.period');
  if (!(Number(form.billedConsumption) > 0)) errors.billedConsumption = t('billing.validation.positive');
  if (!(Number(form.billedAmount) > 0)) errors.billedAmount = t('billing.validation.positive');
  if (!form.dueDate) errors.dueDate = t('devices.validation.required');
  if (!form.readingFrom || !form.readingTo) errors.reading = t('billing.bills.readingRequired');
  else if (form.readingFrom >= form.readingTo) errors.reading = t('billing.bills.readingOrder');
  else if (form.readingTo > today) errors.reading = t('billing.bills.readingFuture');
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
    <div class="field reading">
      <label>{{ t('billing.bills.readingPeriod') }}</label>
      <div class="reading-dates">
        <pv-input-text v-model="form.readingFrom" type="date" :max="today" :aria-label="t('billing.bills.readingFrom')" :invalid="!!errors.reading" />
        <span aria-hidden="true">→</span>
        <pv-input-text v-model="form.readingTo" type="date" :max="today" :aria-label="t('billing.bills.readingTo')" :invalid="!!errors.reading" />
      </div>
      <small v-if="errors.reading" class="p-error">{{ errors.reading }}</small>
      <small v-else class="muted">{{ t('billing.bills.readingHint') }}</small>
    </div>
    <div class="field measured">
      <label>{{ t('billing.bills.measuredConsumption') }} ({{ unit }})</label>
      <div class="measured-box" :class="{ warn: gap && Math.abs(gap.pct) > 10 }">
        <i v-if="measuring" class="pi pi-spin pi-spinner" aria-hidden="true" />
        <template v-else-if="measuredValue !== null">
          <strong>{{ fmt(measuredValue) }} {{ unit }}</strong>
          <small>{{ t('billing.bills.measuredBySensors') }} · {{ rangeText }}</small>
          <small v-if="gap">{{ t('billing.bills.measuredGap', { diff: fmt(gap.diff), unit, pct: gap.pct }) }}</small>
        </template>
        <small v-else>{{ t('billing.bills.measuredUnavailable') }}</small>
      </div>
    </div>
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
.reading-dates { display: flex; align-items: center; gap: .4rem; }
.reading-dates input { flex: 1; min-width: 0; }
.muted { color: #6b7280; }
.measured-box { min-height: 2.6rem; display: flex; flex-direction: column; justify-content: center; gap: .1rem; padding: .35rem .7rem; border-radius: 6px; background: #f0fdf4; border: 1px solid #bbf7d0; }
.measured-box strong { font-variant-numeric: tabular-nums; }
.measured-box small { color: #4b5563; }
.measured-box.warn { background: #fffbeb; border-color: #fde68a; }
</style>
