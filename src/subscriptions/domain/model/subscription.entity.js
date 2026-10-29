import { isActiveStatus } from "./subscription-status.enum.js";

/**
 * Subscription Entity — la suscripción pertenece a un condominio (RF-014)
 */
export class Subscription {
    constructor({ id = 0, administratorId = 0, condominiumId = null, planId = 0, plan = null, status = '',
                  startDate = null, endDate = null, nextBillingDate = null, hardwareIncluded = 0, planName = '' }) {
        this.planName = planName;
        this.id = id;
        this.administratorId = administratorId;
        this.condominiumId = condominiumId;
        this.planId = planId || (plan ? plan.id : 0);
        this.plan = plan;
        this.status = status;
        this.startDate = startDate;
        this.endDate = endDate;
        this.nextBillingDate = nextBillingDate;   // US28: próximo ciclo de cobro
        this.hardwareIncluded = hardwareIncluded; // equipos del kit en comodato
    }

    isActive() { return isActiveStatus(this.status, this.endDate); }
    getFormattedPrice() { return this.plan ? this.plan.getFormattedPrice() : 'S/ 0'; }
    getPlanName() { return this.plan ? this.plan.name : ''; }
}
