import { CommonArea } from './common-area.entity.js';

export class Condominium {
    constructor({
        id = null, administratorId = null, name = '', address = '', district = '',
        totalUnits = 0, commonAreas = [], waterSupplyCode = '', electricitySupplyCode = '', status = 'active',
    } = {}) {
        Object.assign(this, {
            id, administratorId, name, address, district,
            totalUnits: Number(totalUnits) || 0, // número de departamentos: define el plan de suscripción
            commonAreas: (Array.isArray(commonAreas) ? commonAreas : []).map(a => new CommonArea(a)),
            waterSupplyCode, electricitySupplyCode, status,
        });
    }

    // RF-005: estados válidos active / inactive (baja lógica; el histórico se conserva)
    get isActive() { return this.status === 'active'; }
    get commonAreasCount() { return this.commonAreas.length; }
}
