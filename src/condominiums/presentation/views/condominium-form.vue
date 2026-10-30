<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useCondominiumStore } from '../../application/condominium.store.js';
import { Condominium } from '../../domain/model/condominium.entity.js';
import { CommonArea, COMMON_AREA_TYPES } from '../../domain/model/common-area.entity.js';
import { recommendedTier, kitTotal, MAX_PLAN_UNITS } from '../../../subscriptions/domain/model/plan-limits.js';
import { isValidName, isValidSupplyCode, isValidPositiveInteger } from '../../../shared/presentation/validators.js';
import { loadCloudinaryWidget } from '../../../shared/infrastructure/cloudinary-loader.js';
import { getProjectImageUploadConfig } from '../../../shared/infrastructure/cloudinary-config.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = useCondominiumStore();

const isEdit = computed(() => !!route.params.id);
const form = reactive(new Condominium({}));
const errors = reactive({});
const saving = ref(false);

// Distritos de Lima Metropolitana más frecuentes; el campo admite escribir otro
const LIMA_DISTRICTS = ['Barranco', 'Breña', 'Chorrillos', 'Jesús María', 'La Molina', 'La Victoria', 'Lince', 'Magdalena del Mar',
  'Miraflores', 'Pueblo Libre', 'San Borja', 'San Isidro', 'San Miguel', 'Santiago de Surco', 'Surquillo', 'Cercado de Lima'];

// ── Áreas comunes: solo los subsistemas que el kit HaaS monitorea ──
const areaCatalog = computed(() => COMMON_AREA_TYPES.map(a => ({ ...a, label: t(`condominiums.commonAreas.types.${a.type}`) })));
const areaOf = (type) => form.commonAreas.find(a => a.type === type) || null;
function toggleArea(type) {
  const existing = areaOf(type);
  if (existing) form.commonAreas.splice(form.commonAreas.indexOf(existing), 1);
  else form.commonAreas.push(new CommonArea({ type, name: t(`condominiums.commonAreas.types.${type}`) }));
}

// ── Plan HaaS que corresponde (Boutique / Urbano / Gran Torre) ──
const plan = computed(() => form.totalUnits ? recommendedTier(form.totalUnits) : null);

// ── Foto del edificio (Cloudinary) ──
const cloudName = import.meta.env.VITE_CLOUDINARY_CLOUD_NAME;
const uploadPreset = import.meta.env.VITE_CLOUDINARY_UPLOAD_PRESET;
const canUpload = Boolean(cloudName?.trim() && uploadPreset?.trim());
const uploading = ref(false);
async function uploadImage() {
  uploading.value = true;
  try {
    const cloudinary = await loadCloudinaryWidget();
    cloudinary.openUploadWidget(getProjectImageUploadConfig(cloudName, uploadPreset), (error, result) => {
      if (!error && result?.event === 'success') form.imageUrl = result.info.secure_url;
      if (error || result?.event === 'close' || result?.event === 'success') uploading.value = false;
    });
  } catch (_) {
    uploading.value = false;
    toast.add({ severity: 'error', summary: t('common.error'), detail: t('condominiums.form.uploadError'), life: 3000 });
  }
}

onMounted(async () => {
  if (isEdit.value) Object.assign(form, await store.fetchById(route.params.id));
  else {
    form.totalUnits = null; // vacío para que el placeholder guíe en lugar de un 0
    // Un condominio nuevo parte con los cuatro subsistemas que incluye el kit
    COMMON_AREA_TYPES.forEach(a => toggleArea(a.type));
  }
});

function validate() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!isValidName(form.name, 3, 120)) errors.name = t('condominiums.validation.length', { min: 3, max: 120 });
  if (!isValidName(form.address, 5, 200)) errors.address = t('condominiums.validation.length', { min: 5, max: 200 });
  if (!isValidName(form.district, 3, 80)) errors.district = t('condominiums.validation.length', { min: 3, max: 80 });
  if (!isValidPositiveInteger(form.totalUnits, 1, MAX_PLAN_UNITS)) errors.totalUnits = t('condominiums.validation.units', { max: MAX_PLAN_UNITS });
  if (form.totalFloors !== null && !isValidPositiveInteger(form.totalFloors, 1, 80)) errors.totalFloors = t('condominiums.validation.floors');
  if (!isValidSupplyCode(form.waterSupplyCode)) errors.waterSupplyCode = t('condominiums.validation.supplyCode');
  if (!isValidSupplyCode(form.electricitySupplyCode)) errors.electricitySupplyCode = t('condominiums.validation.supplyCode');
  if (!form.commonAreas.length) errors.commonAreas = t('condominiums.validation.commonAreasRequired');
  else if (form.commonAreas.some(a => !isValidName(a.name, 2, 80))) errors.commonAreas = t('condominiums.validation.commonAreaName');
  return !Object.keys(errors).length;
}

