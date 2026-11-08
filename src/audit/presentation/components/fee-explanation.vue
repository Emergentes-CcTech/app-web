<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import { Alert } from '../../../telemetry/domain/model/alert.entity.js';

// Explica una cuota en tres pasos: cuánto costó el edificio, qué parte le toca al departamento y de qué se compone
const props = defineProps({ explanation: { type: Object, required: true } });

const { t, locale } = useI18n();
const router = useRouter();

const month = computed(() => formatPeriod(props.explanation.period, locale.value));
const previousMonth = computed(() => formatPeriod(props.explanation.previousPeriod, locale.value));
const isPaid = computed(() => String(props.explanation.status).toLowerCase() === 'paid');
const aliquot = computed(() => Number(props.explanation.aliquot));
const pct = (n) => n.toLocaleString(locale.value, { minimumFractionDigits: 2, maximumFractionDigits: 4 });
const date = (iso) => iso ? new Date(iso).toLocaleDateString(locale.value, { day: 'numeric', month: 'long', year: 'numeric' }) : '';

// Solo los conceptos con monto: agua y luz siempre; otros gastos si los hubo
const rows = computed(() => {
  const { common, mine } = props.explanation;
  return [
    { key: 'water', icon: 'pi-filter', building: common.water, unit: mine.water, color: '#10B981' },
    { key: 'energy', icon: 'pi-bolt', building: common.energy, unit: mine.energy, color: '#f59e0b' },
    { key: 'other', icon: 'pi-building', building: common.other, unit: mine.other, color: '#64748b' },
  ].filter(r => r.key !== 'other' || Number(r.building) > 0)
    .map(r => ({ ...r, share: mine.total > 0 ? Math.round((r.unit / mine.total) * 1000) / 10 : 0 }));
});

const change = computed(() => {
  const last = props.explanation.previous;
  if (!last) return null;
  const { mine } = props.explanation;
  const delta = mine.total - last.totalSoles;
  const drivers = [
    { key: 'water', delta: mine.water - last.waterSoles },
    { key: 'energy', delta: mine.energy - last.energySoles },
    { key: 'other', delta: mine.other - last.otherSoles },
  ].sort((a, b) => Math.abs(b.delta) - Math.abs(a.delta));
  return { delta, previous: last.totalSoles, driver: drivers[0], percent: last.totalSoles ? Math.round((delta / last.totalSoles) * 1000) / 10 : null };
});

const alerts = computed(() => props.explanation.alerts.map(a => ({ ...a, type: new Alert({ alertType: a.alertType }).type })));
const receipts = computed(() => [
  { key: 'water', url: props.explanation.common.waterReceiptUrl },
  { key: 'energy', url: props.explanation.common.energyReceiptUrl },
].filter(r => r.url));
</script>

