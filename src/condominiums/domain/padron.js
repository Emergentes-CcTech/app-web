// Reglas del padrón de unidades: participación por área y lectura del archivo que exporta Excel (CSV)

const SCALE = 10000; // la alícuota se expresa con 4 decimales (Ley N.° 27157)

/**
 * Participación proporcional al área: área de la unidad ÷ área total.
 * Reparte los diezmilésimos sobrantes por mayor residuo para que la suma sea exactamente 100 %.
 * @returns {number[]} porcentajes en el mismo orden que `areas`
 */
export function participationFromArea(areas) {
    const values = areas.map(a => Math.max(0, Number(a) || 0));
    const total = values.reduce((s, a) => s + a, 0);
    if (!values.length || total <= 0) return values.map(() => 0);
    const target = 100 * SCALE;
    const exact = values.map(a => (a / total) * target);
    const units = exact.map(Math.floor);
    let left = target - units.reduce((s, u) => s + u, 0);
    const order = exact.map((e, i) => ({ i, rest: e - Math.floor(e) })).sort((a, b) => b.rest - a.rest || a.i - b.i);
    for (let k = 0; left > 0; k = (k + 1) % order.length, left--) units[order[k].i] += 1;
    return units.map(u => u / SCALE);
}

const HEADERS = {
    number: ['numero', 'número', 'number', 'unidad', 'unit', 'departamento', 'dpto'],
    type: ['tipo', 'type'],
    floor: ['piso', 'floor'],
    areaM2: ['area', 'área', 'area m2', 'área m2', 'area m²', 'área m²', 'm2', 'm²', 'area_m2', 'aream2'],
    participationPct: ['participacion', 'participación', 'participacion %', 'participación %', '% participacion', '% de participación', '% de participacion', 'alicuota', 'alícuota', 'participation', 'participation %', '%'],
};
const TYPES = {
    apartment: ['departamento', 'dpto', 'apartment', 'depa', 'flat'],
    storage: ['deposito', 'depósito', 'storage', 'almacen', 'almacén'],
    parking: ['estacionamiento', 'cochera', 'parking'],
};

const norm = (s) => String(s ?? '').trim().toLowerCase().replace(/\s+/g, ' ');

function splitLine(line, sep) {
    const out = [];
    let cur = '', quoted = false;
    for (let i = 0; i < line.length; i++) {
        const c = line[i];
        if (c === '"') {
            if (quoted && line[i + 1] === '"') { cur += '"'; i++; } else quoted = !quoted;
        } else if (c === sep && !quoted) { out.push(cur); cur = ''; }
        else cur += c;
    }
    out.push(cur);
    return out.map(v => v.trim());
}

// Excel en español usa ";" como separador y "," como decimal; en inglés, "," y "."
function toNumber(raw) {
    const s = String(raw ?? '').replace(/[%\s]/g, '').replace(/m²|m2/gi, '');
    if (!s) return null;
    const normalized = s.includes(',') && !s.includes('.') ? s.replace(',', '.') : s.replace(/,/g, '');
    const n = Number(normalized);
    return Number.isFinite(n) ? n : NaN;
}

function toType(raw) {
    const v = norm(raw);
    if (!v) return 'apartment';
    return Object.keys(TYPES).find(k => TYPES[k].includes(v)) ?? null;
}

/**
 * Lee el CSV del padrón. Columnas: número y área obligatorias; tipo, piso y participación opcionales.
 * @returns {{ rows: Array<{number:string,type:string,floor:number|null,areaM2:number,participationPct:number|null}>, errors: Array<{line:number,key:string,params?:object}> }}
 */
export function parsePadronCsv(text) {
    const lines = String(text ?? '').replace(/^﻿/, '').split(/\r?\n/).filter(l => l.trim());
    if (!lines.length) return { rows: [], errors: [{ line: 0, key: 'empty' }] };
    const sep = (lines[0].match(/;/g) || []).length > (lines[0].match(/,/g) || []).length ? ';' : (lines[0].includes('\t') ? '\t' : ',');
    const header = splitLine(lines[0], sep).map(norm);
    const col = Object.fromEntries(Object.entries(HEADERS).map(([k, names]) => [k, header.findIndex(h => names.includes(h))]));
    if (col.number < 0 || col.areaM2 < 0) return { rows: [], errors: [{ line: 1, key: 'headers' }] };

    const rows = [], errors = [], seen = new Set();
    lines.slice(1).forEach((line, i) => {
        const at = i + 2;
        const cells = splitLine(line, sep);
        const number = String(cells[col.number] ?? '').trim();
        if (!number) return; // fila vacía o de totales
        const areaM2 = toNumber(cells[col.areaM2]);
        const pct = col.participationPct >= 0 ? toNumber(cells[col.participationPct]) : null;
        const floor = col.floor >= 0 ? toNumber(cells[col.floor]) : null;
        const type = col.type >= 0 ? toType(cells[col.type]) : 'apartment';
        if (number.length > 10) errors.push({ line: at, key: 'number', params: { number } });
        else if (seen.has(number.toLowerCase())) errors.push({ line: at, key: 'duplicate', params: { number } });
        if (!(areaM2 > 0)) errors.push({ line: at, key: 'area', params: { number } });
        if (pct !== null && !(pct > 0 && pct <= 100)) errors.push({ line: at, key: 'participation', params: { number } });
        if (!type) errors.push({ line: at, key: 'type', params: { number } });
        seen.add(number.toLowerCase());
        rows.push({ number, type: type || 'apartment', floor: Number.isInteger(floor) ? floor : null, areaM2, participationPct: pct });
    });
    if (!rows.length && !errors.length) errors.push({ line: 0, key: 'empty' });
    return { rows, errors };
}