async function save() {
  if (!validate()) {
    toast.add({ severity: 'warn', summary: t('condominiums.form.reviewTitle'), detail: t('condominiums.form.reviewDetail'), life: 3000 });
    return;
  }
  saving.value = true;
  try {
    const payload = { ...form, commonAreas: form.commonAreas.map(a => ({ ...a })) };
    const saved = isEdit.value ? await store.update(payload) : await store.create(payload);
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('condominiums.messages.saved'), life: 2500 });
    router.push({ name: 'condominium-details', params: { id: saved.id } });
  } catch (e) {
    // Las reglas del backend son las mismas que valida el formulario; el mensaje se muestra en el idioma activo
    toast.add({ severity: 'error', summary: t('common.error'), detail: t('condominiums.messages.saveError'), life: 4000 });
  } finally { saving.value = false; }
}
</script>

<template>
  <div class="page">
    <nav class="breadcrumb" :aria-label="t('condominiums.form.breadcrumb')">
      <router-link :to="ROUTES.DASHBOARD" :aria-label="t('option.dashboard')"><i class="pi pi-home" /></router-link>
      <span>/</span>
      <router-link :to="ROUTES.CONDOMINIUMS">{{ t('option.condominiums') }}</router-link>
      <span>/</span>
      <span class="current">{{ isEdit ? t('condominiums.editTitle') : t('condominiums.newTitle') }}</span>
    </nav>
    <header class="page-head">
      <h1>{{ isEdit ? t('condominiums.editTitle') : t('condominiums.newTitle') }}</h1>
      <p>{{ t('condominiums.form.subtitle') }}</p>
    </header>

    <form class="layout" novalidate @submit.prevent="save">
      <div class="form-card">
        <!-- 1. Edificio -->
        <section class="section">
          <h2><span class="step">1</span>{{ t('condominiums.form.building') }}</h2>
          <div class="field">
            <label for="name"><i class="pi pi-tag" />{{ t('condominiums.fields.name') }}<span class="req">*</span></label>
            <pv-input-text id="name" v-model="form.name" :invalid="!!errors.name" :placeholder="t('condominiums.form.namePlaceholder')" class="w-full" />
            <small v-if="errors.name" class="p-error">{{ errors.name }}</small>
          </div>
          <div class="grid-2">
            <div class="field">
              <label for="address"><i class="pi pi-map-marker" />{{ t('condominiums.fields.address') }}<span class="req">*</span></label>
              <pv-input-text id="address" v-model="form.address" :invalid="!!errors.address" :placeholder="t('condominiums.form.addressPlaceholder')" class="w-full" />
              <small v-if="errors.address" class="p-error">{{ errors.address }}</small>
            </div>
            <div class="field">
              <label for="district"><i class="pi pi-compass" />{{ t('condominiums.fields.district') }}<span class="req">*</span></label>
              <pv-select inputId="district" v-model="form.district" :options="LIMA_DISTRICTS" editable filter :invalid="!!errors.district"
                         :placeholder="t('condominiums.form.districtPlaceholder')" class="w-full" />
              <small v-if="errors.district" class="p-error">{{ errors.district }}</small>
            </div>
          </div>
          <div class="grid-2">
            <div class="field">
              <label for="totalUnits"><i class="pi pi-home" />{{ t('condominiums.fields.totalUnits') }}<span class="req">*</span></label>
              <pv-input-number inputId="totalUnits" v-model="form.totalUnits" :min="1" :max="MAX_PLAN_UNITS" :useGrouping="false" :invalid="!!errors.totalUnits" placeholder="25" class="w-full" />
              <small v-if="errors.totalUnits" class="p-error">{{ errors.totalUnits }}</small>
              <small v-else class="hint">{{ t('condominiums.form.unitsHint', { max: MAX_PLAN_UNITS }) }}</small>
            </div>
            <div class="field">
              <label for="totalFloors"><i class="pi pi-building" />{{ t('condominiums.fields.totalFloors') }}</label>
              <pv-input-number inputId="totalFloors" v-model="form.totalFloors" :min="1" :max="80" :useGrouping="false" :invalid="!!errors.totalFloors" placeholder="7" class="w-full" />
              <small v-if="errors.totalFloors" class="p-error">{{ errors.totalFloors }}</small>
            </div>
          </div>
          <div class="field">
            <label><i class="pi pi-image" />{{ t('condominiums.form.photo') }}</label>
            <div class="photo">
              <img v-if="form.imageUrl" :src="form.imageUrl" :alt="form.name || t('condominiums.form.photo')" />
              <div v-else class="photo-empty"><i class="pi pi-building" /></div>
              <div class="photo-actions">
                <pv-button v-if="canUpload" type="button" :label="form.imageUrl ? t('condominiums.form.changePhoto') : t('condominiums.form.uploadPhoto')"
                           icon="pi pi-cloud-upload" severity="secondary" outlined :loading="uploading" @click="uploadImage" />
                <pv-input-text v-else v-model="form.imageUrl" placeholder="https://" class="w-full" />
                <pv-button v-if="form.imageUrl" type="button" :label="t('condominiums.form.removePhoto')" icon="pi pi-trash" text severity="danger" size="small" @click="form.imageUrl = ''" />
              </div>
            </div>
          </div>
        </section>

        <!-- 2. Suministros de los recibos matriz -->
        <section class="section">
          <h2><span class="step">2</span>{{ t('condominiums.form.supplies') }}</h2>
          <p class="section-hint">{{ t('condominiums.form.suppliesHint') }}</p>
          <div class="grid-2">
            <div class="field">
              <label for="waterSupplyCode"><i class="pi pi-filter" />{{ t('condominiums.fields.waterSupplyCode') }}<span class="req">*</span></label>
              <pv-input-text id="waterSupplyCode" v-model="form.waterSupplyCode" inputmode="numeric" :invalid="!!errors.waterSupplyCode" placeholder="Sedapal · 1234567" class="w-full" />
              <small v-if="errors.waterSupplyCode" class="p-error">{{ errors.waterSupplyCode }}</small>
            </div>
            <div class="field">
              <label for="electricitySupplyCode"><i class="pi pi-bolt" />{{ t('condominiums.fields.electricitySupplyCode') }}<span class="req">*</span></label>
              <pv-input-text id="electricitySupplyCode" v-model="form.electricitySupplyCode" inputmode="numeric" :invalid="!!errors.electricitySupplyCode" placeholder="Luz del Sur · 7654321" class="w-full" />
              <small v-if="errors.electricitySupplyCode" class="p-error">{{ errors.electricitySupplyCode }}</small>
            </div>
          </div>
        </section>

        <!-- 3. Áreas comunes monitoreadas -->
        <section class="section">
          <h2><span class="step">3</span>{{ t('condominiums.form.areas') }}</h2>
          <p class="section-hint">{{ t('condominiums.form.areasHint') }}</p>
          <div class="areas">
            <div v-for="a in areaCatalog" :key="a.type" class="area" :class="{ on: areaOf(a.type) }">
              <label class="area-toggle">
                <pv-checkbox :modelValue="!!areaOf(a.type)" binary @update:modelValue="toggleArea(a.type)" />
                <i :class="a.icon" aria-hidden="true" />
                <span class="area-label">{{ a.label }}</span>
                <span class="area-meter">{{ t(`condominiums.form.meter.${a.meter}`) }}</span>
              </label>
              <div v-if="areaOf(a.type)" class="area-fields">
                <pv-input-text v-model="areaOf(a.type).name" :aria-label="t('condominiums.commonAreas.name')" :placeholder="a.label" />
                <pv-input-text v-model="areaOf(a.type).location" :aria-label="t('condominiums.form.areaLocation')" :placeholder="t('condominiums.form.areaLocationPlaceholder')" />
              </div>
            </div>
          </div>
          <small v-if="errors.commonAreas" class="p-error block">{{ errors.commonAreas }}</small>
        </section>

        <div class="actions">
          <pv-button type="button" :label="t('common.cancel')" icon="pi pi-times" severity="secondary" outlined class="flex-1" @click="router.back()" />
          <pv-button type="submit" :label="isEdit ? t('common.save') : t('condominiums.form.create')" icon="pi pi-check" :loading="saving" class="flex-1" />
        </div>
      </div>

      <!-- Resumen: plan HaaS y kit de equipos que corresponden al condominio -->
      <aside class="summary">
        <h2>{{ t('condominiums.form.summary') }}</h2>
        <template v-if="plan">
          <div class="plan">
            <span class="plan-label">{{ t('condominiums.form.plan') }}</span>
            <strong class="plan-name">{{ plan.name }}</strong>
            <span class="plan-price">S/ {{ plan.monthlyPen.toFixed(2) }} <small>{{ t('condominiums.form.perMonth') }}</small></span>
            <small class="muted">{{ t('condominiums.form.planRange', { min: plan.minUnits, max: plan.maxUnits }) }}</small>
          </div>
          <dl class="kit">
            <div><dt>{{ t('condominiums.form.kit.gateways') }}</dt><dd>{{ plan.kit.gateways }}</dd></div>
            <div><dt>{{ t('condominiums.form.kit.flowMeters') }}</dt><dd>{{ plan.kit.flowMeters }}</dd></div>
            <div><dt>{{ t('condominiums.form.kit.ctClamps') }}</dt><dd>{{ plan.kit.ctClamps }}</dd></div>
            <div class="total"><dt>{{ t('condominiums.form.kit.total') }}</dt><dd>{{ kitTotal(plan.kit) }}</dd></div>
          </dl>
        </template>
        <p v-else class="muted">{{ t('condominiums.form.planPending') }}</p>
        <p class="summary-areas"><i class="pi pi-eye" aria-hidden="true" /> {{ t('condominiums.form.areasCount', { n: form.commonAreas.length }) }}</p>
      </aside>
    </form>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
