<template>
  <div class="profile-router-view">

    <div v-if="store.isLoading" class="loading-state" style="padding: 2rem; text-align: center;">
        <p></p>
    </div>

    <div v-else-if="store.errors.length > 0">
        <pv-message severity="error" style="margin: 2rem;">
            <ul style="list-style: none; padding: 0;">
                <li v-for="(error, index) in store.errors" :key="index">{{ error }}</li>
            </ul>
        </pv-message>
    </div>

    <div v-else-if="store.viewType === 'resident' && store.profileLoaded">
      <ProfileResidentView />
    </div>

    <div v-else-if="store.viewType === 'administrator' && store.profileLoaded">
      <ProfileAdministratorView />
    </div>

    <div v-else style="padding: 2rem; text-align: center;">
      <pv-message severity="warn"></pv-message>
    </div>
  </div>
</template>

<script setup>
import { useProfileStore } from '../../application/profile.store.js';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { onMounted, watch } from 'vue';
import ProfileResidentView from './profile-resident.vue';
import ProfileAdministratorView from './profile-administrator.vue';

const store = useProfileStore();
const iamStore = useIamStore();

// Function to load profile
const loadProfile = () => {
    const userId = iamStore.currentUser?.id;
    if (userId) {
        console.log('profile-router: Loading profile for userId', userId);
        store.fetchProfile(userId);
    } else {
        store.isLoading = false;
        store.errors.push("No authenticated user found. Please log in.");
    }
};

// Load profile on mount
onMounted(() => {
    loadProfile();
});

// Watch for user changes (e.g., after logout/login with different user)
watch(
    () => iamStore.currentUser?.id,
    (newUserId, oldUserId) => {
        if (newUserId && newUserId !== oldUserId) {
            console.log('profile-router: User changed, reloading profile', { from: oldUserId, to: newUserId });
            // Reset profileLoaded to force reload
            store.profileLoaded = false;
            loadProfile();
        }
    }
);
</script>

<style scoped>
.profile-router-view {
    min-height: 80vh;
}
</style>