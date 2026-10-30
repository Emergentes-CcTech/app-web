import { chromium } from '@playwright/test';

const BASE_URL = 'http://localhost';

async function main() {
  console.log('=== ADVERSARIAL RESIDENT ISOLATION & BOUNDARY TEST ===\n');

  const browser = await chromium.launch({
    executablePath: 'C:/Users/axelm/AppData/Local/Google/Chrome/Application/chrome.exe',
    headless: true
  });
  const context = await browser.newContext();
  const page = await context.newPage();

  let routerBypassErrors = 0;
  let domLeakErrors = 0;

  // 1. Authenticate as Resident
  console.log('1. Authenticating as Resident (cflores@gmail.com)...');
  await page.goto(`${BASE_URL}/iam/login`);
  await page.waitForLoadState('networkidle');

  await page.locator('input[placeholder*="correo"]').fill('cflores@gmail.com');
  await page.locator('input[type="password"]').fill('Resident123!');
  await page.locator('button:has-text("Iniciar Sesión")').click();
  await page.waitForURL('**/telemetry/dashboard', { timeout: 15000 });

  console.log('✅ Resident authenticated. URL:', page.url());

  // 2. Test accessing each PropertyManager route via direct navigation
  const pmRoutes = [
    '/condominiums',
    '/condominiums/1',
    '/condominiums/new',
    '/condominiums/1/edit',
    '/residents',
    '/residents/2',
    '/devices/device-management',
    '/telemetry/monitoring',
    '/telemetry/alerts',
    '/billing/bills',
    '/billing/settlements',
    '/billing/settlements/1',
    '/payments/delinquency',
    '/subscriptions/my-subscription',
    '/cadastre/condominiums',
    '/cadastre/condominiums/1/units',
    '/cadastre/condominiums/1/common-areas',
    '/cadastre/condominiums/1/equipment',
    '/cadastre/condominiums/1/residents',
    '/subscriptions/plans',
    '/analytics/alerts',
    '/proration/utility-bills',
    '/proration/settlements',
    '/proration/delinquency'
  ];

  console.log(`\n2. Testing direct navigation to ${pmRoutes.length} PropertyManager routes as Resident:`);
  for (const r of pmRoutes) {
    await page.goto(`${BASE_URL}${r}`);
    await page.waitForLoadState('networkidle');
    const currentUrl = page.url();

    // The router guard must redirect to /telemetry/dashboard (or /iam/login)
    if (currentUrl.includes('/telemetry/dashboard')) {
      console.log(`  ✅ BLOCKED & REDIRECTED: ${r} -> ${currentUrl}`);
    } else {
      console.error(`  ❌ SECURITY LEAK! Resident reached PM view: ${r} -> Current: ${currentUrl}`);
      routerBypassErrors++;
    }
  }

  // 3. Test Resident legitimate views and verify data boundary
  console.log('\n3. Verifying Resident legitimate views (Data Isolation for Unit 402):');

  // 3.1 Dashboard
  await page.goto(`${BASE_URL}/telemetry/dashboard`);
  await page.waitForLoadState('networkidle');
  const dashText = await page.locator('main').innerText();
  if (dashText.includes('Mi liquidación de octubre de 2026') && dashText.includes('S/ 109.36')) {
    console.log('  ✅ Dashboard displays correct resident quota: S/ 109.36');
  } else {
    console.error('  ❌ Dashboard data mismatch:', dashText.slice(0, 100));
    domLeakErrors++;
  }

  // Check no PM elements on dashboard
  if (dashText.includes('Mis condominios') || dashText.includes('Total de equipos') || dashText.includes('Plan que corresponde')) {
    console.error('  ❌ PM dashboard components leaked to resident!');
    domLeakErrors++;
  } else {
    console.log('  ✅ No PM components present on resident dashboard.');
  }

  // 3.2 Analytics
  await page.goto(`${BASE_URL}/telemetry/analytics`);
  await page.waitForLoadState('networkidle');
  const analyticsText = await page.locator('main').innerText();
  if (analyticsText.includes('Departamento 402') && analyticsText.includes('Residencial Los Robles')) {
    console.log('  ✅ Analytics displays assigned unit: Departamento 402');
  } else {
    console.error('  ❌ Analytics unit header missing:', analyticsText.slice(0, 100));
    domLeakErrors++;
  }

  // 3.3 Account Statement
  await page.goto(`${BASE_URL}/payments/account-statement`);
  await page.waitForLoadState('networkidle');
  const stmtText = await page.locator('main').innerText();
  if (stmtText.includes('S/ 109.36') && stmtText.includes('octubre de 2026')) {
    console.log('  ✅ Account statement displays resident pending statement: S/ 109.36');
  } else {
    console.error('  ❌ Account statement mismatch:', stmtText.slice(0, 100));
    domLeakErrors++;
  }

  // 3.4 Audit Trail
  await page.goto(`${BASE_URL}/audit`);
  await page.waitForLoadState('networkidle');
  const auditText = await page.locator('main').innerText();
  if (auditText.includes('3.85') || auditText.includes('Unit 402')) {
    console.log('  ✅ Audit view displays unit 402 aliquot / participation: 3.85%');
  } else {
    console.error('  ❌ Audit view missing Unit 402 participation data:', auditText.slice(0, 100));
    domLeakErrors++;
  }

  // 3.5 Profile
  await page.goto(`${BASE_URL}/profiles/profile`);
  await page.waitForLoadState('networkidle');
  const profText = await page.locator('main').first().innerText();
  if (profText.includes('cflores@gmail.com')) {
    console.log('  ✅ Profile displays resident email: cflores@gmail.com');
  } else {
    console.error('  ❌ Profile mismatch:', profText.slice(0, 100));
    domLeakErrors++;
  }

  // 4. Check Navigation Sidebar
  console.log('\n4. Verifying Navigation Sidebar items for Resident:');
  await page.locator('button[aria-label*="navigation"]').first().click();
  await page.waitForTimeout(300);
  const menuText = await page.locator('.sidebar-menu').innerText();
  const forbiddenKeywords = ['Condominios', 'Residentes', 'Dispositivos', 'Alertas', 'Recibos', 'Suscripción'];
  let menuLeak = false;
  for (const kw of forbiddenKeywords) {
    if (menuText.includes(kw)) {
      console.error(`  ❌ Sidebar leak: found PM item "${kw}" in resident menu!`);
      menuLeak = true;
      domLeakErrors++;
    }
  }
  if (!menuLeak) {
    console.log('  ✅ Sidebar strictly contains only resident items (0 PM leaks).');
  }

  await browser.close();

  console.log('\n=== SUMMARY ===');
  console.log(`Router Bypass Errors: ${routerBypassErrors}`);
  console.log(`DOM / Data Leak Errors: ${domLeakErrors}`);

  if (routerBypassErrors === 0 && domLeakErrors === 0) {
    console.log('🎉 ALL RESIDENT BOUNDARY AND ISOLATION TESTS PASSED!');
    process.exit(0);
  } else {
    console.error('💥 TESTS FAILED!');
    process.exit(1);
  }
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
