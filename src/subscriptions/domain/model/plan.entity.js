import { findTier, kitTotal } from './plan-limits.js';

/**
 * Plan Entity — plan HaaS por escala de condominio (RF-014).
 * Los valores que no envíe el backend se completan con el catálogo local (plan-limits.js).
 */
export class Plan {
    constructor({ id = 0, code = '', name = '', description = '', features = [], featuresJson = '', minUnits = null,
                  maxUnits = null, kit = null, monthlyPen = null, price = null, supportLevel = '' }) {
        const tier = findTier(code || name);
        this.id = id;
        this.code = code || tier?.code || String(name).toLowerCase().replace(/\s+/g, '-');
        this.name = name || tier?.name || '';
        this.monthlyPen = Number(monthlyPen ?? price ?? tier?.monthlyPen ?? 0);
        this.price = this.monthlyPen; // alias usado por los componentes de comparación
        this.description = description;
        this.featuresJson = featuresJson;
        let parsed = [];
        if (Array.isArray(features) && features.length) parsed = features;
        else if (typeof featuresJson === 'string' && featuresJson.trim()) { try { parsed = JSON.parse(featuresJson); } catch (_) { parsed = []; } }
        else if (typeof features === 'string' && features.trim()) { try { parsed = JSON.parse(features); } catch (_) { parsed = []; } }
        this.features = Array.isArray(parsed) ? parsed : [];
        this.minUnits = Number(minUnits ?? tier?.minUnits ?? 1) || 1;
        this.maxUnits = Number(maxUnits ?? tier?.maxUnits ?? 0) || 0;
        // Kit de equipos incluido: gateways + caudalímetros + pinzas CT
        this.kit = kit ?? tier?.kit ?? { gateways: 0, flowMeters: 0, ctClamps: 0 };
        this.supportLevel = supportLevel;
    }

    /** Total de equipos del kit (Boutique 4, Urbano 6, Gran Torre 10) */
    get hardwareIncluded() { return kitTotal(this.kit); }

    getFormattedPrice() { return `S/ ${this.monthlyPen.toLocaleString('es-PE')}`; }
    get unitsRangeLabel() { return `${this.minUnits}-${this.maxUnits}`; }
}
