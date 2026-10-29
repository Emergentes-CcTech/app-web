import { BaseApi } from "../../shared/infrastructure/base-api.js";

const usersEndpoint = import.meta.env.VITE_USERS_ENDPOINT_PATH;
const sessionsEndpoint = import.meta.env.VITE_IAM_SESSIONS_PATH || import.meta.env.VITE_AUTH_ENDPOINT_PATH || '/sessions';

/**
 * IAM API class to interact with authentication endpoints
 * @extends BaseApi
 * @class
 */
export class IamApi extends BaseApi {
    constructor() {
        super();
    }

    /**
     * Sign in with email and password
     * @param {Object} signInResource - Object containing email and password
     * @returns {Promise} Response with authenticated user and token
     */
    signIn(signInResource) {
        return this.http.post(sessionsEndpoint, signInResource);
    }

    /**
     * Sign up with email, password and role
     * @param {Object} signUpResource - Object containing email, password and role
     * @returns {Promise} Response with created user
     */
    signUp(signUpResource) {
        return this.http.post(`${usersEndpoint}`, signUpResource);
    }

    /**
     * Sign out by revoking the current bearer token server-side.
     * Backend contract: DELETE /api/v1/sessions/current -> 204.
     * @returns {Promise} Response with no content
     */
    signOut() {
        return this.http.delete(`${sessionsEndpoint}/current`);
    }

    /**
     * Get all users
     * @returns {Promise} Response with all users
     */
    getUsers() {
        return this.http.get(usersEndpoint);
    }

    /**
     * Get user by ID
     * @param {number} id - User ID
     * @returns {Promise} Response with user data
     */
    getUserById(id) {
        return this.http.get(`${usersEndpoint}/${id}`);
    }

    /**
     * Check whether an email is already registered
     * @param {string} email - Email address to check
     * @returns {Promise} Response with invitation/assignment details
     */
    checkInvitation(email) {
        return this.http.get('/authentication/invitation', { params: { email } });
    }

    /**
     * Change the password of the current user (security section).
     * Contract assumption: PATCH /users/{id}/password { currentPassword, newPassword }.
     */
    changePassword(userId, { currentPassword, newPassword }) {
        return this.http.patch(`${usersEndpoint}/${userId}/password`, { currentPassword, newPassword });
    }

    /** Active sessions of the current user. Contract assumption: GET /sessions?userId= */
    getSessions(userId) {
        return this.http.get(sessionsEndpoint, { params: { userId } });
    }

    /** Close one session. Contract assumption: DELETE /sessions/{id} */
    revokeSession(sessionId) {
        return this.http.delete(`${sessionsEndpoint}/${sessionId}`);
    }
}
