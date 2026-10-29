import { CURRENT_USER_KEY, TOKEN_KEY } from "../../shared/infrastructure/storage-keys.js";

/**
 * IAM Facade for Profiles Bounded Context
 * Anti-Corruption Layer (ACL) between Profiles and IAM bounded contexts
 * This facade provides a clean interface to access user data from IAM
 * without coupling to IAM's internal structure
 */
export class IamFacade {
    /**
     * Get the current authenticated user's email
     * @returns {string|null} User email or null if not available
     */
    static getCurrentUserEmail() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                return user?.email || null;
            } catch (e) {
                console.error('[Profiles IamFacade] Error parsing stored user:', e);
                return null;
            }
        }
        return null;
    }

    /**
     * Get the current authenticated user's ID
     * @returns {number|null} User ID or null if not authenticated
     */
    static getCurrentUserId() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                return user?.id || null;
            } catch (e) {
                console.error('[Profiles IamFacade] Error parsing stored user:', e);
                return null;
            }
        }
        return null;
    }

    /**
     * Get the current authenticated user's username
     * @returns {string|null} Username or null if not available
     */
    static getCurrentUserUsername() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                return user?.username || null;
            } catch (e) {
                console.error('[Profiles IamFacade] Error parsing stored user:', e);
                return null;
            }
        }
        return null;
    }

    /**
     * Get the current authenticated user's role
     * @returns {string|null} User role or null if not available
     */
    static getCurrentUserRole() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                return user?.role || null;
            } catch (e) {
                console.error('[Profiles IamFacade] Error parsing stored user:', e);
                return null;
            }
        }
        return null;
    }

    /**
     * Check if user is authenticated
     * @returns {boolean} True if user has valid authentication
     */
    static isAuthenticated() {
        const token = localStorage.getItem(TOKEN_KEY);
        const currentUser = localStorage.getItem(CURRENT_USER_KEY);
        return !!(token && currentUser);
    }
}

