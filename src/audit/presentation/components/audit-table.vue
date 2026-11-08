<script setup>
import { useI18n } from 'vue-i18n';

defineProps({ events: Array, loading: Boolean });
const { t, locale } = useI18n();
</script>

<template>
  <pv-data-table :value="events" :loading="loading" dataKey="id" stripedRows paginator :rows="10">
    <template #empty>{{ t('audit.empty') }}</template>
    <pv-column :header="t('telemetry.date')"><template #body="{ data }">{{ new Date(data.at).toLocaleString(locale) }}</template></pv-column>
    <pv-column :header="t('telemetry.type')"><template #body="{ data }">{{ t(`audit.types.${data.type}`) }}</template></pv-column>
    <pv-column field="entity" :header="t('audit.entity')" />
    <pv-column field="actorUserId" :header="t('audit.actor')" />
    <pv-column field="previousValue" :header="t('audit.previous')" />
    <pv-column field="newValue" :header="t('audit.new')" />
  </pv-data-table>
</template>
