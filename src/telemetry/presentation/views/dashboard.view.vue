<script setup>
import { computed } from 'vue';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { isAdmin } from '../../../shared/infrastructure/roles.js';
import AdminDashboard from '../components/admin-dashboard.component.vue';
import ResidentDashboard from '../components/resident-dashboard.component.vue';

const iam = useIamStore();
const admin = computed(() => isAdmin(iam.currentUser?.role));
</script>

<template>
  <AdminDashboard v-if="admin" />
  <ResidentDashboard v-else />
</template>
