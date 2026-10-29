<script setup>
import { ref, watch, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useResidentStore } from '../../application/resident.store.js';
import { isValidEmail } from '../../../shared/presentation/validators.js';

const props = defineProps({ visible: Boolean, condominiums: Array, defaultCondominiumId: [Number, String] });
const emit = defineEmits(['update:visible', 'done']);
const { t } = useI18n();
const store = useResidentStore();

const condominiumId = ref(null);
const unitId = ref(null);
const email = ref('');
const code = ref('');
const error = ref('');
const busy = ref(false);

const condominium = computed(() => props.condominiums?.find(c => c.id === condominiumId.value));
watch(() => props.visible, (v) => {
  if (v) { condominiumId.value = Number(props.defaultCondominiumId) || null; unitId.value = null; email.value = ''; code.value = ''; error.value = ''; }
});
watch(condominiumId, (id) => { unitId.value = null; store.fetchFreeUnits(id); }, { immediate: false });
watch(() => props.visible, (v) => { if (v && condominiumId.value) store.fetchFreeUnits(condominiumId.value); });

async function generateCode() {
  error.value = '';
  const unit = store.freeUnits.find(u => u.id === unitId.value);
  if (!unit) { error.value = t('residents.validation.unitRequired'); return; }
  busy.value = true;
  try { code.value = await store.issueActivationCode(unit, condominium.value?.name); emit('done'); }
  catch (_) { error.value = t('residents.messages.error'); }
  finally { busy.value = false; }
}

async function assignByEmail() {
  error.value = '';
  if (!unitId.value) { error.value = t('residents.validation.unitRequired'); return; }
  if (!isValidEmail(email.value)) { error.value = t('iam.validation.emailInvalid'); return; }
  busy.value = true;
  try { await store.assignExistingUser(unitId.value, email.value); emit('done'); emit('update:visible', false); }
  catch (e) { error.value = e.message === 'user_not_found' ? t('residents.validation.userNotFound') : t('residents.messages.error'); }
  finally { busy.value = false; }
}
</script>

<template>
  <pv-dialog :visible="visible" modal :header="t('residents.addDialogTitle')" :style="{ width: '30rem' }" @update:visible="$emit('update:visible', $event)">
    <div class="field"><label>{{ t('residents.fields.condominium') }}</label>
      <pv-select v-model="condominiumId" :options="condominiums" optionLabel="name" optionValue="id" class="w-full" /></div>
    <div class="field"><label>{{ t('residents.fields.unit') }}</label>
      <pv-select v-model="unitId" :options="store.freeUnits" optionLabel="number" optionValue="id" :placeholder="t('residents.freeUnit')" class="w-full" /></div>

    <div class="block">
      <pv-button :label="t('residents.generateCode')" icon="pi pi-key" :loading="busy" @click="generateCode" />
      <div v-if="code" class="code"><span>{{ t('residents.fields.activationCode') }}:</span> <code>{{ code }}</code></div>
      <small class="hint">{{ t('residents.codeHint') }}</small>
    </div>

    <pv-divider align="center">{{ t('residents.or') }}</pv-divider>

    <div class="field"><label>{{ t('residents.assignByEmail') }}</label>
      <pv-input-text v-model="email" type="email" class="w-full" />
      <pv-button :label="t('residents.assign')" icon="pi pi-user-plus" severity="secondary" class="mt-2" :loading="busy" @click="assignByEmail" /></div>

    <pv-message v-if="error" severity="error" :closable="false">{{ error }}</pv-message>
    <template #footer><pv-button :label="t('common.close')" severity="secondary" @click="$emit('update:visible', false)" /></template>
  </pv-dialog>
</template>

<style scoped>
.field { margin-bottom: 12px; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
.code { margin-top: .75rem; padding: .6rem; background: #ECFDF5; border-radius: 8px; }
.hint { display: block; color: #6B7280; margin-top: .4rem; }
</style>
