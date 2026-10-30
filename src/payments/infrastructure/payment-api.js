import { BaseApi } from "../../shared/infrastructure/base-api.js";

const paymentsPath = import.meta.env.VITE_PAYMENTS_ENDPOINT_PATH;

export class PaymentApi extends BaseApi {
    getByUnits(unitIds) {
        const id = Array.isArray(unitIds) && unitIds.length === 1 ? unitIds[0] : unitIds;
        return this.http.get(paymentsPath, { params: { unitId: id } });
    }              // residente
    getByCondominium(condominiumId) { return this.http.get(paymentsPath, { params: { condominiumId } }); }   // administrador
    // RF-016, registro manual
    register(paymentId, { amount, paidAt, method, reference }) {
        return this.http.patch(`${paymentsPath}/${paymentId}`, { amount, paidAt, method, reference, status: 'paid' });
    }
    createCheckout(paymentId) { return this.http.post(`${paymentsPath}/${paymentId}/checkout-session`); }      // Stripe
    // Al volver de Stripe, el backend consulta la sesión y concilia el pago
    confirmCheckout(paymentId, sessionId) { return this.http.post(`${paymentsPath}/${paymentId}/checkout-session/confirm`, { sessionId }); }
}
