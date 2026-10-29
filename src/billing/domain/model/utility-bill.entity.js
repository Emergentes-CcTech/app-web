export class UtilityBill {
    constructor({ id = null, condominiumId = null, service = 'water', period = '', // 'YYYY-MM'
                  billedConsumption = 0, consumptionUnit = 'm3', billedAmount = 0, dueDate = '',
                  measuredConsumption = null, attachmentUrl = '' } = {}) {
        Object.assign(this, {
            id, condominiumId, service, period, billedConsumption: Number(billedConsumption), consumptionUnit,
            billedAmount: Number(billedAmount), dueDate,
            measuredConsumption: measuredConsumption === null || measuredConsumption === '' ? null : Number(measuredConsumption),
            attachmentUrl,
        });
    }

    get unitPrice() { return this.billedConsumption ? this.billedAmount / this.billedConsumption : 0; }
    get differenceUnits() { return this.measuredConsumption === null ? null : this.billedConsumption - this.measuredConsumption; }
    get differenceAmount() { return this.differenceUnits === null ? null : Math.round(this.differenceUnits * this.unitPrice * 100) / 100; }
    get differencePct() {
        return this.differenceUnits === null || !this.billedConsumption ? null : Math.abs(this.differenceUnits / this.billedConsumption) * 100;
    }
}
