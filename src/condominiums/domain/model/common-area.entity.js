/**
 * Tipos de área común. Coinciden con los puntos de medición de los dispositivos
 * (devices/domain/model/device.entity.js): un medidor instalado en "pool" mide el área común "pool".
 * Textos en condominiums.commonAreas.types.<type>.
 */
export const COMMON_AREA_TYPES = Object.freeze([
    { type: 'lobby', icon: 'pi pi-building' },
    { type: 'pool', icon: 'pi pi-sun' },
    { type: 'gym', icon: 'pi pi-heart' },
    { type: 'event_room', icon: 'pi pi-users' },
    { type: 'garden', icon: 'pi pi-globe' },
    { type: 'parking', icon: 'pi pi-car' },
    { type: 'elevator', icon: 'pi pi-sort-alt' },
    { type: 'pump_room', icon: 'pi pi-cog' },
    { type: 'cistern', icon: 'pi pi-database' },
    { type: 'laundry', icon: 'pi pi-refresh' },
    { type: 'other', icon: 'pi pi-map-marker' },
]);

export const commonAreaIcon = (type) => COMMON_AREA_TYPES.find(a => a.type === type)?.icon ?? 'pi pi-map-marker';

export class CommonArea {
    constructor({ id = null, name = '', type = 'other' } = {}) {
        Object.assign(this, { id, name, type });
    }
}
