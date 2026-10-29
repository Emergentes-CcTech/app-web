/**
 * API de demostración que corre en el navegador (solo con VITE_DEMO=true).
 * Imita a json-server + server/mock-auth.cjs usando server/db.json, para publicar la app
 * sin backend (p. ej. en Vercel). Los cambios se guardan en el localStorage de cada visitante.
 * Clave de los usuarios sembrados: Demo1234!
 */
const STORAGE_KEY = 'iobuild-demo-db';
const DEMO_PASSWORD = 'Demo1234!';

let dbPromise = null;

async function loadDb() {
    if (!dbPromise) {
        window.resetIoBuildDemo = () => { resetDemoData(); window.location.reload(); };
        dbPromise = (async () => {
            try {
                const saved = localStorage.getItem(STORAGE_KEY);
                if (saved) return JSON.parse(saved);
            } catch (_) { /* sin almacenamiento: se usa la semilla */ }
            const seed = await import('../../../server/db.json');
            return structuredClone(seed.default ?? seed);
        })();
    }
    return dbPromise;
}

function persist(db) {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(db)); } catch (_) { /* cuota llena o modo privado */ }
}

/** Vuelve a los datos semilla (útil desde la consola: window.resetIoBuildDemo()) */
export function resetDemoData() {
    try { localStorage.removeItem(STORAGE_KEY); } catch (_) { /* ignore */ }
    dbPromise = null;
}

const nextId = (rows) => rows.reduce((max, r) => Math.max(max, Number(r.id) || 0), 0) + 1;

// Filtros de json-server: igualdad (o lista de valores), _gte, _lte, _sort, _order, _limit, _embed
function query(db, collection, params = {}) {
    let rows = [...db[collection]];
    for (const [key, raw] of Object.entries(params)) {
        if (raw === undefined || raw === null || key === '_t' || key.startsWith('_')) continue;
        if (key.endsWith('_gte')) { const f = key.slice(0, -4); rows = rows.filter(r => String(r[f]) >= String(raw)); continue; }
        if (key.endsWith('_lte')) { const f = key.slice(0, -4); rows = rows.filter(r => String(r[f]) <= String(raw)); continue; }
        const values = (Array.isArray(raw) ? raw : [raw]).map(String);
        rows = rows.filter(r => values.includes(String(r[key])));
    }
    if (params._sort) {
        const dir = String(params._order).toLowerCase() === 'desc' ? -1 : 1;
        rows.sort((a, b) => (a[params._sort] > b[params._sort] ? 1 : a[params._sort] < b[params._sort] ? -1 : 0) * dir);
    }
    if (params._limit) rows = rows.slice(0, Number(params._limit));
    return rows;
}

function embed(db, collection, item, child) {
    if (!child || !db[child]) return item;
    const fk = `${collection.replace(/s$/, '')}Id`;
    return { ...item, [child]: db[child].filter(c => String(c[fk]) === String(item.id)) };
}

