<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";
import { useRouter } from "vue-router";

import useSubscriptionStore from "../../application/subscription.store.js";
import { useCondominiumStore } from "../../../condominiums/application/condominium.store.js";
import { recommendedTier, MAX_PLAN_UNITS } from "../../domain/model/plan-limits.js";
import { IamFacade } from "../../infrastructure/iam.facade.js";
import PlanCard from "../components/plan-card.vue";
import CurrentPlanCard from "../components/current-plan-card.vue";
import PreviousInvoicesModal from "../components/previous-invoices-modal.vue";
import PlanComparisonModal from "../components/plan-comparison-modal.vue";
import ActivationCard from "../components/activation-card.vue";
import { SubscriptionApi } from "../../infrastructure/subscription-api.js";
import {
  TOAST_SUCCESS_DURATION_MS,
  TOAST_ERROR_DURATION_MS,
  TOAST_INVOICE_ERROR_DURATION_MS,
  TOAST_AUTH_ERROR_DURATION_MS
} from "../../../shared/infrastructure/constants.js";

// Stripe
import { getStripeClient } from "../../infrastructure/stripe-client.js";

const { t, locale } = useI18n();
const confirm = useConfirm();
const toast = useToast();
const store = useSubscriptionStore();
const condoStore = useCondominiumStore();
const router = useRouter();
const condosLoaded = ref(false);
const selectedCondoId = ref(null);
const registeredUnits = computed(() => condoStore.condominiums.find(c => c.id === selectedCondoId.value)?.totalUnits || 0);
const recommendedCode = computed(() => registeredUnits.value ? (recommendedTier(registeredUnits.value)?.code ?? '') : '');
const exceedsCatalog = computed(() => registeredUnits.value > MAX_PLAN_UNITS);
const condoLabel = (c) => c.name;
function onCondoChange(id) { selectedCondoId.value = id; store.selectCondominium(id); }
const subscriptionApi = new SubscriptionApi();
const selectedCondoName = computed(() => condoStore.condominiums.find(c => c.id === selectedCondoId.value)?.name || '');

// Pago de activación del condominio seleccionado: el backend asigna el plan por departamentos
const activationQuote = ref(null);
const activationLoading = ref(false);
async function loadActivationQuote() {
  activationQuote.value = null;
  if (!selectedCondoId.value || store.currentPlan) return;
  activationLoading.value = true;
  try {
    const { data } = await subscriptionApi.getActivationQuote(selectedCondoId.value);
    activationQuote.value = data;
  } catch (_) {
    activationQuote.value = null;
  } finally {
    activationLoading.value = false;
  }
}
watch(() => [selectedCondoId.value, store.currentPlan?.id], loadActivationQuote);

// Equipos por encima del kit del plan: suman a la cuota mensual
const deviceAllowance = ref(null);
watch(selectedCondoId, async (id) => {
  deviceAllowance.value = null;
  if (!id) return;
  try { deviceAllowance.value = (await subscriptionApi.getDeviceAllowance(id)).data; } catch (_) { /* sin recuento no se muestran extras */ }
}, { immediate: true });

