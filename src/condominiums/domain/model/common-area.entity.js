/**
 * Subsistemas de áreas comunes que IoBuild monitorea con el kit HaaS. Son los únicos que el backend
 * registra (CommonAreaSubsystem) y coinciden con los puntos de medición de los dispositivos
 * (devices/domain/model/device.entity.js). Textos en condominiums.commonAreas.types.<type>.
 */
export const COMMON_AREA_TYPES = Object.freeze([
    { type: 'cistern', subsystem: 'MainCistern', icon: 'pi pi-database', meter: 'water' },
    { type: 'pump_room', subsystem: 'PressurePumpsRoom', icon: 'pi pi-cog', meter: 'energy' },
    { type: 'elevator', subsystem: 'ElevatorsBank', icon: 'pi pi-sort-alt', meter: 'energy' },
    { type: 'panel', subsystem: 'GeneralElectricalPanel', icon: 'pi pi-bolt', meter: 'energy' },
]);

// Íconos de tipos antiguos (registros previos al catálogo de subsistemas)
const LEGACY_ICONS = { lobby: 'pi pi-building', pool: 'pi pi-sun', gym: 'pi pi-heart', event_room: 'pi pi-users',
    garden: 'pi pi-globe', parking: 'pi pi-car', laundry: 'pi pi-refresh' };

export const commonAreaIcon = (type) => COMMON_AREA_TYPES.find(a => a.type === type)?.icon ?? LEGACY_ICONS[type] ?? 'pi pi-map-marker';
export const typeFromSubsystem = (subsystem) => COMMON_AREA_TYPES.find(a => a.subsystem === subsystem)?.type ?? subsystem;
export const subsystemFromType = (type) => COMMON_AREA_TYPES.find(a => a.type === type)?.subsystem ?? type;

export class CommonArea {
    constructor({ id = null, name = '', type = 'cistern', location = '' } = {}) {
        Object.assign(this, { id, name, type: typeFromSubsystem(type), location });
    }
}
