export class Unit {
    constructor({
        id = null, condominiumId = null, number = '', type = 'apartment', // apartment | storage | parking
        areaM2 = 0, participationPct = 0, ownerUserId = null, residentUserId = null, ownerEmail = null, residentFullName = null, floor = null,
    } = {}) {
        Object.assign(this, {
            id, condominiumId, number, type, floor, areaM2: Number(areaM2) || 0,
            participationPct: Number(participationPct) || 0, ownerUserId, residentUserId, ownerEmail, residentFullName,
        });
    }

    get isAssigned() { return !!this.residentUserId; }
    // Propietario pre-registrado por la administración que aún no crea su cuenta
    get isInvited() { return !this.residentUserId && !!this.ownerEmail; }
}
