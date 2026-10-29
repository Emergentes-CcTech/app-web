export const AlertSeverity = Object.freeze({ CRITICAL: 'critical', WARNING: 'warning' });
export const AlertType = Object.freeze({ NIGHT_LEAK: 'night_leak', PUMP_OVER_BASELINE: 'pump_over_baseline', NO_SIGNAL: 'no_signal' });

export class Alert {
    constructor({ id = null, condominiumId = null, deviceId = null, type = '', severity = 'warning', message = '',
                  createdAt = '', status = 'open' } = {}) {
        Object.assign(this, { id, condominiumId, deviceId, type, severity, message, createdAt, status });
    }
    get isOpen() { return this.status === 'open'; }
    get isCritical() { return this.severity === AlertSeverity.CRITICAL; }
}
