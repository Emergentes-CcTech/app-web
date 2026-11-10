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

  // ── BFF Mobile Resident Endpoints (/api/v1/mobile/resident/*) para desarrollo local con json-server ──
  const getAuthUserId = () => {
    const auth = req.headers.authorization || '';
    const match = auth.match(/mock-token-(\d+)/);
    return match ? Number(match[1]) : 2; // por defecto residente Carla Flores (id=2)
  };

  if (req.method === 'GET' && url === '/mobile/resident/overview') {
    const db = readDb();
    const userId = getAuthUserId();
    const myUnit = (db.units || []).find(u => Number(u.ownerUserId) === userId || Number(u.residentUserId) === userId) || db.units?.[0];
    if (!myUnit) {
      return res.json({ hasUnit: false, unit: null, condominium: null, latestStatement: null, recentStatements: [], commonConsumption: null });
    }
    const condo = (db.condominiums || []).find(c => Number(c.id) === Number(myUnit.condominiumId)) || db.condominiums?.[0];
    const profile = (db.profiles || []).find(p => Number(p.userId) === userId);
    const payments = (db.payments || [])
      .filter(p => Number(p.unitId) === Number(myUnit.id))
      .map(p => {
        const effStatus = p.status || 'pending';
        const isPaid = effStatus === 'paid';
        return {
          id: p.id,
          settlementLineId: p.settlementLineId || p.id,
          unitId: p.unitId,
          condominiumId: p.condominiumId || myUnit.condominiumId,
          period: p.period || null,
          amount: Number(p.amount) || 0,
          dueDate: p.dueDate || null,
          paidAt: p.paidAt ? String(p.paidAt).slice(0, 10) : null,
          status: effStatus,
          effectiveStatus: effStatus,
          daysOverdue: effStatus === 'overdue' ? 12 : 0,
          method: isPaid && p.method ? p.method : null,
          reference: isPaid && p.reference ? p.reference : null
        };
      })
      .sort((a, b) => String(b.period || '').localeCompare(String(a.period || '')));

    const areas = (condo?.commonAreas || []).map((a, idx) => ({
      id: a.id || idx + 1,
      name: a.name,
      type: a.type,
      subsystem: a.type,
      location: 'Sótano 1',
      monitored: true,
      metersOnline: 1,
      metersTotal: 1,
      waterM3: a.type === 'cistern' || a.type === 'pool' ? 142.5 : 0,
      energyKwh: a.type !== 'cistern' ? 310.4 : 0,
      waterSharePct: a.type === 'cistern' ? 75.0 : (a.type === 'pool' ? 25.0 : 0),
      energySharePct: a.type !== 'cistern' ? 33.3 : 0,
      lastReadingAt: new Date().toISOString()
    }));

    return res.json({
      hasUnit: true,
      unit: {
        id: myUnit.id,
        unitNumber: myUnit.number || myUnit.unitNumber || '402',
        floor: myUnit.floor || 4,
        areaM2: myUnit.areaM2 || 85,
        aliquotPercentage: myUnit.participationPct || myUnit.aliquotPercentage || 3.85,
        residentFullName: profile?.fullName || 'Carla Flores'
      },
      condominium: condo ? {
        id: condo.id,
        name: condo.name,
        address: condo.address || null,
        district: condo.district || null,
        totalUnits: condo.totalUnits || 0
      } : null,
      latestStatement: payments[0] || null,
      recentStatements: payments.slice(0, 3),
      commonConsumption: {
        period: '2026-10',
        waterM3: 285.0,
        energyKwh: 931.2,
        previousWaterM3: 270.0,
        previousEnergyKwh: 890.0,
        waterChangePct: 5.6,
        energyChangePct: 4.6,
        projectedWaterM3: 310.0,
        projectedEnergyKwh: 1020.0,
        monitoredAreasCount: areas.length,
        totalAreasCount: areas.length,
        areas,
        dailySeries: [
          { date: '2026-10-01', dayLabel: '01', waterM3: 9.2, energyKwh: 30.1 },
          { date: '2026-10-05', dayLabel: '05', waterM3: 9.8, energyKwh: 31.4 },
          { date: '2026-10-10', dayLabel: '10', waterM3: 10.1, energyKwh: 32.0 },
          { date: '2026-10-15', dayLabel: '15', waterM3: 9.5, energyKwh: 29.8 },
          { date: '2026-10-20', dayLabel: '20', waterM3: 9.4, energyKwh: 31.2 },
          { date: '2026-10-25', dayLabel: '25', waterM3: 9.7, energyKwh: 30.9 }
        ]
      }
    });
  }

  if (req.method === 'GET' && url === '/mobile/resident/account-statement') {
    const db = readDb();
    const userId = getAuthUserId();
    const myUnit = (db.units || []).find(u => Number(u.ownerUserId) === userId || Number(u.residentUserId) === userId) || db.units?.[0];
    const condo = (db.condominiums || []).find(c => Number(c.id) === Number(myUnit?.condominiumId)) || db.condominiums?.[0];
    const profile = (db.profiles || []).find(p => Number(p.userId) === userId);
    const history = (db.payments || [])
      .filter(p => Number(p.unitId) === Number(myUnit?.id))
      .map(p => {
        const effStatus = p.status || 'pending';
        const isPaid = effStatus === 'paid';
        return {
          id: p.id,
          settlementLineId: p.settlementLineId || p.id,
          unitId: p.unitId,
          condominiumId: p.condominiumId || myUnit?.condominiumId || 1,
          period: p.period || null,
          concept: 'Cuota de áreas comunes',
          amount: Number(p.amount) || 0,
          dueDate: p.dueDate || null,
          paidAt: p.paidAt ? String(p.paidAt).slice(0, 10) : null,
          status: effStatus,
          effectiveStatus: effStatus,
          daysOverdue: effStatus === 'overdue' ? 12 : 0,
          method: isPaid && p.method ? p.method : null,
          reference: isPaid && p.reference ? p.reference : null
        };
      })
      .sort((a, b) => String(b.period || '').localeCompare(String(a.period || '')));

    const pendingStatement = history.find(h => h.effectiveStatus !== 'paid') || null;
    return res.json({
      unit: myUnit ? {
        id: myUnit.id,
        unitNumber: myUnit.number || myUnit.unitNumber || '101',
        condominiumId: myUnit.condominiumId || 1,
        condominiumName: condo?.name || 'Condominio Los Álamos',
        aliquotPercentage: myUnit.participationPct || myUnit.aliquotPercentage || 28.5,
        holderName: profile?.fullName || 'Carla Flores',
        floor: myUnit.floor || 1,
        areaM2: myUnit.areaM2 || 85
      } : null,
      pendingStatement,
      history
    });
  }

  if (req.method === 'GET' && url === '/mobile/resident/fee-explanation') {
    const db = readDb();
    const userId = getAuthUserId();
    const myUnit = (db.units || []).find(u => Number(u.ownerUserId) === userId || Number(u.residentUserId) === userId) || db.units?.[0];
    const payments = (db.payments || []).filter(p => Number(p.unitId) === Number(myUnit?.id));
    const p = payments[0] || { id: 1, period: '2026-09', amount: 285, status: 'paid', dueDate: '2026-10-15', paidAt: '2026-10-10' };
    const aliquot = Number(myUnit?.participationPct || 28.5);
    const totalCommon = 1000.0;
    const waterCommon = 420.0;
    const energyCommon = 480.0;
    const otherCommon = 100.0;

    return res.json({
      availableLines: payments.map(x => ({
        lineId: x.settlementLineId || x.id,
        period: x.period || '2026-09',
        amount: Number(x.amount) || 285,
        status: x.status || 'paid'
      })),
      explanation: {
        lineId: p.settlementLineId || p.id,
        unitNumber: myUnit?.number || '101',
        period: p.period || '2026-09',
        previousPeriod: '2026-08',
        status: p.status || 'paid',
        dueDate: p.dueDate || '2026-10-15',
        paidAt: p.paidAt ? String(p.paidAt).slice(0, 10) : null,
        balance: p.status === 'paid' ? 0 : p.amount,
        aliquot,
        common: {
          water: waterCommon,
          energy: energyCommon,
          other: otherCommon,
          total: totalCommon,
          waterM3: 285.0,
          energyKwh: 931.2,
          waterReceiptUrl: null,
          energyReceiptUrl: null
        },
        mine: {
          water: Math.round(waterCommon * aliquot) / 100,
          energy: Math.round(energyCommon * aliquot) / 100,
          other: Math.round(otherCommon * aliquot) / 100,
          total: Number(p.amount) || 285.0
        },
        previous: {
          waterSoles: 114.0,
          energySoles: 131.1,
          otherSoles: 28.5,
          totalSoles: 273.6
        },
        alerts: []
      }
    });
  }

  if (req.method === 'GET' && url === '/mobile/resident/profile') {
    const db = readDb();
    const userId = getAuthUserId();
    const user = (db.users || []).find(u => Number(u.id) === userId) || { id: userId, email: 'carla.f@email.com' };
    const profile = (db.profiles || []).find(p => Number(p.userId) === userId) || { id: 12, userId, fullName: 'Carla Flores', phoneNumber: '+51987654321' };
    const myUnit = (db.units || []).find(u => Number(u.ownerUserId) === userId || Number(u.residentUserId) === userId) || db.units?.[0];
    const condo = (db.condominiums || []).find(c => Number(c.id) === Number(myUnit?.condominiumId)) || db.condominiums?.[0];

    return res.json({
      profileId: profile?.id ?? null,
      userId,
      fullName: profile?.fullName || profile?.name || null,
      email: user?.email || null,
      secondEmail: profile?.secondEmail || null,
      phoneNumber: profile?.phoneNumber || null,
      photoUrl: profile?.photoUrl || null,
      role: 'resident',
      unit: myUnit ? {
        id: myUnit.id,
        unitNumber: myUnit.number || myUnit.unitNumber || null,
        condominiumName: condo?.name || null,
        floor: myUnit.floor ?? null,
        areaM2: myUnit.areaM2 ?? null,
        aliquotPercentage: myUnit.participationPct || myUnit.aliquotPercentage || 0
      } : null,
      sessions: null
    });
  }

  if (req.method === 'POST' && url === '/mobile/resident/ai-explain') {
    return res.json({
      period: '2026-10',
      summary: 'Tu cuota de este mes refleja una ligera variación de +4.1% asociada al consumo de bombeo e impulsión de agua en áreas comunes.',
      waterAnalysis: 'El consumo de agua en la cisterna principal se mantuvo dentro del rango normal (285 m³), sin fugas nocturnas activas.',
      energyAnalysis: 'El cuarto de bombas registró 931.2 kWh debido a mayor demanda en horas punta.',
      aliquotImpact: 'Con tu alícuota legal (Ley N.° 27157), participas únicamente en la proporción exacta de los recibos matrices conciliados.',
      recommendations: [
        'Revisa el desglose paso a paso en la pestaña Mi Cuota.',
        'Verifica los comprobantes matrices de Sedapal y Luz del Sur adjuntos por la administración.'
      ],
      source: 'rules'
    });
  }

  next();
};
