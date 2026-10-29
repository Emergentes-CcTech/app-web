function planSlug(planName) {
  return String(planName ?? '').trim().toLowerCase().replace(/\s+/g, '-');
}

export function translatePlanDescription(plan, t, te) {
  const key = `subscriptions.planCatalog.${planSlug(plan?.name)}.description`;
  return te(key) ? t(key) : (plan?.description ?? '');
}

// Las características llegan en español desde el catálogo de planes; si existe una clave de traducción se usa.
export function translatePlanFeature(planName, feature, t, te) {
  const key = `subscriptions.planCatalog.${planSlug(planName)}.featuresByText.${feature}`;
  return te(key) ? t(key) : feature;
}

/** "Hasta 20 departamentos" o "21 a 50 departamentos" */
export function unitsRangeText(plan, t) {
  return Number(plan?.minUnits) <= 1
    ? t('subscriptions.unitsUpTo', { max: plan?.maxUnits })
    : t('subscriptions.unitsRange', { min: plan?.minUnits, max: plan?.maxUnits });
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
