<script setup>
import { ref, reactive, watch, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useResidentStore } from '../../application/resident.store.js';
import { isValidEmail, isValidName, isValidPhone } from '../../../shared/presentation/validators.js';

// Registro del propietario por la administración: queda pendiente hasta que él cree su cuenta con este correo
const props = defineProps({
  visible: Boolean,
  condominiums: Array,
  defaultCondominiumId: [Number, String],
  unit: { type: Object, default: null },       // unidad fija (desde el padrón o al editar una invitación)
  resident: { type: Object, default: null },   // invitación pendiente que se corrige
});
const emit = defineEmits(['update:visible', 'done']);
const { t } = useI18n();
const store = useResidentStore();

const condominiumId = ref(null);
const unitId = ref(null);
const form = reactive({ firstName: '', lastName: '', email: '', phoneNumber: '' });
const fieldErrors = reactive({});
const error = ref('');
const busy = ref(false);
const saved = ref(null);

const fixedUnit = computed(() => props.unit || (props.resident ? { id: props.resident.unitId, number: props.resident.unitNumber } : null));
const condominiumName = computed(() => props.condominiums?.find(c => c.id === condominiumId.value)?.name || props.resident?.condominiumName || '');
const isEdit = computed(() => !!props.resident);

watch(() => props.visible, (open) => {
  if (!open) return;
  condominiumId.value = Number(props.defaultCondominiumId) || props.resident?.condominiumId || null;
  unitId.value = fixedUnit.value?.id ?? null;
  const [first = '', ...rest] = (props.resident?.fullName || '').split(' ');
  Object.assign(form, { firstName: first, lastName: rest.join(' '), email: props.resident?.email || '', phoneNumber: props.resident?.phoneNumber || '' });
  Object.keys(fieldErrors).forEach(k => delete fieldErrors[k]);
  error.value = '';
  saved.value = null;
  if (!fixedUnit.value && condominiumId.value) store.fetchFreeUnits(condominiumId.value);
});
watch(condominiumId, (id, old) => {
  if (fixedUnit.value || old === null || id === old) return;
  unitId.value = null;
  store.fetchFreeUnits(id);
});

function validate() {
  Object.keys(fieldErrors).forEach(k => delete fieldErrors[k]);
  if (!unitId.value) fieldErrors.unit = t('residents.validation.unitRequired');
  if (!isValidName(form.firstName, 2, 60)) fieldErrors.firstName = t('residents.validation.firstName');
  if (!isValidName(form.lastName, 2, 60)) fieldErrors.lastName = t('residents.validation.lastName');
  if (!isValidEmail(form.email)) fieldErrors.email = t('iam.validation.emailInvalid');
  if (!isValidPhone(form.phoneNumber)) fieldErrors.phoneNumber = t('iam.validation.phoneInvalid');
  return Object.keys(fieldErrors).length === 0;
}

async function submit() {
  error.value = '';
  if (!validate()) return;
  busy.value = true;
  try {
    const owner = { firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim().toLowerCase(), phoneNumber: form.phoneNumber.trim() };
    saved.value = await store.inviteOwner(unitId.value, owner);
    emit('done', saved.value);
  } catch (e) {
    const code = e.response?.data?.code;
    if (code === 'account_exists') fieldErrors.email = t('residents.validation.accountExists');
    else if (code === 'unit_has_owner') error.value = t('residents.validation.unitHasOwner');
    else error.value = e.response?.data?.error || t('residents.messages.error');
  } finally { busy.value = false; }
}

const close = () => emit('update:visible', false);
</script>

