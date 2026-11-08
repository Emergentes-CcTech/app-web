import { BaseApi } from "../../shared/infrastructure/base-api.js";

const residentsPath = import.meta.env.VITE_RESIDENTS_ENDPOINT_PATH;
const unitsPath = import.meta.env.VITE_UNITS_ENDPOINT_PATH;
const profilesPath = import.meta.env.VITE_PROFILES_ENDPOINT_PATH;
const usersPath = import.meta.env.VITE_USERS_ENDPOINT_PATH;

export class ResidentApi extends BaseApi {
    /** Devuelve los datos operationsos para armar residentes: unidades asignadas + perfiles + usuarios. */
    async getRawByCondominium(condominiumId) {
        try {
            // Si el backend expone /residents se usa tal cual
            const res = await this.http.get(residentsPath, { params: { condominiumId } });
            if (Array.isArray(res.data)) return { direct: res.data };
        } catch (_) { /* fallback: armar la vista en el cliente */ }
        const [units, profiles, users] = await Promise.all([
            this.http.get(unitsPath, { params: { condominiumId } }),
            this.http.get(profilesPath),
            this.http.get(usersPath),
        ]);
        return { units: units.data, profiles: profiles.data, users: users.data };
    }

    getById(unitId) { return this.http.get(`${unitsPath}/${unitId}`); }
    getUserByEmail(email) { return this.http.get(usersPath, { params: { email } }).then(r => r.data[0] ?? null); }
    getFreeUnits(condominiumId) {
        return this.http.get(unitsPath, { params: { condominiumId } }).then(r => r.data.filter(u => !u.residentUserId));
    }
    assignToUnit(unitId, userId) { return this.http.patch(`${unitsPath}/${unitId}`, { residentUserId: userId, activationCode: null }); }
    unassign(unitId) { return this.http.patch(`${unitsPath}/${unitId}`, { residentUserId: null }); }
    setActivationCode(unitId, code) { return this.http.patch(`${unitsPath}/${unitId}`, { activationCode: code }); }
    updateProfile(profileId, partial) { return this.http.patch(`${profilesPath}/${profileId}`, partial); }
    getProfileByUserId(userId) { return this.http.get(profilesPath, { params: { userId } }).then(r => r.data[0] ?? null); }
}
