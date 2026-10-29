import { BaseApi } from "../../shared/infrastructure/base-api.js";
import { BaseEndpoint } from "../../shared/infrastructure/base-endpoint.js";

const plansEndpointPath = import.meta.env.VITE_PLANS_ENDPOINT_PATH;

export class PlanApi extends BaseApi {
    #plansEndpoint;

    constructor() {
        super();
        this.#plansEndpoint = new BaseEndpoint(this, plansEndpointPath);
    }

    /**
     * Get all available plans
     */
    getAllPlans() {
        return this.http.get(plansEndpointPath, {
            params: { _t: Date.now() },
            headers: { 'Cache-Control': 'no-cache, no-store, must-revalidate' }
        });
    }

    /**
     * Get plan by ID
     */
    getPlanById(id) {
        return this.#plansEndpoint.getById(id);
    }
}

