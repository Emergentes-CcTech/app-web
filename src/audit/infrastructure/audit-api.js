import { BaseApi } from "../../shared/infrastructure/base-api.js";

const auditPath = import.meta.env.VITE_AUDIT_ENDPOINT_PATH;

// Solo lectura. Sin create, update ni delete: el registro es inalterable (RF-015).
export class AuditApi extends BaseApi {
    getEvents(condominiumId, { type, from, to, entity } = {}) {
        return this.http.get(auditPath, { params: { condominiumId, type, entity, at_gte: from, at_lte: to, _sort: 'at', _order: 'desc' } });
    }
}
