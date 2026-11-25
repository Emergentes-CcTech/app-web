import SwiftUI
import WebKit

/// Stripe Checkout dentro de la app. Cuando Stripe redirige a la página de retorno de IoBuild
/// (/payments/account-statement?paid=1&payment_id=…&session_id=… o ?canceled=1), la hoja se cierra sola
/// y la app confirma el pago en el backend: no hace falta iniciar sesión en el navegador.
struct CheckoutSheet: View {
    let url: URL
    let onReturn: (_ paymentId: Int?, _ sessionId: String?) -> Void
    @Environment(\.dismiss) private var dismiss
    @State private var isLoading = true

    var body: some View {
        NavigationStack {
            ZStack {
                CheckoutWebView(url: url, isLoading: $isLoading, onReturn: onReturn)
                if isLoading {
                    ProgressView().tint(Theme.emerald600)
                }
            }
            .navigationTitle(L10n.t("checkout_title"))
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(L10n.t("action_close")) { dismiss() }
                }
            }
            .safeAreaInset(edge: .bottom) {
                Text(L10n.t("checkout_hint"))
                    .font(.system(size: 12))
                    .foregroundStyle(Theme.slate500)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity)
                    .padding(10)
                    .background(.bar)
            }
        }
    }
}

private struct CheckoutWebView: UIViewRepresentable {
    let url: URL
    @Binding var isLoading: Bool
    let onReturn: (Int?, String?) -> Void

    func makeCoordinator() -> Coordinator { Coordinator(self) }

    func makeUIView(context: Context) -> WKWebView {
        let configuration = WKWebViewConfiguration()
        configuration.websiteDataStore = .nonPersistent()
        let webView = WKWebView(frame: .zero, configuration: configuration)
        webView.navigationDelegate = context.coordinator
        webView.load(URLRequest(url: url))
        return webView
    }

    func updateUIView(_ uiView: WKWebView, context: Context) {}

    final class Coordinator: NSObject, WKNavigationDelegate {
        private let parent: CheckoutWebView
        private var finished = false

        init(_ parent: CheckoutWebView) { self.parent = parent }

        /// Si la web navegó a la página de retorno de IoBuild, el pago terminó (o se canceló) en Stripe.
        private func detectReturn(in webView: WKWebView) {
            guard !finished, let current = webView.url, current.path.hasSuffix("/payments/account-statement") else { return }
            finished = true
            webView.stopLoading()
            let items = URLComponents(url: current, resolvingAgainstBaseURL: false)?.queryItems ?? []
            let paymentId = items.first(where: { $0.name == "payment_id" })?.value.flatMap { Int($0) }
            let sessionId = items.first(where: { $0.name == "session_id" })?.value
            DispatchQueue.main.async { self.parent.onReturn(paymentId, sessionId) }
        }

        func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
            parent.isLoading = true
            detectReturn(in: webView)
        }

        func webView(_ webView: WKWebView, didReceiveServerRedirectForProvisionalNavigation navigation: WKNavigation!) {
            detectReturn(in: webView)
        }

        func webView(_ webView: WKWebView, didCommit navigation: WKNavigation!) {
            detectReturn(in: webView)
        }

        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            parent.isLoading = false
            detectReturn(in: webView)
        }

        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
            parent.isLoading = false
        }

        func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
            parent.isLoading = false
        }
    }
}
