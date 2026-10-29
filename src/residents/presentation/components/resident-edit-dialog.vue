<script setup>
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { isValidPhone } from '../../../shared/presentation/validators.js';

const props = defineProps({ visible: Boolean, resident: Object });
const emit = defineEmits(['update:visible', 'save']);
const { t } = useI18n();
const phone = ref('');
const error = ref('');
watch(() => props.resident, (r) => { phone.value = r?.phoneNumber || ''; error.value = ''; }, { immediate: true });

function save() {
  if (!isValidPhone(phone.value)) { error.value = t('iam.validation.phoneInvalid'); return; }
  emit('save', { phoneNumber: phone.value.trim() });
}
</script>

<template>
  <pv-dialog :visible="visible" modal :header="t('residents.editDialogTitle')" :style="{ width: '26rem' }" @update:visible="$emit('update:visible', $event)">
    <p><strong>{{ resident?.fullName }}</strong> · {{ t('residents.fields.unit') }} {{ resident?.unitNumber }}</p>
    <div class="field"><label>{{ t('residents.fields.phoneNumber') }}</label><pv-input-text v-model="phone" :invalid="!!error" class="w-full" /><small v-if="error" class="p-error">{{ error }}</small></div>
    <template #footer>
      <pv-button :label="t('common.cancel')" severity="secondary" @click="$emit('update:visible', false)" />
      <pv-button :label="t('common.save')" icon="pi pi-check" @click="save" />
    </template>
  </pv-dialog>
</template>
