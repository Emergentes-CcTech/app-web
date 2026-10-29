<script setup>
import { computed, reactive, watch } from 'vue';
import { useI18n } from 'vue-i18n';

const props = defineProps({ visible: Boolean, payment: Object });
const emit = defineEmits(['update:visible', 'confirm']);
const { t } = useI18n();
const form = reactive({ amount: 0, paidAt: '', method: 'cash', reference: '' });
const errors = reactive({});
const methods = computed(() => ['cash', 'transfer', 'card'].map(v => ({ value: v, label: t(`payments.register.methods.${v}`) })));

watch(() => props.visible, (v) => {
  if (!v) return;
  Object.keys(errors).forEach(k => delete errors[k]);
  Object.assign(form, { amount: props.payment?.amount ?? 0, paidAt: new Date().toISOString().slice(0, 10), method: 'cash', reference: '' });
});

function confirm() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!(Number(form.amount) > 0)) errors.amount = t('payments.register.amountInvalid');
  if (!form.paidAt) errors.paidAt = t('devices.validation.required');
  if (Object.keys(errors).length) return;
  emit('confirm', { amount: Number(form.amount), paidAt: new Date(form.paidAt).toISOString(), method: form.method, reference: form.reference.trim() });
}
</script>

<template>
  <pv-dialog :visible="visible" modal :header="t('payments.delinquency.register')" :style="{ width: '26rem' }" @update:visible="$emit('update:visible', $event)">
    <div class="field"><label>{{ t('payments.register.amount') }}</label><pv-input-number v-model="form.amount" mode="currency" currency="PEN" locale="es-PE" :invalid="!!errors.amount" class="w-full" /><small v-if="errors.amount" class="p-error">{{ errors.amount }}</small></div>
    <div class="field"><label>{{ t('payments.register.paidAt') }}</label><pv-input-text v-model="form.paidAt" type="date" :invalid="!!errors.paidAt" class="w-full" /></div>
    <div class="field"><label>{{ t('payments.register.method') }}</label><pv-select v-model="form.method" :options="methods" optionLabel="label" optionValue="value" class="w-full" /></div>
    <div class="field"><label>{{ t('payments.register.reference') }}</label><pv-input-text v-model="form.reference" class="w-full" /></div>
    <template #footer>
      <pv-button :label="t('common.cancel')" severity="secondary" @click="$emit('update:visible', false)" />
      <pv-button :label="t('common.confirm')" icon="pi pi-check" @click="confirm" />
    </template>
  </pv-dialog>
</template>

<style scoped>
.field { margin-bottom: 12px; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
</style>
