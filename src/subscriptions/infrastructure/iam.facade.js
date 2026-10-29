import { CURRENT_USER_KEY, TOKEN_KEY } from "../../shared/infrastructure/storage-keys.js";

/**
 * IAM Facade
 * Anti-Corruption Layer (ACL) between Subscriptions and IAM bounded contexts
 * This facade provides a clean interface to access user authentication data
 * without coupling to IAM's internal structure
 */
export class IamFacade {
    /**
     * Get the current authenticated user's ID
     * @returns {number} User ID (administratorId)
     * @throws {Error} If user is not authenticated
     */
    static getCurrentUserId() {
        // Try to get from localStorage (IAM stores it here after authentication)
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);

        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                if (user?.id) {
                    return user.id;
                }
            } catch (e) {
                console.error('[IamFacade] Error parsing stored user:', e);
            }
        }

        // Try to get token to verify if user is authenticated
        const token = localStorage.getItem(TOKEN_KEY);
        if (!token) {
            throw new Error('Usuario no autenticado. Por favor inicia sesión.');
        }

        // If token exists but no user data, something is wrong
        throw new Error('Datos de usuario no disponibles. Por favor inicia sesión nuevamente.');
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

    /**
     * Get current user email
     * @returns {string|null} User email or null if not authenticated
     */
    static getCurrentUserEmail() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                return user?.email || null;
            } catch (e) {
                console.error('[IamFacade] Error parsing stored user:', e);
                return null;
            }
        }
        return null;
    }

    /**
     * Get current user role
     * @returns {string|null} User role or null if not authenticated
     */
    static getCurrentUserRole() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        if (storedUser) {
            try {
                const user = JSON.parse(storedUser);
                return user?.role || null;
            } catch (e) {
                console.error('[IamFacade] Error parsing stored user:', e);
                return null;
            }
        }
        return null;
    }
}

