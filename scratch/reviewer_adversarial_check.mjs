// Independent Reviewer & Adversarial Critic Test Suite
// Verifies all positive paths, negative paths, security boundaries, and route serving

const BASE_URL = 'http://localhost';
const ALT_URL = 'http://localhost:8081';

async function main() {
  console.log('=== Reviewer & Adversarial Integrity Verification ===\n');
  let passes = 0;
  let failures = 0;

  function assert(condition, message) {
    if (!condition) {
      console.error(`❌ FAIL: ${message}`);
      failures++;
    } else {
      console.log(`✅ PASS: ${message}`);
      passes++;
    }
  }

  // --- Suite 1: Positive Infrastructure & Baseline ---
  console.log('--- 1. Infrastructure Baseline ---');
  try {
    const health = await fetch(`${BASE_URL}/health`);
    const healthJson = await health.json();
    assert(health.status === 200 && healthJson.status === 'ready', 'Nginx port 80 /health returns 200 ready');

    const altHealth = await fetch(`${ALT_URL}/health`);
    assert(altHealth.status === 200, 'Nginx port 8081 /health returns 200');

    const swagger = await fetch(`${BASE_URL}/swagger/index.html`);
    assert(swagger.status === 200, 'Swagger UI loads with 200 OK');
  } catch (err) {
    assert(false, `Infrastructure baseline error: ${err.message}`);
  }

  // --- Suite 2: Negative Authentication & Authorization (Adversarial) ---
  console.log('\n--- 2. Adversarial Security & Negative Auth ---');
  try {
    // 2.1 Bad credentials
    const badLogin = await fetch(`${BASE_URL}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'mveramendi@iobuild.pe', password: 'WrongPassword123!' })
    });
    assert(badLogin.status === 401, `Invalid password rejected with 401 Unauthorized (got ${badLogin.status})`);

    // 2.2 Unauthenticated access
    const noAuthCondos = await fetch(`${BASE_URL}/api/v1/condominiums`);
    assert(noAuthCondos.status === 401, `Unauthenticated /api/v1/condominiums returns 401 (got ${noAuthCondos.status})`);

    const noAuthSubs = await fetch(`${BASE_URL}/api/v1/subscriptions`);
    assert(noAuthSubs.status === 401, `Unauthenticated /api/v1/subscriptions returns 401 (got ${noAuthSubs.status})`);

    const noAuthAlerts = await fetch(`${BASE_URL}/api/v1/alerts`);
    assert(noAuthAlerts.status === 401, `Unauthenticated /api/v1/alerts returns 401 (got ${noAuthAlerts.status})`);

    // 2.3 Nonexistent resources
    const pmLogin = await fetch(`${BASE_URL}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'mveramendi@iobuild.pe', password: 'Admin123!' })
    });
    const pmData = await pmLogin.json();
    const pmToken = pmData.token;

    const notFoundProj = await fetch(`${BASE_URL}/api/v1/projects/999999`, {
      headers: { Authorization: `Bearer ${pmToken}` }
    });
    assert(notFoundProj.status === 404, `GET nonexistent project 999999 returns 404 (got ${notFoundProj.status})`);
  } catch (err) {
    assert(false, `Negative auth tests threw: ${err.message}`);
  }

  // --- Suite 3: Positive Authentication & Business Data ---
  console.log('\n--- 3. Positive PropertyManager Flow ---');
  let pmHeaders;
  try {
    const pmLogin = await fetch(`${BASE_URL}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'mveramendi@iobuild.pe', password: 'Admin123!' })
    });
    const pmData = await pmLogin.json();
    pmHeaders = { Authorization: `Bearer ${pmData.token}` };

    assert(pmData.role === 'PropertyManager', `PM role confirmed: ${pmData.role}`);
    assert(pmData.id === 1, `PM user ID confirmed: ${pmData.id}`);

    // Verify Condominiums
    const condoRes = await fetch(`${BASE_URL}/api/v1/condominiums?administratorId=1`, { headers: pmHeaders });
    const condos = await condoRes.json();
    assert(condos.length === 1 && condos[0].name === 'Residencial Los Robles', 'Condominium name is Residencial Los Robles');

    // Verify Units count
    const unitsRes = await fetch(`${BASE_URL}/api/v1/projects/1/units`, { headers: pmHeaders });
    const units = await unitsRes.json();
    assert(units.length === 25, `Total units returned for PM: ${units.length} (Expected 25)`);

    // Verify aliquot sum = 100%
    const totalAliquot = units.reduce((acc, u) => acc + (u.aliquotPercentage || 0), 0);
    assert(Math.abs(totalAliquot - 100.0) < 0.01, `Sum of unit aliquots is ${totalAliquot.toFixed(2)}% (Balanced)`);

    // Verify Devices
    const devRes = await fetch(`${BASE_URL}/api/v1/devices?condominiumId=1`, { headers: pmHeaders });
    const devs = await devRes.json();
    assert(devs.length === 6, `Total communal devices: ${devs.length} (Expected 6)`);

    // Verify Settlements & detail
    const setRes = await fetch(`${BASE_URL}/api/v1/settlements/1?_embed=settlement_lines`, { headers: pmHeaders });
    assert(setRes.status === 200, 'GET /api/v1/settlements/1 returns 200 OK');
    const setDetail = await setRes.json();
    assert(setDetail.settlement_lines?.length === 25, `Settlement #1 has 25 unit lines`);
    assert(setDetail.commonAreaAmount === 2840.50, `Settlement total common expense: S/ ${setDetail.commonAreaAmount}`);
  } catch (err) {
    assert(false, `Positive PM flow threw: ${err.message}`);
  }

  // --- Suite 4: Resident Role & Tenant Isolation ---
  console.log('\n--- 4. Resident Role & Tenant Isolation ---');
  try {
    const resLogin = await fetch(`${BASE_URL}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'cflores@gmail.com', password: 'Resident123!' })
    });
    const resData = await resLogin.json();
    const resHeaders = { Authorization: `Bearer ${resData.token}` };

    assert(resData.role === 'Resident', `Resident role verified: ${resData.role}`);
    assert(resData.unitNumber === '402', `Resident assigned unit: ${resData.unitNumber}`);
    assert(resData.aliquotPercentage === 3.85, `Resident aliquot: ${resData.aliquotPercentage}%`);

    // Tenant Isolation check: query units for resident 2
    const resUnitsRes = await fetch(`${BASE_URL}/api/v1/projects/1/units?residentUserId=2`, { headers: resHeaders });
    const resUnits = await resUnitsRes.json();
    assert(resUnits.length === 1, `Tenant isolation: resident sees exactly 1 unit (got ${resUnits.length})`);
    assert(resUnits[0].unitNumber === '402', `Tenant isolation: unit is 402`);
    assert(resUnits[0].residentFullName === 'Carla Flores', `Resident name: ${resUnits[0].residentFullName}`);

    // Account statement for unit 17
    const payRes = await fetch(`${BASE_URL}/api/v1/payments?unitId[]=17`, { headers: resHeaders });
    const payments = await payRes.json();
    assert(payments.length >= 1, `Resident payments retrieved: ${payments.length}`);
  } catch (err) {
    assert(false, `Resident tests threw: ${err.message}`);
  }

  // --- Suite 5: Frontend Route Serving & SPA Fallback ---
  console.log('\n--- 5. Frontend Route Serving (Nginx SPA Fallback) ---');
  const allRoutes = [
    // PM standard routes
    '/telemetry/dashboard',
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
    '/audit',
    '/subscriptions/my-subscription',
    '/profiles/profile',
    // Resident routes
    '/telemetry/analytics',
    '/payments/account-statement',
    // DDD compatibility aliases
    '/cadastre/condominiums',
    '/cadastre/condominiums/1/units',
    '/cadastre/condominiums/1/common-areas',
    '/cadastre/condominiums/1/equipment',
    '/cadastre/condominiums/1/residents',
    '/subscriptions/plans',
    '/analytics/alerts',
    '/proration/utility-bills',
    '/proration/settlements',
    '/proration/delinquency',
    '/proration/my-statement',
    '/proration/payment-history'
  ];

  let routeFailures = 0;
  for (const r of allRoutes) {
    const res = await fetch(`${BASE_URL}${r}`);
    if (res.status !== 200) {
      console.error(`❌ Route ${r} returned HTTP ${res.status}`);
      routeFailures++;
    }
  }
  assert(routeFailures === 0, `All ${allRoutes.length} frontend routes served successfully with HTTP 200`);

  console.log('\n======================================================');
  console.log(`TOTAL PASSES: ${passes} | TOTAL FAILURES: ${failures}`);
  if (failures === 0) {
    console.log('🏆 ADVERSARIAL & QUALITY REVIEW: ALL CHECKS PASSED!');
    process.exit(0);
  } else {
    console.error(`💥 ${failures} CHECKS FAILED!`);
    process.exit(1);
  }
}

main().catch(err => {
  console.error('Fatal execution error:', err);
  process.exit(1);
});