function handleCustom(db, method, path, body, params) {
    // ── IAM (equivale a server/mock-auth.cjs) ──
    if (method === 'post' && path === '/sessions') {
        const email = String(body.email || '').trim().toLowerCase();
        const user = db.users.find(u => u.email === email);
        const ok = user && (body.password === user.password
            || (String(user.password).startsWith('hashed_password') && body.password === DEMO_PASSWORD));
        if (!ok) return [401, { message: 'Invalid credentials' }];
        return [201, { id: user.id, email: user.email, role: user.role, token: `demo-token-${user.id}` }];
    }
    if (method === 'delete' && path.startsWith('/sessions/')) return [204, null];
    if (method === 'get' && path === '/sessions') {
        return [200, params.userId ? [{ id: 1, userId: Number(params.userId), device: 'Navegador actual (demo)', createdAt: new Date().toISOString() }] : []];
    }
    if (method === 'post' && path === '/users') {
        const email = String(body.email || '').trim().toLowerCase();
        if (db.users.some(u => u.email === email)) return [409, { error: 'User already exists' }];
        const user = { ...body, email, id: nextId(db.users) };
        db.users.push(user);
        return [201, { id: user.id, email: user.email, role: user.role }];
    }
    if (method === 'patch' && /^\/users\/\d+\/password$/.test(path)) return [204, null];
    if (path.startsWith('/authentication/invitation')) return [200, { assigned: false, alreadyRegistered: false }];

    // ── Suscripciones: el checkout de Stripe se simula activando el plan ──
    if (method === 'post' && path === '/subscriptions/payments/sessions') {
        const plan = db.plans.find(p => String(p.id) === String(body.planId));
        if (!plan) return [404, { message: 'Plan not found' }];
        const now = new Date();
        const next = new Date(now.getFullYear(), now.getMonth() + 1, 1);
        const end = new Date(now.getFullYear() + 1, now.getMonth(), now.getDate());
        db.subscriptions.forEach(s => {
            if (String(s.condominiumId) === String(body.condominiumId) && s.status === 'active') s.status = 'cancelled';
        });
        db.subscriptions.push({
            id: nextId(db.subscriptions), administratorId: Number(body.administratorId), condominiumId: Number(body.condominiumId),
            plan: plan.name, planId: plan.id, status: 'active', startDate: now.toISOString(), endDate: end.toISOString(),
            price: plan.monthlyPen, nextBillingDate: next.toISOString().slice(0, 10),
        });
        return [201, { url: body.successUrl }];
    }
    if (method === 'patch' && path.startsWith('/subscriptions/payments/sessions/')) return [200, { status: 'confirmed' }];
    if (method === 'get' && path === '/subscriptions/payments/invoices') {
        const rows = db.subscriptions.filter(s => String(s.administratorId) === String(params.administratorId));
        return [200, rows.map(s => ({
            id: `demo_${s.id}`, description: `Plan ${s.plan}`, amount: s.price, currency: 'PEN',
            status: 'paid', date: s.startDate, receiptUrl: null,
        }))];
    }
    const cancel = path.match(/^\/subscriptions\/(\d+)\/cancel$/);
    if (method === 'post' && cancel) {
        const sub = db.subscriptions.find(s => String(s.id) === cancel[1]);
        if (!sub) return [404, {}];
        sub.status = 'cancelled';
        return [200, sub];
    }
    if (method === 'post' && /^\/payments\/\d+\/checkout-session$/.test(path)) {
        return [400, { message: 'Los pagos con Stripe no están disponibles en la demo.' }];
    }
    return null;
}

function handleRest(db, method, path, body, params) {
    const [, collection, id] = path.match(/^\/([^/]+)(?:\/([^/]+))?$/) || [];
    if (!collection || !Array.isArray(db[collection])) return [404, {}];
    const rows = db[collection];
    const index = id === undefined ? -1 : rows.findIndex(r => String(r.id) === String(id));

    if (method === 'get') {
        if (id === undefined) return [200, query(db, collection, params)];
        return index < 0 ? [404, {}] : [200, embed(db, collection, rows[index], params._embed)];
    }
    if (method === 'post' && id === undefined) {
        const item = { ...body, id: body.id ?? nextId(rows) };
        rows.push(item);
        return [201, item];
    }
    if (index < 0) return [404, {}];
    if (method === 'put') { rows[index] = { ...body, id: rows[index].id }; return [200, rows[index]]; }
    if (method === 'patch') { rows[index] = { ...rows[index], ...body }; return [200, rows[index]]; }
    if (method === 'delete') { rows.splice(index, 1); return [200, {}]; }
    return [405, {}];
}

/** Adaptador de axios: responde las peticiones sin salir del navegador */
export async function demoAdapter(config) {
    const db = await loadDb();
    const method = String(config.method || 'get').toLowerCase();
    const url = new URL(config.url, 'http://demo.local');
    const path = url.pathname.replace(/^\/api\/v1/, '').replace(/\/$/, '') || '/';
    const params = { ...Object.fromEntries(url.searchParams), ...(config.params || {}) };
    let body = config.data ?? {};
    if (typeof body === 'string') { try { body = JSON.parse(body); } catch (_) { body = {}; } }

    const [status, data] = handleCustom(db, method, path, body, params) ?? handleRest(db, method, path, body, params);
    if (method !== 'get') persist(db);

    const response = { data: data === null ? '' : structuredClone(data), status, statusText: String(status), headers: {}, config, request: {} };
    if (status >= 400) {
        const error = new Error(`Request failed with status code ${status}`);
        error.response = response;
        error.config = config;
        error.isAxiosError = true;
        throw error;
    }
    return response;
}
