<script setup>
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';

defineProps({
  condominium: { type: Object, required: true },
  // Suscripción del condominio (null si aún no tiene plan) y nombre del plan
  subscription: { type: Object, default: null },
  planName: { type: String, default: '' },
});
defineEmits(['open']);
const { t } = useI18n();
const imageBroken = ref(false);
</script>

<template>
  <article class="condo-card">
    <div class="media">
      <img v-if="condominium.imageUrl && !imageBroken" :src="condominium.imageUrl" :alt="condominium.name" loading="lazy" @error="imageBroken = true" />
      <div v-else class="media-empty"><i class="pi pi-building" aria-hidden="true" /></div>
      <pv-tag class="status" :severity="condominium.isActive ? 'success' : 'secondary'"
              :value="condominium.isActive ? t('condominiums.status.active') : t('condominiums.status.inactive')" />
    </div>

    <div class="body">
      <div>
        <h3 :title="condominium.name">{{ condominium.name }}</h3>
        <p v-if="condominium.locationLabel" class="location"><i class="pi pi-map-marker" aria-hidden="true" /> {{ condominium.locationLabel }}</p>
      </div>

      <dl class="stats">
        <div><dt>{{ t('condominiums.card.units') }}</dt><dd>{{ condominium.totalUnits }}</dd></div>
        <div><dt>{{ t('condominiums.card.areas') }}</dt><dd>{{ condominium.commonAreasCount }}</dd></div>
      </dl>

      <div class="occupancy">
        <div class="occupancy-label">
          <span>{{ t('condominiums.card.registered') }}</span>
          <span class="num">{{ condominium.occupiedUnits }} / {{ condominium.totalUnits }}</span>
        </div>
        <div class="track" role="progressbar" :aria-valuenow="condominium.occupancyRate" aria-valuemin="0" aria-valuemax="100">
          <div class="fill" :style="{ width: `${condominium.occupancyRate}%` }"></div>
        </div>
      </div>

      <div class="plan">
        <template v-if="subscription">
          <span>{{ t('condominiums.card.plan', { plan: planName || '—' }) }}</span>
          <pv-tag :severity="subscription.isActive() ? 'success' : 'warn'"
                  :value="subscription.isActive() ? t('subscriptions.status-active') : t('subscriptions.status-cancelled')" />
        </template>
        <span v-else class="muted">{{ t('condominiums.noSubscription') }}</span>
      </div>

      <pv-button :label="t('condominiums.card.view')" icon="pi pi-arrow-right" iconPos="right" size="small" class="w-full" @click="$emit('open', condominium)" />
    </div>
  </article>
</template>

<style scoped>
.condo-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden; display: flex; flex-direction: column; transition: box-shadow .15s, border-color .15s; }
.condo-card:hover { box-shadow: 0 6px 16px rgba(0,0,0,.08); border-color: #a7f3d0; }
.media { position: relative; height: 140px; background: #ECFDF5; }
.media img { width: 100%; height: 100%; object-fit: cover; }
.media-empty { height: 100%; display: grid; place-items: center; color: #10B981; }
.media-empty .pi { font-size: 2.5rem; }
.status { position: absolute; top: .75rem; right: .75rem; }
.body { padding: 1rem 1.1rem 1.1rem; display: flex; flex-direction: column; gap: .9rem; flex: 1; }
h3 { margin: 0; font-size: 1.1rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.location { margin: .25rem 0 0; color: #6B7280; font-size: .875rem; }
.stats { margin: 0; display: grid; grid-template-columns: 1fr 1fr; gap: .5rem; }
.stats div { background: #f9fafb; border-radius: 8px; padding: .5rem .75rem; }
.stats dt { font-size: .75rem; color: #6B7280; }
.stats dd { margin: 0; font-size: 1.25rem; font-weight: 700; font-variant-numeric: tabular-nums; }
.occupancy-label { display: flex; justify-content: space-between; font-size: .8rem; color: #6B7280; margin-bottom: .35rem; }
.num { font-variant-numeric: tabular-nums; font-weight: 600; color: #111827; }
.track { height: 6px; background: #e5e7eb; border-radius: 3px; overflow: hidden; }
.fill { height: 100%; background: #10B981; }
.plan { display: flex; align-items: center; justify-content: space-between; gap: .5rem; font-size: .875rem; margin-top: auto; }
.muted { color: #6B7280; }
</style>
