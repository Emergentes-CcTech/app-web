<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

const search = defineModel('search');
const type = defineModel('type');
defineEmits(['add']);
const { t } = useI18n();
const typeOptions = computed(() => ['water_pulse_meter', 'current_sensor', 'gateway'].map(v => ({ value: v, label: t(`devices.types.${v}`) })));
</script>

<template>
  <div class="header">
    <pv-icon-field><pv-input-icon class="pi pi-search" /><pv-input-text v-model="search" :placeholder="t('devices.search')" /></pv-icon-field>
    <pv-select v-model="type" :options="typeOptions" optionLabel="label" optionValue="value" showClear :placeholder="t('devices.fields.type')" class="filter" />
    <pv-button :label="t('devices.add')" icon="pi pi-plus" class="ml-auto" @click="$emit('add')" />
  </div>
</template>

<style scoped>
.header { display: flex; gap: .75rem; flex-wrap: wrap; align-items: center; margin-bottom: 1rem; }
.filter { min-width: 14rem; }
.ml-auto { margin-left: auto; }
</style>
