import { Device, DeviceType, MeasurementPoint, DEFAULT_NIGHT_FLOW_M3H } from '../domain/model/device.entity.js';

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
const invert = (map) => Object.fromEntries(Object.entries(map).map(([k, v]) => [v, k]));
const TYPE_TO_API = invert(TYPE_FROM_API);
const POINT_TO_API = invert(POINT_FROM_API);

export class DeviceAssembler {
    static toEntity(r) {
        const raw = r || {};
        return new Device({
            ...raw,
            condominiumId: raw.condominiumId ?? raw.projectId ?? null,
            deviceId: raw.deviceId || raw.name || '',
            type: TYPE_FROM_API[raw.type] || raw.type || '',
            measurementPoint: raw.measurementPoint || POINT_FROM_API[raw.subsystem] || '',
            status: String(raw.status || 'offline').toLowerCase(),
            lastSeenAt: raw.lastSeenAt || raw.lastHeartbeatUtc || null,
            nightFlowThresholdM3h: raw.nightFlowThresholdM3h ?? raw.nightFlowThresholdM3H ?? null,
            location: raw.location || '',
        });
    }
    static toEntityList(list) { return (Array.isArray(list) ? list : []).map(DeviceAssembler.toEntity); }

    // Contrato de POST/PUT /devices (CreateDeviceRequest en el backend)
    static toResource(e) {
        const isWater = e.type === DeviceType.WATER_PULSE_METER;
        return {
            id: e.id,
            name: String(e.deviceId || '').trim(),
            type: TYPE_TO_API[e.type] || e.type,
            location: String(e.location || '').trim(),
            macAddress: String(e.macAddress || '').trim().toUpperCase(),
            projectId: Number(e.condominiumId),
            status: e.isOnline ? 'Online' : 'Offline',
            subsystem: POINT_TO_API[e.measurementPoint] || 'MainCistern',
            nightFlowThresholdM3H: isWater ? (e.nightFlowThresholdM3h ?? DEFAULT_NIGHT_FLOW_M3H) : null,
        };
    }
}
