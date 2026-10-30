import { Device, DeviceType, MeasurementPoint } from '../domain/model/device.entity.js';

// El backend publica el kit HaaS con sus nombres de dominio (WaterFlowSensor, MainCistern...);
// la interfaz trabaja con los tipos y puntos de medición del frontend.
const TYPE_FROM_API = {
    GatewayHub: DeviceType.GATEWAY,
    WaterFlowSensor: DeviceType.WATER_PULSE_METER,
    CurrentTransformerCT: DeviceType.CURRENT_SENSOR,
};
const POINT_FROM_API = {
    MainCistern: MeasurementPoint.CISTERN,
    PressurePumpsRoom: MeasurementPoint.PUMP_ROOM,
    ElevatorsBank: MeasurementPoint.ELEVATOR,
    GeneralElectricalPanel: MeasurementPoint.PANEL,
};

export class DeviceAssembler {
    static toEntity(r) {
        const raw = r || {};
        return new Device({
            ...raw,
            deviceId: raw.deviceId || raw.name || '',
            type: TYPE_FROM_API[raw.type] || raw.type || '',
            measurementPoint: raw.measurementPoint || POINT_FROM_API[raw.subsystem] || '',
            status: String(raw.status || 'offline').toLowerCase(),
            lastSeenAt: raw.lastSeenAt || raw.lastHeartbeatUtc || null,
            location: raw.location || '',
        });
    }
    static toEntityList(list) { return (Array.isArray(list) ? list : []).map(DeviceAssembler.toEntity); }
    static toResource(e) {
        const { isOnline, ...rest } = e;
        return rest;
    }
}
