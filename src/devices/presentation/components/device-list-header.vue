<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

const props = defineProps({ devices: { type: Array, default: () => [] }, condominiums: { type: Array, default: () => [] } });
const search = defineModel('search');
const type = defineModel('type');
const condominium = defineModel('condominium');
defineEmits(['add']);
const { t } = useI18n();
const tabs = computed(() => [
  { value: null, label: t('residents.filters.all'), count: props.devices.length },
  ...['water_pulse_meter', 'current_sensor', 'gateway'].map(v => ({ value: v, label: t(`devices.types.${v}`), count: props.devices.filter(d => d.type === v).length })),
]);
</script>

<template>
  <div class="header">
    <div class="title-row">
      <div>
        <h1>{{ t('devices.title') }}</h1>
        <p class="subtitle">{{ t('devices.subtitle') }}</p>
      </div>
      <pv-button :label="t('devices.add')" icon="pi pi-plus" @click="$emit('add')" />
    </div>
    <slot />
    <div class="filters">
      <div class="tabs" role="tablist">
        <button v-for="tab in tabs" :key="String(tab.value)" type="button" role="tab" class="tab" :class="{ active: type === tab.value }"
                :aria-selected="type === tab.value" @click="type = tab.value">
          {{ tab.label }} <span class="count">{{ tab.count }}</span>
        </button>
      </div>
      <div class="right">
        <pv-select v-if="condominiums.length > 1" v-model="condominium" :options="condominiums" optionLabel="name" optionValue="id" class="filter" />
        <pv-icon-field><pv-input-icon class="pi pi-search" /><pv-input-text v-model="search" :placeholder="t('devices.search')" /></pv-icon-field>
      </div>
    </div>
  </div>
</template>

<style scoped>
.header { display: flex; flex-direction: column; gap: 1rem; margin-bottom: 1rem; }
.title-row { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; }
.title-row h1 { margin: 0; }
.subtitle { margin: .25rem 0 0; color: #6B7280; }
.filters { display: flex; justify-content: space-between; align-items: center; gap: .75rem; flex-wrap: wrap; }
.tabs { display: inline-flex; flex-wrap: wrap; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: 3px; gap: 2px; }
.tab { border: 0; background: transparent; font: inherit; font-size: .9rem; color: #374151; padding: .4rem .8rem; border-radius: 7px; cursor: pointer; display: inline-flex; align-items: center; gap: .4rem; }
.tab:hover { background: #f3f4f6; }
.tab.active { background: #ECFDF5; color: #047857; font-weight: 600; }
.tab:focus-visible { outline: 2px solid #10B981; outline-offset: 1px; }
.count { font-size: .75rem; background: #f3f4f6; color: #374151; border-radius: 999px; padding: 0 .45rem; font-variant-numeric: tabular-nums; }
.tab.active .count { background: #10B981; color: #fff; }
.right { display: flex; gap: .5rem; flex-wrap: wrap; }
.filter { min-width: 14rem; }
</style>
