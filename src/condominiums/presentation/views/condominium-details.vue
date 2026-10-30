<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import { useCondominiumStore } from '../../application/condominium.store.js';
import { useSubscriptionStore } from '../../../subscriptions/application/subscription.store.js';
import { commonAreaIcon } from '../../domain/model/common-area.entity.js';
import { kitSummary } from '../../../subscriptions/presentation/plan-copy.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import { Unit } from '../../domain/model/unit.entity.js';
import { sumParticipation, isParticipationBalanced, isValidPercentage, isValidPositiveInteger, isValidName } from '../../../shared/presentation/validators.js';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const confirm = useConfirm();
const store = useCondominiumStore();
const subscriptions = useSubscriptionStore();
const subscription = computed(() => subscriptions.subscriptionFor(id.value));
const plan = computed(() => subscriptions.planFor(subscription.value));

const id = computed(() => Number(route.params.id));
const rows = ref([]);              // copia local del padrón; los cambios se envían juntos al guardar
const search = ref('');
const saving = ref(false);
const loading = ref(true);
const dialog = ref(false);
const newUnit = reactive({ number: '', type: 'apartment', areaM2: null, participationPct: null });
const unitErrors = reactive({});
let tempId = -1;

async function load() {
  loading.value = true;
  try {
    await Promise.all([store.fetchById(id.value), subscriptions.fetchAvailablePlans(), subscriptions.fetchSubscriptions()]);
    const list = await store.fetchUnits(id.value);
    rows.value = list.map(u => ({ ...u }));
  } finally { loading.value = false; }
}
onMounted(load);

const visible = computed(() => rows.value.filter(r => !r._deleted));
const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  return visible.value.filter(r => !q || `${r.number} ${r.type}`.toLowerCase().includes(q));
});
const total = computed(() => sumParticipation(visible.value));
const balanced = computed(() => isParticipationBalanced(visible.value));
const assigned = computed(() => visible.value.filter(r => r.residentUserId).length);
const average = computed(() => visible.value.length ? Math.round((total.value / visible.value.length) * 10000) / 10000 : 0);
const dirty = computed(() => rows.value.some(r => r._new || r._deleted || r._dirty));
const diffText = computed(() => {
  const d = Math.round((100 - total.value) * 10000) / 10000;
  if (d > 0.0001) return t('condominiums.participation.missing', { n: d.toFixed(4) });
  if (d < -0.0001) return t('condominiums.participation.exceeds', { n: Math.abs(d).toFixed(4) });
  return t('condominiums.participation.balanced');
});

const typeOptions = computed(() => ['apartment', 'storage', 'parking'].map(v => ({ value: v, label: t(`condominiums.unitTypes.${v}`) })));

function onPctEdit(row, value) {
  row.participationPct = Number(value) || 0;
  row._dirty = true;
}

function openDialog() {
  Object.assign(newUnit, { number: '', type: 'apartment', areaM2: null, participationPct: null });
  Object.keys(unitErrors).forEach(k => delete unitErrors[k]);
  dialog.value = true;
}
function addUnit() {
  Object.keys(unitErrors).forEach(k => delete unitErrors[k]);
  if (!isValidName(newUnit.number, 1, 10)) unitErrors.number = t('condominiums.validation.required');
  else if (visible.value.some(r => String(r.number) === String(newUnit.number).trim())) unitErrors.number = t('condominiums.validation.duplicateUnit');
  if (!isValidPositiveInteger(Math.round(newUnit.areaM2 || 0), 1, 100000)) unitErrors.areaM2 = t('condominiums.validation.area');
  if (!isValidPercentage(newUnit.participationPct)) unitErrors.participationPct = t('condominiums.validation.percentage');
  if (Object.keys(unitErrors).length) return;
  rows.value.push({ ...new Unit({ ...newUnit, id: tempId--, condominiumId: id.value, number: String(newUnit.number).trim() }), _new: true });
  dialog.value = false;
}
function removeUnit(row) {
  if (row._new) rows.value = rows.value.filter(r => r !== row);
  else row._deleted = true;
}

