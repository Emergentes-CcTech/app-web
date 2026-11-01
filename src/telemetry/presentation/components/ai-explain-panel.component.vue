<script setup>
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { TelemetryApi } from '../../infrastructure/telemetry-api.js';

// Asistente que explica el consumo del mes. La detección la hacen las reglas; aquí solo se redacta la respuesta.
const props = defineProps({
  condominiumId: { type: Number, default: null },
  unitId: { type: Number, default: null },
  audience: { type: String, default: 'admin' } // 'admin' | 'resident': cambia las preguntas sugeridas
});

const { t, locale } = useI18n();
const api = new TelemetryApi();
const question = ref('');
const answer = ref(null);
const loading = ref(false);
const failed = ref(false);

const now = new Date();
const period = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
const suggestions = computed(() => [1, 2, 3].map(i => t(`ai.suggestions.${props.audience}.${i}`)));

async function ask(text = question.value) {
  const q = String(text || '').trim() || suggestions.value[0];
  if (!props.condominiumId) return;
  question.value = q;
  loading.value = true;
  failed.value = false;
  try {
    const { data } = await api.explainConsumption({
      projectId: props.condominiumId,
      unitId: props.unitId ?? null,
      billingPeriod: period,
      question: q,
      language: locale.value === 'en' ? 'en' : 'es'
    });
    answer.value = data;
  } catch (_) {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

watch(() => [props.condominiumId, props.unitId], () => { answer.value = null; question.value = ''; });
</script>

<template>
  <section class="ai-card" aria-labelledby="ai-title">
    <div class="ai-head">
      <h2 id="ai-title"><i class="pi pi-sparkles" aria-hidden="true" /> {{ t('ai.title') }}</h2>
      <small class="muted">{{ t('ai.subtitle') }}</small>
    </div>

    <form class="ai-ask" @submit.prevent="ask()">
      <label for="ai-question" class="sr-only">{{ t('ai.placeholder') }}</label>
      <pv-input-text id="ai-question" v-model="question" :placeholder="t('ai.placeholder')" maxlength="500" class="flex-1" :disabled="loading || !condominiumId" />
      <pv-button type="submit" :label="t('ai.ask')" icon="pi pi-send" :loading="loading" :disabled="!condominiumId" />
    </form>

    <div v-if="!answer && !loading" class="chips">
      <button v-for="s in suggestions" :key="s" type="button" class="chip" :disabled="!condominiumId" @click="ask(s)">{{ s }}</button>
    </div>

    <pv-message v-if="failed" severity="warn" :closable="false">{{ t('ai.error') }}</pv-message>

    <div v-if="answer" class="ai-answer" aria-live="polite">
      <p class="summary">{{ answer.summary }}</p>
      <dl>
        <div><dt><i class="pi pi-filter" aria-hidden="true" /> {{ t('ai.water') }}</dt><dd>{{ answer.waterAnalysis }}</dd></div>
        <div><dt><i class="pi pi-bolt" aria-hidden="true" /> {{ t('ai.energy') }}</dt><dd>{{ answer.energyAnalysis }}</dd></div>
        <div><dt><i class="pi pi-percentage" aria-hidden="true" /> {{ t(audience === 'resident' ? 'ai.aliquot' : 'ai.aliquotAdmin') }}</dt><dd>{{ answer.aliquotImpact }}</dd></div>
      </dl>
      <div v-if="answer.recommendations?.length">
        <strong class="rec-title">{{ t('ai.recommendations') }}</strong>
        <ul class="recs"><li v-for="(r, i) in answer.recommendations" :key="i">{{ r }}</li></ul>
      </div>
      <small class="source muted">
        {{ answer.source === 'gemini' ? t('ai.sourceGemini') : t('ai.sourceRules') }}
      </small>
    </div>
  </section>
</template>

<style scoped>
.ai-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: .9rem; }
.ai-head h2 { font-size: 1.1rem; margin: 0 0 .15rem; display: flex; align-items: center; gap: .5rem; }
.ai-head h2 i { color: #10B981; }
.muted { color: #6B7280; }
.ai-ask { display: flex; gap: .5rem; flex-wrap: wrap; }
.flex-1 { flex: 1 1 16rem; }
.chips { display: flex; flex-wrap: wrap; gap: .5rem; }
.chip { background: #ECFDF5; color: #065F46; border: 1px solid #A7F3D0; border-radius: 999px; padding: .35rem .8rem; font: inherit; font-size: .82rem; cursor: pointer; }
.chip:hover:not(:disabled) { background: #D1FAE5; }
.chip:disabled { opacity: .6; cursor: not-allowed; }
.ai-answer { display: flex; flex-direction: column; gap: .75rem; border-top: 1px solid #f1f5f9; padding-top: .9rem; }
.summary { margin: 0; font-weight: 600; color: #0f172a; line-height: 1.5; }
dl { margin: 0; display: grid; gap: .6rem; }
dt { font-weight: 600; font-size: .85rem; color: #334155; display: flex; align-items: center; gap: .4rem; }
dd { margin: .15rem 0 0; color: #475569; font-size: .9rem; line-height: 1.5; }
.rec-title { font-size: .85rem; color: #334155; }
.recs { margin: .35rem 0 0; padding-left: 1.2rem; color: #475569; font-size: .9rem; display: grid; gap: .25rem; }
.source { font-size: .75rem; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
