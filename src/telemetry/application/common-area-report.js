const round2 = (n) => Math.round(n * 100) / 100;

/**
 * Totales de un conjunto de consumos por medidor (respuesta de /analytics/projects/{id}/device-consumption).
 * La impulsión de la cisterna (countsTowardTotal = false) no suma: ese agua ya pasó por el medidor de ingreso.
 */
export function sumTotals(deviceTotals) {
    let waterM3 = 0, energyKwh = 0;
    for (const d of deviceTotals) {
        if (d.countsTowardTotal !== false) waterM3 += Number(d.waterM3) || 0;
        energyKwh += Number(d.energyKwh) || 0;
    }
    return { waterM3: round2(waterM3), energyKwh: round2(energyKwh) };
}

/**
 * Reporte por área común. Un dispositivo mide un área cuando su punto de medición
 * es igual al tipo del área. Las áreas sin medidor se listan como no monitoreadas.
 */
export function buildCommonAreaReport(commonAreas, devices, deviceTotals) {
    const totalsByDevice = {};
    for (const d of deviceTotals) (totalsByDevice[d.deviceId] ||= []).push(d);

    const rows = commonAreas.map(area => {
        const meters = devices.filter(d => d.type !== 'gateway' && d.measurementPoint === area.type);
        const areaTotals = meters.flatMap(d => totalsByDevice[d.id] || []);
        const lastReadingAt = areaTotals.reduce((last, d) => (!last || d.lastReadingAt > last ? d.lastReadingAt : last), null);
        return {
            id: area.id, name: area.name, type: area.type,
            monitored: meters.length > 0,
            metersOnline: meters.filter(d => d.isOnline).length,
            metersTotal: meters.length,
            lastReadingAt,
            ...sumTotals(areaTotals),
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

/** Serie diaria del mes (día "01".."31") para agua o energía, a partir de /daily-consumption */
export function dailySeries(days, monthKey, metric = 'water') {
    const list = days.filter(d => d.date.startsWith(monthKey));
    const key = metric === 'water' ? 'waterM3' : 'energyKwh';
    return { labels: list.map(d => d.date.slice(8, 10)), values: list.map(d => round2(Number(d[key]) || 0)) };
}

/** Variación porcentual respecto al mes anterior; null si no hay base de comparación */
export function changePct(current, previous) {
    if (!previous) return null;
    return Math.round(((current - previous) / previous) * 1000) / 10;
}
