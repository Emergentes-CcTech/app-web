<script setup>
import { computed, reactive, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useProfileStore } from '../../application/profile.store.js';

// RF-003: preferencias de notificación por rol
const props = defineProps({ variant: { type: String, default: 'administrator' } });
const { t } = useI18n();
const store = useProfileStore();

const keys = computed(() => props.variant === 'administrator'
  ? ['email', 'sms', 'criticalAlerts', 'billingReminders']
  : ['email', 'newSettlement', 'dueDates', 'condominiumAlerts']);

const prefs = reactive({});
watch(() => store.profile.notificationPrefs, (v) => Object.assign(prefs, v), { immediate: true, deep: true });

async function save() {
  try {
    await store.updateNotificationPrefs({ ...prefs });
  } catch (_) { /* el store ya registra el error */ }
}
</script>

<template>
  <section class="notifications-section">
    <h3 class="card-title">{{ t('profile.notifications.title') }}</h3>
    <div v-for="k in keys" :key="k" class="toggle-row">
      <label :for="`pref-${k}`">{{ t(`profile.notifications.${k}`) }}</label>
      <pv-checkbox v-model="prefs[k]" :inputId="`pref-${k}`" binary />
    </div>
    <pv-button :label="t('profile.notifications.save')" icon="pi pi-save" size="small" class="mt-3" @click="save" />
  </section>
</template>

<style scoped>
.toggle-row { display: flex; justify-content: space-between; align-items: center; padding: 10px 15px; background: #ECFDF5; border: 1px solid #e5e7eb; border-radius: 6px; margin-bottom: 8px; }
.card-title { margin: 1rem 0 .75rem; }
</style>
