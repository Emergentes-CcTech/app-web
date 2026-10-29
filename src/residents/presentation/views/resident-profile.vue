<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { ResidentApi } from '../../infrastructure/resident-api.js';
import { ResidentAssembler } from '../../infrastructure/resident.assembler.js';
import { CondominiumsFacade } from '../../infrastructure/condominiums.facade.js';
import { PaymentApi } from '../../../payments/infrastructure/payment-api.js';
import { Payment } from '../../../payments/domain/model/payment.entity.js';
import { collectionSeverity } from '../../domain/model/collection-status.enum.js';
import { deriveCollectionStatus } from '../../application/resident.store.js';
import { formatPeriod, formatPen } from '../../../shared/infrastructure/session.js';

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const api = new ResidentApi();
const paymentApi = new PaymentApi();

const resident = ref(null);
const payments = ref([]);
const loading = ref(true);
const lastThree = computed(() => [...payments.value].sort((a, b) => String(b.period).localeCompare(String(a.period))).slice(0, 3));

onMounted(async () => {
  try {
    const unit = (await api.getById(route.params.id)).data;
    const [profile, users, condos] = await Promise.all([
      api.getProfileByUserId(unit.residentUserId),
      api.getRawByCondominium(unit.condominiumId).then(r => r.users || []),
      CondominiumsFacade.getCondominiums(),
    ]);
    const raw = (await paymentApi.getByUnits([unit.id])).data;
    payments.value = raw.map(p => new Payment(p));
    resident.value = ResidentAssembler.fromJoin({
      unit, profile, user: users.find(u => u.id === unit.residentUserId),
      condominium: condos.find(c => c.id === unit.condominiumId), collectionStatus: deriveCollectionStatus(raw),
    });
  } finally { loading.value = false; }
});
</script>

<template>
  <div class="page">
    <pv-button :label="t('common.back')" icon="pi pi-arrow-left" text @click="router.push({ name: 'residents' })" />
    <div v-if="loading" class="center"><pv-progress-spinner /></div>
    <template v-else-if="resident">
      <h1>{{ resident.fullName }}</h1>
      <div class="grid">
        <section class="card">
          <h3>{{ t('residents.profile.personal') }}</h3>
          <p><b>{{ t('residents.fields.dni') }}:</b> {{ resident.dni }}</p>
          <p><b>{{ t('residents.fields.email') }}:</b> {{ resident.email }}</p>
          <p><b>{{ t('residents.fields.phoneNumber') }}:</b> {{ resident.phoneNumber }}</p>
          <h3>{{ t('residents.profile.unit') }}</h3>
          <p>{{ resident.condominiumName }} · {{ t('residents.fields.unit') }} {{ resident.unitNumber }} · {{ t(`residents.relation.${resident.relation}`) }}</p>
        </section>
        <section class="card">
          <h3>{{ t('residents.profile.account') }} <pv-tag :severity="collectionSeverity(resident.collectionStatus)" :value="t(`residents.status.${resident.collectionStatus}`)" /></h3>
          <p v-if="!lastThree.length" class="muted">{{ t('residents.profile.noSettlements') }}</p>
          <ul v-else class="list">
            <li v-for="p in lastThree" :key="p.id"><span>{{ formatPeriod(p.period, locale) }}</span><span>{{ formatPen(p.amount) }}</span>
              <pv-tag :severity="{ paid: 'success', overdue: 'danger', pending: 'warn' }[p.effectiveStatus]" :value="t(`payments.status.${p.effectiveStatus}`)" /></li>
          </ul>
        </section>
      </div>
    </template>
  </div>
</template>

<style scoped>
.page { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1rem; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.25rem; }
.list { list-style: none; padding: 0; }
.list li { display: flex; justify-content: space-between; align-items: center; padding: .5rem 0; border-bottom: 1px solid #f3f4f6; }
.muted { color: #6B7280; }
.center { text-align: center; padding: 2rem; }
</style>
