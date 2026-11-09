import { SubscriptionApi } from "../infrastructure/subscription-api.js";
import { IamFacade } from "../infrastructure/iam.facade.js";

const api = new SubscriptionApi();

/**
 * Abre el pago de activación de un condominio: el backend asigna el plan por su número de departamentos.
 * Devuelve la URL de Stripe Checkout (o la de retorno, si los pagos están simulados) a la que hay que ir.
 */
export async function startActivationCheckout(condominiumId) {
    const { data: quote } = await api.getActivationQuote(condominiumId);
    const administratorId = IamFacade.getCurrentUserId();
    const { data } = await api.createCheckoutSession({ administratorId, condominiumId, planId: quote.planId });
    const url = data.checkoutUrl || data.CheckoutUrl || data.url || data.Url;
    if (!url) throw new Error("checkout_url_missing");
    return url;
}
