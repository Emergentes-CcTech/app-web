import { BaseApi } from "../../shared/infrastructure/base-api.js";

const readingsPath = import.meta.env.VITE_TELEMETRY_ENDPOINT_PATH;
const alertsPath = import.meta.env.VITE_ALERTS_ENDPOINT_PATH;

// timestamp_gte y _sort son filtros de json-server; en el backend real se traducen al contrato definido.
export class TelemetryApi extends BaseApi {
    getLiveReadings(condominiumId, minutes = 10) {
        const from = new Date(Date.now() - minutes * 60_000).toISOString();
        return this.http.get(readingsPath, { params: { condominiumId, timestamp_gte: from, _sort: 'timestamp', _order: 'asc' } });
    }
    getLatestReadings(condominiumId, limit = 50) {
        return this.http.get(readingsPath, { params: { condominiumId, _sort: 'timestamp', _order: 'desc', _limit: limit } });
    }
    getReadings(condominiumId, deviceId, from, to) {
        const params = { condominiumId, timestamp_gte: from, timestamp_lte: to, _sort: 'timestamp', _order: 'asc' };
        if (deviceId) params.deviceId = deviceId;
        return this.http.get(readingsPath, { params });
    }
    getAlerts(condominiumId, status = null) {
        return this.http.get(alertsPath, { params: { condominiumId, status, _sort: 'createdAt', _order: 'desc' } });
    }
    getOpenAlerts() { return this.http.get(alertsPath, { params: { status: 'open', _sort: 'createdAt', _order: 'desc' } }); }
    acknowledgeAlert(id) { return this.http.patch(`${alertsPath}/${id}`, { status: 'acknowledged' }); }
}