<template>
  <div class="fee">
    <section class="hero" :class="{ paid: isPaid }">
      <div>
        <span>{{ t('audit.fee.yourFee', { month, unit: explanation.unitNumber }) }}</span>
        <strong>{{ formatPen(explanation.mine.total) }}</strong>
        <small v-if="isPaid">{{ explanation.paidAt ? t('audit.fee.paidOn', { date: date(explanation.paidAt) }) : t('payments.status.paid') }}</small>
        <small v-else>{{ t('audit.fee.dueOn', { date: date(explanation.dueDate) }) }}</small>
      </div>
      <pv-button v-if="!isPaid" :label="t('audit.fee.goPay')" icon="pi pi-wallet" @click="router.push(ROUTES.ACCOUNT_STATEMENT)" />
    </section>

    <ol class="steps">
      <li class="step">
        <span class="number" aria-hidden="true">1</span>
        <div class="body">
          <h2>{{ t('audit.fee.step1.title', { month }) }}</h2>
          <p class="muted">{{ t('audit.fee.step1.text') }}</p>
          <dl class="amounts">
            <div v-for="r in rows" :key="r.key"><dt><i class="pi" :class="r.icon" aria-hidden="true" /> {{ t(`audit.fee.concepts.${r.key}`) }}</dt><dd>{{ formatPen(r.building) }}</dd></div>
            <div class="total"><dt>{{ t('audit.fee.step1.total') }}</dt><dd>{{ formatPen(explanation.common.total) }}</dd></div>
          </dl>
        </div>
      </li>

      <li class="step">
        <span class="number" aria-hidden="true">2</span>
        <div class="body">
          <h2>{{ t('audit.fee.step2.title', { pct: pct(aliquot) }) }}</h2>
          <p class="muted">{{ t('audit.fee.step2.text', { amount: formatPen(aliquot) }) }}</p>
          <p class="formula">{{ formatPen(explanation.common.total) }} × {{ pct(aliquot) }}% = <strong>{{ formatPen(explanation.mine.total) }}</strong></p>
        </div>
      </li>

      <li class="step">
        <span class="number" aria-hidden="true">3</span>
        <div class="body">
          <h2>{{ t('audit.fee.step3.title') }}</h2>
          <div class="bar" role="img" :aria-label="rows.map(r => `${t(`audit.fee.concepts.${r.key}`)} ${r.share}%`).join(', ')">
            <span v-for="r in rows" :key="r.key" :style="{ width: `${r.share}%`, background: r.color }" />
          </div>
          <dl class="amounts">
            <div v-for="r in rows" :key="r.key">
              <dt><span class="dot" :style="{ background: r.color }" aria-hidden="true" /> {{ t(`audit.fee.concepts.${r.key}`) }} <small class="muted">({{ r.share }}%)</small></dt>
              <dd>{{ formatPen(r.unit) }}</dd>
            </div>
            <div class="total"><dt>{{ t('audit.fee.step3.total') }}</dt><dd>{{ formatPen(explanation.mine.total) }}</dd></div>
          </dl>
        </div>
      </li>
    </ol>

    <section class="card">
      <h2>{{ t('audit.fee.change.title', { month: previousMonth }) }}</h2>
      <p v-if="!change" class="muted">{{ t('audit.fee.change.none', { month: previousMonth }) }}</p>
      <template v-else>
        <p class="change-amount">
          {{ formatPen(change.previous) }} <i class="pi pi-arrow-right" aria-hidden="true" /> {{ formatPen(explanation.mine.total) }}
          <span :class="change.delta > 0 ? 'up' : 'down'">{{ change.delta > 0 ? '+' : '−' }}{{ formatPen(Math.abs(change.delta)) }}<template v-if="change.percent !== null"> ({{ Math.abs(change.percent) }}%)</template></span>
        </p>
        <p v-if="change.driver.delta !== 0" class="muted">
          {{ t(change.driver.delta > 0 ? 'audit.fee.change.up' : 'audit.fee.change.down', { concept: t(`audit.fee.concepts.${change.driver.key}`).toLowerCase(), amount: formatPen(Math.abs(change.driver.delta)) }) }}
        </p>
      </template>
    </section>

    <section class="card">
      <h2>{{ t('audit.fee.alerts.title', { month }) }}</h2>
      <p v-if="!alerts.length" class="muted">{{ t('audit.fee.alerts.none') }}</p>
      <template v-else>
        <p class="muted">{{ t('audit.fee.alerts.text') }}</p>
        <ul class="alerts">
          <li v-for="(a, i) in alerts" :key="i">
            <div>
              <strong>{{ t(`telemetry.alertTypes.${a.type}`, a.type) }}</strong>
              <small class="muted">{{ date(a.detectedAt) }}</small>
            </div>
            <div class="alert-side">
              <small>{{ t('audit.fee.alerts.share', { building: formatPen(a.overcost), mine: formatPen(a.myShare) }) }}</small>
              <pv-tag :severity="a.isOpen ? 'warn' : 'success'" :value="t(a.isOpen ? 'audit.fee.alerts.open' : 'audit.fee.alerts.resolved')" />
            </div>
          </li>
        </ul>
      </template>
    </section>

    <section class="card">
      <h2>{{ t('audit.fee.evidence.title') }}</h2>
      <p class="muted">{{ t('audit.fee.evidence.text', { water: explanation.common.waterM3, energy: explanation.common.energyKwh }) }}</p>
      <div v-if="receipts.length" class="receipts">
        <a v-for="r in receipts" :key="r.key" :href="r.url" target="_blank" rel="noopener noreferrer"><i class="pi pi-file" aria-hidden="true" /> {{ t(`audit.fee.evidence.${r.key}`) }}</a>
      </div>
      <p v-else class="muted">{{ t('audit.fee.evidence.noReceipts') }}</p>
    </section>

    <section class="card">
      <h2>{{ t('audit.fee.glossary.title') }}</h2>
      <dl class="glossary">
        <div v-for="term in ['aliquot', 'commonExpense', 'masterBill']" :key="term">
          <dt>{{ t(`audit.fee.glossary.${term}.term`) }}</dt>
          <dd>{{ t(`audit.fee.glossary.${term}.text`) }}</dd>
        </div>
      </dl>
      <pv-button :label="t('audit.fee.askAi')" icon="pi pi-sparkles" text class="ask" @click="router.push(ROUTES.DASHBOARD)" />
    </section>
  </div>
