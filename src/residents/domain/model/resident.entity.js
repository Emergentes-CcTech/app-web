export class Resident {
    constructor({ id = null, userId = null, fullName = '', dni = '', email = '', phoneNumber = '',
                  condominiumId = null, condominiumName = '', unitId = null, unitNumber = '',
                  relation = 'owner', collectionStatus = 'current', aliquotPercentage = null, photoUrl = null,
                  accountStatus = 'active' } = {}) {
        Object.assign(this, { id, userId, fullName, dni: dni || '', email: email || '', phoneNumber: phoneNumber || '', condominiumId,
            condominiumName, unitId, unitNumber, relation, collectionStatus, accountStatus,
            aliquotPercentage: aliquotPercentage === null || aliquotPercentage === undefined ? null : Number(aliquotPercentage), photoUrl });
    }

    // Pre-registrado por la administración, aún sin cuenta en IoBuild
    get isPending() { return this.accountStatus === 'pending'; }
}
