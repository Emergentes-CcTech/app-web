export const ROLES = Object.freeze({
    ADMIN: 'administrator',
    RESIDENT: 'resident',
});

// Acepta los valores del backend (PropertyManager) y los viejos durante la migración.
// Cuando el backend esté migrado, elimina 'builder' y 'owner'.
export function normalizeRole(raw) {
    const r = String(raw ?? '').trim().toLowerCase();
    if (['administrator', 'propertymanager', 'property_manager', 'admin', 'builder'].includes(r)) return ROLES.ADMIN;
    if (['resident', 'residente', 'owner', 'propietario'].includes(r)) return ROLES.RESIDENT;
    return '';
}

export const isAdmin = (role) => normalizeRole(role) === ROLES.ADMIN;
export const isResident = (role) => normalizeRole(role) === ROLES.RESIDENT;
