<script setup>
import { defineAsyncComponent, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import ProfileEditor from '../components/profile-editor.vue';

const AuditView = defineAsyncComponent(() => import('../../../audit/presentation/views/audit.view.vue'));

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const validTabs = ['profile', 'audit'];
const initialTab = (route.query.tab && validTabs.includes(route.query.tab))
  ? route.query.tab
  : 'profile';

const activeTab = ref(initialTab);

watch(() => route.query.tab, (val) => {
  if (val && validTabs.includes(val)) {
    activeTab.value = val;
  }
});

function onTabChange(tab) {
  activeTab.value = tab;
  router.replace({ query: { ...route.query, tab } }).catch(() => {});
}
</script>

<template>
  <div class="profile-admin-container">
    <pv-tabs :value="activeTab" @update:value="onTabChange" class="profile-admin-tabs">
      <pv-tab-list class="tabs-header mb-4">
        <pv-tab value="profile">
          <i class="pi pi-user mr-2"></i>{{ t('profile.accountInformation') }}
        </pv-tab>
        <pv-tab value="audit">
          <i class="pi pi-history mr-2"></i>{{ t('audit.title') }}
        </pv-tab>
      </pv-tab-list>

      <pv-tab-panels>
        <pv-tab-panel value="profile">
          <div v-if="activeTab === 'profile'">
            <ProfileEditor variant="administrator" />
          </div>
        </pv-tab-panel>
        <pv-tab-panel value="audit">
          <div v-if="activeTab === 'audit'">
            <AuditView />
          </div>
        </pv-tab-panel>
      </pv-tab-panels>
    </pv-tabs>
  </div>
</template>

<style scoped>
.profile-admin-container {
  max-width: 1200px;
  margin: 0 auto;
}
.profile-admin-tabs {
  background: transparent;
}
.tabs-header {
  border-bottom: 1px solid #e2e8f0;
}
.mb-4 {
  margin-bottom: 1.5rem;
}
.mr-2 {
  margin-right: 0.5rem;
}
</style>
