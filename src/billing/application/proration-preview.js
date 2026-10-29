import { isParticipationBalanced } from '../../shared/presentation/validators.js';

/**
 * Vista previa en cliente. El monto oficial lo calcula el backend; esto sirve para mostrar el
 * resultado antes de emitir y para el mock. Reparte con método de mayor resto para que la suma
 * sea exactamente el total, sin céntimos perdidos.
 */
export function previewProration(totalAmount, units) {
    if (!isParticipationBalanced(units)) throw new Error('La suma de participaciones debe ser 100 %');
    const totalCents = Math.round(totalAmount * 100);
    const rows = units.map(u => {
        const exact = (totalCents * u.participationPct) / 100;
        const base = Math.floor(exact + 1e-9);
        return { unitId: u.id, participationPct: u.participationPct, cents: base, rest: exact - base };
    });
    let missing = totalCents - rows.reduce((a, r) => a + r.cents, 0);
    [...rows].sort((a, b) => b.rest - a.rest).forEach(r => { if (missing > 0) { r.cents += 1; missing -= 1; } });
    return rows.map(r => ({ unitId: r.unitId, participationPct: r.participationPct, amount: r.cents / 100 }));
}
