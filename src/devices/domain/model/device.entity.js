export const DeviceType = Object.freeze({
    WATER_PULSE_METER: 'water_pulse_meter',
    CURRENT_SENSOR: 'current_sensor',
    GATEWAY: 'gateway',
});

// El punto de medición coincide con el subsistema de área común que mide (ver condominiums/common-area.entity.js)
export const MeasurementPoint = Object.freeze({
    CISTERN: 'cistern', PUMP_ROOM: 'pump_room', PANEL: 'panel', ELEVATOR: 'elevator', LOBBY: 'lobby',
    POOL: 'pool', GYM: 'gym', EVENT_ROOM: 'event_room', GARDEN: 'garden', PARKING: 'parking', LAUNDRY: 'laundry',
});

// Umbral de caudal nocturno por defecto del motor de fugas (01:00–04:00), en m³/h
export const DEFAULT_NIGHT_FLOW_M3H = 0.15;

export class Device {
    constructor({ id = null, condominiumId = null, deviceId = '', type = '', measurementPoint = '',
                  macAddress = '', status = 'offline', lastSeenAt = null,
                  nightFlowThresholdM3h = null, location = '' } = {}) {
        Object.assign(this, { id, condominiumId, deviceId, type, measurementPoint, macAddress,
            status, lastSeenAt,
            nightFlowThresholdM3h: nightFlowThresholdM3h === null || nightFlowThresholdM3h === undefined ? null : Number(nightFlowThresholdM3h),
            location });
    }

    get isOnline() { return String(this.status).toLowerCase() === 'online'; }
}
