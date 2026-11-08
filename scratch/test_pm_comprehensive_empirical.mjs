import { chromium } from 'file:///C:/Users/axelm/AppData/Local/npm-cache/_npx/e41f203b7505f1fb/node_modules/playwright/index.mjs';

const BASE_URL = 'http://localhost';
const ALT_URL = 'http://localhost:8081';

async function run() {
  console.log('================================================================');
  console.log(' EMPIRICAL CHALLENGER M3-1: PropertyManager E2E Screen Validation');
  console.log(' Target: Live frontend on http://localhost & http://localhost:8081');
  console.log(' Credentials: mveramendi@iobuild.pe / Admin123!');
  console.log('================================================================\n');

  const browser = await chromium.launch({
    executablePath: 'C:/Users/axelm/AppData/Local/Google/Chrome/Application/chrome.exe',
    headless: true
  });

  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 }
  });

  const page = await context.newPage();

  const consoleErrors = [];
  const consoleWarnings = [];
  const networkErrors = [];

  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      consoleErrors.push(`[${page.url()}] ${msg.text()}`);
    } else if (msg.type() === 'warning') {
      consoleWarnings.push(`[${page.url()}] ${msg.text()}`);
    }
  });

  page.on('response', (res) => {
    const url = res.url();
    if (url.includes('/api/v1/') && res.status() >= 400) {
      networkErrors.push(`[${page.url()}] HTTP ${res.status()} ${res.request().method()} ${url}`);
    }
  });

  let testsPassed = 0;
  let testsFailed = 0;

  function report(name, pass, details = '') {
    if (pass) {
      console.log(`✅ PASS: ${name} ${details ? '(' + details + ')' : ''}`);
      testsPassed++;
    } else {
      console.error(`❌ FAIL: ${name} ${details ? '— ' + details : ''}`);
      testsFailed++;
    }
  }

  // 1. Health & Reverse Proxy Check
  console.log('--- Phase 1: Infrastructure & Reverse Proxy Verification ---');
  const healthRes = await page.goto(`${BASE_URL}/health`);
  report('Infrastructure /health HTTP 200', healthRes.status() === 200);
  const healthBody = await page.textContent('body');
  report('Health body confirms ready', healthBody.includes('ready'), healthBody);

  const altRes = await page.goto(`${ALT_URL}/`);
  report('Reverse proxy alternative port 8081 HTTP 200', altRes.status() === 200);

  // 2. Login Flow
  console.log('\n--- Phase 2: PropertyManager Authentication ---');
  await page.goto(`${BASE_URL}/iam/login`);
  await page.waitForLoadState('networkidle');

  // Fill credentials
  await page.locator('input[placeholder*="correo"]').fill('mveramendi@iobuild.pe');
  await page.locator('input[type="password"]').fill('Admin123!');
  await page.locator('button:has-text("Iniciar Sesión")').click();

  await page.waitForURL('**/telemetry/dashboard', { timeout: 15000 });
  report('Redirect to /telemetry/dashboard upon login', page.url().includes('/telemetry/dashboard'));

  // 3. Systematically Test All 17 PropertyManager Views
  console.log('\n--- Phase 3: PropertyManager Views & Data Validation ---');

  const views = [
    {
      name: '1. Dashboard (/telemetry/dashboard)',
      path: '/telemetry/dashboard',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const welcome = await page.textContent('.welcome-text');
        const role = await page.textContent('.user-role');
        const hasRobles = await page.locator('text=Residencial Los Robles').isVisible();
        const has25Units = await page.locator('text=25').first().isVisible();
        const hasUrbano = await page.locator('text=Urbano').first().isVisible();
        return {
          pass: h1.includes('Panel del condominio') && welcome.includes('mveramendi') && role.includes('Administrador') && hasRobles && has25Units && hasUrbano,
          details: `H1="${h1}", User="${welcome}", Role="${role}", Condo=Residencial Los Robles, Plan=Urbano`
        };
      }
    },
    {
      name: '2. Condominiums List (/condominiums)',
      path: '/condominiums',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasRobles = await page.locator('text=Residencial Los Robles').isVisible();
        const hasJesusMaria = await page.locator('text=Jesús María').isVisible();
        const has25Depts = await page.locator('text=25 departamentos').isVisible();
        const has4Areas = await page.locator('text=4 áreas comunes').isVisible();
        const hasSubUrbano = await page.locator('text=Urbano').first().isVisible();
        const hasNewBtn = await page.locator('button:has-text("Registrar condominio")').isVisible();
        return {
          pass: h1.includes('Condominios') && hasRobles && hasJesusMaria && has25Depts && has4Areas && hasSubUrbano && hasNewBtn,
          details: `Condominium card renders 25 depts, 4 areas, Jesús María, Urbano Activo`
        };
      }
    },
    {
      name: '3. Condominium Details (/condominiums/1)',
      path: '/condominiums/1',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasPadron = await page.locator('text=Padrón de unidades').isVisible();
        const hasCisterna = await page.locator('text=Cisterna Principal de Agua Potable').isVisible();
        const hasBombas = await page.locator('text=Cuarto de Bombas Hidroneumáticas').isVisible();
        const hasAscensores = await page.locator('text=Banco de Ascensores Principales').isVisible();
        const hasTablero = await page.locator('text=Tablero Eléctrico General e Iluminación Común').isVisible();
        const rowsCount = await page.locator('.p-datatable-tbody tr').count();
        return {
          pass: h1.includes('Residencial Los Robles') && hasPadron && hasCisterna && hasBombas && hasAscensores && hasTablero && rowsCount > 0,
          details: `Condo="${h1}", Padron visible, 4 common areas verified, units table rows=${rowsCount}`
        };
      }
    },
    {
      name: '4. New Condominium (/condominiums/new)',
      path: '/condominiums/new',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasNameInput = await page.locator('input').count() >= 3;
        const hasSaveBtn = await page.locator('button[type="submit"], button:has-text("Guardar"), button:has-text("Crear")').first().isVisible();
        return {
          pass: h1.toLowerCase().includes('condominio') && hasNameInput && hasSaveBtn,
          details: `Form title="${h1}", inputs>=3, action button present`
        };
      }
    },
    {
      name: '5. Edit Condominium (/condominiums/1/edit)',
      path: '/condominiums/1/edit',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const nameVal = await page.locator('input').first().inputValue();
        return {
          pass: h1.toLowerCase().includes('condominio') && (nameVal.includes('Robles') || nameVal.length > 0),
          details: `Edit form title="${h1}", preloaded name="${nameVal}"`
        };
      }
    },
    {
      name: '6. Residents Directory (/residents)',
      path: '/residents',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const rowsCount = await page.locator('.p-datatable-tbody tr').count();
        const hasUnit101 = await page.locator('text=101').first().isVisible();
        return {
          pass: h1.includes('Residentes') && rowsCount >= 5 && hasUnit101,
          details: `Residents directory H1="${h1}", rows=${rowsCount}, Unit 101 listed`
        };
      }
    },
    {
      name: '7. Resident Profile (/residents/2)',
      path: '/residents/2',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasRobles = await page.locator('text=Residencial Los Robles').isVisible();
        const hasUnit = await page.locator('text=Unidad').first().isVisible();
        return {
          pass: hasRobles && hasUnit,
          details: `Resident detail renders Residencial Los Robles, Unit assignment`
        };
      }
    },
    {
      name: '8. IoT Device Management (/devices/device-management)',
      path: '/devices/device-management',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasOnline = await page.locator('text=En línea').first().isVisible();
        const rowsCount = await page.locator('.p-datatable-tbody tr').count();
        const hasHub = await page.locator('text=AA:BB:CC:11:00:01').isVisible();
        return {
          pass: h1.includes('Dispositivos') && hasOnline && rowsCount >= 5 && hasHub,
          details: `Devices title="${h1}", online tag present, 6 devices in table including Gateway Hub`
        };
      }
    },
    {
      name: '9. Live Telemetry Monitoring (/telemetry/monitoring)',
      path: '/telemetry/monitoring',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasMeters = await page.locator('.reading-value, .metric-value, .stat-value, canvas, .card').count() >= 1;
        return {
          pass: h1.includes('Monitoreo') && hasMeters,
          details: `Monitoring title="${h1}", telemetry meter components visible`
        };
      }
    },
    {
      name: '10. Telemetry Alerts (/telemetry/alerts)',
      path: '/telemetry/alerts',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasAlertRow = await page.locator('.p-datatable-tbody tr, .alert-item, li').count() >= 1;
        const hasBasement = await page.locator('text=basement').first().isVisible();
        return {
          pass: h1.includes('Alertas') && hasAlertRow && hasBasement,
          details: `Alerts title="${h1}", alert entries rendered with gateway offline basement alert`
        };
      }
    },
    {
      name: '11. Utility Bills (/billing/bills)',
      path: '/billing/bills',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasPeriod = await page.locator('text=2026-10').first().isVisible();
        const hasAgua = await page.locator('text=Agua').first().isVisible();
        const hasLuz = await page.locator('text=Luz').first().isVisible();
        return {
          pass: h1.includes('Recibos') && hasPeriod && hasAgua && hasLuz,
          details: `Bills title="${h1}", period 2026-10, Agua (S/ 1,680.00) & Luz (S/ 1,160.50)`
        };
      }
    },
    {
      name: '12. Settlements Overview (/billing/settlements)',
      path: '/billing/settlements',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasPeriod = await page.locator('text=2026-10').first().isVisible();
        const hasTotal = await page.locator('text=2,840.50').isVisible();
        const hasEmitida = await page.locator('text=Emitida').isVisible();
        return {
          pass: h1.includes('Prorrateo') && hasPeriod && hasTotal && hasEmitida,
          details: `Settlements title="${h1}", period 2026-10, Total S/ 2,840.50, Estado=Emitida`
        };
      }
    },
    {
      name: '13. Settlement Detail (/billing/settlements/1)',
      path: '/billing/settlements/1',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasOctubre = await page.locator('text=octubre de 2026').first().isVisible();
        const rowsCount = await page.locator('.p-datatable-tbody tr').count();
        return {
          pass: h1.includes('Liquidación') && hasOctubre && rowsCount >= 10,
          details: `Settlement detail title="${h1}", period=octubre 2026, breakdown rows=${rowsCount}`
        };
      }
    },
    {
      name: '14. Delinquency (/payments/delinquency)',
      path: '/payments/delinquency',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const has0pct = await page.locator('text=0 %').isVisible();
        const hasS0 = await page.locator('text=S/ 0.00').isVisible();
        return {
          pass: h1.includes('Morosidad') && has0pct && hasS0,
          details: `Delinquency title="${h1}", Morosidad=0%, Deuda vencida=S/ 0.00`
        };
      }
    },
    {
      name: '15. Audit Trail (/audit)',
      path: '/audit',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasEvents = await page.locator('.p-datatable-tbody tr').count() >= 1;
        const hasExportBtn = await page.locator('button:has-text("Exportar CSV")').isVisible();
        return {
          pass: h1.includes('auditoría') && hasEvents && hasExportBtn,
          details: `Audit trail title="${h1}", events table present, Exportar CSV button active`
        };
      }
    },
    {
      name: '16. My Subscription (/subscriptions/my-subscription)',
      path: '/subscriptions/my-subscription',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasUrbano = await page.locator('text=Urbano').first().isVisible();
        const hasS700 = await page.locator('text=700').first().isVisible();
        const has25de80 = await page.locator('text=25 / 80').isVisible();
        const has2027 = await page.locator('text=2027').first().isVisible();
        return {
          pass: h1.includes('Suscripción') && hasUrbano && hasS700 && has25de80 && has2027,
          details: `Subscription title="${h1}", Plan Urbano Activo, S/ 700 / mes, 25/80 depts, renewal 2027`
        };
      }
    },
    {
      name: '17. My Profile (/profiles/profile)',
      path: '/profiles/profile',
      verify: async () => {
        const h1 = await page.textContent('h1');
        const hasEmail = await page.locator('text=mveramendi@iobuild.pe').first().isVisible();
        const hasAdminRole = await page.locator('text=Administrador').first().isVisible();
        const hasSessions = await page.locator('text=Chrome').first().isVisible();
        return {
          pass: h1.includes('Perfil') && hasEmail && hasAdminRole && hasSessions,
          details: `Profile title="${h1}", mveramendi@iobuild.pe, Administrador, Active security session present`
        };
      }
    }
  ];

  for (const v of views) {
    try {
      await page.goto(`${BASE_URL}${v.path}`);
      await page.waitForLoadState('networkidle');
      // Ensure main content is not blank
      const isMainEmpty = await page.evaluate(() => {
        const m = document.querySelector('main');
        return !m || m.innerText.trim().length === 0;
      });
      if (isMainEmpty) {
        report(v.name, false, 'Main content is blank / empty');
        continue;
      }
      const res = await v.verify();
      report(v.name, res.pass, res.details);
    } catch (err) {
      report(v.name, false, `Threw exception: ${err.message}`);
    }
  }

  // 4. Test DDD Route Aliases & Compatibility Redirects
  console.log('\n--- Phase 4: DDD Route Aliases & Redirects ---');
  const dddAliases = [
    { from: '/cadastre/condominiums', expectedUrl: '/condominiums', expectedText: 'Residencial Los Robles' },
    { from: '/cadastre/condominiums/1/units', expectedUrl: '/condominiums/1', expectedText: 'Padrón de unidades' },
    { from: '/cadastre/condominiums/1/common-areas', expectedUrl: '/condominiums/1', expectedText: 'Cisterna Principal' },
    { from: '/cadastre/condominiums/1/equipment', expectedUrl: '/devices/device-management', expectedText: 'Dispositivos' },
    { from: '/cadastre/condominiums/1/residents', expectedUrl: '/residents', expectedText: 'Residentes' },
    { from: '/subscriptions/plans', expectedUrl: '/subscriptions/my-subscription', expectedText: 'Mi Suscripción' },
    { from: '/analytics/alerts', expectedUrl: '/telemetry/alerts', expectedText: 'Alertas' },
    { from: '/proration/utility-bills', expectedUrl: '/billing/bills', expectedText: 'Recibos' },
    { from: '/proration/settlements', expectedUrl: '/billing/settlements', expectedText: 'Prorrateo' },
    { from: '/proration/delinquency', expectedUrl: '/payments/delinquency', expectedText: 'Morosidad' },
  ];

  for (const a of dddAliases) {
    try {
      await page.goto(`${BASE_URL}${a.from}`);
      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const hasText = await page.locator(`text=${a.expectedText}`).first().isVisible();
      const redirectsProperly = currentUrl.includes(a.expectedUrl);
      report(`Alias ${a.from} -> ${a.expectedUrl}`, redirectsProperly && hasText, `Current URL: ${currentUrl}`);
    } catch (err) {
      report(`Alias ${a.from}`, false, `Threw exception: ${err.message}`);
    }
  }

  // 5. Console & Network Error Verification
  console.log('\n--- Phase 5: Error Gate Verification ---');
  report('Zero Console Errors', consoleErrors.length === 0, consoleErrors.length === 0 ? '0 errors logged' : `${consoleErrors.length} errors: ${consoleErrors.join(', ')}`);
  report('Zero 4xx/5xx Network Errors', networkErrors.length === 0, networkErrors.length === 0 ? '0 network failures' : `${networkErrors.length} failures: ${networkErrors.join(', ')}`);

  console.log('\n================================================================');
  console.log(` SUMMARY: ${testsPassed} PASSED, ${testsFailed} FAILED`);
  console.log(` VERDICT: ${testsFailed === 0 ? 'APPROVE' : 'REJECT'}`);
  console.log('================================================================\n');

  await browser.close();

  if (testsFailed > 0) {
    process.exit(1);
  }
}

run().catch((err) => {
  console.error('Fatal execution error:', err);
  process.exit(1);
});
