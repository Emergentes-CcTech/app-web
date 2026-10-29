import { User } from "../domain/model/user.entity.js";

/**
 * User Assembler
 * Transform user data from API responses to User entities
 */
export class UserAssembler {
    /**
     * Transform a single user response to User entity
     * @param {Object} data - User data from API
     * @returns {User} User entity
     */
    static toEntityFromResponse(data) {
        return new User({
            id: data.id,
            email: data.email,
            role: data.role
        });
    }

    /**
     * Transform an array of user responses to User entities
     * @param {Array} data - Array of user data from API
     * @returns {Array<User>} Array of User entities
     */
    static toEntitiesFromResponse(data) {
        return data.map(item => this.toEntityFromResponse(item));
    }

    /**
     * Transform User entity to API resource format
     * @param {User} entity - User entity
     * @returns {Object} User resource for API
     */
    static toResourceFromEntity(entity) {
        return {
            id: entity.id,
            email: entity.email,
            role: entity.role
        };
    }
}
