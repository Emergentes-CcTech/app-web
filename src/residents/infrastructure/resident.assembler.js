import { Resident } from '../domain/model/resident.entity.js';

export class ResidentAssembler {
    /** Une unidad + perfil + usuario (+ estado de cobranza) en la vista de residente. */
    static fromJoin({ unit, profile, user, condominium, collectionStatus }) {
        return new Resident({
            id: unit.id,
            userId: unit.residentUserId,
            fullName: profile?.fullName || '',
            dni: profile?.dni || '',
            email: user?.email || '',
            phoneNumber: profile?.phoneNumber || '',
            condominiumId: unit.condominiumId,
            condominiumName: condominium?.name || '',
            unitId: unit.id,
            unitNumber: unit.number,
            relation: unit.ownerUserId && unit.ownerUserId === unit.residentUserId ? 'owner' : 'tenant',
            collectionStatus: collectionStatus || 'current',
        });
    }

    static toEntity(r) { return new Resident(r || {}); }
}
