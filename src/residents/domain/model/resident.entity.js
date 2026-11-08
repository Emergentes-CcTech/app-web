export class Resident {
    constructor({ id = null, userId = null, fullName = '', dni = '', email = '', phoneNumber = '',
                  condominiumId = null, condominiumName = '', unitId = null, unitNumber = '',
                  relation = 'owner', collectionStatus = 'current', aliquotPercentage = null, photoUrl = null } = {}) {
        Object.assign(this, { id, userId, fullName, dni: dni || '', email: email || '', phoneNumber: phoneNumber || '', condominiumId,
            condominiumName, unitId, unitNumber, relation, collectionStatus,
            aliquotPercentage: aliquotPercentage === null || aliquotPercentage === undefined ? null : Number(aliquotPercentage), photoUrl });
    }
}
