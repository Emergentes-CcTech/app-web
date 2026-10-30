<script setup>
import { computed, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useProfileStore } from '../../application/profile.store.js';
import { ProfileAssembler } from '../../infrastructure/profile.assembler.js';
import { isValidName, isValidPhone, isValidEmail } from '../../../shared/presentation/validators.js';
import NotificationsSection from './notifications-section.vue';
import SecuritySection from './security-section.vue';

// Editor de perfil compartido por los dos roles (RF-003).
const props = defineProps({ variant: { type: String, required: true } }); // 'administrator' | 'resident'
const { t } = useI18n();
const toast = useToast();
const store = useProfileStore();

const isAdmin = computed(() => props.variant === 'administrator');
const isEditing = ref(false);
const isSaving = ref(false);
const errors = reactive({});
const draft = reactive({});

const profile = computed(() => store.profile);
const title = computed(() => profile.value.displayName);
const initial = computed(() => (title.value || '?').charAt(0).toUpperCase());

const editableFields = computed(() => isAdmin.value
  ? ['companyName', 'legalRepresentative', 'phoneNumber', 'address', 'secondEmail']
  : ['fullName', 'phoneNumber', 'secondEmail']);
const readOnlyFields = computed(() => isAdmin.value ? ['ruc', 'email'] : ['dni', 'email']);

function startEdit() { Object.assign(draft, profile.value); isEditing.value = true; }
function cancelEdit() { isEditing.value = false; Object.keys(errors).forEach(k => delete errors[k]); }

function validate() {
  Object.keys(errors).forEach(k => delete errors[k]);
  const main = isAdmin.value ? 'companyName' : 'fullName';
  if (!isValidName(draft[main], 2)) errors[main] = t('iam.validation.nameInvalid');
  if (isAdmin.value && !isValidName(draft.legalRepresentative)) errors.legalRepresentative = t('iam.validation.nameInvalid');
  if (!isValidPhone(draft.phoneNumber || '')) errors.phoneNumber = t('iam.validation.phoneInvalid');
  if (draft.secondEmail && !isValidEmail(draft.secondEmail)) errors.secondEmail = t('iam.validation.emailInvalid');
  return Object.keys(errors).length === 0;
}

async function save() {
  if (!validate()) return;
  isSaving.value = true;
  try {
    await store.updateProfile(profile.value.id, ProfileAssembler.toResource({ ...profile.value, ...draft }));
    isEditing.value = false;
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('profile.saveSuccess'), life: 2500 });
  } catch (_) {
    toast.add({ severity: 'error', summary: t('common.error'), detail: t('profile.saveError'), life: 3000 });
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <div class="profile-container">
    <h1>{{ t('profile.title') }}</h1>
    <div class="profile-grid">
      <aside class="card identity">
        <img v-if="profile.photoUrl" :src="profile.photoUrl" alt="" class="photo" />
        <div v-else class="photo placeholder">{{ initial }}</div>
        <h2>{{ title }}</h2>
        <p class="role">{{ t(isAdmin ? 'profile.administratorRole' : 'profile.residentRole') }}</p>
        <p class="muted">{{ profile.email }}</p>
        <p class="muted">ID {{ profile.userId }}</p>
      </aside>

      <main class="card">
        <div class="head">
          <h3>{{ t('profile.accountInformation') }}</h3>
          <pv-button v-if="!isEditing" :label="t('profile.edit')" icon="pi pi-pencil" size="small" @click="startEdit" />
          <div v-else class="flex gap-2">
            <pv-button :label="t('profile.save')" icon="pi pi-check" size="small" :loading="isSaving" @click="save" />
            <pv-button :label="t('profile.cancel')" severity="secondary" size="small" :disabled="isSaving" @click="cancelEdit" />
          </div>
        </div>

        <div v-for="f in editableFields" :key="f" class="field">
          <label :for="`pf-${f}`">{{ t(`profile.${f}`) }}</label>
          <pv-input-text :id="`pf-${f}`" :modelValue="isEditing ? draft[f] : profile[f]" :disabled="!isEditing"
                         :invalid="!!errors[f]" class="w-full" @update:modelValue="draft[f] = $event" />
          <small v-if="errors[f]" class="p-error">{{ errors[f] }}</small>
        </div>
        <div v-for="f in readOnlyFields" :key="f" class="field">
          <label :for="`pf-${f}`">{{ t(`profile.${f}`) }}</label>
          <pv-input-text :id="`pf-${f}`" :modelValue="profile[f]" disabled class="w-full" />
        </div>

        <h3 class="mt-3">{{ t('profile.appLanguage') }}</h3>
        <select v-model="$i18n.locale" class="language-select">
          <option value="es">Español</option>
          <option value="en">English</option>
        </select>

        <notifications-section :variant="variant" />
        <security-section />
      </main>
    </div>
  </div>
</template>

<style scoped>
.profile-container { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
.profile-grid { display: grid; grid-template-columns: 280px 1fr; gap: 1.5rem; }
@media (max-width: 800px) { .profile-grid { grid-template-columns: 1fr; } }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; }
.identity { text-align: center; }
.photo { width: 96px; height: 96px; border-radius: 50%; object-fit: cover; }
.placeholder { background: #10B981; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 2.5rem; margin: 0 auto; }
.role { color: #059669; font-weight: 600; margin: 0; }
.muted { color: #6B7280; margin: .25rem 0; }
.head { display: flex; justify-content: space-between; align-items: center; }
.field { margin-bottom: 12px; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
.language-select { padding: 8px; border-radius: 6px; border: 1px solid #d1d5db; }
</style>
