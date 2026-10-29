<script setup>
import { useI18n } from 'vue-i18n';

defineProps({ condominiums: Array, statusOptions: Array });
const condominium = defineModel('condominium');
const status = defineModel('status');
const search = defineModel('search');
defineEmits(['add']);
const { t } = useI18n();
</script>

<template>
  <div class="header">
    <pv-select v-model="condominium" :options="condominiums" optionLabel="name" optionValue="id" :placeholder="t('residents.fields.condominium')" class="filter" />
    <pv-select v-model="status" :options="statusOptions" optionLabel="label" optionValue="value" showClear :placeholder="t('residents.fields.collectionStatus')" class="filter" />
    <pv-icon-field><pv-input-icon class="pi pi-search" /><pv-input-text v-model="search" :placeholder="t('residents.search')" /></pv-icon-field>
    <pv-button :label="t('residents.add')" icon="pi pi-plus" class="ml-auto" @click="$emit('add')" />
  </div>
</template>

<style scoped>
.header { display: flex; gap: .75rem; flex-wrap: wrap; align-items: center; margin-bottom: 1rem; }
.filter { min-width: 14rem; }
.ml-auto { margin-left: auto; }
</style>
