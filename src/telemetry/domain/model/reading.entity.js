// value: lectura acumulada del medidor (m³ o kWh); rate: magnitud instantánea (m³/h o kW)
export class Reading {
    constructor({ id = null, deviceId = null, condominiumId = null, timestamp = '', value = 0, unit = '',
                  rate = null, rateUnit = '' } = {}) {
        Object.assign(this, { id, deviceId, condominiumId, timestamp, value: Number(value), unit,
            rate: rate === null || rate === undefined ? null : Number(rate), rateUnit });
    }
}
