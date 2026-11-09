import { defineStore } from "pinia";
import { ref } from "vue";
import { ResidentApi } from "../infrastructure/resident-api.js";
import { ResidentAssembler } from "../infrastructure/resident.assembler.js";
import { CondominiumsFacade } from "../infrastructure/condominiums.facade.js";
import { PaymentApi } from "../../payments/infrastructure/payment-api.js";
import { Payment, PaymentStatus } from "../../payments/domain/model/payment.entity.js";

const api = new ResidentApi();
const paymentApi = new PaymentApi();

/** Estado de cobranza derivado de los pagos: overdue si hay alguno vencido, pending si solo pendientes, current si no. */
export function deriveCollectionStatus(payments) {
    const list = payments.map(p => (p instanceof Payment ? p : new Payment(p)));
    if (list.some(p => p.effectiveStatus === PaymentStatus.OVERDUE)) return 'overdue';
    if (list.some(p => p.effectiveStatus === PaymentStatus.PENDING)) return 'pending';
    return 'current';
}

export const useResidentStore = defineStore('resident', () => {
    const residents = ref([]);
    const freeUnits = ref([]);
    const isLoading = ref(false);
    const errors = ref([]);

    async function fetchByCondominium(condominiumId) {
        isLoading.value = true;
        errors.value = [];
        try {
            const condominiums = await CondominiumsFacade.getCondominiums();
            const condominium = condominiums.find(c => c.id === Number(condominiumId));
            const raw = await api.getRawByCondominium(condominiumId);
            let payments = [];
            try { payments = (await paymentApi.getByCondominium(condominiumId)).data; } catch (_) { /* sin pagos */ }
            // El padrón del backend no calcula la cobranza; se deriva de las cuotas de cada unidad
            if (raw.direct) {
                residents.value = raw.direct.map(r => ResidentAssembler.toEntity({
                    ...r, collectionStatus: deriveCollectionStatus(payments.filter(p => p.unitId === r.unitId)),
                }));
                return;
            }
            residents.value = raw.units
                .filter(u => u.residentUserId)
                .map(unit => ResidentAssembler.fromJoin({
                    unit, condominium,
                    profile: raw.profiles.find(p => p.userId === unit.residentUserId),
                    user: raw.users.find(x => x.id === unit.residentUserId),
                    collectionStatus: deriveCollectionStatus(payments.filter(p => p.unitId === unit.id)),
                }));
        } catch (e) {
            errors.value.push(e.message || 'Error');
        } finally { isLoading.value = false; }
    }

    async function fetchFreeUnits(condominiumId) {
        freeUnits.value = condominiumId ? await api.getFreeUnits(condominiumId) : [];
        return freeUnits.value;
    }

    /** Registra (o corrige) al propietario de una unidad; queda pendiente hasta que cree su cuenta con ese correo. */
    async function inviteOwner(unitId, owner) {
        return api.inviteOwner(unitId, owner);
    }

    async function cancelInvitation(unitId) {
        await api.cancelInvitation(unitId);
        residents.value = residents.value.filter(r => r.unitId !== unitId);
    }

    async function unassign(unitId) { await api.unassign(unitId); residents.value = residents.value.filter(r => r.unitId !== unitId); }

    async function updateContact(resident, { phoneNumber }) {
        // Sin userId, /profiles devolvería el perfil de quien consulta y se editaría el contacto equivocado
        if (!resident.userId) throw new Error('user_not_found');
        const profile = await api.getProfileByUserId(resident.userId);
        if (profile) await api.updateProfile(profile.id, { phoneNumber });
    }

    function clearUserSession() { residents.value = []; freeUnits.value = []; errors.value = []; }

    return { residents, freeUnits, isLoading, errors, fetchByCondominium, fetchFreeUnits, inviteOwner,
        cancelInvitation, unassign, updateContact, clearUserSession };
});
