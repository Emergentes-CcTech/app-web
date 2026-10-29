import { Payment } from '../domain/model/payment.entity.js';

export class PaymentAssembler {
    static toEntity(r) { return new Payment(r || {}); }
    static toEntities(list) { return (Array.isArray(list) ? list : []).map(PaymentAssembler.toEntity); }
}
