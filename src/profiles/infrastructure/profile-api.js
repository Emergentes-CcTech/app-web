import { BaseApi } from "../../shared/infrastructure/base-api.js";

const profilesEndpoint = import.meta.env.VITE_PROFILES_ENDPOINT_PATH;

/**
 * Profile API class to interact with profiles endpoints
 * @extends BaseApi
 * @class
 */
export class ProfileApi extends BaseApi {
  constructor() {
    super();
  }

  /**
   * Get profile by user ID
   * @param {number} userId - User ID
   * @returns {Promise} Response with profile data
   */
  async getProfile(userId) {
    if (!userId) throw new Error('User ID is required for fetching profile.');

    try {
      const response = await this.http.get(profilesEndpoint, { params: { userId } });
      // Backend returns an array of profiles, get the first one
      return Array.isArray(response.data) ? response.data[0] : null;
    } catch (error) {
      console.error('Error fetching profile:', error);
      throw error;
    }
  }

  /**
   * Create a new profile
   * @param {Object} profileResource - Profile data
   * @returns {Promise} Response with created profile
   */
  async createProfile(profileResource) {
    try {
      const response = await this.http.post(profilesEndpoint, profileResource);
      return response.data;
    } catch (error) {
      console.error('Error creating profile:', error);
      throw error;
    }
  }

  /**
   * Update an existing profile
   * @param {number} profileId - Profile ID
   * @param {Object} updatedData - Updated profile data
   * @returns {Promise} Response with updated profile
   */
  async updateProfile(profileId, updatedData) {
    if (!profileId) throw new Error('Profile ID is required for updating profile.');

    try {
      const response = await this.http.put(`${profilesEndpoint}/${profileId}`, updatedData);
      return response.data;
    } catch (error) {
      console.error('Error updating profile:', error);
      throw error;
    }
  }
}
