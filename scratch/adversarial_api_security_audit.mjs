// Adversarial API Security & Tenant Isolation Audit
// Probing API endpoints with Resident token to verify server-side authorization boundaries

const BASE_URL = 'http://localhost';

async function main() {
  console.log('=== ADVERSARIAL API SECURITY & TENANT ISOLATION AUDIT ===\n');

  // 1. Authenticate Resident
  const resLogin = await fetch(`${BASE_URL}/api/v1/sessions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'cflores@gmail.com', password: 'Resident123!' })
  });
  if (resLogin.status !== 201) {
    console.error('Failed to log in as resident:', resLogin.status);
    process.exit(1);
  }
  const resAuth = await resLogin.json();
  const resToken = resAuth.token;
  const resUserId = resAuth.id;
  const headers = {
    'Authorization': `Bearer ${resToken}`,
    'Content-Type': 'application/json'
  };

  console.log(`Resident authenticated: ID=${resUserId}, Role=${resAuth.role}\n`);

  const results = [];

  async function probe(name, method, path, body = null) {
    try {
      const res = await fetch(`${BASE_URL}${path}`, {
        method,
        headers,
        body: body ? JSON.stringify(body) : null
      });
      let data = null;
      try { data = await res.json(); } catch (_) {}
      const summary = {
        name,
        method,
        path,
        status: res.status,
        dataLength: Array.isArray(data) ? data.length : (data ? typeof data : 'empty')
      };
      results.push(summary);
      console.log(`[${method}] ${path} -> Status ${res.status} | Data: ${JSON.stringify(summary.dataLength)}`);
      return { status: res.status, data };
    } catch (err) {
      console.error(`[${method}] ${path} threw: ${err.message}`);
      results.push({ name, method, path, status: 'ERROR', error: err.message });
      return { status: 'ERROR', error: err.message };
    }
  }

  console.log('--- Probing Resident Boundary on Admin Endpoints ---');
  // Resident accessing PM subscriptions
  await probe('Subscriptions list', 'GET', '/api/v1/subscriptions');

  // Resident accessing all units without filter
  const allUnits = await probe('All project units (unfiltered)', 'GET', '/api/v1/projects/1/units');

  // Resident querying own unit
  const ownUnit = await probe('Resident own unit', 'GET', `/api/v1/projects/1/units?residentUserId=${resUserId}`);
  if (ownUnit.data && ownUnit.data.length === 1 && ownUnit.data[0].unitNumber === '402') {
    console.log('  -> Confirmed: Resident gets ONLY Unit 402 with residentUserId filter.');
  }

  // Resident attempting to query another user's unit (Admin ID 1)
  const otherUnit = await probe('Other resident units (user 1)', 'GET', '/api/v1/projects/1/units?residentUserId=1');

  // Resident attempting to mutate units (POST)
  await probe('Create unit (malicious POST)', 'POST', '/api/v1/units', {
    projectId: 1,
    unitNumber: '999',
    floor: 9,
    roomNumber: 99
  });

  // Resident attempting to mutate unit (PATCH)
  await probe('Update unit 1 (malicious PATCH)', 'PATCH', '/api/v1/units/1', {
    ownerEmail: 'hacker@evil.com'
  });

  // Resident attempting to create a project
  await probe('Create project (malicious POST)', 'POST', '/api/v1/projects', {
    name: 'Rogue Condo',
    location: 'Somewhere',
    description: 'A rogue building'
  });

  // Resident accessing Alerts
  await probe('Anomaly alerts', 'GET', '/api/v1/alerts');

  // Resident accessing Utility Bills
  await probe('Utility bills', 'GET', '/api/v1/utility_bills');

  // Resident accessing Settlements
  await probe('Settlements', 'GET', '/api/v1/settlements');

  // Resident accessing Delinquency / Pending Payments for other units
  await probe('Delinquency (all pending payments)', 'GET', '/api/v1/payments?status=pending');

  // Resident accessing unit 1 payments
  await probe('Payments for unit 1', 'GET', '/api/v1/payments?unitId[]=1');

  // Resident accessing unit 17 payments (Unit 402)
  await probe('Payments for unit 17 (Unit 402)', 'GET', '/api/v1/payments?unitId[]=17');

  console.log('\n--- Summary of Probes ---');
  for (const r of results) {
    console.log(`${r.name}: ${r.method} ${r.path} -> ${r.status}`);
  }
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
