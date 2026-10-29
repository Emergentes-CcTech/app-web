export class Resident {
    constructor({ id = null, userId = null, fullName = '', dni = '', email = '', phoneNumber = '',
                  condominiumId = null, condominiumName = '', unitId = null, unitNumber = '',
                  relation = 'owner', collectionStatus = 'current' } = {}) {
        Object.assign(this, { id, userId, fullName, dni, email, phoneNumber, condominiumId,
            condominiumName, unitId, unitNumber, relation, collectionStatus });
    }
}
