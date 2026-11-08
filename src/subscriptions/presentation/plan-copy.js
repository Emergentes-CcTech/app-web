import { MAX_PLAN_UNITS } from '../domain/model/plan-limits.js';

// "Gran Torre" y "GranTorre" (nombre del catálogo del backend) apuntan a la misma clave: gran-torre
function planSlug(planName) {
  return String(planName ?? '').trim().replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase().replace(/\s+/g, '-');
}

export function translatePlanDescription(plan, t, te) {
  const key = `subscriptions.planCatalog.${planSlug(plan?.name)}.description`;
  return te(key) ? t(key) : (plan?.description ?? '');
}

// Las características llegan en español desde el catálogo de planes. Se ubican en la lista en español
// (planCatalog.<plan>.features.<i>) y se muestra la misma posición en el idioma activo; un texto que el
// backend haya cambiado se muestra tal cual en lugar de una traducción equivocada.
export function translatePlanFeature(planName, feature, t, te) {
  const base = `subscriptions.planCatalog.${planSlug(planName)}.features`;
  for (let i = 0; te(`${base}.${i}`, 'es'); i++) {
    if (t(`${base}.${i}`, {}, { locale: 'es' }) === feature) return t(`${base}.${i}`);
  }
  return feature;
}

/** "Hasta 20 departamentos", "21 a 80 departamentos" o "Más de 80 departamentos" (último plan, sin tope comercial) */
export function unitsRangeText(plan, t) {
  if (Number(plan?.minUnits) <= 1) return t('subscriptions.unitsUpTo', { max: plan?.maxUnits });
  if (Number(plan?.maxUnits) >= MAX_PLAN_UNITS) return t('subscriptions.unitsFrom', { min: Number(plan?.minUnits) - 1 });
  return t('subscriptions.unitsRange', { min: plan?.minUnits, max: plan?.maxUnits });
}

/** Líneas del kit: "1 Gateway", "2 Caudalímetros", "3 Pinzas CT" */
export function kitLines(kit, t) {
  const n = (v) => Number(v) || 0;
  return [
    { icon: 'pi pi-wifi', text: t('subscriptions.kit.gateways', { n: n(kit?.gateways) }, n(kit?.gateways)) },
    { icon: 'pi pi-filter', text: t('subscriptions.kit.flowMeters', { n: n(kit?.flowMeters) }, n(kit?.flowMeters)) },
    { icon: 'pi pi-bolt', text: t('subscriptions.kit.ctClamps', { n: n(kit?.ctClamps) }, n(kit?.ctClamps)) },
  ];
}

/** "1 Gateway + 1 Caudalímetro + 2 Pinzas CT" */
export const kitSummary = (kit, t) => kitLines(kit, t).map(l => l.text).join(' + ');
