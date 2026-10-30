/**
 * Planes HaaS por escala del condominio (número de departamentos). Precio en soles por condominio.
 * Cada plan incluye un kit de equipos IoT para las áreas comunes:
 * gateways + caudalímetros (agua) + pinzas CT (energía).
 */
export const PLAN_TIERS = Object.freeze([
  { code: 'boutique',   name: 'Boutique',   minUnits: 1,  maxUnits: 20, monthlyPen: 500,  kit: { gateways: 1, flowMeters: 1, ctClamps: 2 } },
  { code: 'urbano',     name: 'Urbano',     minUnits: 21, maxUnits: 80, monthlyPen: 700,  kit: { gateways: 1, flowMeters: 2, ctClamps: 3 } },
  // Gran Torre no tiene tope comercial ("más de 80"); 500 es el límite técnico que valida el backend al registrar un condominio
  { code: 'gran-torre', name: 'Gran Torre', minUnits: 81, maxUnits: 500, monthlyPen: 1000, kit: { gateways: 2, flowMeters: 3, ctClamps: 5 }, openEnded: true },
]);

/** Máximo de departamentos que cubre el catálogo (Gran Torre) */
export const MAX_PLAN_UNITS = PLAN_TIERS[PLAN_TIERS.length - 1].maxUnits;

export const kitTotal = (kit) => (Number(kit?.gateways) || 0) + (Number(kit?.flowMeters) || 0) + (Number(kit?.ctClamps) || 0);

// "Gran Torre", "gran-torre" y "GranTorre" (nombre del catálogo del backend) resuelven al mismo plan
const slugOf = (plan) => String(plan?.code ?? plan?.name ?? plan ?? '').trim().replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase().replace(/\s+/g, '-');

export const findTier = (plan) => PLAN_TIERS.find(t => t.code === slugOf(plan)) ?? null;

/** Plan que corresponde al número de departamentos; null si supera los 80 del catálogo */
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
