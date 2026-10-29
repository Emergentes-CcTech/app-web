/**
 * User Entity
 * @class
 */
export class User {
    /**
     * Constructor
     * @param id - User ID
     * @param email - User Email
     * @param role - User Role (administrator | resident)
     */
    constructor({ id = null, email = '', role = 'resident' }) {
        this.id = id;
        this.email = email;
        this.role = role;
    }
}
