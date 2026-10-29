import { BaseApi } from "../../shared/infrastructure/base-api.js";
import { BaseEndpoint } from "../../shared/infrastructure/base-endpoint.js";
import { APP_URL } from "../../shared/infrastructure/constants.js";

const subscriptionsEndpointPath = import.meta.env.VITE_SUBSCRIPTIONS_ENDPOINT_PATH;

export class SubscriptionApi extends BaseApi {
    #subscriptionsEndpoint;

    constructor() {
        super();
        this.#subscriptionsEndpoint = new BaseEndpoint(this, subscriptionsEndpointPath);
    }

    /** Suscripciones del administrador (una por condominio), más reciente primero. */
    async getSubscriptionsByAdministratorId(administratorId) {
        const response = await this.#subscriptionsEndpoint.getAll();
        const all = Array.isArray(response.data) ? response.data : [];
        const mine = all.filter(s => Number(s.administratorId) === Number(administratorId));
        mine.sort((a, b) => new Date(b.startDate || 0) - new Date(a.startDate || 0) || (b.id - a.id));
        return mine;
    }

    getSubscriptionById(id) {
        return this.#subscriptionsEndpoint.getById(id);
    }

    createSubscription(resource) {
        return this.#subscriptionsEndpoint.create(resource);
    }

    updateSubscription(resource) {
        return this.#subscriptionsEndpoint.update(resource.id, resource);
    }

    cancelSubscription(subscriptionId) {
        return this.http.post(`${subscriptionsEndpointPath}/${subscriptionId}/cancel`);
    }

    createCheckoutSession({ administratorId, condominiumId, planId }) {
        // El backend espera las URLs de success y cancel; Stripe agrega session_id como query parameter
        const baseUrl = (typeof window !== 'undefined' && window.location?.origin) ? window.location.origin : APP_URL;
        const successUrl = `${baseUrl}/subscriptions/my-subscription?success=true`;
        const cancelUrl = `${baseUrl}/subscriptions/my-subscription?canceled=true`;
        return this.http.post(`${subscriptionsEndpointPath}/payments/sessions`, { administratorId, condominiumId, planId, successUrl, cancelUrl });
    }

    confirmPayment({ administratorId, condominiumId }, sessionId) {
        return this.http.patch(`${subscriptionsEndpointPath}/payments/sessions/${sessionId}`, { administratorId, condominiumId, status: 'confirmed' });
    }

    getInvoicesByAdministrator(administratorId, condominiumId = null) {
        return this.http.get(`${subscriptionsEndpointPath}/payments/invoices`, { params: { administratorId, condominiumId } });
    }
}
