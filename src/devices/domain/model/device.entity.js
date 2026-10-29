export const DeviceType = Object.freeze({
    WATER_PULSE_METER: 'water_pulse_meter',
    CURRENT_SENSOR: 'current_sensor',
    GATEWAY: 'gateway',
});

// El punto de medición coincide con el tipo de área común que mide (excepto "panel", donde va el gateway)
export const MeasurementPoint = Object.freeze({
    CISTERN: 'cistern', PUMP_ROOM: 'pump_room', PANEL: 'panel', ELEVATOR: 'elevator', LOBBY: 'lobby',
    POOL: 'pool', GYM: 'gym', EVENT_ROOM: 'event_room', GARDEN: 'garden', PARKING: 'parking', LAUNDRY: 'laundry',
});

export class Device {
    constructor({ id = null, condominiumId = null, deviceId = '', type = '', measurementPoint = '',
                  macAddress = '', installedAt = '', status = 'offline', lastSeenAt = null,
                  nightFlowThresholdLpm = null, baselineAmps = null } = {}) {
        Object.assign(this, { id, condominiumId, deviceId, type, measurementPoint, macAddress,
            installedAt, status, lastSeenAt, nightFlowThresholdLpm, baselineAmps });
    }

    get isOnline() { return String(this.status).toLowerCase() === 'online'; }
}