.breadcrumb { display: flex; align-items: center; gap: .5rem; font-size: .875rem; color: #6b7280; margin-bottom: .5rem; }
.breadcrumb a { color: #6b7280; text-decoration: none; }
.breadcrumb a:hover { color: #059669; }
.breadcrumb .current { color: #059669; font-weight: 500; }
.page-head { margin-bottom: 1.5rem; }
.page-head h1 { margin: 0; }
.page-head p { margin: .25rem 0 0; color: #6b7280; }

.layout { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 1.25rem; align-items: start; }
@media (max-width: 900px) { .layout { grid-template-columns: 1fr; } .summary { order: -1; position: static; } }

.form-card { background: #fff; border: 1px solid #e5e7eb; border-radius: .75rem; box-shadow: 0 1px 2px rgba(0,0,0,.05); padding: 1.5rem; }
.section + .section { border-top: 1px solid #f1f5f9; margin-top: 1.5rem; padding-top: 1.5rem; }
.section h2 { display: flex; align-items: center; gap: .6rem; font-size: 1.05rem; margin: 0 0 1rem; }
.step { display: inline-grid; place-items: center; width: 1.6rem; height: 1.6rem; border-radius: 50%; background: #ECFDF5; color: #047857; font-size: .85rem; font-weight: 700; }
.section-hint { margin: -.5rem 0 1rem; color: #6b7280; font-size: .875rem; }

.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0 1rem; }
@media (max-width: 600px) { .grid-2 { grid-template-columns: 1fr; } }
.field { display: flex; flex-direction: column; gap: .4rem; margin-bottom: 1rem; }
.field label { display: flex; align-items: center; gap: .5rem; font-size: .875rem; font-weight: 600; color: #374151; }
.field label .pi { color: #059669; }
.req { color: #ef4444; margin-left: -.25rem; }
.hint { color: #6b7280; font-size: .8rem; }

.photo { display: flex; align-items: center; gap: 1rem; }
.photo img, .photo-empty { width: 120px; height: 80px; border-radius: .5rem; object-fit: cover; flex-shrink: 0; }
.photo-empty { display: grid; place-items: center; background: #f9fafb; border: 1px dashed #d1d5db; color: #9ca3af; }
.photo-empty .pi { font-size: 1.75rem; }
.photo-actions { display: flex; flex-direction: column; align-items: flex-start; gap: .25rem; flex: 1; min-width: 0; }

.areas { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem; }
@media (max-width: 600px) { .areas { grid-template-columns: 1fr; } }
.area { border: 1px solid #e5e7eb; border-radius: .6rem; padding: .85rem; transition: border-color .15s ease, background .15s ease; }
.area.on { border-color: #10B981; background: #f0fdf4; }
.area-toggle { display: flex; align-items: center; gap: .6rem; cursor: pointer; }
.area-toggle .pi { color: #059669; }
.area-label { font-weight: 600; flex: 1; }
.area-meter { font-size: .75rem; color: #6b7280; }
.area-fields { display: flex; flex-direction: column; gap: .5rem; margin-top: .75rem; }

.actions { display: flex; gap: .75rem; border-top: 1px solid #e5e7eb; margin-top: 1.5rem; padding-top: 1.25rem; }
.flex-1 { flex: 1; }

.summary { position: sticky; top: 6rem; background: #fff; border: 1px solid #e5e7eb; border-radius: .75rem; padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
.summary h2 { font-size: 1rem; margin: 0; }
.plan { display: flex; flex-direction: column; gap: .2rem; padding: 1rem; border-radius: .6rem; background: #ECFDF5; }
.plan-label { font-size: .75rem; text-transform: uppercase; letter-spacing: .04em; color: #047857; font-weight: 600; }
.plan-name { font-size: 1.35rem; color: #064e3b; }
.plan-price { font-weight: 700; font-variant-numeric: tabular-nums; }
.plan-price small { font-weight: 500; color: #6b7280; }
.kit { margin: 0; display: flex; flex-direction: column; gap: .4rem; }
.kit div { display: flex; justify-content: space-between; font-size: .9rem; }
.kit dt { color: #6b7280; }
.kit dd { margin: 0; font-weight: 600; font-variant-numeric: tabular-nums; }
.kit .total { border-top: 1px solid #e5e7eb; padding-top: .4rem; }
.summary-areas { margin: 0; font-size: .875rem; color: #374151; display: flex; gap: .4rem; align-items: center; }
.muted { color: #6b7280; }
</style>
