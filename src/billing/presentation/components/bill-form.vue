<script setup>
import { computed, reactive, watch } from 'vue';
import { useI18n } from 'vue-i18n';

const props = defineProps({ period: String, loading: Boolean });
const emit = defineEmits(['submit']);
const { t } = useI18n();

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
    <div class="field"><label>{{ t('billing.bills.attachment') }}</label><pv-input-text v-model="form.attachmentUrl" placeholder="https://" class="w-full" /></div>
    <pv-message v-if="errors.period" severity="error" :closable="false">{{ errors.period }}</pv-message>
    <pv-button type="submit" :label="t('common.save')" icon="pi pi-check" :loading="loading" />
  </form>
</template>

<style scoped>
.bill-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; align-items: end; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
</style>
