<script setup>
import { ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { IamApi } from '../../../iam/infrastructure/iam-api.js';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { isValidPassword, doPasswordsMatch, isNewPasswordDifferent } from '../../../shared/presentation/validators.js';

const { t } = useI18n();
const toast = useToast();
const api = new IamApi();
const iam = useIamStore();

const form = ref({ current: '', next: '', confirm: '' });
const error = ref('');
const sessions = ref([]);

async function changePassword() {
  error.value = '';
  if (!isValidPassword(form.value.next)) { error.value = t('iam.validation.passwordMinLength'); return; }
  if (!isNewPasswordDifferent(form.value.current, form.value.next)) { error.value = t('profile.security.sameAsCurrent'); return; }
  if (!doPasswordsMatch(form.value.next, form.value.confirm)) { error.value = t('iam.validation.passwordMismatch'); return; }
  try {
    await api.changePassword(iam.currentUser.id, { currentPassword: form.value.current, newPassword: form.value.next });
    form.value = { current: '', next: '', confirm: '' };
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('profile.security.passwordChanged'), life: 2500 });
  } catch (_) {
    error.value = t('profile.security.passwordError');
  }
}

async function loadSessions() {
  try { sessions.value = (await api.getSessions(iam.currentUser.id)).data || []; } catch (_) { sessions.value = []; }
}
async function closeSession(id) {
  try { await api.revokeSession(id); await loadSessions(); } catch (_) { /* ignore */ }
}
onMounted(loadSessions);
</script>

<template>
  <section class="security-section">
    <h3 class="card-title">{{ t('profile.security.title') }}</h3>
    <div class="field"><label>{{ t('profile.security.current') }}</label><pv-password v-model="form.current" :feedback="false" toggleMask class="w-full" inputClass="w-full" /></div>
    <div class="field"><label>{{ t('profile.security.new') }}</label><pv-password v-model="form.next" :feedback="false" toggleMask class="w-full" inputClass="w-full" /></div>
    <div class="field"><label>{{ t('profile.security.confirm') }}</label><pv-password v-model="form.confirm" :feedback="false" toggleMask class="w-full" inputClass="w-full" /></div>
    <pv-message v-if="error" severity="error" :closable="false">{{ error }}</pv-message>
    <pv-button :label="t('profile.security.changePassword')" icon="pi pi-lock" size="small" @click="changePassword" />

    <h4 class="mt-4">{{ t('profile.security.sessions') }}</h4>
    <p v-if="!sessions.length" class="muted">{{ t('profile.security.noSessions') }}</p>
    <ul v-else class="sessions">
      <li v-for="s in sessions" :key="s.id">
        <span>{{ s.device || s.userAgent || `#${s.id}` }} · {{ s.lastActiveAt || s.createdAt || '' }}</span>
        <pv-button :label="t('profile.security.closeSession')" size="small" text severity="danger" @click="closeSession(s.id)" />
      </li>
    </ul>

    <div class="toggle-row mt-3">
      <label>{{ t('profile.security.twoFactor') }}</label>
      <pv-tag severity="secondary" :value="t('profile.security.comingSoon')" />
    </div>
  </section>
</template>

<style scoped>
.field { margin-bottom: 10px; }
.field label { display: block; margin-bottom: 4px; }
.muted { color: #6B7280; }
.sessions { list-style: none; padding: 0; }
.sessions li { display: flex; justify-content: space-between; align-items: center; padding: 6px 0; border-bottom: 1px solid #e5e7eb; }
.toggle-row { display: flex; justify-content: space-between; align-items: center; padding: 10px 15px; background: #F9FAFB; border: 1px solid #e5e7eb; border-radius: 6px; }
.card-title { margin: 1rem 0 .75rem; }
</style>
