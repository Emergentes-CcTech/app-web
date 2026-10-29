import { BaseApi } from "../../shared/infrastructure/base-api.js";
import { BaseEndpoint } from "../../shared/infrastructure/base-endpoint.js";

const condominiumsPath = import.meta.env.VITE_CONDOMINIUMS_ENDPOINT_PATH;
const unitsPath = import.meta.env.VITE_UNITS_ENDPOINT_PATH;

export class CondominiumApi extends BaseApi {
    #condominiums;

    constructor() {
        super();
        this.#condominiums = new BaseEndpoint(this, condominiumsPath);
    }

    getMine(administratorId) { return this.http.get(condominiumsPath, { params: { administratorId } }); }
    getById(id) { return this.#condominiums.getById(id); }
    create(resource) { return this.#condominiums.create(resource); }
    update(resource) { return this.#condominiums.update(resource.id, resource); }
    // RF-005: baja lógica; el backend conserva lecturas y liquidaciones (auditoría)
    deactivate(id) { return this.http.patch(`${condominiumsPath}/${id}`, { status: 'inactive' }); }

    async getUnits(condominiumId) {
        const { data } = await this.http.get(unitsPath, { params: { condominiumId } });
        return data;
    }
    async getUnitsByResident(userId) {
        const { data } = await this.http.get(unitsPath, { params: { residentUserId: userId } });
        return data;
    }
    createUnit(resource) { return this.http.post(unitsPath, resource); }
    updateUnit(resource) { return this.http.put(`${unitsPath}/${resource.id}`, resource); }
    patchUnit(id, partial) { return this.http.patch(`${unitsPath}/${id}`, partial); }
    deleteUnit(id) { return this.http.delete(`${unitsPath}/${id}`); }

    findByActivationCode(code) {
        return this.http.get(unitsPath, { params: { activationCode: code } }).then(r => r.data[0] ?? null);
    }
    linkResident(unitId, userId) {
        return this.http.patch(`${unitsPath}/${unitId}`, { residentUserId: userId, activationCode: null });
    }
}
