/**
 * Authenticated User Entity
 * @class
 */
export class AuthenticatedUser {
    /**
     * Constructor
     * @param id - User ID
     * @param email - User Email
     * @param role - User Role
     * @param token - JWT Token
     */
    constructor({ id = null, email = '', role = '', token = '' }) {
        this.id = id;
        this.email = email;
        this.role = role;
        this.token = token;
    }
}
