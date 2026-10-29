import { defineStore } from "pinia";
import { ref, computed } from "vue";
import { SubscriptionApi } from "../infrastructure/subscription-api.js";
import { SubscriptionAssembler } from "../infrastructure/subscription.assembler.js";
import { PlanApi } from "../infrastructure/plan-api.js";
import { PlanAssembler } from "../infrastructure/plan.assembler.js";
import { IamFacade } from "../infrastructure/iam.facade.js";

const subscriptionApi = new SubscriptionApi();
const planApi = new PlanApi();

export const useSubscriptionStore = defineStore("subscriptions", () => {
    const subscriptions = ref([]);
    const selectedCondominiumId = ref(null);
    const currentSubscription = ref(null);
    const availablePlans = ref([]);
    const errors = ref([]);
    const isLoading = ref(false);

    // Helper function to get administratorId from authenticated user using Facade (ACL)
    const getAdministratorId = () => {
        try {
            return IamFacade.getCurrentUserId();
        } catch (error) {
            console.error('[SubscriptionStore] Error getting user ID:', error);
            throw error;
        }
    };

    // Fetch available plans from API
    function fetchAvailablePlans() {
        isLoading.value = true;
        return planApi
            .getAllPlans()
            .then((response) => {
                const plans = PlanAssembler.toEntitiesFromResourceArray(response.data);
                availablePlans.value = plans.length ? plans : PlanAssembler.catalog();
                isLoading.value = false;
            })
            .catch((error) => {
                // Sin /plans en el backend se usa el catálogo local (Boutique, Urbano, Gran Torre)
                availablePlans.value = PlanAssembler.catalog();
                errors.value.push(error);
                isLoading.value = false;
            });
    }

    /** Suscripción vigente (o la más reciente) de un condominio */
    function subscriptionFor(condominiumId) {
        const forCondo = subscriptions.value.filter(s => Number(s.condominiumId) === Number(condominiumId));
        return forCondo.find(s => s.isActive()) || forCondo[0] || null;
    }

    /** Plan de una suscripción, resuelto contra el catálogo cargado */
    function planFor(subscription) {
        if (!subscription) return null;
        if (subscription.plan) return subscription.plan;
        const name = String(subscription.planName || '').toLowerCase();
        return availablePlans.value.find(p => p.id === subscription.planId || p.name.toLowerCase() === name)
            || PlanAssembler.catalog().find(p => p.name.toLowerCase() === name) || null;
    }

    const currentPlan = computed(() => {
        if (!currentSubscription.value) return null;
        if (currentSubscription.value.plan) return currentSubscription.value.plan;
        if (currentSubscription.value.planId && availablePlans.value.length > 0) {
            return availablePlans.value.find(p => p.id === currentSubscription.value.planId) || null;
        }
        if (currentSubscription.value.planName && availablePlans.value.length > 0) {
            return availablePlans.value.find(p => p.name.toLowerCase() === String(currentSubscription.value.planName).toLowerCase()) || null;
        }
        return null;
    });

    const otherPlans = computed(() => {
        const plan = currentPlan.value;
        if (!plan) return availablePlans.value;
        return availablePlans.value.filter(p => p.id !== plan.id);
    });

    function pickCurrent() {
        const list = subscriptions.value;
        const forCondo = selectedCondominiumId.value ? list.filter(s => Number(s.condominiumId) === Number(selectedCondominiumId.value)) : list;
        const chosen = forCondo.find(s => s.isActive()) || forCondo[0] || null;
        currentSubscription.value = chosen;
        return chosen;
    }

    /** Todas las suscripciones del administrador (una por condominio) */
    function fetchSubscriptions() {
        isLoading.value = true;
        // Sin sesión (p. ej. justo al cerrar sesión) el error queda dentro de la promesa y se registra en errors
        return Promise.resolve()
            .then(() => subscriptionApi.getSubscriptionsByAdministratorId(getAdministratorId()))
            .then((rows) => {
                subscriptions.value = rows.map(r => SubscriptionAssembler.toEntityFromResource(r));
                pickCurrent();
                isLoading.value = false;
            })
            .catch((error) => {
                if (error?.response?.status === 404) { subscriptions.value = []; currentSubscription.value = null; }
                else errors.value.push(error);
                isLoading.value = false;
            });
    }

    function selectCondominium(condominiumId) {
        selectedCondominiumId.value = condominiumId;
        pickCurrent();
    }

    const fetchCurrentSubscription = fetchSubscriptions;

    function cancelSubscription() {
        if (!currentSubscription.value) return Promise.reject('No current subscription');

        isLoading.value = true;
        return subscriptionApi
            .cancelSubscription(currentSubscription.value.id)
            .then(() => {
                // Refresh current subscription
                return fetchCurrentSubscription();
            })
            .catch((error) => {
                errors.value.push(error);
                isLoading.value = false;
                throw error;
            });
    }

    function clearUserSession() {
        subscriptions.value = [];
        selectedCondominiumId.value = null;
        currentSubscription.value = null;
        errors.value = [];
    }

    return {
        subscriptions,
        selectedCondominiumId,
        fetchSubscriptions,
        selectCondominium,
        subscriptionFor,
        planFor,
        currentSubscription,
        availablePlans,
        currentPlan,
        otherPlans,
        errors,
        isLoading,
        fetchCurrentSubscription,
        fetchAvailablePlans,
        cancelSubscription,
        clearUserSession
    };
});

export default useSubscriptionStore;

