import { Condominium } from '../domain/model/condominium.entity.js';
import { Unit } from '../domain/model/unit.entity.js';
import { subsystemFromType } from '../domain/model/common-area.entity.js';

export class CondominiumAssembler {
    static toEntity(r) { return new Condominium(r || {}); }
    static toEntities(list) { return (Array.isArray(list) ? list : []).map(CondominiumAssembler.toEntity); }
    static toUnitEntity(r) { return new Unit(r || {}); }
    static toUnitEntities(list) { return (Array.isArray(list) ? list : []).map(CondominiumAssembler.toUnitEntity); }

    // Contrato de POST/PUT /condominiums (CondominiumRequest en el backend)
    static toResource(e) {
        return {
            id: e.id, name: String(e.name || '').trim(), address: String(e.address || '').trim(), district: String(e.district || '').trim(),
            totalUnits: Number(e.totalUnits) || 0,
            totalFloors: e.totalFloors ? Number(e.totalFloors) : null,
            waterSupplyCode: e.waterSupplyCode, electricitySupplyCode: e.electricitySupplyCode,
            imageUrl: e.imageUrl || null, description: e.description || '',
            commonAreas: (e.commonAreas || []).map(a => ({
                name: String(a.name || '').trim(), subsystem: subsystemFromType(a.type), location: String(a.location || '').trim(),
            })),
        };
    }

    static toUnitResource(u) {
        const { isAssigned, ...rest } = u;
        return rest;
    }
}
