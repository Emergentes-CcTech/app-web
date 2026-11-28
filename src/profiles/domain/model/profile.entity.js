export class Profile {
  constructor({ id = 0, userId = 0, role = '', email = '', phoneNumber = '', secondEmail = '', photoUrl = '',
                // administrador
                companyName = '', legalRepresentative = '', address = '',
                // residente
                fullName = '' } = {}) {
    Object.assign(this, { id, userId, role, email, phoneNumber, secondEmail, photoUrl,
      companyName, legalRepresentative, address, fullName });
  }

  get displayName() { return this.companyName || this.fullName || this.email; }
}