const money = (value) => `S/ ${Number(value || 0).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const isProcessing = ref(false);

// Invoices modal
const invoicesVisible = ref(false);
const invoicesLoading = ref(false);
const invoices = ref([]);
const invoicesError = ref("");

// Comparison matrix modal
const comparisonVisible = ref(false);

function formatInvoiceDate(value) {
  const dateLocale = locale.value === 'es' ? 'es-ES' : 'en-US';
  return new Date(value || Date.now()).toLocaleDateString(dateLocale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
}

const getAdministratorId = () => {
  try {
    return IamFacade.getCurrentUserId();
  } catch (error) {
    console.error("[Subscriptions] Error getting user ID:", error);
    toast.add({
      severity: "error",
      summary: t('subscriptions.error'),
      detail: t('subscriptions.errors.authentication'),
      life: TOAST_AUTH_ERROR_DURATION_MS
    });
    throw error;
  }
};

const openInvoicesDialog = async () => {
  invoicesVisible.value = true;
  invoicesLoading.value = true;
  invoicesError.value = "";
  invoices.value = [];

  try {
    const administratorId = getAdministratorId();
    const { data } = await subscriptionApi.getInvoicesByAdministrator(administratorId);
    invoices.value = (data || []).map((r) => ({
      ...r,
      amount: r.amount ?? (r.amountInCents ? (r.amountInCents / 100) : 0),
      currency: r.currency || 'PEN',
      downloadUrl: r.receiptUrl ?? r.downloadUrl ?? null,
      date: formatInvoiceDate(r.date)
    }));
  } catch (error) {
    console.warn("Could not fetch remote Stripe invoices, falling back to local subscription data:", error);
    if (store.currentSubscription && store.currentPlan) {
      invoices.value = [{
        id: `in_sub_${store.currentSubscription.id}`,
        description: t('subscriptions.invoice-plan-description', { planName: store.currentPlan.name }),
        amount: store.currentPlan.monthlyPenWithIgv,
        currency: 'PEN',
        status: store.currentSubscription.status === 'active' ? 'paid' : store.currentSubscription.status,
        date: formatInvoiceDate(store.currentSubscription.startDate),
        downloadUrl: null
      }];
    } else {
      invoices.value = [];
    }
  } finally {
    invoicesLoading.value = false;
  }
};

onMounted(async () => {
  store.fetchAvailablePlans();
  store.fetchCurrentSubscription();

  try {
    await condoStore.fetchAll();
    condosLoaded.value = true;
    // Al volver de Stripe la URL indica qué condominio se pagó
    const returned = Number(new URLSearchParams(window.location.search).get("condominium"));
    selectedCondoId.value = (condoStore.condominiums.find(c => c.id === returned) || condoStore.condominiums[0])?.id ?? null;
    store.selectCondominium(selectedCondoId.value);
    await loadActivationQuote();
  } catch (_) {}

  // Check URL parameters after returning from Stripe checkout
  const urlParams = new URLSearchParams(window.location.search);
  const sessionId = urlParams.get("session_id");
  const hasSuccess = urlParams.get("success") === "true";
  const paidCondominium = Number(urlParams.get("condominium")) || null;

  if (urlParams.get("canceled") === "true") {
    toast.add({ severity: "info", summary: t("subscriptions.warning"), detail: t("condominiums.activation.canceled"), life: TOAST_ERROR_DURATION_MS });
    window.history.replaceState({}, document.title, window.location.pathname);
  }

  if (sessionId || hasSuccess) {
    try {
      if (sessionId) {
        const administratorId = getAdministratorId();
        await subscriptionApi.confirmPayment({ administratorId, condominiumId: selectedCondoId.value }, sessionId);
      }

      toast.add({
        severity: "success",
        summary: t("subscriptions.success"),
        detail: t("subscriptions.payment-success"),
        life: TOAST_SUCCESS_DURATION_MS
      });

      await store.fetchCurrentSubscription();
      store.selectCondominium(selectedCondoId.value);
      // Activado con su kit de equipos: se muestra la ficha del condominio
      if (sessionId && paidCondominium) {
        await condoStore.fetchAll();
        router.replace({ name: 'condominium-details', params: { id: paidCondominium } });
        return;
      }
    } catch (error) {
      console.error("Error confirming payment:", error);
      toast.add({
        severity: "warn",
        summary: t("subscriptions.warning"),
        detail: t('subscriptions.messages.paymentProcessedRefresh'),
        life: TOAST_AUTH_ERROR_DURATION_MS
      });
    } finally {
      const cleanUrl = window.location.pathname;
      window.history.replaceState({}, document.title, cleanUrl);
    }
  }
});

const handleRenewPlan = async () => {
  if (!store.currentPlan) return;
  await handlePayPlan(store.currentPlan);
};

const handleCancelPlan = async () => {
  let message = t("subscriptions.confirm-cancel");
  try {
    const { data } = await subscriptionApi.getCancellationQuote(store.currentSubscription.id);
    // El mes adelantado se aplica al saldo de los equipos; en el último mes ya está pagando ese mes
    const amounts = { months: data.monthsRemaining, balance: money(data.equipmentBalance), credit: money(data.creditApplied),
      due: money(data.amountDue), refund: money(data.refund) };
    if (data.monthsRemaining <= 0) message = Number(data.refund) > 0 ? t('subscriptions.cancellation.completedRefund', amounts) : t('subscriptions.cancellation.completed');
    else if (Number(data.creditApplied) > 0) message = t('subscriptions.cancellation.early', amounts);
    else message = t('subscriptions.cancellation.lastMonth', amounts);
  } catch (_) { /* sin liquidación disponible se muestra la confirmación simple */ }
  confirm.require({
    message,
    acceptLabel: t('common.confirm'),
    rejectLabel: t('common.back'),
    header: t("subscriptions.cancel-header"),
    icon: "pi pi-exclamation-triangle",
    acceptClass: "p-button-danger",
    accept: async () => {
      try {
        await store.cancelSubscription();
        toast.add({
          severity: "success",
          summary: t("subscriptions.success"),
          detail: t("subscriptions.cancelled-successfully"),
          life: TOAST_SUCCESS_DURATION_MS
        });
      } catch (error) {
        toast.add({
          severity: "error",
          summary: t("subscriptions.error"),
          detail: t("subscriptions.cancel-failed"),
          life: TOAST_ERROR_DURATION_MS
        });
      }
    }
  });
};

const handleSelectPlan = (plan) => {
  // El plan no se elige: solo se paga el que corresponde a los departamentos del condominio
  const assignedId = activationQuote.value?.planId ?? store.currentPlan?.id;
  if (Number(plan.id) !== Number(assignedId)) {
    toast.add({ severity: "info", summary: t("subscriptions.warning"), detail: t('subscriptions.activation.assignedOnly'), life: TOAST_ERROR_DURATION_MS });
    return;
  }
  comparisonVisible.value = false;
  handlePayPlan(plan);
};

const handlePayPlan = async (plan) => {
  if (!selectedCondoId.value) {
    toast.add({ severity: "warn", summary: t("subscriptions.warning"), detail: t("subscriptions.needsCondominium"), life: TOAST_ERROR_DURATION_MS });
    return;
  }
  try {
    isProcessing.value = true;
    const administratorId = getAdministratorId();

    const { data } = await subscriptionApi.createCheckoutSession({ administratorId, condominiumId: selectedCondoId.value, planId: plan.id });

    const redirectUrl = data.checkoutUrl || data.CheckoutUrl || data.url || data.Url;
    if (redirectUrl) {
      window.location.href = redirectUrl;
      return;
    }

    const sessionId = data.sessionId || data.SessionId || data.id || data.Id;
    if (sessionId) {
      // La clave pública de Stripe solo hace falta cuando el backend devuelve un sessionId sin URL de checkout
      if (!import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY) {
        throw new Error(t('subscriptions.errors.stripeNotConfigured'));
      }
      const stripe = await getStripeClient();
      if (!stripe) throw new Error(t('subscriptions.errors.stripeInitializationFailed'));

      const { error } = await stripe.redirectToCheckout({ sessionId });
      if (error) {
        console.error("Stripe redirect error:", error);
        toast.add({
          severity: "error",
          summary: t("subscriptions.error"),
          detail: t('subscriptions.errors.paymentRedirectFailed'),
          life: TOAST_ERROR_DURATION_MS
        });
      }
      return;
    }

    throw new Error(t('subscriptions.errors.checkoutSessionMissing'));
  } catch (err) {
    console.error("Payment error:", err);
    toast.add({
      severity: "error",
      summary: t("subscriptions.error"),
      detail: t('subscriptions.errors.paymentStartFailed'),
      life: TOAST_ERROR_DURATION_MS
    });
  } finally {
    isProcessing.value = false;
  }
};
</script>

<template>
  <div class="subscription-page">
    <div class="page-container">
      <!-- Page Header -->
      <div class="page-header">
        <div class="header-left">
          <h1 class="page-title">{{ t("subscriptions.title") }}</h1>
          <p class="page-subtitle">
            {{ t('subscriptions.pageDescription') }}
          </p>
        </div>

        <div class="header-actions">
          <button
            type="button"
            class="header-outline-btn"
            @click="comparisonVisible = true"
          >
            <i class="pi pi-table"></i>
            <span>{{ t('subscriptions.compare-plans') }}</span>
          </button>

          <button
            type="button"
            class="header-outline-btn"
            @click="openInvoicesDialog"
          >
            <i class="pi pi-receipt"></i>
            <span>{{ t('subscriptions.view-invoices') }}</span>
          </button>
        </div>
      </div>

      <div v-if="condoStore.condominiums.length" class="condo-selector" style="margin: 0 0 1rem;">
        <pv-select :modelValue="selectedCondoId" :options="condoStore.condominiums" optionLabel="name" optionValue="id"
                   :placeholder="t('residents.fields.condominium')" style="min-width: 18rem" @update:modelValue="onCondoChange" />
      </div>
      <!-- La suscripción es por condominio: sin condominios no hay a qué asignar el plan -->
      <pv-message v-else-if="condosLoaded" severity="info" :closable="false" class="mb-3">
        <div class="flex align-items-center justify-content-between gap-3 flex-wrap">
          <span>{{ t('subscriptions.needsCondominium') }}</span>
          <pv-button :label="t('condominiums.add')" icon="pi pi-plus" size="small" @click="router.push({ name: 'condominium-new' })" />
        </div>
      </pv-message>

      <!-- Spinner -->
      <div v-if="store.isLoading && !store.availablePlans.length" class="loading-state">
        <pv-progress-spinner style="width: 48px; height: 48px" />
        <span class="loading-text">{{ t('subscriptions.loading') }}</span>
      </div>

      <div v-else>
        <!-- 1. Hero Card: Current Plan & Live Quotas -->
        <CurrentPlanCard
          v-if="store.currentPlan"
          :allowance="deviceAllowance"
          :plan="store.currentPlan"
          :subscription="store.currentSubscription"
          :registeredUnits="registeredUnits"
          :isProcessing="isProcessing"
          @renew="handleRenewPlan"
          @cancel="handleCancelPlan"
          @view-invoices="openInvoicesDialog"
          @compare-plans="comparisonVisible = true"
        />

        <!-- Sin suscripción: activación del condominio con el plan que le corresponde -->
        <ActivationCard
          v-else-if="selectedCondoId"
          :quote="activationQuote"
          :condominiumName="selectedCondoName"
          :loading="activationLoading"
          :isProcessing="isProcessing"
          @activate="(quote) => handlePayPlan({ id: quote.planId })"
        />

        <!-- 2. Plans Grid Section -->
        <div class="plans-section">
          <div class="plans-section-header">
            <h2 class="section-title">{{ t("subscriptions.all-plans") }}</h2>
            <p class="section-subtitle">
              {{ t('subscriptions.allPlansDescription') }}
            </p>
          </div>

          <pv-message v-if="exceedsCatalog" severity="warn" :closable="false" class="mb-3">
            {{ t('subscriptions.exceedsCatalog', { units: registeredUnits, max: MAX_PLAN_UNITS }) }}
          </pv-message>

          <div class="plans-grid">
            <PlanCard
              v-for="plan in store.availablePlans"
              :key="plan.id"
              :plan="plan"
              :currentPlan="store.currentPlan"
              :isPopular="plan.code === recommendedCode"
              :registeredUnits="registeredUnits"
              :isProcessing="isProcessing"
              :selectable="false"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Modals -->
    <PreviousInvoicesModal
      :visible="invoicesVisible"
      :invoices="invoices"
      :loading="invoicesLoading"
      :error="invoicesError"
      @close="invoicesVisible = false"
    />

    <PlanComparisonModal
      :visible="comparisonVisible"
      :plans="store.availablePlans"
      :recommendedCode="recommendedCode"
        :currentPlanId="store.currentPlan?.id"
      :isProcessing="isProcessing"
      @close="comparisonVisible = false"
      @select-plan="handleSelectPlan"
    />
  </div>
</template>

<style scoped>
.subscription-page {
  min-height: 100vh;
  background-color: #f8fafc;
  padding: 2.5rem 2rem 5rem 2rem;
  color: #0f172a;
  font-family: inherit;
}

.page-container {
  max-width: 1240px;
  margin: 0 auto;
}

/* Page Header */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.page-title {
  font-size: 2rem;
  font-weight: 800;
  color: #0f172a;
  letter-spacing: -0.025em;
  margin: 0 0 0.35rem 0;
}

.page-subtitle {
  font-size: 0.88rem;
  color: #64748b;
  margin: 0;
  line-height: 1.5;
  max-width: 600px;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.header-outline-btn {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  background: #ffffff;
  color: #334155;
  border: 1px solid #cbd5e1;
  padding: 0.55rem 1rem;
  border-radius: 0.75rem;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  font-family: inherit;
}

.header-outline-btn:hover {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.header-outline-btn i {
  color: #64748b;
  font-size: 0.8rem;
}

/* Loading */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 6rem 0;
  gap: 1rem;
}

.loading-text {
  font-size: 0.88rem;
  color: #64748b;
}

/* Plans Section */
.plans-section-header {
  margin-bottom: 1.75rem;
}

.section-title {
  font-size: 1.4rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 0.25rem 0;
}

.section-subtitle {
  font-size: 0.82rem;
  color: #64748b;
  margin: 0;
}

/* Plans Grid */
.plans-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1.75rem;
  align-items: stretch;
}

/* Responsive */
@media (max-width: 1024px) {
  .plans-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .subscription-page {
    padding: 1.5rem 1rem 3rem 1rem;
  }
  .plans-grid {
    grid-template-columns: 1fr;
  }
  .page-header {
    flex-direction: column;
  }
  .header-actions {
    width: 100%;
  }
  .header-outline-btn {
    flex: 1;
    justify-content: center;
  }
}
</style>
