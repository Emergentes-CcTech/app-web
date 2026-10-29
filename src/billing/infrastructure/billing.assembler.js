import { UtilityBill } from '../domain/model/utility-bill.entity.js';
import { Settlement } from '../domain/model/settlement.entity.js';
import { SettlementLine } from '../domain/model/settlement-line.entity.js';

export class BillingAssembler {
    static toBill(r) { return new UtilityBill(r || {}); }
    static toBills(list) { return (Array.isArray(list) ? list : []).map(BillingAssembler.toBill); }
    static toLine(r) { return new SettlementLine(r || {}); }
    static toSettlement(r) {
        const lines = (r?.settlement_lines || r?.lines || []).map(BillingAssembler.toLine);
        return new Settlement({ ...(r || {}), lines });
    }
    static toSettlements(list) { return (Array.isArray(list) ? list : []).map(BillingAssembler.toSettlement); }
}
