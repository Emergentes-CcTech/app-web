import { Device } from '../domain/model/device.entity.js';

export class DeviceAssembler {
    static toEntity(r) { return new Device(r || {}); }
    static toEntityList(list) { return (Array.isArray(list) ? list : []).map(DeviceAssembler.toEntity); }
    static toResource(e) {
        const { isOnline, ...rest } = e;
        return rest;
    }
}
