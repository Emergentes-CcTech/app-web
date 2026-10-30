// Test units endpoints with PM token
async function run() {
  const loginRes = await fetch('http://localhost/api/v1/sessions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'mveramendi@iobuild.pe', password: 'Admin123!' })
  });
  const data = await loginRes.json();
  const token = data.token;
  console.log('Login OK:', data.email, 'Role:', data.role);

  const testUrls = [
    'http://localhost/api/v1/units',
    'http://localhost/api/v1/units?condominiumId=1',
    'http://localhost/api/v1/projects/1/units'
  ];

  for (const url of testUrls) {
    const res = await fetch(url, {
      headers: { Authorization: 'Bearer ' + token }
    });
    console.log(url, '-> status:', res.status);
    const body = await res.json();
    console.log('   items count:', Array.isArray(body) ? body.length : typeof body);
  }
}
run();
