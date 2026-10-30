// Comprehensive Verification Script for IoBuild Endpoints & Services
// Uses native Node fetch to test real HTTP responses across ports 80, 8081, 8080

const BASE_PROXY_URL = 'http://localhost';
const ALT_PROXY_URL = 'http://localhost:8081';
const BACKEND_URL = 'http://localhost:8080';

async function main() {
  console.log('=== IoBuild End-to-End Service & Route Verification ===\n');
  let failures = 0;

  function assert(condition, message) {
    if (!condition) {
      console.error(`❌ FAIL: ${message}`);
      failures++;
    } else {
      console.log(`✅ PASS: ${message}`);
    }
  }

  // 1. Health & Infrastructure checks
  console.log('--- 1. Infrastructure & Health Endpoints ---');
  try {
    const health = await fetch(`${BASE_PROXY_URL}/health`);
    const healthData = await health.json();
    assert(health.status === 200 && healthData.status === 'ready', `GET ${BASE_PROXY_URL}/health -> 200 OK ready`);

    const proxyRoot = await fetch(`${BASE_PROXY_URL}/`);
    assert(proxyRoot.status === 200, `GET ${BASE_PROXY_URL}/ -> 200 OK`);

    const altProxyRoot = await fetch(`${ALT_PROXY_URL}/`);
    assert(altProxyRoot.status === 200, `GET ${ALT_PROXY_URL}/ -> 200 OK (Port 8081)`);

    const swagger = await fetch(`${BASE_PROXY_URL}/swagger/index.html`);
    assert(swagger.status === 200, `GET ${BASE_PROXY_URL}/swagger/index.html -> 200 OK`);
  } catch (err) {
    assert(false, `Infrastructure check threw: ${err.message}`);
  }

  // 2. PropertyManager Authentication & APIs
  console.log('\n--- 2. PropertyManager Role (mveramendi@iobuild.pe) ---');
  let pmToken = null;
  let pmUserId = null;
  try {
    const loginRes = await fetch(`${BASE_PROXY_URL}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'mveramendi@iobuild.pe', password: 'Admin123!' })
    });
    assert(loginRes.status === 201, `POST /api/v1/sessions (PM Login) -> 201 Created`);
    const loginData = await loginRes.json();
    pmToken = loginData.token;
    pmUserId = loginData.id;
    assert(!!pmToken, `JWT token received for PropertyManager`);
    assert(loginData.role === 'PropertyManager', `PM role claim verified: ${loginData.role}`);
  } catch (err) {
    assert(false, `PM Login threw: ${err.message}`);
  }

  const pmHeaders = { 'Authorization': `Bearer ${pmToken}` };

  try {
    // Subscriptions
    const subRes = await fetch(`${BASE_PROXY_URL}/api/v1/subscriptions`, { headers: pmHeaders });
    assert(subRes.status === 200, `GET /api/v1/subscriptions -> 200 OK`);
    const subs = await subRes.json();
    assert(subs.length >= 1, `PM holds ${subs.length} active subscription(s)`);
    assert(subs[0].condominiumId === 1 && subs[0].status.toLowerCase() === 'active', `Subscription details: condominiumId=${subs[0].condominiumId}, status=${subs[0].status}`);

    // Condominiums
    const condoRes = await fetch(`${BASE_PROXY_URL}/api/v1/condominiums?administratorId=${pmUserId}`, { headers: pmHeaders });
    assert(condoRes.status === 200, `GET /api/v1/condominiums -> 200 OK`);
    const condos = await condoRes.json();
    assert(condos.length >= 1 && condos[0].name === 'Residencial Los Robles', `Condominium: ${condos[0]?.name}`);

    // Project Details
    const projRes = await fetch(`${BASE_PROXY_URL}/api/v1/projects/1`, { headers: pmHeaders });
    assert(projRes.status === 200, `GET /api/v1/projects/1 -> 200 OK`);
    const proj = await projRes.json();
    assert(proj.totalUnits === 25, `Project total units: ${proj.totalUnits}`);

    // Units
    const unitsRes = await fetch(`${BASE_PROXY_URL}/api/v1/projects/1/units`, { headers: pmHeaders });
    assert(unitsRes.status === 200, `GET /api/v1/projects/1/units -> 200 OK`);
    const units = await unitsRes.json();
    assert(units.length === 25, `All 25 units returned for PropertyManager`);

    // Alerts
    const alertsRes = await fetch(`${BASE_PROXY_URL}/api/v1/alerts`, { headers: pmHeaders });
    assert(alertsRes.status === 200, `GET /api/v1/alerts -> 200 OK`);
    const alerts = await alertsRes.json();
    assert(alerts.length >= 1, `${alerts.length} alerts returned`);

    // Utility Bills
    const billsRes = await fetch(`${BASE_PROXY_URL}/api/v1/utility_bills`, { headers: pmHeaders });
    assert(billsRes.status === 200, `GET /api/v1/utility_bills -> 200 OK`);
    const bills = await billsRes.json();
    assert(bills.length >= 1, `${bills.length} utility bill(s) returned`);

    // Settlements
    const settlementsRes = await fetch(`${BASE_PROXY_URL}/api/v1/settlements`, { headers: pmHeaders });
    assert(settlementsRes.status === 200, `GET /api/v1/settlements -> 200 OK`);
    const settlements = await settlementsRes.json();
    assert(settlements.length >= 1, `${settlements.length} settlement(s) returned`);

    // Delinquency / Payments
    const payRes = await fetch(`${BASE_PROXY_URL}/api/v1/payments?status=pending`, { headers: pmHeaders });
    assert(payRes.status === 200, `GET /api/v1/payments?status=pending -> 200 OK`);

    // Audit Events
    const auditRes = await fetch(`${BASE_PROXY_URL}/api/v1/audit_events?condominiumId=1`, { headers: pmHeaders });
    assert(auditRes.status === 200, `GET /api/v1/audit_events -> 200 OK`);

    // Security Sessions
    const sessRes = await fetch(`${BASE_PROXY_URL}/api/v1/sessions?userId=${pmUserId}`, { headers: pmHeaders });
    assert(sessRes.status === 200, `GET /api/v1/sessions?userId=${pmUserId} -> 200 OK`);

    // Devices & Readings
    const devRes = await fetch(`${BASE_PROXY_URL}/api/v1/devices?condominiumId=1`, { headers: pmHeaders });
    assert(devRes.status === 200, `GET /api/v1/devices -> 200 OK`);
    const readRes = await fetch(`${BASE_PROXY_URL}/api/v1/readings?condominiumId=1`, { headers: pmHeaders });
    assert(readRes.status === 200, `GET /api/v1/readings -> 200 OK`);
  } catch (err) {
    assert(false, `PM API calls threw: ${err.message}`);
  }

  // 3. Resident Authentication & Data Isolation
  console.log('\n--- 3. Resident Role (cflores@gmail.com) ---');
  let resToken = null;
  let resUserId = null;
  try {
    const loginRes = await fetch(`${BASE_PROXY_URL}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'cflores@gmail.com', password: 'Resident123!' })
    });
    assert(loginRes.status === 201, `POST /api/v1/sessions (Resident Login) -> 201 Created`);
    const loginData = await loginRes.json();
    resToken = loginData.token;
    resUserId = loginData.id;
    assert(!!resToken, `JWT token received for Resident`);
    assert(loginData.role === 'Resident', `Resident role claim verified: ${loginData.role}`);
  } catch (err) {
    assert(false, `Resident Login threw: ${err.message}`);
  }

  const resHeaders = { 'Authorization': `Bearer ${resToken}` };

  try {
    // Project details (resident access permitted)
    const projRes = await fetch(`${BASE_PROXY_URL}/api/v1/projects/1`, { headers: resHeaders });
    assert(projRes.status === 200, `GET /api/v1/projects/1 (Resident read project) -> 200 OK`);

    // Resident unit query filtering
    const unitsRes = await fetch(`${BASE_PROXY_URL}/api/v1/projects/1/units?residentUserId=${resUserId}`, { headers: resHeaders });
    assert(unitsRes.status === 200, `GET /api/v1/projects/1/units?residentUserId=${resUserId} -> 200 OK`);
    const units = await unitsRes.json();
    assert(units.length === 1, `Resident sees exactly 1 unit (no data leakage)`);
    assert(units[0].unitNumber === '402', `Assigned Unit Number: ${units[0]?.unitNumber}`);
    assert(Math.abs(units[0].aliquotPercentage - 3.85) < 0.001, `Aliquot Percentage: ${units[0]?.aliquotPercentage}% (Expected 3.8500%)`);
    assert(units[0].residentFullName === 'Carla Flores', `Resident name: ${units[0]?.residentFullName}`);

    // Payments for unit 17 (Unit 402)
    const payRes = await fetch(`${BASE_PROXY_URL}/api/v1/payments?unitId[]=${units[0].id}`, { headers: resHeaders });
    assert(payRes.status === 200, `GET /api/v1/payments?unitId[]=17 -> 200 OK`);
    const payments = await payRes.json();
    assert(payments.length >= 1, `${payments.length} payment statement record(s) returned`);

    // Security Sessions
    const sessRes = await fetch(`${BASE_PROXY_URL}/api/v1/sessions?userId=${resUserId}`, { headers: resHeaders });
    assert(sessRes.status === 200, `GET /api/v1/sessions?userId=${resUserId} -> 200 OK`);
  } catch (err) {
    assert(false, `Resident API calls threw: ${err.message}`);
  }

  console.log(`\n======================================================`);
  if (failures === 0) {
    console.log(`🎉 ALL VERIFICATION CHECKS PASSED WITH ZERO FAILURES!`);
    process.exit(0);
  } else {
    console.error(`💥 ${failures} CHECK(S) FAILED!`);
    process.exit(1);
  }
}

main().catch(err => {
  console.error('Fatal execution error:', err);
  process.exit(1);
});
