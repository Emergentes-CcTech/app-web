import { defineStore } from "pinia";
import { ref } from "vue";
import { BillingApi } from "../infrastructure/billing-api.js";
import { BillingAssembler } from "../infrastructure/billing.assembler.js";
import { CondominiumApi } from "../../condominiums/infrastructure/condominium-api.js";
import { CondominiumAssembler } from "../../condominiums/infrastructure/condominium.assembler.js";
import { previewProration } from "./proration-preview.js";
import { isParticipationBalanced } from "../../shared/presentation/validators.js";

const api = new BillingApi();
const condoApi = new CondominiumApi();

export const useBillingStore = defineStore('billing', () => {
    const bills = ref([]);
    const settlements = ref([]);
    const currentSettlement = ref(null);
    const isLoading = ref(false);
    const errors = ref([]);

    async function fetchBills(condominiumId, period = null) {
        isLoading.value = true; errors.value = [];
        try { bills.value = BillingAssembler.toBills((await api.getBills(condominiumId, period)).data); }
        catch (e) { errors.value.push(e.message || 'Error'); }
        finally { isLoading.value = false; }
    }

    async function addBill(bill) {
        // El backend guarda la imagen del recibo (Cloudinary) en el periodo como receiptImageUrl
        const payload = { ...bill, receiptImageUrl: bill.attachmentUrl || null }; delete payload.id;
        const { data } = await api.createBill(payload);
        bills.value.unshift(BillingAssembler.toBill(data));
    }

    async function fetchSettlements(condominiumId) {
        isLoading.value = true; errors.value = [];
        try { settlements.value = BillingAssembler.toSettlements((await api.getSettlements(condominiumId)).data); }
        catch (e) { errors.value.push(e.message || 'Error'); }
        finally { isLoading.value = false; }
    }

    async function fetchSettlement(id) {
        const { data } = await api.getSettlement(id);
        currentSettlement.value = BillingAssembler.toSettlement(data);
        return currentSettlement.value;
    }

    /**
     * RF-013: el cálculo oficial lo hace el backend. Rechaza con un mensaje claro si el padrón no suma 100 %
     * o si no hay recibo del periodo.
     */
    async function calculate(condominiumId, period) {
        const units = CondominiumAssembler.toUnitEntities(await condoApi.getUnits(condominiumId));
        if (!isParticipationBalanced(units)) throw new Error('participation_not_100');
        const periodBills = BillingAssembler.toBills((await api.getBills(condominiumId, period)).data);
        if (!periodBills.length) throw new Error('no_bill');
        const total = Math.round(periodBills.reduce((a, b) => a + b.billedAmount * 100, 0)) / 100;
        const billIds = periodBills.map(b => b.id);
        const { data } = await api.calculate(condominiumId, period, billIds);
        const settlement = BillingAssembler.toSettlement({ ...data, commonAreaAmount: data.commonAreaAmount ?? total });
        // json-server no genera las líneas: se crean desde la vista previa (en el backend real las crea el servidor)
        if (!settlement.lines.length) {
            const preview = previewProration(total, units);
            for (const row of preview) {
                await api.createLine({
                    settlementId: settlement.id, ...row,
                    breakdown: { billIds, commonAreaAmount: total, participationPct: row.participationPct,
                        readingsFrom: `${period}-01`, readingsTo: `${period}-30` },
                });
            }
            if (data.commonAreaAmount == null) await api.http.patch(`${import.meta.env.VITE_SETTLEMENTS_ENDPOINT_PATH}/${settlement.id}`, { commonAreaAmount: total });
        }
        return settlement.id;
    }

    async function issue(id) {
        await api.issue(id);
        if (currentSettlement.value?.id === id) { currentSettlement.value.status = 'issued'; currentSettlement.value.issuedAt = new Date().toISOString(); }
    }

    function clearUserSession() { bills.value = []; settlements.value = []; currentSettlement.value = null; errors.value = []; }

    return { bills, settlements, currentSettlement, isLoading, errors, fetchBills, addBill, fetchSettlements, fetchSettlement, calculate, issue, clearUserSession };
});
