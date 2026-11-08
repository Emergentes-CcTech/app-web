import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { participationFromArea, parsePadronCsv } from '../../src/condominiums/domain/padron.js';
import { sumParticipation } from '../../src/shared/presentation/validators.js';

describe('Padrón: participación por área', () => {
  it('es proporcional al área y suma exactamente 100 %', () => {
    const pct = participationFromArea([100, 50, 50]);
    expect(pct).toEqual([50, 25, 25]);
  });

  it('reparte los decimales sobrantes sin pasarse de 100 %', () => {
    const pct = participationFromArea([1, 1, 1]);
    expect(pct.reduce((s, p) => s + p * 10000, 0)).toBe(1000000);
    expect(pct.every(p => p >= 33.3333 && p <= 33.3334)).toBe(true);
  });

  it('sin área no inventa porcentajes', () => {
    expect(participationFromArea([0, 0])).toEqual([0, 0]);
  });
});

describe('Padrón: lectura del CSV', () => {
  it('lee el archivo de demostración y su participación por área suma 100 %', () => {
    const { rows, errors } = parsePadronCsv(readFileSync('public/demo/padron-demo.csv', 'utf8'));
    expect(errors).toEqual([]);
    expect(rows).toHaveLength(12);
    expect(rows.at(-1)).toMatchObject({ number: 'E-01', type: 'parking', floor: 0, areaM2: 12.5, participationPct: null });
    const pct = participationFromArea(rows.map(r => r.areaM2));
    expect(sumParticipation(pct.map(p => ({ participationPct: p })))).toBe(100);
  });

  it('acepta el formato de Excel en español (punto y coma y coma decimal)', () => {
    const { rows, errors } = parsePadronCsv('Número;Tipo;Área m²;Participación %\n101;Departamento;85,5;50,25\n102;Depósito;10;49,75');
    expect(errors).toEqual([]);
    expect(rows[0]).toMatchObject({ number: '101', areaM2: 85.5, participationPct: 50.25 });
    expect(rows[1].type).toBe('storage');
  });

  it('señala filas inválidas y columnas faltantes', () => {
    expect(parsePadronCsv('nombre,valor\nA,1').errors[0].key).toBe('headers');
    const { errors } = parsePadronCsv('numero,area\n101,80\n101,0\n');
    expect(errors.map(e => e.key)).toEqual(['duplicate', 'area']);
  });
});
