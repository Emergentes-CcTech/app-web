import {createRouter, createWebHistory} from "vue-router";
import automationRoutes from "./devices/presentation/automation-routes.js";
import subscriptionsRoutes from "./subscriptions/presentation/subscriptions-routes.js";
import profilesRoutes from "./profiles/presentation/profiles-routes.js";
import condominiumsRoutes from "./condominiums/presentation/condominiums-routes.js";
import telemetryRoutes from "./telemetry/presentation/telemetry-routes.js";
import residentsRoutes from "./residents/presentation/residents-routes.js";
import billingRoutes from "./billing/presentation/billing-routes.js";
import paymentsRoutes from "./payments/presentation/payments-routes.js";
import auditRoutes from "./audit/presentation/audit-routes.js";
import iamRoutes from "./iam/presentation/iam-routes.js";
import { SubscriptionApi } from "./subscriptions/infrastructure/subscription-api.js";
import { TOKEN_KEY, CURRENT_USER_KEY } from "./shared/infrastructure/storage-keys.js";
import { ROUTES } from "./shared/infrastructure/paths.js";
import { RETRY_DELAY_SHORT_MS } from "./shared/infrastructure/constants.js";
import { isActiveStatus } from "./subscriptions/domain/model/subscription-status.enum.js";
import { normalizeRole, isAdmin, ROLES } from "./shared/infrastructure/roles.js";

const subscriptionApi = new SubscriptionApi();

// An administrator may only use the platform once they hold at least one active
// subscription (one per condominium). Fail-closed on error so paid features stay gated.
async function administratorHasActiveSubscription() {
    try {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        if (!currentUser?.id) return false;
        const subs = await subscriptionApi.getSubscriptionsByAdministratorId(currentUser.id);
        return subs.some((sub) => isActiveStatus(sub.status, sub.endDate));
    } catch (error) {
        console.error('[subscription-gate] check failed:', error);
        return false;
    }
}

const routes = [
    { path: '/iam', name: 'iam', children: iamRoutes },
    { path: '/telemetry', children: telemetryRoutes },                                  // ambos roles
    { path: '/devices', meta: { requiresRole: ROLES.ADMIN }, children: automationRoutes },
    { path: '/profiles', name: 'profiles', redirect: '/profiles/profile', children: profilesRoutes },
    { path: '/condominiums', meta: { requiresRole: ROLES.ADMIN }, children: condominiumsRoutes },
    { path: '/residents', meta: { requiresRole: ROLES.ADMIN }, children: residentsRoutes },
    { path: '/billing', meta: { requiresRole: ROLES.ADMIN }, children: billingRoutes },
    { path: '/payments', children: paymentsRoutes },                                    // el rol se define por hijo
    { path: '/audit', children: auditRoutes },                                          // ambos roles
    { path: '/subscriptions', meta: { requiresRole: ROLES.ADMIN }, children: subscriptionsRoutes },
    { path: '/', redirect: ROUTES.IAM_LOGIN },
    { path: '/home', name: 'home', redirect: ROUTES.DASHBOARD },
    { path: '/:pathMatch(.*)*', name: 'NotFound', component: () => import('./shared/presentation/views/page-not-found.vue') },
]

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: routes
});

router.beforeEach(async (to, from, next) => {
    // Check if route requires authentication
    const isPublicRoute = to.meta?.public === true;
    const token = localStorage.getItem(TOKEN_KEY);
    const isAuthenticated = !!token;

    // If route is not public and user is not authenticated, redirect to login page
    if (!isPublicRoute && !isAuthenticated) {
        next(ROUTES.IAM_LOGIN);
        return;
    }
    // If user is authenticated and trying to access login, redirect to home
    if (to.path === ROUTES.IAM_LOGIN && isAuthenticated) {
        next(ROUTES.HOME);
        return;
    }

    // ── Role gate: RBAC validation ──────────────────────────────────────
    // Check if route or any parent route requires a specific role.
    const requiredRoleRecord = to.matched.slice().reverse().find(record => record.meta?.requiresRole);
    if (requiredRoleRecord && isAuthenticated) {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        const userRole = normalizeRole(currentUser?.role);
        const requiredRole = normalizeRole(requiredRoleRecord.meta.requiresRole);
        if (userRole !== requiredRole) {
            // Unauthorized role: redirect to their own dashboard
            next(ROUTES.DASHBOARD);
            return;
        }
    }

    // ── Subscription gate (administrators only) ──────────────────────────────
    // Los administradores sin suscripción activa solo acceden a perfil, suscripción y login;
    // condominios, residentes, dispositivos y prorrateo redirigen a suscripción.
    // El residente no pasa por este guard.
    if (isAuthenticated) {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        const isAdministrator = isAdmin(currentUser?.role);
        // Condominios también se permite: la suscripción es por condominio, así que primero hay que registrarlo.
        const isAllowedWithoutSub = to.path.startsWith(ROUTES.SUBSCRIPTIONS_BASE)
            || to.path.startsWith(ROUTES.IAM_BASE)
            || to.path.startsWith(ROUTES.PROFILES_BASE)
            || to.path.startsWith(ROUTES.CONDOMINIUMS_BASE);

        if (isAdministrator && !isAllowedWithoutSub) {
            // Retry up to 2 times with a short delay to handle the race window
            // between Stripe checkout redirect and the incoming webhook setting
            // status to 'active'. Without this, the first navigation after
            // checkout fails the gate before the webhook fires.
            let active = await administratorHasActiveSubscription();
            if (!active) {
                await new Promise((r) => setTimeout(r, RETRY_DELAY_SHORT_MS));
                active = await administratorHasActiveSubscription();
            }
            if (!active) {
                next(ROUTES.SUBSCRIPTION_DETAIL);
                return;
            }
        }
    }

    next();
});

router.afterEach((to) => {
    // Synchronize document.title after navigation resolves
    const baseTitle = 'IoBuild';
    const title = to.meta?.title || to.matched.slice().reverse().find(r => r.meta?.title)?.meta?.title;
    document.title = title ? `${baseTitle} - ${title}` : baseTitle;
});

export default router;