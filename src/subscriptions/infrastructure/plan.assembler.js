import { Plan } from "../domain/model/plan.entity.js";
import { PLAN_TIERS } from "../domain/model/plan-limits.js";

/**
 * Plan Assembler
 * Handles the conversion between API resources and Plan entities
 */
export class PlanAssembler {
    /**
     * Convert API resource to Plan entity
     */
    static toEntityFromResource(resource) {
        if (!resource) return null;

        let features = [];
        if (Array.isArray(resource.features)) {
            features = resource.features;
        } else if (typeof resource.featuresJson === 'string' && resource.featuresJson.trim()) {
            try {
                features = JSON.parse(resource.featuresJson);
            } catch (_) {
                features = [];
            }
        } else if (typeof resource.features === 'string' && resource.features.trim()) {
            try {
                features = JSON.parse(resource.features);
            } catch (_) {
                features = [];
            }
        }

        return new Plan({
            id: resource.id,
            code: resource.code,
            name: resource.name,
            monthlyPen: resource.monthlyPen ?? resource.price,
            description: resource.description,
            features: features,
            featuresJson: resource.featuresJson || (resource.features ? JSON.stringify(resource.features) : ''),
            minUnits: resource.minUnits,
            maxUnits: resource.maxUnits,
            kit: resource.kit,
            supportLevel: resource.supportLevel
        });
    }

    /**
     * Convert API response to Plan entity
     */
    static toEntityFromResponse(response) {
        if (!response.data) return null;
        return PlanAssembler.toEntityFromResource(response.data);
    }

    /**
     * Convert array of API resources to Plan entities
     */
    static toEntitiesFromResourceArray(resources) {
        if (!Array.isArray(resources)) return [];
        return resources.map(resource => PlanAssembler.toEntityFromResource(resource));
    }

    /**
     * Local catalog, used when the backend does not expose /plans
     */
    static catalog() {
        return PLAN_TIERS.map((tier, i) => new Plan({ id: i + 1, code: tier.code, name: tier.name }));
    }

    /**
     * Convert Plan entity to API resource
     */
    static toResourceFromEntity(entity) {
        return {
            id: entity.id,
            code: entity.code,
            name: entity.name,
            monthlyPen: entity.monthlyPen,
            description: entity.description,
            features: entity.features,
            minUnits: entity.minUnits,
            maxUnits: entity.maxUnits,
            kit: entity.kit,
            supportLevel: entity.supportLevel
        };
    }
}
