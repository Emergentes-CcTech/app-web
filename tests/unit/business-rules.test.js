import { describe, it, expect } from 'vitest';
import { previewProration } from '../../src/billing/application/proration-preview.js';
import { projectToMonthEnd } from '../../src/telemetry/application/projection.js';
import { Payment } from '../../src/payments/domain/model/payment.entity.js';
import { UtilityBill } from '../../src/billing/domain/model/utility-bill.entity.js';
import { Settlement } from '../../src/billing/domain/model/settlement.entity.js';
import { recommendedTier, PLAN_TIERS } from '../../src/subscriptions/domain/model/plan-limits.js';
import { Plan } from '../../src/subscriptions/domain/model/plan.entity.js';
import { buildCommonAreaReport, dailySeries, changePct } from '../../src/telemetry/application/common-area-report.js';
import { isParticipationBalanced, isValidRuc, isValidPassword, isValidDni, sumParticipation, isValidPercentage } from '../../src/shared/presentation/validators.js';
import { normalizeRole, ROLES } from '../../src/shared/infrastructure/roles.js';

const units = [
  { id: 1, participationPct: 28.5 }, { id: 2, participationPct: 26.5 },
  { id: 3, participationPct: 25 }, { id: 4, participationPct: 20 },
];

describe('RF-013 prorrateo', () => {
  it('reparte 1000 como 285, 265, 250 y 200 y la suma es exacta', () => {
    const r = previewProration(1000, units);
    expect(r.map(x => x.amount)).toEqual([285, 265, 250, 200]);
    expect(Math.round(r.reduce((a, x) => a + x.amount * 100, 0))).toBe(100000);
  });
  it('no pierde céntimos con montos que no dividen exacto', () => {
    const r = previewProration(100.01, [{ id: 1, participationPct: 33.33 }, { id: 2, participationPct: 33.33 }, { id: 3, participationPct: 33.34 }]);
    expect(Math.round(r.reduce((a, x) => a + x.amount * 100, 0))).toBe(10001);
  });
  it('rechaza un padrón que no suma 100', () => {
    expect(() => previewProration(1000, [{ id: 1, participationPct: 90 }])).toThrow();
  });
  it('línea de liquidación suma el gasto común', () => {
    const lines = previewProration(1000, units);
    expect(new Settlement({ commonAreaAmount: 1000, lines }).linesMatchTotal).toBe(true);
  });
});

describe('RF-006 participación', () => {
  it('falla con 99.99 y con 100.01', () => {
    expect(isParticipationBalanced([{ participationPct: 99.99 }])).toBe(false);
    expect(isParticipationBalanced([{ participationPct: 100.01 }])).toBe(false);
  });
  it('suma exacta en diezmilésimas', () => { expect(sumParticipation(units)).toBe(100); });
  it('alícuotas de 4 decimales que suman 100.0000 % están balanceadas (Ley N.° 27157)', () => {
    expect(isParticipationBalanced([{ participationPct: 3.85 }, { participationPct: 4.0062 }, { participationPct: 92.1438 }])).toBe(true);
    expect(isParticipationBalanced([{ participationPct: 3.85 }, { participationPct: 96.1499 }])).toBe(false);
  });
  it('porcentaje válido: 0.0001–100 con hasta 4 decimales', () => {
    expect(isValidPercentage(0.01)).toBe(true);
    expect(isValidPercentage(4.0062)).toBe(true);
    expect(isValidPercentage(0)).toBe(false);
    expect(isValidPercentage(100.5)).toBe(false);
    expect(isValidPercentage(10.12345)).toBe(false);
  });
});

describe('RF-010 proyección', () => {
  it('projectToMonthEnd(150, 10 oct) = 465', () => {
    expect(projectToMonthEnd(150, new Date('2026-10-10T12:00:00'))).toBe(465);
  });
});

describe('RF-016 pagos', () => {
  it('overdue con fecha pasada y sin pago', () => {
    expect(new Payment({ dueDate: '2020-01-01', paidAt: null }).effectiveStatus).toBe('overdue');
  });
  it('paid si tiene paidAt', () => {
    expect(new Payment({ dueDate: '2020-01-01', paidAt: '2020-01-02' }).effectiveStatus).toBe('paid');
  });
});

