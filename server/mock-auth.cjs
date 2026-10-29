/**
 * Middleware de autenticación SOLO PARA DESARROLLO con json-server.
 * - POST /sessions        -> login (clave de demo: Demo1234! para los usuarios sembrados)
 * - DELETE /sessions/current -> 204
 * - POST /users           -> registro con correo normalizado y 409 si ya existe
 * - GET  /sessions        -> sesión activa simulada del usuario (para la sección de seguridad)
 * - POST /subscriptions/payments/sessions -> simula el pago de Stripe y deja el plan activo
 * Se activa con: npm run server
 */
const fs = require('fs');
const path = require('path');
const DB = path.join(__dirname, 'db.json');
const DEMO_PASSWORD = 'Demo1234!';

const readDb = () => JSON.parse(fs.readFileSync(DB, 'utf8'));
const strip = (url) => url.replace(/^\/api\/v1/, '');

module.exports = (req, res, next) => {
  const url = strip(req.url.split('?')[0]);

  if (req.method === 'POST' && url === '/sessions') {
    const { email, password } = req.body || {};
    const user = readDb().users.find(u => u.email === String(email || '').trim().toLowerCase());
    const ok = user && (password === user.password || (String(user.password).startsWith('hashed_password') && password === DEMO_PASSWORD));
    if (!ok) return res.status(401).json({ message: 'Invalid credentials' });
    return res.status(201).json({ id: user.id, email: user.email, role: user.role, token: `mock-token-${user.id}` });
  }

  if (req.method === 'DELETE' && url === '/sessions/current') return res.sendStatus(204);

  if (req.method === 'GET' && url === '/sessions') {
    const userId = Number(new URLSearchParams(req.url.split('?')[1] || '').get('userId'));
    return res.json(userId ? [{ id: 1, userId, device: 'Navegador actual (mock)', createdAt: new Date().toISOString() }] : []);
  }

  if (req.method === 'POST' && url === '/users') {
    req.body = req.body || {};
    req.body.email = String(req.body.email || '').trim().toLowerCase();
    if (readDb().users.some(u => u.email === req.body.email)) return res.status(409).json({ error: 'User already exists' });
  }

  // GET /authentication/invitation (flujo viejo) no existe en el mock
  if (url.startsWith('/authentication/invitation')) return res.json({ assigned: false, alreadyRegistered: false });

  // ── Suscripciones: el checkout de Stripe se simula creando la suscripción activa ──
  // Se reescribe a POST /subscriptions para que json-server la guarde; checkoutUrl hace que el frontend
  // vuelva a "Mi suscripción" como si regresara de Stripe.
  if (req.method === 'POST' && url === '/subscriptions/payments/sessions') {
    const { administratorId, condominiumId, planId, successUrl } = req.body || {};
    const plan = readDb().plans.find(p => String(p.id) === String(planId));
    if (!plan) return res.status(404).json({ message: 'Plan not found' });
    const now = new Date();
    req.url = '/subscriptions';
    req.body = {
      administratorId: Number(administratorId), condominiumId: Number(condominiumId), plan: plan.name, planId: plan.id,
      status: 'active', startDate: now.toISOString(),
      endDate: new Date(now.getFullYear() + 1, now.getMonth(), now.getDate()).toISOString(),
      price: plan.monthlyPen, nextBillingDate: new Date(now.getFullYear(), now.getMonth() + 1, 1).toISOString().slice(0, 10),
      checkoutUrl: successUrl,
    };
    return next();
  }
  if (req.method === 'PATCH' && url.startsWith('/subscriptions/payments/sessions/')) return res.json({ status: 'confirmed' });
  if (req.method === 'GET' && url === '/subscriptions/payments/invoices') {
    const administratorId = new URLSearchParams(req.url.split('?')[1] || '').get('administratorId');
    const rows = readDb().subscriptions.filter(s => String(s.administratorId) === String(administratorId));
    return res.json(rows.map(s => ({ id: `mock_${s.id}`, description: `Plan ${s.plan}`, amount: s.price, currency: 'PEN',
      status: 'paid', date: s.startDate, receiptUrl: null })));
  }
  const cancel = url.match(/^\/subscriptions\/(\d+)\/cancel$/);
  if (req.method === 'POST' && cancel) {
    req.method = 'PATCH';
    req.url = `/subscriptions/${cancel[1]}`;
    req.body = { status: 'cancelled' };
    return next();
  }

  next();
};
