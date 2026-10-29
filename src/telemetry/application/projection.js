// Proyección lineal: lo consumido hasta hoy extendido a todos los días del mes.
export function projectToMonthEnd(consumedToDate, today = new Date()) {
    const day = today.getDate();
    const daysInMonth = new Date(today.getFullYear(), today.getMonth() + 1, 0).getDate();
    return day === 0 ? 0 : Math.round((consumedToDate / day) * daysInMonth * 100) / 100;
}

// Supuestos de conversión (lecturas cada 1 min): L/min → m³ y A (monofásico 230 V) → kWh
export const SAMPLE_MINUTES = 1;
export const GRID_VOLTAGE = 230;
export const litersPerMinToM3 = (lpm, minutes = SAMPLE_MINUTES) => (lpm * minutes) / 1000;
export const ampsToKwh = (amps, minutes = SAMPLE_MINUTES) => (amps * GRID_VOLTAGE * (minutes / 60)) / 1000;
