export class Profile {
  constructor({ id = 0, userId = 0, role = '', email = '', phoneNumber = '', secondEmail = '', photoUrl = '',
                // administrador
                companyName = '', ruc = '', legalRepresentative = '', address = '',
                // residente
                fullName = '', dni = '',
                notificationPrefs = null } = {}) {
    const defaults = { email: true, sms: false, criticalAlerts: true, billingReminders: true, newSettlement: true, dueDates: true, condominiumAlerts: true };
    Object.assign(this, { id, userId, role, email, phoneNumber, secondEmail, photoUrl,
      companyName, ruc, legalRepresentative, address, fullName, dni,
      notificationPrefs: { ...defaults, ...(notificationPrefs || {}) } });
  }

  get displayName() { return this.companyName || this.fullName || this.email; }
}
