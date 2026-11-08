/**
 * Centralized validation utility for IoBuild frontend forms.
 * Provides pure validator functions for the condominium domain.
 */

// Email regex according to RFC 5322 standard (simplified)
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
// Phone: optional '+' prefix, digits, spaces, hyphens, parentheses; 7 to 15 digits
const PHONE_REGEX = /^\+?[0-9\s\-()]{7,20}$/;
// MAC address: 6 pairs of hex digits separated by colon or hyphen, or 12 continuous hex chars
const MAC_REGEX = /^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^[0-9A-Fa-f]{12}$/;
const RUC_REGEX = /^(10|15|17|20)\d{9}$/;
const DNI_REGEX = /^\d{8}$/;
// RF-001: mínimo 8 caracteres, mayúscula, minúscula, número y carácter especial
const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/;
// Código de suministro (agua / luz): 6 a 12 dígitos
const SUPPLY_CODE_REGEX = /^\d{6,12}$/;

export function isValidEmail(email) {
    if (!email || typeof email !== 'string') return false;
    return EMAIL_REGEX.test(email.trim());
}

/** RF-001: el correo se guarda normalizado en minúsculas. */
export const normalizeEmail = (v) => String(v ?? '').trim().toLowerCase();

export function isValidPhone(phone) {
    if (!phone || typeof phone !== 'string') return false;
    const trimmed = phone.trim();
    if (!PHONE_REGEX.test(trimmed)) return false;
    const digitsOnly = trimmed.replace(/\D/g, '');
    return digitsOnly.length >= 7 && digitsOnly.length <= 15;
}

export function isValidName(name, minLength = 2, maxLength = 120) {
    if (!name || typeof name !== 'string') return false;
    const trimmed = name.trim();
    return trimmed.length >= minLength && trimmed.length <= maxLength;
}

export const isValidRuc = (v) => RUC_REGEX.test(String(v ?? '').trim());
export const isValidDni = (v) => DNI_REGEX.test(String(v ?? '').trim());
export const isValidSupplyCode = (v) => SUPPLY_CODE_REGEX.test(String(v ?? '').trim());

/** RF-001: contraseña fuerte. */
export const isValidPassword = (v) => STRONG_PASSWORD.test(String(v ?? ''));

/** Validates a MAC address (optional; if empty returns true). */
export function isValidMacAddress(mac) {
    if (!mac || typeof mac !== 'string' || !mac.trim()) return true;
    return MAC_REGEX.test(mac.trim());
}

export function isValidPositiveInteger(val, min = 1, max = 100000) {
    if (val === null || val === undefined || val === '') return false;
    const num = Number(val);
    return !isNaN(num) && Number.isInteger(num) && num >= min && num <= max;
}

/** Porcentaje de participación: 0.01 a 100, hasta 2 decimales. */
export function isValidPercentage(val) {
    if (val === null || val === undefined || val === '') return false;
    const num = Number(val);
    // Ley N.° 27157: la alícuota se expresa con hasta 4 decimales
    if (isNaN(num) || num < 0.0001 || num > 100) return false;
    return Math.round(num * 10000) / 10000 === num;
}

export function isValidUrl(url) {
    if (!url || typeof url !== 'string' || !url.trim()) return true;
    try {
        const parsed = new URL(url.trim());
        return parsed.protocol === 'http:' || parsed.protocol === 'https:';
    } catch {
        return false;
    }
}

/** Cross-field: password confirmation must match (case-sensitive, no trim). */
export function doPasswordsMatch(password, confirmation) {
    if (typeof password !== 'string' || typeof confirmation !== 'string') return false;
    if (!confirmation) return false;
    return password === confirmation;
}

/** Business rule: new password must differ from current password. */
export function isNewPasswordDifferent(currentPassword, newPassword) {
    if (typeof currentPassword !== 'string' || typeof newPassword !== 'string') return false;
    if (!currentPassword || !newPassword) return false;
    return currentPassword !== newPassword;
}

// RF-006: la suma de participaciones debe ser exactamente 100 (comparar en centésimas)
// Suma en diezmilésimas para evitar errores de coma flotante; el backend acepta ±0.0001 sobre 100.0000 %
export const sumParticipation = (units) =>
    Math.round(units.reduce((acc, u) => acc + Number(u.participationPct || 0) * 10000, 0)) / 10000;
export const isParticipationBalanced = (units) => Math.abs(sumParticipation(units) - 100) <= 0.0001;
