<script setup>
import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';

const props = defineProps({ device: { type: Object, required: true } });
const emit = defineEmits(['edit', 'unlink']);
const { t } = useI18n();
const menu = ref(null);
const items = computed(() => [
  { label: t('devices.actions.edit'), icon: 'pi pi-pencil', command: () => emit('edit', props.device) },
  { label: t('devices.actions.unlink'), icon: 'pi pi-link', command: () => emit('unlink', props.device) },
]);
</script>

<template>
  <div>
    <pv-button icon="pi pi-ellipsis-v" text rounded severity="secondary" :aria-label="t('common.actions')" @click="menu.toggle($event)" />
    <pv-menu ref="menu" :model="items" popup />
  </div>
</template>
