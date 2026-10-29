import { AuthenticatedUser } from "../domain/model/authenticated-user.entity.js";

/**
 * Authenticated User Assembler
 * Transform authenticated user data from API responses to AuthenticatedUser entities
 */
export class AuthenticatedUserAssembler {
    /**
     * Transform authenticated user response to AuthenticatedUser entity
     * @param {Object} data - Authenticated user data from API
     * @returns {AuthenticatedUser} AuthenticatedUser entity
     */
    static toEntityFromResponse(data) {
        return new AuthenticatedUser({
            id: data.id,
            email: data.email,
            role: data.role,
            token: data.token
        });
    }
}
