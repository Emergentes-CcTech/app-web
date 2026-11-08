import { CommonArea } from './common-area.entity.js';

export class Condominium {
    constructor({
        id = null, administratorId = null, name = '', address = '', district = '',
        totalUnits = 0, occupiedUnits = 0, totalFloors = null, commonAreas = [], waterSupplyCode = '', electricitySupplyCode = '',
        imageUrl = '', description = '', status = 'active',
    } = {}) {
        Object.assign(this, {
            id, administratorId, name, address, district,
            totalUnits: Number(totalUnits) || 0, // número de departamentos: define el plan de suscripción
            occupiedUnits: Number(occupiedUnits) || 0, // departamentos con residente registrado en el padrón
            totalFloors: totalFloors === null || totalFloors === undefined ? null : Number(totalFloors),
            commonAreas: (Array.isArray(commonAreas) ? commonAreas : []).map(a => new CommonArea(a)),
            waterSupplyCode, electricitySupplyCode, imageUrl: imageUrl || '', description: description || '', status,
        });
    }

    // RF-005: estados válidos active / inactive (baja lógica; el histórico se conserva)
    get isActive() { return this.status === 'active'; }
    get commonAreasCount() { return this.commonAreas.length; }
    get occupancyRate() { return this.totalUnits ? Math.round((this.occupiedUnits / this.totalUnits) * 100) : 0; }
    get locationLabel() { return [this.address, this.district].filter(v => v && String(v).trim()).join(', '); }
}
