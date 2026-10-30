<script setup>
import { useI18n } from 'vue-i18n';
import { collectionSeverity } from '../../domain/model/collection-status.enum.js';
import ResidentActionsMenu from './resident-actions-menu.vue';

defineProps({ residents: Array, loading: Boolean });
defineEmits(['view', 'edit', 'resend', 'unassign']);
const { t } = useI18n();
const initials = (n) => (n || '?').split(' ').filter(Boolean).map(w => w[0]).slice(0, 2).join('').toUpperCase();
</script>

<template>
  <div class="table-card">
    <pv-data-table :value="residents" :loading="loading" :paginator="residents.length > 10" :rows="10" dataKey="id" rowHover
                   sortField="unitNumber" :sortOrder="1" class="residents" @row-click="$emit('view', $event.data)">
      <template #empty><p class="empty">{{ t('residents.empty') }}</p></template>
      <pv-column field="unitNumber" :header="t('residents.fields.unit')" sortable style="width: 6rem">
        <template #body="{ data }"><span class="unit">{{ data.unitNumber }}</span></template>
      </pv-column>
      <pv-column :header="t('residents.fields.fullName')" sortField="fullName" sortable>
        <template #body="{ data }">
          <div class="who">
            <img v-if="data.photoUrl" :src="data.photoUrl" :alt="data.fullName" class="avatar" />
            <span v-else class="avatar" aria-hidden="true">{{ initials(data.fullName) }}</span>
            <span class="who-text"><strong>{{ data.fullName }}</strong><small>{{ data.email }}</small></span>
          </div>
        </template>
      </pv-column>
      <pv-column :header="t('residents.fields.phoneNumber')">
        <template #body="{ data }"><span v-if="data.phoneNumber">{{ data.phoneNumber }}</span><span v-else class="muted">—</span></template>
      </pv-column>
      <pv-column field="aliquotPercentage" :header="t('residents.profile.aliquot')" sortable>
        <template #body="{ data }"><span class="num">{{ data.aliquotPercentage != null ? `${data.aliquotPercentage.toFixed(4)} %` : '—' }}</span></template>
      </pv-column>
      <pv-column :header="t('residents.fields.relation')"><template #body="{ data }">{{ t(`residents.relation.${data.relation}`) }}</template></pv-column>
      <pv-column field="collectionStatus" :header="t('residents.fields.collectionStatus')" sortable>
        <template #body="{ data }"><pv-tag :severity="collectionSeverity(data.collectionStatus)" :value="t(`residents.status.${data.collectionStatus}`)" /></template>
      </pv-column>
      <pv-column style="width: 4rem">
        <template #body="{ data }">
          <span @click.stop>
            <resident-actions-menu :resident="data" @view="$emit('view', $event)" @edit="$emit('edit', $event)" @resend="$emit('resend', $event)" @unassign="$emit('unassign', $event)" />
          </span>
        </template>
      </pv-column>
    </pv-data-table>
  </div>
</template>

<style scoped>
.table-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden; }
.residents :deep(tbody tr) { cursor: pointer; }
.unit { font-weight: 700; font-variant-numeric: tabular-nums; }
.who { display: flex; align-items: center; gap: .7rem; }
.avatar { width: 36px; height: 36px; border-radius: 50%; background: #10B981; color: #fff; display: inline-flex; align-items: center; justify-content: center; font-size: .8rem; font-weight: 600; object-fit: cover; flex-shrink: 0; }
.who-text { display: flex; flex-direction: column; min-width: 0; }
.who-text small { color: #6B7280; overflow: hidden; text-overflow: ellipsis; }
.num { font-variant-numeric: tabular-nums; }
.muted { color: #9ca3af; }
.empty { text-align: center; color: #6B7280; padding: 1.5rem 0; margin: 0; }
</style>
