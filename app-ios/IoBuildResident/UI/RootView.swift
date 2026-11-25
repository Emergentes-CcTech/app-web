import SwiftUI

/// Muestra el inicio de sesión o las pestañas del propietario y confirma los pagos al volver a la app.
struct RootView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        Group {
            if model.isAuthenticated {
                MainTabView()
            } else {
                AuthFlowView()
            }
        }
        .preferredColorScheme(.light)
        .onChange(of: scenePhase) { _, phase in
            // Al volver a la app se confirma cualquier pago de Stripe que haya quedado abierto
            if phase == .active {
                Task { await model.confirmPendingCheckout() }
            }
        }
    }
}

struct MainTabView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        @Bindable var model = model
        TabView(selection: $model.selectedTab) {
            NavigationStack { ConsumptionView() }
                .tabItem { Label(L10n.t("tab_consumption"), systemImage: "drop.fill") }
                .tag(AppModel.Tab.consumption)
            NavigationStack { StatementView() }
                .tabItem { Label(L10n.t("tab_payments"), systemImage: "creditcard.fill") }
                .tag(AppModel.Tab.payments)
            NavigationStack { FeeView() }
                .tabItem { Label(L10n.t("tab_fee"), systemImage: "doc.text.magnifyingglass") }
                .tag(AppModel.Tab.fee)
            NavigationStack { ProfileView() }
                .tabItem { Label(L10n.t("tab_profile"), systemImage: "person.crop.circle") }
                .tag(AppModel.Tab.profile)
        }
        .sheet(item: $model.checkout, onDismiss: {
            Task { await model.confirmPendingCheckout() }
        }) { page in
            CheckoutSheet(url: page.url) { paymentId, sessionId in
                model.checkoutReturned(paymentId: paymentId, sessionId: sessionId)
            }
        }
    }
}

/// Encabezado común de las pestañas: logo y departamento del propietario.
struct TabTopBar: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        HStack(spacing: 10) {
            IoBuildLogo(size: 26)
            VStack(alignment: .leading, spacing: 1) {
                Text("IoBuild").font(.system(size: 16, weight: .heavy)).foregroundStyle(Theme.slate900)
                Text(subtitle).font(.system(size: 11)).foregroundStyle(Theme.slate500).lineLimit(1)
            }
            Spacer()
        }
    }

    private var subtitle: String {
        if let unit = model.overview?.unit?.unitNumber, let condo = model.overview?.condominium?.name {
            return L10n.t("unit_in_condominium", unit, condo)
        }
        return L10n.t("owner_portal")
    }
}
