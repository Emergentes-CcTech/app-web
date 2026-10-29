export class SettlementLine {
    constructor({ id = null, settlementId = null, unitId = null, participationPct = 0, amount = 0, breakdown = null } = {}) {
        Object.assign(this, { id, settlementId, unitId, participationPct: Number(participationPct), amount: Number(amount), breakdown });
    }
}
