/**
 * Planes HaaS por escala del condominio (número de departamentos). Precio mensual en soles sin IGV, por condominio.
 * Cada plan incluye un kit de equipos IoT no invasivos para las áreas comunes:
 * gateway 4G + puntos de medición de agua + puntos de medición de energía (un tablero trifásico cada uno).
 */
export const PLAN_TIERS = Object.freeze([
  { code: 'esencial',   name: 'Esencial',   minUnits: 1,  maxUnits: 40,  monthlyPen: 350, kit: { gateways: 1, flowMeters: 1, ctClamps: 1 } },
  { code: 'urbano',     name: 'Urbano',     minUnits: 41, maxUnits: 80,  monthlyPen: 420, kit: { gateways: 1, flowMeters: 2, ctClamps: 2 } },
  // Gran Torre no tiene tope comercial ("más de 80"); 500 es el límite técnico que valida el backend al registrar un condominio
  { code: 'gran-torre', name: 'Gran Torre', minUnits: 81, maxUnits: 500, monthlyPen: 650, kit: { gateways: 1, flowMeters: 3, ctClamps: 3 }, openEnded: true },
]);

/** La junta de propietarios paga el precio más IGV (no lo recupera como crédito fiscal) */
export const IGV_RATE = 0.18;
export const withIgv = (pen) => Math.round((Number(pen) || 0) * (1 + IGV_RATE) * 100) / 100;

/** Máximo de departamentos que cubre el catálogo (Gran Torre) */
export const MAX_PLAN_UNITS = PLAN_TIERS[PLAN_TIERS.length - 1].maxUnits;

export const kitTotal = (kit) => (Number(kit?.gateways) || 0) + (Number(kit?.flowMeters) || 0) + (Number(kit?.ctClamps) || 0);

// "Gran Torre", "gran-torre" y "GranTorre" (nombre del catálogo del backend) resuelven al mismo plan
const slugOf = (plan) => String(plan?.code ?? plan?.name ?? plan ?? '').trim().replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase().replace(/\s+/g, '-');

export const findTier = (plan) => PLAN_TIERS.find(t => t.code === slugOf(plan)) ?? null;

/** Plan que corresponde al número de departamentos; null si supera el límite técnico del catálogo */
export function recommendedTier(totalUnits) {
  const units = Number(totalUnits) || 0;
  if (units > MAX_PLAN_UNITS) return null;
  return PLAN_TIERS.find(t => units >= t.minUnits && units <= t.maxUnits) ?? PLAN_TIERS[0];
}

export function getPlanLimits(plan) {
  const tier = findTier(plan) ?? PLAN_TIERS[0];
  const maxUnits = Number(plan?.maxUnits) > 0 && Number.isFinite(Number(plan?.maxUnits)) ? Number(plan.maxUnits) : tier.maxUnits;
  return { name: tier.name, maxUnits, label: String(maxUnits), isUnlimited: false };
}
