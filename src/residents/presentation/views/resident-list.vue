<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import { useResidentStore } from '../../application/resident.store.js';
import { CondominiumsFacade } from '../../infrastructure/condominiums.facade.js';
import ResidentListHeader from '../components/resident-list-header.vue';
import ResidentsTable from '../components/residents-table.vue';
import ResidentAddDialog from '../components/resident-add-dialog.vue';
import ResidentEditDialog from '../components/resident-edit-dialog.vue';

const { t } = useI18n();
const router = useRouter();
const toast = useToast();
const confirm = useConfirm();
const store = useResidentStore();

const condominiums = ref([]);
const condominiumId = ref(null);
const status = ref(null);
const search = ref('');
const showAdd = ref(false);
const editing = ref(null);
const showEdit = ref(false);


onMounted(async () => {
  condominiums.value = (await CondominiumsFacade.getCondominiums()).filter(c => c.isActive);
  condominiumId.value = condominiums.value[0]?.id ?? null;
});
watch(condominiumId, (id) => { if (id) store.fetchByCondominium(id); }, { immediate: true });

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  return store.residents.filter(r =>
    (!status.value || r.collectionStatus === status.value) &&
    (!q || `${r.fullName} ${r.email} ${r.unitNumber} ${r.dni}`.toLowerCase().includes(q)));
});

const reload = () => condominiumId.value && store.fetchByCondominium(condominiumId.value);

async function resend(resident) {
  // El residente ya está vinculado: se reenvía el código solo si la unidad aún no tiene residente
  toast.add({ severity: 'info', summary: t('residents.actions.resendCode'), detail: t('residents.messages.alreadyLinked', { unit: resident.unitNumber }), life: 3000 });
}
function unassign(resident) {
  confirm.require({
    message: t('residents.confirmUnassign', { name: resident.fullName, unit: resident.unitNumber }),
    header: t('residents.actions.unassign'), icon: 'pi pi-exclamation-triangle',
    acceptLabel: t('residents.actions.unassign'), rejectLabel: t('common.cancel'), acceptProps: { severity: 'danger' },
    accept: async () => { await store.unassign(resident.unitId); toast.add({ severity: 'success', summary: t('common.success'), detail: t('residents.messages.unassigned'), life: 2500 }); },
  });
}
function edit(resident) { editing.value = resident; showEdit.value = true; }
async function saveEdit(partial) {
  try {
    await store.updateContact(editing.value, partial);
    showEdit.value = false;
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('residents.messages.updated'), life: 2500 });
    reload();
  } catch (_) {
    toast.add({ severity: 'error', summary: 'Error', detail: t('residents.messages.error'), life: 3000 });
  }
}
</script>

<template>
  <div class="page">
    <resident-list-header v-model:condominium="condominiumId" v-model:status="status" v-model:search="search"
                          :condominiums="condominiums" :residents="store.residents" @add="showAdd = true" />
    <residents-table :residents="filtered" :loading="store.isLoading"
                     @view="router.push({ name: 'resident-profile', params: { id: $event.unitId } })"
                     @edit="edit" @resend="resend" @unassign="unassign" />
    <resident-add-dialog v-model:visible="showAdd" :condominiums="condominiums" :defaultCondominiumId="condominiumId" @done="reload" />
    <resident-edit-dialog v-model:visible="showEdit" :resident="editing" @save="saveEdit" />
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
</style>
