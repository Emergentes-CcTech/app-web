import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { PaymentApi } from "../infrastructure/payment-api.js";
import { PaymentAssembler } from "../infrastructure/payment.assembler.js";
import { PaymentStatus } from "../domain/model/payment.entity.js";
import { CondominiumApi } from "../../condominiums/infrastructure/condominium-api.js";

const api = new PaymentApi();
const condoApi = new CondominiumApi();

export const usePaymentStore = defineStore('payment', () => {
    const payments = ref([]);
    const unitsTotal = ref(0);
    const myUnits = ref([]);
    const myCondominium = ref(null);
    const isLoading = ref(false);
    const errors = ref([]);

    const overduePayments = computed(() => payments.value.filter(p => p.effectiveStatus === PaymentStatus.OVERDUE));
    const overdueUnitIds = computed(() => [...new Set(overduePayments.value.map(p => p.unitId))]);
    const overdueAmount = computed(() => Math.round(overduePayments.value.reduce((a, p) => a + p.amount * 100, 0)) / 100);
    // RF-010: % de morosidad = unidades con deuda vencida / total de unidades
    const delinquencyRate = computed(() => unitsTotal.value ? Math.round((overdueUnitIds.value.length / unitsTotal.value) * 1000) / 10 : 0);

    const pendingForResident = computed(() =>
        payments.value.filter(p => p.effectiveStatus !== PaymentStatus.PAID).sort((a, b) => String(a.dueDate).localeCompare(String(b.dueDate)))[0] || null);

    async function fetchForResident(userId) {
        isLoading.value = true; errors.value = [];
        try {
            myUnits.value = await condoApi.getUnitsByResident(userId);
            const ids = myUnits.value.map(u => u.id);
            const condominiumId = myUnits.value[0]?.condominiumId;
            const [paymentsRes, condoRes] = await Promise.all([
                ids.length ? api.getByUnits(ids) : Promise.resolve({ data: [] }),
                condominiumId ? condoApi.getById(condominiumId).catch(() => ({ data: null })) : Promise.resolve({ data: null }),
            ]);
            payments.value = PaymentAssembler.toEntities(paymentsRes.data);
            myCondominium.value = condoRes.data;
        } catch (e) { errors.value.push(e.message || 'Error'); }
        finally { isLoading.value = false; }
    }

    async function fetchByCondominium(condominiumId) {
        isLoading.value = true; errors.value = [];
        try {
            const [{ data }, units] = await Promise.all([api.getByCondominium(condominiumId), condoApi.getUnits(condominiumId)]);
            payments.value = PaymentAssembler.toEntities(data);
            unitsTotal.value = units.length;
        } catch (e) { errors.value.push(e.message || 'Error'); }
        finally { isLoading.value = false; }
    }

    async function register(paymentId, data) {
        await api.register(paymentId, data);
        const p = payments.value.find(x => x.id === paymentId);
        if (p) Object.assign(p, { ...data, status: PaymentStatus.PAID });
    }

    async function createCheckout(paymentId) {
        const { data } = await api.createCheckout(paymentId);
        return data;
    }

    async function confirmCheckout(paymentId, sessionId) {
        const { data } = await api.confirmCheckout(paymentId, sessionId);
        return data;
    }

    function clearUserSession() { payments.value = []; myUnits.value = []; myCondominium.value = null; unitsTotal.value = 0; errors.value = []; }

    return { payments, unitsTotal, myUnits, myCondominium, isLoading, errors, overduePayments, overdueUnitIds, overdueAmount,
        delinquencyRate, pendingForResident, fetchForResident, fetchByCondominium, register, createCheckout, confirmCheckout, clearUserSession };
});
