import { test, expect } from '@playwright/test';
import crypto from 'node:crypto';

// Requiere el backend real (no json-server): el aislamiento por administrador lo hace el servidor.
async function createAdministrator(page, suffix) {
  const email = `tenant.${suffix}@example.test`;
  const password = 'Admin-secure-123!';
  const registration = await page.request.post('/api/v1/users', { data: { email, password, role: 'administrator' } });
  expect(registration.status()).toBe(201);
  const login = await page.request.post('/api/v1/sessions', { data: { email, password } });
  expect(login.status()).toBe(201);
  const user = await login.json();
  return { user, headers: { Authorization: `Bearer ${user.token}` } };
}

async function createCondominium(page, admin, name) {
  const response = await page.request.post('/api/v1/condominiums', {
    headers: admin.headers,
    data: { name, address: 'Av. E2E 123', district: 'Lima', totalUnits: 1, waterSupplyCode: '123456', electricitySupplyCode: '654321', administratorId: admin.user.id, status: 'active' },
  });
  expect(response.status()).toBe(201);
  return response.json();
}

test('SECURITY un administrador no ve condominios, unidades ni lecturas de otro administrador', async ({ page }) => {
  const suffix = `${Date.now()}${crypto.randomInt(100, 1000)}`;
  const adminA = await createAdministrator(page, `${suffix}.a`);
  const adminB = await createAdministrator(page, `${suffix}.b`);
  const condoA = await createCondominium(page, adminA, `Tenant A ${suffix}`);
  const condoB = await createCondominium(page, adminB, `Tenant B ${suffix}`);

  const listA = await page.request.get('/api/v1/condominiums', { headers: adminA.headers });
  const idsA = (await listA.json()).map(c => c.id);
  expect(idsA).toContain(condoA.id);
  expect(idsA).not.toContain(condoB.id);

  for (const path of ['units', 'devices', 'readings', 'alerts', 'utility_bills', 'settlements', 'payments', 'audit_events']) {
    const res = await page.request.get(`/api/v1/${path}?condominiumId=${condoB.id}`, { headers: adminA.headers });
    expect([200, 403, 404]).toContain(res.status());
    if (res.status() === 200) expect(await res.json()).toEqual([]);
  }
});