describe('RF-012 conciliación', () => {
  it('310 facturados vs 298 medidos = 12 m³ y S/ 38.71', () => {
    const b = new UtilityBill({ billedConsumption: 310, billedAmount: 1000, measuredConsumption: 298 });
    expect(b.differenceUnits).toBe(12);
    expect(b.differenceAmount).toBe(38.71);
  });
});

describe('RF-014 planes', () => {
  it('tramos por departamentos: ≤20 Boutique, 21-50 Urbano, 51-80 Gran Torre', () => {
    expect(recommendedTier(20).name).toBe('Boutique');
    expect(recommendedTier(21).name).toBe('Urbano');
    expect(recommendedTier(50).name).toBe('Urbano');
    expect(recommendedTier(51).name).toBe('Gran Torre');
    expect(recommendedTier(80).name).toBe('Gran Torre');
    expect(recommendedTier(81)).toBeNull();
  });

  it('precios y kits de equipos', () => {
    const [boutique, urbano, granTorre] = PLAN_TIERS.map(t => new Plan({ code: t.code }));
    expect([boutique.monthlyPen, urbano.monthlyPen, granTorre.monthlyPen]).toEqual([500, 700, 1000]);
    expect([boutique.hardwareIncluded, urbano.hardwareIncluded, granTorre.hardwareIncluded]).toEqual([4, 6, 10]);
    expect(granTorre.kit).toEqual({ gateways: 2, flowMeters: 3, ctClamps: 5 });
    expect(boutique.getFormattedPrice()).toBe('S/ 500');
  });
});

describe('Analytics: reporte de áreas comunes', () => {
  const areas = [
    { id: 1, name: 'Cisterna', type: 'cistern' },
    { id: 2, name: 'Cuarto de bombas', type: 'pump_room' },
    { id: 3, name: 'Piscina', type: 'pool' },
  ];
  const devices = [
    { id: 10, type: 'water_pulse_meter', measurementPoint: 'cistern', isOnline: true },
    { id: 20, type: 'current_sensor', measurementPoint: 'pump_room', isOnline: false },
    { id: 30, type: 'gateway', measurementPoint: 'panel', isOnline: true },
  ];
  const readings = [
    { deviceId: 10, timestamp: '2026-10-01T10:00:00Z', value: 1000, unit: 'L/min' }, // 1 m³
    { deviceId: 10, timestamp: '2026-10-02T10:00:00Z', value: 3000, unit: 'L/min' }, // 3 m³
    { deviceId: 20, timestamp: '2026-10-01T10:00:00Z', value: 60, unit: 'A' },       // 0.23 kWh
  ];

  it('agrupa el consumo por área según el punto de medición', () => {
    const { rows, totals, monitoredCount } = buildCommonAreaReport(areas, devices, readings);
    expect(rows.map(r => [r.name, r.waterM3, r.energyKwh])).toEqual([['Cisterna', 4, 0], ['Cuarto de bombas', 0, 0.23], ['Piscina', 0, 0]]);
    expect(totals).toEqual({ waterM3: 4, energyKwh: 0.23 });
    expect(monitoredCount).toBe(2);
    expect(rows[0].waterShare).toBe(100);
    expect(rows[1].metersOnline).toBe(0);
    expect(rows[2].monitored).toBe(false);
  });

  it('serie diaria y variación vs mes anterior', () => {
    expect(dailySeries(readings, 'water')).toEqual({ labels: ['01', '02'], values: [1, 3] });
    expect(changePct(110, 100)).toBe(10);
    expect(changePct(5, 0)).toBeNull();
  });
});

describe('RF-001 validaciones y roles', () => {
  it('RUC, DNI y contraseña fuerte', () => {
    expect(isValidRuc('20601234567')).toBe(true);
    expect(isValidRuc('30601234567')).toBe(false);
    expect(isValidDni('45678912')).toBe(true);
    expect(isValidPassword('abc12345')).toBe(false);
    expect(isValidPassword('Abc12345!')).toBe(true);
  });
  it('normalizeRole acepta PropertyManager y valores viejos', () => {
    expect(normalizeRole('PropertyManager')).toBe(ROLES.ADMIN);
    expect(normalizeRole('Builder')).toBe(ROLES.ADMIN);
    expect(normalizeRole('Residente')).toBe(ROLES.RESIDENT);
    expect(normalizeRole('xyz')).toBe('');
  });
});
