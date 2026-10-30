export const AlertSeverity = Object.freeze({ CRITICAL: 'critical', WARNING: 'warning' });
export const AlertType = Object.freeze({ NIGHT_LEAK: 'night_leak', PUMP_OVER_BASELINE: 'pump_over_baseline', NO_SIGNAL: 'no_signal' });

// Reglas del motor de anomalías del backend → tipos de alerta de la interfaz
const TYPE_FROM_API = {
    NocturnalWaterLeak: AlertType.NIGHT_LEAK,
    PumpElectricalOverload: AlertType.PUMP_OVER_BASELINE,
    GatewayOfflineInBasement: AlertType.NO_SIGNAL,
};

// El backend guarda UTC pero algunas fechas llegan sin zona; sin la "Z" el navegador las leería como hora local
const asUtc = (iso) => (iso && !/[zZ]|[+-]\d{2}:\d{2}$/.test(iso) ? `${iso}Z` : iso);

export class Alert {
    constructor({ id = null, condominiumId = null, deviceId = null, type = '', alertType = '', severity = 'warning', message = '',
                  title = '', description = '', estimatedOvercostSoles = null, createdAt = '', status = 'open' } = {}) {
        Object.assign(this, {
            id, condominiumId, deviceId,
            type: TYPE_FROM_API[alertType] || type,
            severity: String(severity).toLowerCase(),
            message, title, description,
            estimatedOvercostSoles: estimatedOvercostSoles === null ? null : Number(estimatedOvercostSoles),
            createdAt: asUtc(createdAt),
            status: String(status).toLowerCase(),
        });
    }
    get isOpen() { return this.status === 'open'; }
    get isCritical() { return this.severity === AlertSeverity.CRITICAL; }
}
