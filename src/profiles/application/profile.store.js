import { defineStore } from "pinia";
import { ref } from "vue";
import { ProfileAssembler } from "../infrastructure/profile.assembler.js";
import { ProfileApi } from "../infrastructure/profile-api.js";
import { Profile } from "../domain/model/profile.entity.js";
import { useIamStore } from "../../iam/application/iam.store.js";
import { normalizeRole, ROLES } from "../../shared/infrastructure/roles.js";

const profileApi = new ProfileApi();

export const useProfileStore = defineStore('profile', () => {
    const profile = ref(new Profile({}));
    const viewType = ref('');
    const isLoading = ref(false);
    const profileLoaded = ref(false);
    const errors = ref([]);

    function syncWithIamStore(profileEntity) {
        if (!profileEntity) return;
        try {
            const iamStore = useIamStore();
            if (profileEntity.displayName || profileEntity.photoUrl) {
                iamStore.updateUserProfile({
                    name: profileEntity.displayName,
                    photoUrl: profileEntity.photoUrl
                });
            }
        } catch (_) {
            // Pinia circular dependency safeguard
        }
    }

    async function fetchProfile(userId) {
        isLoading.value = true;
        errors.value = [];

        try {
            const response = await profileApi.getProfile(userId);
            const { profileEntity } = ProfileAssembler.toDomainFromResponse(response);

            profile.value = profileEntity;
            viewType.value = normalizeRole(profileEntity.role) || ROLES.RESIDENT;
            profileLoaded.value = true;
            syncWithIamStore(profileEntity);

        } catch (error) {
            console.error('', error);
            errors.value.push(error.message || '');
            profileLoaded.value = false;

        } finally {
            isLoading.value = false;
        }
    }

    async function createProfile(profileData) {
        isLoading.value = true;
        errors.value = [];

        try {
            let response;
            try {
                response = await profileApi.createProfile(profileData);
            } catch (error) {
                // El alta de usuario ya deja un perfil provisional (409): se completa con PUT en vez de duplicarlo
                if (error.response?.status !== 409) throw error;
                const existing = await profileApi.getProfile(profileData.userId);
                if (!existing?.id) throw error;
                response = await profileApi.updateProfile(existing.id, profileData);
            }
            const { profileEntity } = ProfileAssembler.toDomainFromResponse(response);
            profile.value = profileEntity;
            viewType.value = normalizeRole(profileEntity.role) || ROLES.RESIDENT;
            profileLoaded.value = true;
            syncWithIamStore(profileEntity);
            return response;
        } catch (error) {
            console.error('Error creating profile:', error);
            errors.value.push(error.message || 'Error creating profile');
            throw error;
        } finally {
            isLoading.value = false;
        }
    }

    async function updateProfile(profileId, profileData) {
        isLoading.value = true;
        errors.value = [];

        try {
            const response = await profileApi.updateProfile(profileId, profileData);
            const { profileEntity } = ProfileAssembler.toDomainFromResponse(response);
            profile.value = profileEntity;
            viewType.value = normalizeRole(profileEntity.role) || ROLES.RESIDENT;
            profileLoaded.value = true;
            syncWithIamStore(profileEntity);
            return response;
        } catch (error) {
            console.error('Error updating profile:', error);
            errors.value.push(error.message || 'Error updating profile');
            throw error;
        } finally {
            isLoading.value = false;
        }
    }

    /** RF-003: preferencias de notificación (se persisten de inmediato). */
    async function updateNotificationPrefs(prefs) {
        const merged = { ...profile.value.notificationPrefs, ...prefs };
        return updateProfile(profile.value.id, { ...ProfileAssembler.toResource(profile.value), notificationPrefs: merged });
    }

    return {
        profile,
        viewType,
        isLoading,
        profileLoaded,
        errors,
        fetchProfile,
        createProfile,
        updateProfile,
        updateNotificationPrefs
    };
});