export class Reading {
    constructor({ id = null, deviceId = null, condominiumId = null, timestamp = '', value = 0, unit = '' } = {}) {
        Object.assign(this, { id, deviceId, condominiumId, timestamp, value: Number(value), unit });
    }
}
