/**
 * Centralized application constants — avoids magic numbers scattered across stores and views.
 * Values that are deployment-specific are overridable via VITE_* env vars.
 */

// RNF-012: una lectura debe reflejarse en < 5 s y una alerta crítica notificarse en < 60 s.
export const LIVE_POLLING_MS = Number(import.meta.env.VITE_POLLING_INTERVAL_MS) || 5000;

export const ALERT_POLLING_MS = 15000;

// Diferencia (facturado vs medido) a partir de la cual se muestra una advertencia en la conciliación (no bloquea).
export const RECONCILIATION_WARNING_PCT = 5;

export const RETRY_DELAY_SHORT_MS = 1500;

export const RETRY_DELAY_LONG_MS = 2500;

export const TOAST_SUCCESS_DURATION_MS = 2500;

export const TOAST_ERROR_DURATION_MS = 3000;

export const TOAST_INVOICE_ERROR_DURATION_MS = 4000;

export const TOAST_AUTH_ERROR_DURATION_MS = 5000;

export const CLOUDINARY_WIDGET_URL =
    import.meta.env.VITE_CLOUDINARY_WIDGET_URL || 'https://widget.cloudinary.com/v2.0/global/all.js';

export const APP_URL = (typeof window !== 'undefined' && window.location?.origin)
    ? window.location.origin
    : (import.meta.env.VITE_APP_URL || 'http://localhost');
