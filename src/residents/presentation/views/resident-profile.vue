<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { ResidentApi } from '../../infrastructure/resident-api.js';
import { PaymentApi } from '../../../payments/infrastructure/payment-api.js';
import { Payment, PaymentStatus } from '../../../payments/domain/model/payment.entity.js';
import { collectionSeverity } from '../../domain/model/collection-status.enum.js';
import { deriveCollectionStatus } from '../../application/resident.store.js';
import { formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const api = new ResidentApi();
const paymentApi = new PaymentApi();

const resident = ref(null);
const unit = ref(null);
const payments = ref([]);
const collectionStatus = ref('current');
const loading = ref(true);
const failed = ref(false);

const history = computed(() => [...payments.value].sort((a, b) => String(b.period).localeCompare(String(a.period))));
const unpaid = computed(() => payments.value.filter(p => p.effectiveStatus !== PaymentStatus.PAID));
const pendingBalance = computed(() => Math.round(unpaid.value.reduce((acc, p) => acc + p.amount * 100, 0)) / 100);
const paidCount = computed(() => payments.value.length - unpaid.value.length);
const paymentSeverity = (s) => ({ paid: 'success', overdue: 'danger', pending: 'warn' }[s]);

// Iniciales para el avatar cuando el residente no subió foto a Cloudinary
const initials = computed(() => (resident.value?.fullName || '')
  .split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join(''));
const aliquot = computed(() => {
  const v = unit.value?.aliquotPercentage ?? unit.value?.participationPct;
  return v == null ? null : Number(v).toFixed(4);
});

onMounted(async () => {
  try {
    unit.value = (await api.getById(route.params.id)).data;
    // El padrón de la administradora ya trae contacto y foto; /profiles solo expone el perfil propio
    const [raw, paymentsRes] = await Promise.all([
      api.getRawByCondominium(unit.value.condominiumId),
      paymentApi.getByUnits([unit.value.id]),
    ]);
    const roster = raw.direct || [];
    resident.value = roster.find(r => r.unitId === unit.value.id) || null;
    payments.value = paymentsRes.data.map(p => new Payment(p));
    collectionStatus.value = deriveCollectionStatus(paymentsRes.data);
  } catch (_) {
    failed.value = true;
  } finally { loading.value = false; }
});
</script>

<template>
  <div class="page">
    <pv-button :label="t('common.back')" icon="pi pi-arrow-left" text class="back" @click="router.push({ name: 'residents' })" />
    <div v-if="loading" class="center"><pv-progress-spinner /></div>
    <pv-message v-else-if="failed || !resident" severity="error" :closable="false">{{ t('residents.profile.loadError') }}</pv-message>

    <template v-else>
      <header class="identity">
        <img v-if="resident.photoUrl" :src="resident.photoUrl" :alt="resident.fullName" class="avatar" />
        <span v-else class="avatar initials" aria-hidden="true">{{ initials }}</span>
        <div class="identity-text">
          <h1>{{ resident.fullName }}</h1>
          <p class="subtitle">{{ t(`residents.relation.${resident.relation}`) }} · {{ resident.condominiumName }} · {{ t('residents.fields.unit') }} {{ resident.unitNumber }}</p>
          <pv-tag :severity="collectionSeverity(collectionStatus)" :value="t(`residents.status.${collectionStatus}`)" />
        </div>
      </header>

      <div class="layout">
        <div class="side">
          <section class="panel">
            <h2>{{ t('residents.profile.contact') }}</h2>
            <dl class="facts">
              <div>
                <dt>{{ t('residents.fields.email') }}</dt>
                <dd><a v-if="resident.email" :href="`mailto:${resident.email}`">{{ resident.email }}</a><span v-else class="muted">{{ t('residents.profile.notRegistered') }}</span></dd>
              </div>
              <div>
                <dt>{{ t('residents.fields.phoneNumber') }}</dt>
                <dd><a v-if="resident.phoneNumber" :href="`tel:${resident.phoneNumber}`">{{ resident.phoneNumber }}</a><span v-else class="muted">{{ t('residents.profile.notRegistered') }}</span></dd>
              </div>
              <div>
                <dt>{{ t('residents.fields.dni') }}</dt>
                <dd><span v-if="resident.dni">{{ resident.dni }}</span><span v-else class="muted">{{ t('residents.profile.notRegistered') }}</span></dd>
              </div>
            </dl>
          </section>

          <section class="panel">
            <h2>{{ t('residents.profile.unit') }}</h2>
            <dl class="facts two-col">
              <div><dt>{{ t('residents.fields.unit') }}</dt><dd>{{ resident.unitNumber }}</dd></div>
              <div v-if="unit.floor != null"><dt>{{ t('residents.profile.floor') }}</dt><dd>{{ unit.floor }}</dd></div>
              <div v-if="unit.areaM2 != null"><dt>{{ t('residents.profile.area') }}</dt><dd class="num">{{ Number(unit.areaM2).toFixed(2) }} m²</dd></div>
              <div v-if="aliquot"><dt>{{ t('residents.profile.aliquot') }}</dt><dd class="num strong">{{ aliquot }}%</dd></div>
            </dl>
          </section>
        </div>

        <section class="panel main">
          <div class="panel-head">
            <h2>{{ t('residents.profile.account') }}</h2>
          </div>
          <div class="summary">
            <div>
              <span class="label">{{ t('residents.profile.pendingBalance') }}</span>
              <strong class="amount" :class="{ due: pendingBalance > 0 }">{{ formatPen(pendingBalance) }}</strong>
            </div>
            <div>
              <span class="label">{{ t('residents.profile.paidPeriods') }}</span>
              <strong class="amount">{{ paidCount }} / {{ payments.length }}</strong>
            </div>
          </div>

          <pv-data-table :value="history" dataKey="id" size="small" :rows="6" :paginator="history.length > 6">
            <template #empty>{{ t('residents.profile.noSettlements') }}</template>
            <pv-column :header="t('payments.statement.period')"><template #body="{ data }">{{ formatPeriod(data.period, locale) }}</template></pv-column>
            <pv-column :header="t('payments.statement.amount')" bodyClass="num"><template #body="{ data }">{{ formatPen(data.amount) }}</template></pv-column>
            <pv-column :header="t('payments.statement.dueDate')" field="dueDate" />
            <pv-column :header="t('payments.statement.status')"><template #body="{ data }"><pv-tag :severity="paymentSeverity(data.effectiveStatus)" :value="t(`payments.status.${data.effectiveStatus}`)" /></template></pv-column>
          </pv-data-table>
        </section>
      </div>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
.back { margin-left: -.75rem; margin-bottom: .5rem; }
.center { text-align: center; padding: 2rem; }

.identity { display: flex; align-items: center; gap: 1.25rem; margin-bottom: 1.5rem; }
.avatar { width: 72px; height: 72px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
.avatar.initials { display: grid; place-items: center; background: var(--iobuilt-primary-hover); color: #fff; font-weight: 700; font-size: 1.5rem; }
.identity h1 { margin: 0; font-size: 1.6rem; }
.subtitle { margin: .2rem 0 .6rem; color: var(--iobuilt-text-secondary); }

.layout { display: grid; grid-template-columns: minmax(260px, 1fr) 2fr; gap: 1rem; align-items: start; }
.side { display: flex; flex-direction: column; gap: 1rem; }
.panel { background: var(--iobuilt-surface); border: 1px solid var(--iobuilt-border); border-radius: 12px; padding: 1.25rem; }
.panel h2 { font-size: 1rem; margin: 0 0 1rem; }

.facts { margin: 0; display: flex; flex-direction: column; gap: .85rem; }
.facts.two-col { display: grid; grid-template-columns: 1fr 1fr; }
.facts dt { font-size: .8rem; color: var(--iobuilt-text-secondary); }
.facts dd { margin: 2px 0 0; overflow-wrap: anywhere; }
.facts a { color: var(--iobuilt-primary-hover); text-decoration: none; }
.facts a:hover { text-decoration: underline; }
.muted { color: var(--iobuilt-text-secondary); font-style: italic; }
.num { font-variant-numeric: tabular-nums; }
.strong { font-weight: 700; }

.summary { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; margin-bottom: 1rem; padding-bottom: 1rem; border-bottom: 1px solid var(--iobuilt-border); }
.label { display: block; font-size: .8rem; color: var(--iobuilt-text-secondary); }
.amount { font-size: 1.5rem; font-variant-numeric: tabular-nums; }
.amount.due { color: #b91c1c; }

@media (max-width: 760px) {
  .layout { grid-template-columns: 1fr; }
  .identity { align-items: flex-start; }
}
</style>
