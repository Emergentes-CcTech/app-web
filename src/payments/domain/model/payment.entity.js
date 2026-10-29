export const PaymentStatus = Object.freeze({ PENDING: 'pending', PAID: 'paid', OVERDUE: 'overdue' });

export class Payment {
    constructor({ id = null, settlementLineId = null, unitId = null, condominiumId = null, period = '', amount = 0,
                  dueDate = '', paidAt = null, status = 'pending', method = null, reference = '' } = {}) {
        Object.assign(this, { id, settlementLineId, unitId, condominiumId, period, amount: Number(amount),
            dueDate, paidAt, status, method, reference });
    }

    // El backend es la fuente de verdad; esto cubre el mock y la hora del día de vencimiento.
    get effectiveStatus() {
        if (this.paidAt) return PaymentStatus.PAID;
        return new Date(this.dueDate) < new Date() ? PaymentStatus.OVERDUE : PaymentStatus.PENDING;
    }

    get daysOverdue() {
        if (this.effectiveStatus !== PaymentStatus.OVERDUE) return 0;
        return Math.floor((Date.now() - new Date(this.dueDate)) / 86_400_000);
    }
}
