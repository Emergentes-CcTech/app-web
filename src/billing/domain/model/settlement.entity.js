export class Settlement {
    constructor({ id = null, condominiumId = null, billId = null, period = '', commonAreaAmount = 0,
                  status = 'draft', issuedAt = null, lines = [] } = {}) { // draft | issued
        Object.assign(this, { id, condominiumId, billId, period, commonAreaAmount: Number(commonAreaAmount), status, issuedAt, lines });
    }

    get isIssued() { return this.status === 'issued'; }
    get linesTotal() { return Math.round(this.lines.reduce((a, l) => a + l.amount * 100, 0)) / 100; }
    get linesMatchTotal() { return this.linesTotal === Math.round(this.commonAreaAmount * 100) / 100; }
}
