import { BaseApi } from "../../shared/infrastructure/base-api.js";
import { BaseEndpoint } from "../../shared/infrastructure/base-endpoint.js";
import { DeviceAssembler } from "./device.assembler.js";

export class DeviceApi extends BaseApi {
    #devices;

    constructor() {
        super();
        this.devicesEndpoint = import.meta.env.VITE_DEVICES_ENDPOINT_PATH;
        this.#devices = new BaseEndpoint(this, this.devicesEndpoint);
    }

    async getByCondominium(condominiumId) {
        const { data } = await this.http.get(this.devicesEndpoint, { params: { condominiumId } });
        return DeviceAssembler.toEntityList(data);
    }
    getById(id) { return this.#devices.getById(id); }
    create(resource) { return this.#devices.create(resource); }
    update(id, resource) { return this.#devices.update(id, resource); }
    delete(id) { return this.#devices.delete(id); }
    // Desvincular conserva el histórico: baja lógica
    unlink(id) { return this.http.patch(`${this.devicesEndpoint}/${id}`, { status: 'offline', unlinkedAt: new Date().toISOString() }); }
}
