export class CondominiumSummary {
    constructor({ condominiumId = null, name = '', district = '', devicesOnline = 0, devicesTotal = 0, openAlerts = 0 } = {}) {
        Object.assign(this, { condominiumId, name, district, devicesOnline, devicesTotal, openAlerts });
    }
}