<template>
  <pv-dialog :visible="visible" modal :header="isEdit ? t('residents.invite.editTitle') : t('residents.invite.title')" :style="{ width: '32rem' }"
             :breakpoints="{ '640px': '94vw' }" @update:visible="$emit('update:visible', $event)">
    <div v-if="saved" class="done">
      <i class="pi pi-check-circle" aria-hidden="true" />
      <p><strong>{{ t('residents.invite.savedTitle', { name: saved.fullName, unit: saved.unitNumber }) }}</strong></p>
      <p>{{ t('residents.invite.savedBody', { email: saved.email }) }}</p>
    </div>

    <form v-else id="owner-invite" novalidate @submit.prevent="submit">
      <p class="lead">{{ t('residents.invite.lead') }}</p>
      <div class="grid2">
        <div class="field">
          <label for="inv-condo">{{ t('residents.fields.condominium') }}</label>
          <pv-select v-if="!fixedUnit" inputId="inv-condo" v-model="condominiumId" :options="condominiums" optionLabel="name" optionValue="id" class="w-full" />
          <p v-else class="fixed-value">{{ condominiumName }}</p>
        </div>
        <div class="field">
          <label for="inv-unit">{{ t('residents.fields.unit') }} *</label>
          <pv-select v-if="!fixedUnit" inputId="inv-unit" v-model="unitId" :options="store.freeUnits" optionLabel="number" optionValue="id"
                     :placeholder="t('residents.freeUnit')" :emptyMessage="t('residents.invite.noFreeUnits')" :invalid="!!fieldErrors.unit" class="w-full" />
          <p v-else class="fixed-value">{{ fixedUnit.number }}</p>
          <small v-if="fieldErrors.unit" class="p-error">{{ fieldErrors.unit }}</small>
        </div>
        <div class="field">
          <label for="inv-first">{{ t('residents.fields.firstName') }} *</label>
          <pv-input-text id="inv-first" v-model="form.firstName" autocomplete="off" :invalid="!!fieldErrors.firstName" class="w-full" />
          <small v-if="fieldErrors.firstName" class="p-error">{{ fieldErrors.firstName }}</small>
        </div>
        <div class="field">
          <label for="inv-last">{{ t('residents.fields.lastName') }} *</label>
          <pv-input-text id="inv-last" v-model="form.lastName" autocomplete="off" :invalid="!!fieldErrors.lastName" class="w-full" />
          <small v-if="fieldErrors.lastName" class="p-error">{{ fieldErrors.lastName }}</small>
        </div>
        <div class="field">
          <label for="inv-email">{{ t('residents.fields.email') }} *</label>
          <pv-input-text id="inv-email" v-model="form.email" type="email" autocomplete="off" :invalid="!!fieldErrors.email" class="w-full" />
          <small v-if="fieldErrors.email" class="p-error">{{ fieldErrors.email }}</small>
        </div>
        <div class="field">
          <label for="inv-phone">{{ t('residents.fields.phoneNumber') }} *</label>
          <pv-input-text id="inv-phone" v-model="form.phoneNumber" type="tel" placeholder="987 654 321" :invalid="!!fieldErrors.phoneNumber" class="w-full" />
          <small v-if="fieldErrors.phoneNumber" class="p-error">{{ fieldErrors.phoneNumber }}</small>
        </div>
      </div>
      <small class="hint">{{ t('residents.invite.hint') }}</small>
      <pv-message v-if="error" severity="error" :closable="false" class="mt-2">{{ error }}</pv-message>
    </form>

    <template #footer>
      <pv-button v-if="saved" :label="t('common.close')" @click="close" />
      <template v-else>
        <pv-button :label="t('common.cancel')" severity="secondary" text @click="close" />
        <pv-button type="submit" form="owner-invite" :label="isEdit ? t('common.save') : t('residents.invite.submit')" icon="pi pi-user-plus" :loading="busy" />
      </template>
    </template>
  </pv-dialog>
</template>

<style scoped>
.lead { margin: 0 0 1rem; color: #4B5563; }
.grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0 .9rem; }
.field { margin-bottom: .85rem; display: flex; flex-direction: column; gap: .3rem; }
.field label { font-weight: 500; }
.fixed-value { margin: 0; padding: .55rem 0; font-weight: 600; }
.hint { display: block; color: #6B7280; }
.done { text-align: center; padding: .5rem 0; }
.done i { font-size: 2.2rem; color: #10B981; }
.done p { margin: .6rem 0 0; }
@media (max-width: 520px) { .grid2 { grid-template-columns: 1fr; } }
</style>
