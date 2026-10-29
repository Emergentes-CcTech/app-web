import { Subscription } from "../domain/model/subscription.entity.js";
import { PlanAssembler } from "./plan.assembler.js";

/**
 * Subscription Assembler
 * Handles the conversion between API resources and Subscription entities
 */
export class SubscriptionAssembler {
    /**
     * Convert API resource to Subscription entity
     */
    static toEntityFromResource(resource) {
        return new Subscription({
            id: resource.id,
            administratorId: resource.administratorId,
            condominiumId: resource.condominiumId ?? null,
            planId: resource.planId || (resource.plan && typeof resource.plan === 'object' ? resource.plan.id : 0),
            plan: resource.plan && typeof resource.plan === 'object' ? PlanAssembler.toEntityFromResource(resource.plan) : null,
            planName: typeof resource.plan === 'string' ? resource.plan : undefined,
            nextBillingDate: resource.nextBillingDate ?? null,
            hardwareIncluded: resource.hardwareIncluded ?? 0,
            status: resource.status,
            startDate: resource.startDate,
            endDate: resource.endDate
        });
    }

    /**
     * Convert API response to Subscription entity
     */
    static toEntityFromResponse(response) {
        if (!response.data) return null;
        return this.toEntityFromResource(response.data);
    }

    /**
     * Convert Subscription entity to API resource
     */
    static toResourceFromEntity(entity) {
        return {
            id: entity.id,
            administratorId: entity.administratorId,
            condominiumId: entity.condominiumId,
            planId: entity.plan ? entity.plan.id : null,
            status: entity.status,
            startDate: entity.startDate,
            endDate: entity.endDate
        };
    }
}
