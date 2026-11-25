<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';

// Mientras no hay liquidaciones: cómo se calculará la cuota, una estimación y lo que IoBuild ya mide
const props = defineProps({ preview: { type: Object, required: true } });

const { t, locale } = useI18n();
const router = useRouter();

const pct = computed(() => Number(props.preview.participationPct) || 0);
// Mismo formato numérico que los montos en soles (punto decimal en es-PE)
const numberLocale = computed(() => (locale.value === 'en' ? 'en-US' : 'es-PE'));
const pctText = computed(() => pct.value.toLocaleString(numberLocale.value, { minimumFractionDigits: 2, maximumFractionDigits: 4 }));
const lastMonth = computed(() => props.preview.lastBill ? formatPeriod(props.preview.lastBill.period, locale.value) : '');
const thisMonth = computed(() => formatPeriod(props.preview.currentMonth?.period, locale.value));
// Sin un gasto común registrado se muestra un ejemplo con S/ 10,000 para que se entienda la proporción
const exampleFee = computed(() => formatPen(10000 * pct.value / 100));
const num = (n, digits = 2) => Number(n || 0).toLocaleString(numberLocale.value, { maximumFractionDigits: digits });
</script>

<template>
  <div class="preview">
    <section class="hero">
      <div>
        <span class="eyebrow">{{ t('audit.preview.eyebrow', { unit: preview.unitNumber, condominium: preview.condominiumName || '' }) }}</span>
        <h2>{{ t('audit.preview.title') }}</h2>
        <p>{{ t('audit.preview.lead') }}</p>
      </div>
      <div class="share">
        <span>{{ t('audit.preview.yourShare') }}</span>
        <strong>{{ pctText }} %</strong>
        <small v-if="preview.areaM2">{{ t('audit.preview.area', { area: num(preview.areaM2) }) }}</small>
      </div>
    </section>

    <div class="grid">
      <section class="card">
        <h3><i class="pi pi-calculator" aria-hidden="true" /> {{ t('audit.preview.howTitle') }}</h3>
        <p class="formula">{{ t('audit.preview.formula', { pct: pctText }) }}</p>
        <div v-if="preview.estimatedFee != null" class="estimate">
          <span>{{ t('audit.preview.estimateLabel') }}</span>
          <strong>{{ formatPen(preview.estimatedFee) }}</strong>
          <small>{{ t('audit.preview.estimateBasis', { month: lastMonth, total: formatPen(preview.lastBill.total) }) }}</small>
        </div>
        <p v-else class="muted">{{ t('audit.preview.example', { fee: exampleFee }) }}</p>
      </section>

      <section class="card">
        <h3><i class="pi pi-chart-line" aria-hidden="true" /> {{ t('audit.preview.measuringTitle', { month: thisMonth }) }}</h3>
        <template v-if="preview.currentMonth?.daysWithData">
          <div class="metrics">
            <div><span>{{ t('audit.fee.concepts.water') }}</span><strong>{{ num(preview.currentMonth.waterM3) }} m³</strong></div>
            <div><span>{{ t('audit.fee.concepts.energy') }}</span><strong>{{ num(preview.currentMonth.energyKwh) }} kWh</strong></div>
          </div>
          <small class="muted">{{ t('audit.preview.measuringNote', { days: preview.currentMonth.daysWithData }) }}</small>
        </template>
        <p v-else class="muted">{{ t('audit.preview.noReadings') }}</p>
        <pv-button :label="t('audit.preview.seeConsumption')" icon="pi pi-arrow-right" iconPos="right" text size="small" class="link" @click="router.push(ROUTES.DASHBOARD)" />
      </section>
    </div>

    <section class="card steps">
      <h3><i class="pi pi-list-check" aria-hidden="true" /> {{ t('audit.preview.stepsTitle') }}</h3>
      <ol>
        <li>{{ t('audit.preview.step1') }}</li>
        <li>{{ t('audit.preview.step2') }}</li>
        <li>{{ t('audit.preview.step3') }}</li>
      </ol>
      <pv-button :label="t('audit.fee.askAi')" icon="pi pi-sparkles" severity="secondary" outlined size="small" @click="router.push(ROUTES.DASHBOARD)" />
    </section>
  </div>
</template>

<style scoped>
.preview { display: grid; gap: 1rem; }
.hero { display: flex; justify-content: space-between; gap: 1.5rem; align-items: center; flex-wrap: wrap; padding: 1.5rem; border-radius: 1rem;
  background: linear-gradient(135deg, #059669 0%, #0f766e 100%); color: #fff; }
.hero h2 { margin: .35rem 0 .4rem; font-size: 1.4rem; }
.hero p { margin: 0; max-width: 34rem; color: #d1fae5; line-height: 1.5; }
.eyebrow { font-size: .8rem; font-weight: 600; color: #a7f3d0; }
.share { background: rgba(255, 255, 255, .14); border-radius: .85rem; padding: .9rem 1.2rem; display: flex; flex-direction: column; min-width: 11rem; }
.share span, .share small { font-size: .78rem; color: #d1fae5; }
.share strong { font-size: 1.8rem; font-variant-numeric: tabular-nums; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr)); gap: 1rem; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: .9rem; padding: 1.1rem 1.2rem; }
.card h3 { margin: 0 0 .7rem; font-size: 1rem; display: flex; align-items: center; gap: .45rem; }
.card h3 i { color: #059669; }
.formula { margin: 0 0 .8rem; color: #334155; line-height: 1.5; }
.estimate { background: #ecfdf5; border-radius: .7rem; padding: .75rem .9rem; display: flex; flex-direction: column; gap: .15rem; }
.estimate span { font-size: .78rem; color: #047857; font-weight: 600; }
.estimate strong { font-size: 1.5rem; color: #065f46; font-variant-numeric: tabular-nums; }
.estimate small { color: #475569; }
.metrics { display: flex; gap: 1.5rem; margin-bottom: .4rem; }
.metrics div { display: flex; flex-direction: column; }
.metrics span { font-size: .78rem; color: #64748b; }
.metrics strong { font-size: 1.3rem; font-variant-numeric: tabular-nums; }
.steps ol { margin: 0 0 .9rem; padding-left: 1.2rem; display: grid; gap: .35rem; color: #334155; }
.link { margin-top: .4rem; padding-left: 0; }
.muted { color: #6B7280; }
</style>
