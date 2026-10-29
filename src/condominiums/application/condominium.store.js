import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { CondominiumApi } from "../infrastructure/condominium-api.js";
import { CondominiumAssembler } from "../infrastructure/condominium.assembler.js";
import { getSessionUserId } from "../../shared/infrastructure/session.js";
import { sumParticipation, isParticipationBalanced } from "../../shared/presentation/validators.js";

const api = new CondominiumApi();

export const useCondominiumStore = defineStore('condominium', () => {
    const condominiums = ref([]);
    const current = ref(null);
    const units = ref([]);
    const isLoading = ref(false);
    const errors = ref([]);

    const getCurrentAdministratorId = () => getSessionUserId();
    const participationTotal = computed(() => sumParticipation(units.value));
    const isBalanced = computed(() => isParticipationBalanced(units.value));
    const activeCondominiums = computed(() => condominiums.value.filter(c => c.isActive));

    async function fetchAll() {
        isLoading.value = true;
        errors.value = [];
        try {
            const { data } = await api.getMine(getCurrentAdministratorId());
            condominiums.value = CondominiumAssembler.toEntities(data);
        } catch (e) {
            errors.value.push(e.message || 'Error');
            throw e;
        } finally { isLoading.value = false; }
    }

    async function fetchById(id) {
        const { data } = await api.getById(id);
        current.value = CondominiumAssembler.toEntity(data);
        return current.value;
    }

    /** Unidades vinculadas a un residente (dueño de departamento) */
    async function fetchUnitsByResident(userId) {
        return CondominiumAssembler.toUnitEntities(await api.getUnitsByResident(userId));
    }

    async function fetchUnits(condominiumId) {
        units.value = CondominiumAssembler.toUnitEntities(await api.getUnits(condominiumId));
        return units.value;
    }

    async function create(entity) {
        const payload = CondominiumAssembler.toResource({ ...entity, administratorId: getCurrentAdministratorId(), status: 'active' });
        delete payload.id;
        const { data } = await api.create(payload);
        const created = CondominiumAssembler.toEntity(data);
        condominiums.value.push(created);
        return created;
    }

    async function update(entity) {
        const { data } = await api.update(CondominiumAssembler.toResource(entity));
        const updated = CondominiumAssembler.toEntity(data);
        const i = condominiums.value.findIndex(c => c.id === updated.id);
        if (i >= 0) condominiums.value[i] = updated;
        current.value = updated;
        return updated;
    }

    async function deactivate(id) {
        await api.deactivate(id);
        const c = condominiums.value.find(x => x.id === id);
        if (c) c.status = 'inactive';
        if (current.value?.id === id) current.value.status = 'inactive';
    }

    /** RF-006: guarda el padrón completo (nuevas, modificadas, eliminadas). El backend audita cada cambio. */
    async function saveUnits(condominiumId, rows) {
        if (!isParticipationBalanced(rows.filter(r => !r._deleted))) {
            throw new Error('La suma de participaciones debe ser 100 %');
        }
        for (const r of rows) {
            const { _new, _deleted, _dirty, ...clean } = r;
            const resource = CondominiumAssembler.toUnitResource({ ...clean, condominiumId });
            if (_deleted && !_new) await api.deleteUnit(r.id);
            else if (_deleted) continue;
            else if (_new) { delete resource.id; await api.createUnit(resource); }
            else if (_dirty) await api.updateUnit(resource);
        }
        await fetchUnits(condominiumId);
    }

    function clearUserSession() {
        condominiums.value = []; current.value = null; units.value = []; errors.value = [];
    }

    return {
        condominiums, current, units, isLoading, errors, participationTotal, isBalanced, activeCondominiums,
        getCurrentAdministratorId, fetchAll, fetchById, fetchUnits, fetchUnitsByResident, create, update, deactivate, saveUnits, clearUserSession,
    };
});
