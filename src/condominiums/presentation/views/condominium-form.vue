<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { useCondominiumStore } from '../../application/condominium.store.js';
import { Condominium } from '../../domain/model/condominium.entity.js';
import { CommonArea, COMMON_AREA_TYPES } from '../../domain/model/common-area.entity.js';
import { recommendedTier, MAX_PLAN_UNITS } from '../../../subscriptions/domain/model/plan-limits.js';
import { isValidName, isValidSupplyCode, isValidPositiveInteger } from '../../../shared/presentation/validators.js';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = useCondominiumStore();

const isEdit = computed(() => !!route.params.id);
const form = reactive(new Condominium({}));
const errors = reactive({});
const saving = ref(false);

const areaTypeOptions = computed(() => COMMON_AREA_TYPES.map(a => ({ value: a.type, label: t(`condominiums.commonAreas.types.${a.type}`) })));
// El número de departamentos define el plan (Boutique ≤ 20, Urbano 21-50, Gran Torre 51-80)
const suggestedPlan = computed(() => form.totalUnits ? recommendedTier(form.totalUnits) : null);

onMounted(async () => {
  if (isEdit.value) Object.assign(form, await store.fetchById(route.params.id));
});

function addArea() {
  form.commonAreas.push(new CommonArea({ type: 'other' }));
}
function removeArea(index) {
  form.commonAreas.splice(index, 1);
}
function onAreaTypeChange(area, type) {
  // Si el nombre está vacío o era el nombre por defecto del tipo anterior, se sugiere el del tipo nuevo
  const previousDefault = t(`condominiums.commonAreas.types.${area.type}`);
  if (!area.name || area.name === previousDefault) area.name = t(`condominiums.commonAreas.types.${type}`);
  area.type = type;
}

function validate() {
  Object.keys(errors).forEach(k => delete errors[k]);
  if (!isValidName(form.name, 3, 120)) errors.name = t('condominiums.validation.length', { min: 3, max: 120 });
  if (!isValidName(form.address, 5, 200)) errors.address = t('condominiums.validation.length', { min: 5, max: 200 });
  if (!isValidName(form.district, 3, 80)) errors.district = t('condominiums.validation.length', { min: 3, max: 80 });
  if (!isValidPositiveInteger(form.totalUnits, 1, MAX_PLAN_UNITS)) errors.totalUnits = t('condominiums.validation.units', { max: MAX_PLAN_UNITS });
  if (!isValidSupplyCode(form.waterSupplyCode)) errors.waterSupplyCode = t('condominiums.validation.supplyCode');
  if (!isValidSupplyCode(form.electricitySupplyCode)) errors.electricitySupplyCode = t('condominiums.validation.supplyCode');
  if (!form.commonAreas.length) errors.commonAreas = t('condominiums.validation.commonAreasRequired');
  else if (form.commonAreas.some(a => !isValidName(a.name, 2, 60))) errors.commonAreas = t('condominiums.validation.commonAreaName');
  return !Object.keys(errors).length;
}

async function save() {
  if (!validate()) return;
  saving.value = true;
  try {
    const payload = { ...form, commonAreas: form.commonAreas.map(a => ({ ...a })) };
    const saved = isEdit.value ? await store.update(payload) : await store.create(payload);
    toast.add({ severity: 'success', summary: t('common.success'), detail: t('condominiums.messages.saved'), life: 2500 });
    router.push({ name: 'condominium-details', params: { id: saved.id } });
  } catch (e) {
    toast.add({ severity: 'error', summary: 'Error', detail: t('condominiums.messages.saveError'), life: 3000 });
  } finally { saving.value = false; }
}
</script>

<template>
  <div class="page">
    <h1>{{ isEdit ? t('condominiums.editTitle') : t('condominiums.newTitle') }}</h1>
    <div class="form-card">
      <div v-for="f in ['name','address','district']" :key="f" class="field">
        <label :for="f">{{ t(`condominiums.fields.${f}`) }} *</label>
        <pv-input-text :id="f" v-model="form[f]" :invalid="!!errors[f]" class="w-full" />
        <small v-if="errors[f]" class="p-error">{{ errors[f] }}</small>
      </div>
      <div class="field">
        <label for="totalUnits">{{ t('condominiums.fields.totalUnits') }} *</label>
        <pv-input-number inputId="totalUnits" v-model="form.totalUnits" :min="1" :max="MAX_PLAN_UNITS" :useGrouping="false" :invalid="!!errors.totalUnits" class="w-full" />
        <small v-if="errors.totalUnits" class="p-error">{{ errors.totalUnits }}</small>
        <small v-else-if="suggestedPlan" class="hint">{{ t('condominiums.suggestedPlan', { plan: suggestedPlan.name }) }}</small>
      </div>
      <div v-for="f in ['waterSupplyCode','electricitySupplyCode']" :key="f" class="field">
        <label :for="f">{{ t(`condominiums.fields.${f}`) }} *</label>
        <pv-input-text :id="f" v-model="form[f]" inputmode="numeric" :invalid="!!errors[f]" class="w-full" />
        <small v-if="errors[f]" class="p-error">{{ errors[f] }}</small>
      </div>

      <fieldset class="areas">
        <legend>{{ t('condominiums.commonAreas.title') }} *</legend>
        <p class="hint">{{ t('condominiums.commonAreas.hint') }}</p>
        <div v-for="(area, i) in form.commonAreas" :key="i" class="area-row">
          <pv-select :modelValue="area.type" :options="areaTypeOptions" optionLabel="label" optionValue="value"
                     :aria-label="t('condominiums.commonAreas.type')" class="area-type" @update:modelValue="onAreaTypeChange(area, $event)" />
          <pv-input-text v-model="area.name" :placeholder="t('condominiums.commonAreas.namePlaceholder')"
                         :aria-label="t('condominiums.commonAreas.name')" class="area-name" />
          <pv-button icon="pi pi-trash" text rounded severity="danger" :aria-label="t('common.delete')" @click="removeArea(i)" />
        </div>
        <pv-button :label="t('condominiums.commonAreas.add')" icon="pi pi-plus" severity="secondary" outlined size="small" @click="addArea" />
        <small v-if="errors.commonAreas" class="p-error block">{{ errors.commonAreas }}</small>
      </fieldset>

      <div class="actions">
        <pv-button :label="t('common.cancel')" severity="secondary" @click="router.back()" />
        <pv-button :label="t('common.save')" icon="pi pi-check" :loading="saving" @click="save" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 720px; margin: 0 auto; }
.form-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.5rem; }
.field { margin-bottom: 14px; }
.field label { display: block; margin-bottom: 4px; font-weight: 500; }
.hint { display: block; color: #6B7280; font-size: .85rem; margin: 4px 0 8px; }
.areas { border: 1px solid #e5e7eb; border-radius: 10px; padding: 1rem; margin: 1rem 0; }
.areas legend { font-weight: 600; padding: 0 .4rem; }
.area-row { display: flex; gap: .5rem; align-items: center; margin-bottom: .5rem; }
.area-type { width: 12rem; flex-shrink: 0; }
.area-name { flex: 1; min-width: 0; }
@media (max-width: 560px) { .area-row { flex-wrap: wrap; } .area-type { width: 100%; } }
.actions { display: flex; justify-content: flex-end; gap: .5rem; margin-top: 1rem; }
</style>
