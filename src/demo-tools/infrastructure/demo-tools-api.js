import { BaseApi } from '../../shared/infrastructure/base-api.js';

/** Herramientas de demostración del backend (solo existen con DEMO_ENABLED en el servidor). */
export class DemoToolsApi extends BaseApi {
    status() { return this.http.get('/demo/status').then(r => r.data); }
    snapshot(condominiumId) { return this.http.get(`/demo/condominiums/${condominiumId}`).then(r => r.data); }
    createAlert(condominiumId, type) { return this.http.post(`/demo/condominiums/${condominiumId}/alerts`, { type }).then(r => r.data); }
    resolveAll(condominiumId) { return this.http.post(`/demo/condominiums/${condominiumId}/alerts/resolve-all`).then(r => r.data); }
    advanceMonth(condominiumId) { return this.http.post(`/demo/condominiums/${condominiumId}/advance-month`).then(r => r.data); }
}
