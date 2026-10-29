export const AuditEventType = Object.freeze({
    READING: 'reading', PRORATION: 'proration', MANUAL_ADJUSTMENT: 'manual_adjustment', PARTICIPATION_CHANGED: 'participation_changed',
});

export class AuditEvent {
    constructor({ id = null, condominiumId = null, type = '', entity = '', actorUserId = null, at = '', previousValue = null, newValue = null } = {}) {
        Object.assign(this, { id, condominiumId, type, entity, actorUserId, at, previousValue, newValue });
    }
}
