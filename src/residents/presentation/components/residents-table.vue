<script setup>
import { useI18n } from 'vue-i18n';
import { collectionSeverity } from '../../domain/model/collection-status.enum.js';
import ResidentActionsMenu from './resident-actions-menu.vue';

defineProps({ residents: Array, loading: Boolean });
defineEmits(['view', 'edit', 'resend', 'unassign']);
const { t } = useI18n();
const initials = (n) => (n || '?').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
</script>

<template>
  <pv-data-table :value="residents" :loading="loading" paginator :rows="10" dataKey="id" stripedRows>
    <template #empty>{{ t('residents.empty') }}</template>
    <pv-column :header="t('residents.fields.fullName')" sortField="fullName" sortable>
      <template #body="{ data }">
        <div class="who"><span class="avatar">{{ initials(data.fullName) }}</span><span>{{ data.fullName }}</span></div>
      </template>
    </pv-column>
    <pv-column :header="t('residents.fields.contact')"><template #body="{ data }">{{ data.email }}<br /><small>{{ data.phoneNumber }}</small></template></pv-column>
    <pv-column field="condominiumName" :header="t('residents.fields.condominium')" />
    <pv-column field="unitNumber" :header="t('residents.fields.unit')" sortable />
    <pv-column :header="t('residents.fields.relation')"><template #body="{ data }">{{ t(`residents.relation.${data.relation}`) }}</template></pv-column>
    <pv-column :header="t('residents.fields.collectionStatus')">
      <template #body="{ data }"><pv-tag :severity="collectionSeverity(data.collectionStatus)" :value="t(`residents.status.${data.collectionStatus}`)" /></template>
    </pv-column>
    <pv-column :header="t('common.actions')">
      <template #body="{ data }">
        <resident-actions-menu :resident="data" @view="$emit('view', $event)" @edit="$emit('edit', $event)" @resend="$emit('resend', $event)" @unassign="$emit('unassign', $event)" />
      </template>
    </pv-column>
  </pv-data-table>
</template>

<style scoped>
.who { display: flex; align-items: center; gap: .6rem; }
.avatar { width: 32px; height: 32px; border-radius: 50%; background: #10B981; color: #fff; display: inline-flex; align-items: center; justify-content: center; font-size: .8rem; font-weight: 600; }
</style>