</template>

<style scoped>
.fee { display: flex; flex-direction: column; gap: 1rem; }
.muted { color: #6B7280; }
.hero { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 1.5rem; border-radius: 14px; background: #fff; border: 1px solid #e5e7eb; }
.hero.paid { background: #ECFDF5; border-color: #10B981; }
.hero div { display: flex; flex-direction: column; gap: .25rem; }
.hero strong { font-size: 2.2rem; line-height: 1.1; }
.steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 1rem; }
.step { display: flex; gap: 1rem; background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; }
.number { flex: 0 0 2rem; height: 2rem; border-radius: 50%; background: #10B981; color: #fff; display: flex; align-items: center; justify-content: center; font-weight: 700; }
.body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: .6rem; }
h2 { font-size: 1.05rem; margin: 0; }
.body p, .card p { margin: 0; line-height: 1.5; }
.amounts { margin: 0; display: grid; gap: .4rem; max-width: 30rem; }
.amounts > div { display: flex; justify-content: space-between; gap: 1rem; }
dt { color: #334155; display: inline-flex; align-items: center; gap: .4rem; }
dd { margin: 0; font-weight: 600; font-variant-numeric: tabular-nums; white-space: nowrap; }
.amounts .total { border-top: 1px solid #e5e7eb; padding-top: .4rem; font-weight: 700; }
.amounts .total dt { font-weight: 700; color: #0f172a; }
.formula { background: #ECFDF5; border-radius: 8px; padding: .6rem .8rem; color: #065F46; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.bar { display: flex; height: 12px; border-radius: 999px; overflow: hidden; background: #f1f5f9; max-width: 30rem; }
.dot { width: .65rem; height: .65rem; border-radius: 50%; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: .6rem; }
.change-amount { font-weight: 600; display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; }
.up { color: #b91c1c; }
.down { color: #047857; }
.alerts { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; }
.alerts li { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; padding: .6rem 0; border-top: 1px solid #f3f4f6; }
.alerts li > div:first-child { display: flex; flex-direction: column; }
.alert-side { display: flex; align-items: center; gap: .6rem; flex-wrap: wrap; }
.receipts { display: flex; gap: 1rem; flex-wrap: wrap; }
.receipts a { color: #047857; font-weight: 600; text-decoration: none; display: inline-flex; gap: .35rem; align-items: center; }
.receipts a:hover { text-decoration: underline; }
.glossary { margin: 0; display: grid; gap: .6rem; }
.glossary dt { font-weight: 600; }
.glossary dd { font-weight: 400; color: #475569; white-space: normal; margin-top: .1rem; line-height: 1.5; }
.ask { align-self: flex-start; }
</style>
