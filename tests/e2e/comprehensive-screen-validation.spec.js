import { test, expect } from '@playwright/test';

test.describe('IoBuild Comprehensive Screen & Route Validation', () => {

  test('PropertyManager: authenticate and traverse all views and subroutes without errors', async ({ page }) => {
    const consoleErrors = [];
    const networkFailures = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });

    page.on('response', (response) => {
      const url = response.url();
      if (url.includes('/api/v1/') && response.status() >= 400) {
        networkFailures.push(`${response.status()} ${response.request().method()} ${url}`);
      }
    });

    // 1. Authenticate as PropertyManager
    await page.goto('/iam/login');
    await page.waitForLoadState('networkidle');

    await page.locator('input[type="email"], input[placeholder*="correo"]').fill('mveramendi@iobuild.pe');
    await page.locator('input[type="password"]').fill('Admin123!');
    await page.locator('button:has-text("Iniciar Sesión")').click();

    await page.waitForURL('**/telemetry/dashboard', { timeout: 15000 });
    await expect(page.locator('main')).not.toBeEmpty();
    await expect(page.locator('text=Residencial Los Robles')).toBeVisible({ timeout: 10000 });

    const pmRoutes = [
      { path: '/telemetry/dashboard', expectedText: 'Panel del condominio' },
      { path: '/condominiums', expectedText: 'Residencial Los Robles' },
      { path: '/condominiums/1', expectedText: 'Padrón de unidades' },
      { path: '/condominiums/new', expectedText: 'Nuevo condominio' },
      { path: '/condominiums/1/edit', expectedText: 'Editar condominio' },
      { path: '/residents', expectedText: 'Residentes' },
      { path: '/residents/2', expectedText: 'Datos personales' },
      { path: '/devices/device-management', expectedText: 'Dispositivos IoT' },
      { path: '/telemetry/monitoring', expectedText: 'Monitoreo' },
      { path: '/telemetry/alerts', expectedText: 'Alertas' },
      { path: '/billing/bills', expectedText: 'Recibos matriz' },
      { path: '/billing/settlements', expectedText: 'Prorrateo y liquidación' },
      { path: '/billing/settlements/1', expectedText: 'Liquidación' },
      { path: '/payments/delinquency', expectedText: 'Morosidad' },
      { path: '/audit', expectedText: 'Historial de auditoría' },
      { path: '/subscriptions/my-subscription', expectedText: 'Mi Suscripción' },
      { path: '/profiles/profile', expectedText: 'Información de la cuenta' },
      // DDD route aliases
      { path: '/cadastre/condominiums', expectedText: 'Residencial Los Robles' },
      { path: '/cadastre/condominiums/1/units', expectedText: 'Padrón de unidades' },
      { path: '/cadastre/condominiums/1/common-areas', expectedText: 'Áreas comunes' },
      { path: '/cadastre/condominiums/1/equipment', expectedText: 'Dispositivos IoT' },
      { path: '/cadastre/condominiums/1/residents', expectedText: 'Residentes' },
      { path: '/subscriptions/plans', expectedText: 'Mi Suscripción' },
      { path: '/analytics/alerts', expectedText: 'Alertas' },
      { path: '/proration/utility-bills', expectedText: 'Recibos matriz' },
      { path: '/proration/settlements', expectedText: 'Prorrateo y liquidación' },
      { path: '/proration/delinquency', expectedText: 'Morosidad' },
    ];

    for (const r of pmRoutes) {
      await page.goto(r.path);
      await page.waitForLoadState('networkidle');

      // Verify no blank screen
      const main = page.locator('main');
      await expect(main).toBeVisible();
      await expect(main).not.toBeEmpty();

      // Verify expected text
      await expect(page.locator(`text=${r.expectedText}`).first()).toBeVisible({ timeout: 10000 });
    }

    // Verify zero console errors and zero network 4xx/5xx failures
    expect(consoleErrors).toEqual([]);
    expect(networkFailures).toEqual([]);
  });

  test('Resident: authenticate and traverse all views and subroutes verifying Unit 402 data', async ({ page }) => {
    const consoleErrors = [];
    const networkFailures = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });

    page.on('response', (response) => {
      const url = response.url();
      if (url.includes('/api/v1/') && response.status() >= 400) {
        networkFailures.push(`${response.status()} ${response.request().method()} ${url}`);
      }
    });

    // 1. Authenticate as Resident
    await page.goto('/iam/login');
    await page.waitForLoadState('networkidle');

    await page.locator('input[type="email"], input[placeholder*="correo"]').fill('cflores@gmail.com');
    await page.locator('input[type="password"]').fill('Resident123!');
    await page.locator('button:has-text("Iniciar Sesión")').click();

    await page.waitForURL('**/telemetry/dashboard', { timeout: 15000 });
    await expect(page.locator('main')).not.toBeEmpty();
    await expect(page.locator('text=Mi consumo')).toBeVisible({ timeout: 10000 });

    const residentRoutes = [
      { path: '/telemetry/dashboard', expectedText: 'Mi consumo' },
      { path: '/telemetry/analytics', expectedText: 'Departamento 402' },
      { path: '/payments/account-statement', expectedText: 'Estado de cuenta' },
      { path: '/audit', expectedText: '3.85' },
      { path: '/profiles/profile', expectedText: 'cflores@gmail.com' },
      // DDD route aliases
      { path: '/proration/my-statement', expectedText: 'Estado de cuenta' },
      { path: '/proration/payment-history', expectedText: 'Estado de cuenta' },
    ];

    for (const r of residentRoutes) {
      await page.goto(r.path);
      await page.waitForLoadState('networkidle');

      // Verify no blank screen
      const main = page.locator('main');
      await expect(main).toBeVisible();
      await expect(main).not.toBeEmpty();

      // Verify expected text
      await expect(page.locator(`text=${r.expectedText}`).first()).toBeVisible({ timeout: 10000 });
    }

    // Verify zero console errors and zero network 4xx/5xx failures
    expect(consoleErrors).toEqual([]);
    expect(networkFailures).toEqual([]);
  });

  test('Direct REST / Fetch API endpoint validation', async ({ request }) => {
    // 1. PM Login
    const pmAuthRes = await request.post('/api/v1/sessions', {
      data: { email: 'mveramendi@iobuild.pe', password: 'Admin123!' }
    });
    expect(pmAuthRes.status()).toBe(201);
    const pmAuth = await pmAuthRes.json();
    const pmHeaders = { Authorization: `Bearer ${pmAuth.token}` };

    // 2. Condominiums & Projects
    const condoRes = await request.get('/api/v1/condominiums?administratorId=1', { headers: pmHeaders });
    expect(condoRes.status()).toBe(200);
    const condos = await condoRes.json();
    expect(condos.length).toBeGreaterThanOrEqual(1);
    expect(condos[0].name).toBe('Residencial Los Robles');

    // 3. Subscriptions
    const subRes = await request.get('/api/v1/subscriptions', { headers: pmHeaders });
    expect(subRes.status()).toBe(200);
    const subs = await subRes.json();
    expect(subs.length).toBeGreaterThanOrEqual(1);
    expect(subs[0].condominiumId).toBe(1);
    expect(subs[0].status.toLowerCase()).toBe('active');

    // 4. Units
    const unitsRes = await request.get('/api/v1/projects/1/units', { headers: pmHeaders });
    expect(unitsRes.status()).toBe(200);
    const units = await unitsRes.json();
    expect(units.length).toBe(25);

    // 5. Alerts
    const alertsRes = await request.get('/api/v1/alerts', { headers: pmHeaders });
    expect(alertsRes.status()).toBe(200);
    const alerts = await alertsRes.json();
    expect(alerts.length).toBeGreaterThanOrEqual(1);

    // 6. Bills & Settlements
    const billsRes = await request.get('/api/v1/utility_bills', { headers: pmHeaders });
    expect(billsRes.status()).toBe(200);
    const setRes = await request.get('/api/v1/settlements', { headers: pmHeaders });
    expect(setRes.status()).toBe(200);

    // 7. Sessions
    const sessRes = await request.get('/api/v1/sessions?userId=1', { headers: pmHeaders });
    expect(sessRes.status()).toBe(200);

    // 8. Resident Login & Unit 402 isolation
    const resAuthRes = await request.post('/api/v1/sessions', {
      data: { email: 'cflores@gmail.com', password: 'Resident123!' }
    });
    expect(resAuthRes.status()).toBe(201);
    const resAuth = await resAuthRes.json();
    const resHeaders = { Authorization: `Bearer ${resAuth.token}` };

    const resUnitsRes = await request.get('/api/v1/projects/1/units?residentUserId=2', { headers: resHeaders });
    expect(resUnitsRes.status()).toBe(200);
    const resUnits = await resUnitsRes.json();
    expect(resUnits.length).toBe(1);
    expect(resUnits[0].unitNumber).toBe('402');
    expect(resUnits[0].aliquotPercentage).toBeCloseTo(3.85, 2);

    // Resident can access own project details
    const resProjRes = await request.get('/api/v1/projects/1', { headers: resHeaders });
    expect(resProjRes.status()).toBe(200);
  });
});