async function savePadron() {
  if (!balanced.value) return;
  saving.value = true;
  try {
    await store.saveUnits(id.value, rows.value);
    rows.value = store.units.map(u => ({ ...u }));
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('condominiums.messages.padronSaved'), life: 2500 });
  } catch (e) {
    toast.add({ severity: 'error', summary: t('common.error'), detail: t('condominiums.messages.saveError'), life: 3000 });
  } finally { saving.value = false; }
}

function deactivate() {
  confirm.require({
    message: t('condominiums.confirmDeactivate'),
    header: t('condominiums.deactivate'),
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: t('condominiums.deactivate'),
    rejectLabel: t('common.cancel'),
    acceptProps: { severity: 'danger' },
    accept: async () => {
      await store.deactivate(id.value);
      toast.add({ severity: 'info', summary: t('common.success'), detail: t('condominiums.messages.deactivated'), life: 2500 });
    },
  });
}
</script>

<template>
  <div class="page" v-if="!loading && store.current">
    <nav class="breadcrumb" :aria-label="t('condominiums.form.breadcrumb')">
      <router-link :to="ROUTES.DASHBOARD" :aria-label="t('option.dashboard')"><i class="pi pi-home" /></router-link>
      <span>/</span>
      <router-link :to="ROUTES.CONDOMINIUMS">{{ t('option.condominiums') }}</router-link>
      <span>/</span>
      <span class="current">{{ store.current.name }}</span>
    </nav>

    <header class="hero">
      <div class="hero-media">
        <img v-if="store.current.imageUrl" :src="store.current.imageUrl" :alt="store.current.name" />
        <i v-else class="pi pi-building" aria-hidden="true" />
      </div>
      <div class="hero-text">
        <div class="hero-title">
          <h1>{{ store.current.name }}</h1>
          <pv-tag :severity="store.current.isActive ? 'success' : 'secondary'" :value="t(`condominiums.status.${store.current.status}`)" />
        </div>
        <p v-if="store.current.locationLabel" class="muted"><i class="pi pi-map-marker" aria-hidden="true" /> {{ store.current.locationLabel }}</p>
        <p class="muted">{{ t('condominiums.details.unitsAndFloors', { units: store.current.totalUnits }) }}<template v-if="store.current.totalFloors"> · {{ t('condominiums.details.floors', { n: store.current.totalFloors }) }}</template></p>
      </div>
      <div class="hero-actions">
        <pv-button :label="t('common.edit')" icon="pi pi-pencil" severity="secondary" outlined @click="router.push({ name: 'condominium-edit', params: { id } })" />
        <pv-button v-if="store.current.isActive" :label="t('condominiums.deactivate')" icon="pi pi-ban" severity="danger" text @click="deactivate" />
      </div>
    </header>

    <div class="summary">
      <section class="info">
        <h2>{{ t('condominiums.form.supplies') }}</h2>
        <dl>
          <div><dt><i class="pi pi-filter" /> {{ t('condominiums.fields.waterSupplyCode') }}</dt>
            <dd><span v-if="store.current.waterSupplyCode">{{ store.current.waterSupplyCode }}</span><span v-else class="missing">{{ t('condominiums.details.notRegistered') }}</span></dd></div>
          <div><dt><i class="pi pi-bolt" /> {{ t('condominiums.fields.electricitySupplyCode') }}</dt>
            <dd><span v-if="store.current.electricitySupplyCode">{{ store.current.electricitySupplyCode }}</span><span v-else class="missing">{{ t('condominiums.details.notRegistered') }}</span></dd></div>
        </dl>
      </section>
      <section class="info">
        <h2>{{ t('condominiums.form.areas') }}</h2>
        <ul v-if="store.current.commonAreasCount" class="areas">
          <li v-for="a in store.current.commonAreas" :key="a.id">
            <i :class="commonAreaIcon(a.type)" aria-hidden="true" />
            <span>{{ a.name }}<small v-if="a.location" class="muted"> · {{ a.location }}</small></span>
          </li>
        </ul>
        <small v-else class="muted">{{ t('condominiums.commonAreas.empty') }}</small>
      </section>
      <section class="info">
        <h2>{{ t('condominiums.subscription') }}</h2>
        <template v-if="subscription">
          <p class="plan-line"><strong>{{ plan?.name }}</strong> <pv-tag :severity="subscription.isActive() ? 'success' : 'warn'"
            :value="subscription.isActive() ? t('subscriptions.status-active') : t('subscriptions.status-cancelled')" /></p>
          <small v-if="plan" class="muted">{{ plan.getFormattedPrice() }} / {{ t('subscriptions.month') }} · {{ kitSummary(plan.kit, t) }}</small>
          <small v-if="subscription.nextBillingDate" class="muted">{{ t('subscriptions.nextBilling') }}: {{ subscription.nextBillingDate }}</small>
        </template>
        <pv-tag v-else severity="secondary" :value="t('condominiums.noSubscription')" />
        <pv-button :label="t('condominiums.manageSubscription')" text size="small" icon="pi pi-credit-card" class="link-btn"
                   @click="router.push(ROUTES.SUBSCRIPTION_DETAIL)" />
      </section>
    </div>

    <h3 class="section-title">{{ t('condominiums.units.registry') }}</h3>
    <div class="stats">
      <div class="stat"><span>{{ t('condominiums.stats.totalUnits') }}</span><strong>{{ visible.length }}</strong></div>
      <div class="stat"><span>{{ t('condominiums.stats.assigned') }}</span><strong>{{ assigned }}</strong></div>
      <div class="stat" :class="balanced ? 'ok' : 'bad'"><span>{{ t('condominiums.stats.sum') }}</span><strong>{{ total.toFixed(4) }} %</strong><small>{{ diffText }}</small></div>
      <div class="stat"><span>{{ t('condominiums.stats.average') }}</span><strong>{{ average.toFixed(4) }} %</strong></div>
    </div>

    <div class="toolbar">
      <pv-icon-field><pv-input-icon class="pi pi-search" /><pv-input-text v-model="search" :placeholder="t('condominiums.search')" /></pv-icon-field>
      <div class="flex gap-2">
        <pv-button :label="t('condominiums.units.add')" icon="pi pi-plus" severity="secondary" @click="openDialog" />
        <pv-button :label="t('condominiums.units.save')" icon="pi pi-save" :disabled="!balanced || !dirty" :loading="saving" @click="savePadron" />
      </div>
    </div>
    <pv-message v-if="!balanced" severity="warn" :closable="false" class="mb-2">{{ t('condominiums.participation.saveDisabled') }}</pv-message>

    <pv-data-table :value="filtered" :paginator="filtered.length > 10" :rows="10" dataKey="id" stripedRows>
      <template #empty>
        <div class="empty-registry">
          <i class="pi pi-th-large" aria-hidden="true" />
          <p>{{ t('condominiums.units.empty', { n: store.current.totalUnits }) }}</p>
        </div>
      </template>
      <pv-column field="number" :header="t('condominiums.units.number')" sortable />
      <pv-column :header="t('condominiums.units.type')"><template #body="{ data }">{{ t(`condominiums.unitTypes.${data.type}`) }}</template></pv-column>
      <pv-column field="areaM2" :header="t('condominiums.units.area')" sortable />
      <pv-column :header="t('condominiums.units.participation')">
        <template #body="{ data }">
          <pv-input-number :modelValue="data.participationPct" :min="0.0001" :max="100" :minFractionDigits="4" :maxFractionDigits="4" suffix=" %"
                           inputClass="pct-input" :aria-label="t('condominiums.units.participation')" @update:modelValue="onPctEdit(data, $event)" />
        </template>
      </pv-column>
      <pv-column :header="t('condominiums.units.resident')">
        <template #body="{ data }">
          <pv-tag v-if="data.residentUserId" severity="success" :value="t('condominiums.units.assigned')" />
          <pv-tag v-else severity="secondary" :value="t('condominiums.units.unassigned')" />
        </template>
      </pv-column>
      <pv-column :header="t('condominiums.units.activationCode')"><template #body="{ data }"><code v-if="data.activationCode">{{ data.activationCode }}</code><span v-else>—</span></template></pv-column>
      <pv-column :header="t('common.actions')">
        <template #body="{ data }"><pv-button icon="pi pi-trash" text rounded severity="danger" :disabled="!!data.residentUserId" :aria-label="t('common.delete')" @click="removeUnit(data)" /></template>
      </pv-column>
    </pv-data-table>

    <pv-dialog v-model:visible="dialog" modal :header="t('condominiums.units.add')" :style="{ width: '28rem' }">
      <div class="field"><label>{{ t('condominiums.units.number') }} *</label><pv-input-text v-model="newUnit.number" :invalid="!!unitErrors.number" class="w-full" /><small v-if="unitErrors.number" class="p-error">{{ unitErrors.number }}</small></div>
      <div class="field"><label>{{ t('condominiums.units.type') }}</label><pv-select v-model="newUnit.type" :options="typeOptions" optionLabel="label" optionValue="value" class="w-full" /></div>
      <div class="field"><label>{{ t('condominiums.units.area') }} *</label><pv-input-number v-model="newUnit.areaM2" :min="1" :maxFractionDigits="2" :invalid="!!unitErrors.areaM2" class="w-full" /><small v-if="unitErrors.areaM2" class="p-error">{{ unitErrors.areaM2 }}</small></div>
      <div class="field"><label>{{ t('condominiums.units.participation') }} *</label><pv-input-number v-model="newUnit.participationPct" :min="0.0001" :max="100" :minFractionDigits="0" :maxFractionDigits="4" suffix=" %" :invalid="!!unitErrors.participationPct" class="w-full" /><small v-if="unitErrors.participationPct" class="p-error">{{ unitErrors.participationPct }}</small></div>
      <template #footer>
        <pv-button :label="t('common.cancel')" severity="secondary" @click="dialog = false" />
        <pv-button :label="t('common.add')" icon="pi pi-check" @click="addUnit" />
      </template>
    </pv-dialog>
  </div>
  <div v-else class="center"><pv-progress-spinner /></div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
