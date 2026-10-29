import { describe, it, expect } from 'vitest';
import { ROUTES } from '../../src/shared/infrastructure/paths.js';

describe('rutas canónicas', () => {
  it('no quedan rutas del dominio anterior', () => {
    const joined = Object.values(ROUTES).join(' ');
    // /telemetry/analytics es la sección nueva del dueño; lo prohibido es el módulo viejo /analytics
    expect(joined).not.toMatch(/(^|\s)\/analytics|projects|clients|builder|owner/);
  });
  it('expone las rutas del nuevo dominio', () => {
    expect(ROUTES.CONDOMINIUMS).toBe('/condominiums');
    expect(ROUTES.BILLING_SETTLEMENTS).toBe('/billing/settlements');
    expect(ROUTES.ACCOUNT_STATEMENT).toBe('/payments/account-statement');
    expect(ROUTES.AUDIT).toBe('/audit');
    expect(ROUTES.ANALYTICS).toBe('/telemetry/analytics');
  });
});
