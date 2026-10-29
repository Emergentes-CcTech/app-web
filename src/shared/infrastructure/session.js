import { CURRENT_USER_KEY } from './storage-keys.js';

/** Usuario autenticado persistido por IAM (lectura simple, sin acoplar a su store). */
export function getSessionUser() {
    try {
        return JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
    } catch {
        return null;
    }
}

export const getSessionUserId = () => getSessionUser()?.id ?? null;

/** Convierte "YYYY-MM" en una etiqueta legible (es/en) */
export function formatPeriod(period, locale = 'es') {
    if (!period) return '';
    const [y, m] = String(period).split('-').map(Number);
    return new Date(y, (m || 1) - 1, 1).toLocaleDateString(locale === 'en' ? 'en-US' : 'es-PE', { month: 'long', year: 'numeric' });
}

export const formatPen = (n) => `S/ ${Number(n || 0).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
