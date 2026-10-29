import { Condominium } from '../domain/model/condominium.entity.js';
import { Unit } from '../domain/model/unit.entity.js';

export class CondominiumAssembler {
    static toEntity(r) { return new Condominium(r || {}); }
    static toEntities(list) { return (Array.isArray(list) ? list : []).map(CondominiumAssembler.toEntity); }
    static toUnitEntity(r) { return new Unit(r || {}); }
    static toUnitEntities(list) { return (Array.isArray(list) ? list : []).map(CondominiumAssembler.toUnitEntity); }

    static toResource(e) {
        return {
            id: e.id, administratorId: e.administratorId, name: e.name, address: e.address, district: e.district,
            totalUnits: Number(e.totalUnits) || 0,
            commonAreas: (e.commonAreas || []).map((a, i) => ({ id: a.id ?? i + 1, name: String(a.name || '').trim(), type: a.type })),
            waterSupplyCode: e.waterSupplyCode,
            electricitySupplyCode: e.electricitySupplyCode, status: e.status,
        };
    }

    static toUnitResource(u) {
        const { isAssigned, ...rest } = u;
        return rest;
    }
}
