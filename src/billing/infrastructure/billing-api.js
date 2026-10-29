import { BaseApi } from "../../shared/infrastructure/base-api.js";

const billsPath = import.meta.env.VITE_BILLS_ENDPOINT_PATH;
const settlementsPath = import.meta.env.VITE_SETTLEMENTS_ENDPOINT_PATH;
const linesPath = import.meta.env.VITE_SETTLEMENT_LINES_ENDPOINT_PATH;

export class BillingApi extends BaseApi {
    getBills(condominiumId, period = null) { return this.http.get(billsPath, { params: { condominiumId, period } }); }
    createBill(resource) { return this.http.post(billsPath, resource); }   // RF-012

    getSettlements(condominiumId) {
        return this.http.get(settlementsPath, { params: { condominiumId, _sort: 'period', _order: 'desc' } });
    }
    getSettlement(id) { return this.http.get(`${settlementsPath}/${id}`, { params: { _embed: 'settlement_lines' } }); }

    // RF-013, lo calcula el backend
    calculate(condominiumId, period, billIds) {
        return this.http.post(settlementsPath, { condominiumId, period, billIds, status: 'draft' });
    }
    // Solo mock (json-server no genera líneas): el backend real las crea al calcular
    createLine(resource) { return this.http.post(linesPath, resource); }
    issue(id) { return this.http.patch(`${settlementsPath}/${id}`, { status: 'issued', issuedAt: new Date().toISOString() }); }
    getLinesByUnit(unitId) { return this.http.get(linesPath, { params: { unitId } }); }
    getLineById(id) { return this.http.get(`${linesPath}/${id}`); }
}
