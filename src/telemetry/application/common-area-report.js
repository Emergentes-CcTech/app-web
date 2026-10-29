import { litersPerMinToM3, ampsToKwh } from './projection.js';

const round2 = (n) => Math.round(n * 100) / 100;

/** Consumo de un conjunto de lecturas: m³ para caudalímetros (L/min) y kWh para pinzas CT (A) */
export function consumption(readings) {
    let waterM3 = 0, energyKwh = 0;
    for (const r of readings) {
        if (r.unit === 'L/min') waterM3 += litersPerMinToM3(r.value);
        else if (r.unit === 'A') energyKwh += ampsToKwh(r.value);
    }
    return { waterM3: round2(waterM3), energyKwh: round2(energyKwh) };
}

/**
 * Reporte por área común. Un dispositivo mide un área cuando su punto de medición
 * es igual al tipo del área. Las áreas sin medidor se listan como no monitoreadas.
 */
export function buildCommonAreaReport(commonAreas, devices, readings) {
    const readingsByDevice = {};
    for (const r of readings) (readingsByDevice[r.deviceId] ||= []).push(r);

    const rows = commonAreas.map(area => {
        const meters = devices.filter(d => d.type !== 'gateway' && d.measurementPoint === area.type);
        const areaReadings = meters.flatMap(d => readingsByDevice[d.id] || []);
        const lastReading = areaReadings.reduce((last, r) => (!last || r.timestamp > last.timestamp ? r : last), null);
        return {
            id: area.id, name: area.name, type: area.type,
            monitored: meters.length > 0,
            metersOnline: meters.filter(d => d.isOnline).length,
            metersTotal: meters.length,
            lastReadingAt: lastReading?.timestamp ?? null,
            ...consumption(areaReadings),
        };
    });

    const totals = rows.reduce((acc, r) => ({ waterM3: acc.waterM3 + r.waterM3, energyKwh: acc.energyKwh + r.energyKwh }), { waterM3: 0, energyKwh: 0 });
    totals.waterM3 = round2(totals.waterM3);
    totals.energyKwh = round2(totals.energyKwh);

    rows.forEach(r => {
        r.waterShare = totals.waterM3 ? round2((r.waterM3 / totals.waterM3) * 100) : 0;
        r.energyShare = totals.energyKwh ? round2((r.energyKwh / totals.energyKwh) * 100) : 0;
    });

    return { rows, totals, monitoredCount: rows.filter(r => r.monitored).length };
}

/** Serie diaria del mes (día "01".."31") para agua o energía, solo de los medidores de áreas comunes */
export function dailySeries(readings, metric = 'water') {
    const unit = metric === 'water' ? 'L/min' : 'A';
    const byDay = {};
    for (const r of readings) if (r.unit === unit) (byDay[r.timestamp.slice(8, 10)] ||= []).push(r);
    const labels = Object.keys(byDay).sort();
    const key = metric === 'water' ? 'waterM3' : 'energyKwh';
    return { labels, values: labels.map(d => consumption(byDay[d])[key]) };
}

/** Variación porcentual respecto al mes anterior; null si no hay base de comparación */
export function changePct(current, previous) {
    if (!previous) return null;
    return Math.round(((current - previous) / previous) * 1000) / 10;
}