.breadcrumb { display: flex; align-items: center; gap: .5rem; font-size: .875rem; color: #6b7280; margin-bottom: 1rem; }
.breadcrumb a { color: #6b7280; text-decoration: none; }
.breadcrumb a:hover { color: #059669; }
.breadcrumb .current { color: #059669; font-weight: 500; }
.muted { color: #6B7280; margin: .25rem 0; }
.hero { display: flex; align-items: center; gap: 1.25rem; flex-wrap: wrap; background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; }
.hero-media { width: 112px; height: 84px; border-radius: 10px; overflow: hidden; background: #ECFDF5; display: grid; place-items: center; color: #10B981; flex-shrink: 0; }
.hero-media .pi { font-size: 2rem; }
.hero-media img { width: 100%; height: 100%; object-fit: cover; }
.hero-text { flex: 1; min-width: 220px; }
.hero-title { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; }
.hero-title h1 { margin: 0; font-size: 1.6rem; }
.hero-actions { display: flex; gap: .5rem; }
.summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; margin: 1rem 0; }
@media (max-width: 900px) { .summary { grid-template-columns: 1fr; } }
.info { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.1rem; display: flex; flex-direction: column; gap: .5rem; }
.info h2 { font-size: .95rem; margin: 0 0 .25rem; }
.info dl { margin: 0; display: flex; flex-direction: column; gap: .6rem; }
.info dt { font-size: .8rem; color: #6B7280; }
.info dt .pi { color: #059669; }
.info dd { margin: 2px 0 0; font-weight: 600; font-variant-numeric: tabular-nums; }
.missing { color: #b45309; font-weight: 500; font-style: italic; }
.areas { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: .45rem; }
.areas li { display: flex; align-items: baseline; gap: .5rem; }
.areas .pi { color: #059669; }
.plan-line { display: flex; align-items: center; gap: .5rem; margin: 0; }
.link-btn { align-self: flex-start; padding-left: 0 !important; }
.empty-registry { text-align: center; padding: 2rem 1rem; color: #6B7280; }
.empty-registry .pi { font-size: 1.75rem; color: #10B981; }
.empty-registry p { margin: .5rem 0 0; }
.section-title { margin: 1.5rem 0 0; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 1rem; margin: 1rem 0; }
.stat { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1rem; display: flex; flex-direction: column; gap: 4px; }
.stat span { color: #6B7280; font-size: .85rem; }
.stat strong { font-size: 1.5rem; }
.stat.ok { background: #ECFDF5; border-color: #10B981; }
.stat.bad { background: #FEF2F2; border-color: #ef4444; }
.toolbar { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; margin-bottom: .75rem; }
.field { margin-bottom: 12px; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
.center { text-align: center; padding: 3rem; }
:deep(.pct-input) { width: 7rem; }
</style>
