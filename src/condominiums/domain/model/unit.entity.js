export class Unit {
    constructor({
        id = null, condominiumId = null, number = '', type = 'apartment', // apartment | storage | parking
        areaM2 = 0, participationPct = 0, ownerUserId = null, residentUserId = null, activationCode = null,
    } = {}) {
        Object.assign(this, {
            id, condominiumId, number, type, areaM2: Number(areaM2) || 0,
            participationPct: Number(participationPct) || 0, ownerUserId, residentUserId, activationCode,
        });
    }

    get isAssigned() { return !!this.residentUserId; }
}
