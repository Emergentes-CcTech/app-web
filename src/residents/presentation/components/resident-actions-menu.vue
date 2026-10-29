<script setup>
import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';

const props = defineProps({ resident: { type: Object, required: true } });
const emit = defineEmits(['view', 'edit', 'resend', 'unassign']);
const { t } = useI18n();
const menu = ref(null);
const items = computed(() => [
  { label: t('residents.actions.view'), icon: 'pi pi-user', command: () => emit('view', props.resident) },
  { label: t('residents.actions.edit'), icon: 'pi pi-pencil', command: () => emit('edit', props.resident) },
  { label: t('residents.actions.resendCode'), icon: 'pi pi-send', command: () => emit('resend', props.resident) },
  { label: t('residents.actions.unassign'), icon: 'pi pi-user-minus', command: () => emit('unassign', props.resident) },
]);
</script>

<template>
  <div>
    <pv-button icon="pi pi-ellipsis-v" text rounded severity="secondary" :aria-label="t('common.actions')" @click="menu.toggle($event)" />
    <pv-menu ref="menu" :model="items" popup />
  </div>
</template>
