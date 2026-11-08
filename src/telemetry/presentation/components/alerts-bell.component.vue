<script setup>
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import { useTelemetryStore } from '../../application/telemetry.store.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';

// Campana del encabezado: muestra las alertas abiertas sin salir de la pantalla actual
const MAX_VISIBLE = 6;

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const telemetry = useTelemetryStore();
const panel = ref(null);
const failed = ref(false);

// Lo crítico primero y, dentro de cada grupo, lo más reciente
const sorted = computed(() => [...telemetry.openAlerts].sort((a, b) =>
  Number(b.isCritical) - Number(a.isCritical) || new Date(b.createdAt) - new Date(a.createdAt)));
const visible = computed(() => sorted.value.slice(0, MAX_VISIBLE));
const hidden = computed(() => sorted.value.length - visible.value.length);
const badge = computed(() => (telemetry.openAlertsCount > 99 ? '99+' : telemetry.openAlertsCount));

const typeLabel = (a) => t(`telemetry.alertTypes.${a.type}`, a.title || a.type);
const when = (a) => new Date(a.createdAt).toLocaleString(locale.value, { dateStyle: 'medium', timeStyle: 'short' });

async function acknowledge(alert) {
  failed.value = false;
  try {
    await telemetry.acknowledge(alert.id);
  } catch (_) {
    failed.value = true;
  }
}

async function viewAll() {
  panel.value?.hide();
  // Ya en el panel no cambia la ruta: se baja directo a la sección de alertas
  if (route.path === ROUTES.DASHBOARD) document.getElementById('alerts')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  else await router.push(ROUTES.ALERTS);
}
</script>

<template>
  <div class="bell">
    <pv-button
      class="notification-button"
      :aria-label="t('alertsBell.open', { n: telemetry.openAlertsCount })"
      aria-haspopup="dialog"
      icon="pi pi-bell"
      text
      rounded
      severity="secondary"
      @click="panel.toggle($event)"
    />
    <span v-if="telemetry.openAlertsCount > 0" class="badge" aria-hidden="true">{{ badge }}</span>

    <pv-popover ref="panel" class="bell-panel">
      <div class="panel">
        <div class="panel-head">
          <strong>{{ t('header.notifications') }}</strong>
          <small class="muted">{{ t('alertsBell.count', { n: telemetry.openAlertsCount }, telemetry.openAlertsCount) }}</small>
        </div>

        <p v-if="!visible.length" class="muted empty">{{ t('telemetry.noAlerts') }}</p>
        <ul v-else class="list">
          <li v-for="a in visible" :key="a.id" :class="{ critical: a.isCritical }">
            <i class="pi" :class="a.isCritical ? 'pi-exclamation-triangle' : 'pi-info-circle'" aria-hidden="true" />
            <div class="main">
              <strong>{{ typeLabel(a) }}</strong>
              <small class="muted">{{ when(a) }}</small>
              <small v-if="a.estimatedOvercostSoles" class="overcost">{{ t('dashboard.admin.overcost', { amount: `S/ ${a.estimatedOvercostSoles.toFixed(2)}` }) }}</small>
            </div>
            <pv-button :label="t('telemetry.acknowledge')" size="small" text @click="acknowledge(a)" />
          </li>
        </ul>

        <small v-if="failed" class="error" role="alert">{{ t('alertsBell.error') }}</small>
        <small v-if="hidden > 0" class="muted more">{{ t('alertsBell.more', { n: hidden }) }}</small>
        <pv-button :label="t('alertsBell.viewAll')" icon="pi pi-arrow-right" iconPos="right" size="small" outlined class="view-all" @click="viewAll" />
      </div>
    </pv-popover>
  </div>
</template>

<style scoped>
.bell { position: relative; display: inline-flex; }
.badge { position: absolute; top: 2px; right: 2px; min-width: 18px; height: 18px; padding: 0 4px; border-radius: 9px; background: #ef4444; color: #fff; font-size: 11px; display: flex; align-items: center; justify-content: center; pointer-events: none; }
.panel { width: min(22rem, calc(100vw - 2.5rem)); display: flex; flex-direction: column; gap: .6rem; }
.panel-head { display: flex; justify-content: space-between; align-items: baseline; gap: .75rem; }
.muted { color: #6B7280; }
.empty { margin: .5rem 0; }
.list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; max-height: 22rem; overflow-y: auto; }
.list li { display: flex; align-items: flex-start; gap: .55rem; padding: .55rem 0; border-top: 1px solid #f3f4f6; }
.list li > i { color: #d97706; margin-top: .15rem; }
.list li.critical > i { color: #dc2626; }
.main { display: flex; flex-direction: column; gap: .1rem; flex: 1; min-width: 0; overflow-wrap: anywhere; }
.overcost { color: #b91c1c; }
.error { color: #b91c1c; }
.more { text-align: center; }
.view-all { width: 100%; }
</style>
