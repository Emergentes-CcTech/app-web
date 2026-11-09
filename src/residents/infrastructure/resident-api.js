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
    // Libre = sin cuenta vinculada ni propietario pendiente de registro
    getFreeUnits(condominiumId) {
        return this.http.get(unitsPath, { params: { condominiumId } }).then(r => r.data.filter(u => !u.residentUserId && !u.ownerEmail));
    }
    inviteOwner(unitId, owner) { return this.http.put(`${unitsPath}/${unitId}/owner-invitation`, owner).then(r => r.data); }
    cancelInvitation(unitId) { return this.http.delete(`${unitsPath}/${unitId}/owner-invitation`); }
    unassign(unitId) { return this.http.patch(`${unitsPath}/${unitId}`, { residentUserId: null }); }
    updateProfile(profileId, partial) { return this.http.patch(`${profilesPath}/${profileId}`, partial); }
    getProfileByUserId(userId) { return this.http.get(profilesPath, { params: { userId } }).then(r => r.data[0] ?? null); }
}
