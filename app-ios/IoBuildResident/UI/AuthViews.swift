import SwiftUI

struct AuthFlowView: View {
    var body: some View {
        NavigationStack {
            LoginView()
        }
    }
}

// MARK: - Inicio de sesión

struct LoginView: View {
    @Environment(AppModel.self) private var model
    @State private var email = ""
    @State private var password = ""
    @State private var showPassword = false
    @State private var showServer = false
    @FocusState private var focused: Field?

    private enum Field { case email, password }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                HStack(spacing: 12) {
                    IoBuildLogo(size: 44)
                    Text("IoBuild").font(.system(size: 26, weight: .heavy)).foregroundStyle(Theme.slate900)
                }
                .padding(.top, 24)

                VStack(alignment: .leading, spacing: 6) {
                    Text(L10n.t("login_greeting")).font(.system(size: 28, weight: .heavy)).foregroundStyle(Theme.slate900)
                    Text(L10n.t("login_subtitle")).font(.system(size: 15)).foregroundStyle(Theme.slate500)
                }

                VStack(spacing: 12) {
                    LabeledField(title: L10n.t("field_email")) {
                        TextField("", text: $email, prompt: Text(verbatim: "nombre@correo.com"))
                            .textContentType(.username)
                            .keyboardType(.emailAddress)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .focused($focused, equals: .email)
                            .submitLabel(.next)
                            .onSubmit { focused = .password }
                    }
                    LabeledField(title: L10n.t("field_password")) {
                        HStack {
                            Group {
                                if showPassword {
                                    TextField("", text: $password)
                                } else {
                                    SecureField("", text: $password)
                                }
                            }
                            .textContentType(.password)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .focused($focused, equals: .password)
                            .submitLabel(.go)
                            .onSubmit(signIn)
                            Button { showPassword.toggle() } label: {
                                Image(systemName: showPassword ? "eye.slash" : "eye").foregroundStyle(Theme.slate500)
                            }
                            .accessibilityLabel(L10n.t(showPassword ? "password_hide" : "password_show"))
                        }
                    }
                }

                if let error = model.authError {
                    ErrorBanner(message: error)
                }

                Button(action: signIn) {
                    if model.authLoading { ProgressView().tint(.white) } else { Text(L10n.t("action_sign_in")) }
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(model.authLoading)

                VStack(alignment: .leading, spacing: 8) {
                    Text(L10n.t("login_first_time")).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
                    Text(L10n.t("login_first_time_body")).font(.system(size: 13)).foregroundStyle(Theme.slate700)
                    NavigationLink {
                        RegisterView()
                    } label: {
                        Text(L10n.t("action_create_account"))
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(Theme.emerald700)
                    }
                    .simultaneousGesture(TapGesture().onEnded { model.authError = nil })
                }
                .card(background: Theme.emerald50, border: Theme.emerald500.opacity(0.3))

                #if DEBUG
                Button { showServer = true } label: {
                    Text(L10n.t("debug_server_in_use", model.session.baseURL))
                        .font(.system(size: 11))
                        .foregroundStyle(Theme.slate500)
                        .frame(maxWidth: .infinity)
                }
                #endif
            }
            .padding(20)
        }
        .scrollDismissesKeyboard(.interactively)
        .background(Theme.background.ignoresSafeArea())
        .sheet(isPresented: $showServer) { ServerSheet() }
    }

    private func signIn() {
        focused = nil
        Task { await model.signIn(email: email, password: password) }
    }
}

/// Solo en compilaciones Debug: apuntar la app a un backend de pruebas.
private struct ServerSheet: View {
    @Environment(AppModel.self) private var model
    @Environment(\.dismiss) private var dismiss
    @State private var url = ""

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField(L10n.t("debug_server_url"), text: $url)
                        .keyboardType(.URL)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                } footer: {
                    Text(verbatim: "Ej.: http://192.168.1.20:8080/api/v1/ — " + AppConfig.productionBaseURL)
                }
                Section {
                    Button(L10n.t("debug_server_production")) {
                        model.session.resetServer()
                        dismiss()
                    }
                }
            }
            .navigationTitle(L10n.t("debug_server_title"))
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button(L10n.t("action_cancel")) { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button(L10n.t("action_save")) {
                        if !url.isBlank { model.session.baseURL = url }
                        dismiss()
                    }
                }
            }
            .onAppear { url = model.session.baseURL }
        }
        .presentationDetents([.medium])
    }
}

// MARK: - Registro en dos pasos (propietario invitado por la administración)

struct RegisterView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.dismiss) private var dismiss
    @State private var email = ""
    @State private var password = ""
    @State private var confirmation = ""
    @State private var fullName = ""
    @State private var dni = ""
    @State private var phone = ""
    @State private var acceptedPrivacy = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                Text(L10n.t("register_step", model.registerStep))
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(Theme.emerald700)
                    .padding(.horizontal, 10).padding(.vertical, 4)
                    .background(Theme.emerald50, in: Capsule())

                if model.registerStep == 1 { accountStep } else { detailsStep }

                if let error = model.authError {
                    ErrorBanner(message: error)
                }
            }
            .padding(20)
        }
        .scrollDismissesKeyboard(.interactively)
        .background(Theme.background.ignoresSafeArea())
        .navigationBarTitleDisplayMode(.inline)
        .onDisappear {
            if !model.isAuthenticated { model.backToRegisterStep1() }
        }
    }

    private var accountStep: some View {
        VStack(alignment: .leading, spacing: 16) {
            ScreenHeader(title: L10n.t("register_title"), subtitle: L10n.t("register_subtitle"))
            LabeledField(title: L10n.t("field_email")) {
                TextField("", text: $email)
                    .textContentType(.username)
                    .keyboardType(.emailAddress)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
            }
            LabeledField(title: L10n.t("field_password"), hint: L10n.t("register_password_hint")) {
                SecureField("", text: $password).textContentType(.newPassword)
            }
            LabeledField(title: L10n.t("field_confirm_password")) {
                SecureField("", text: $confirmation).textContentType(.newPassword)
            }
            Button {
                Task { await model.verifyInvitation(email: email, password: password, confirmation: confirmation) }
            } label: {
                if model.authLoading { ProgressView().tint(.white) } else { Text(L10n.t("action_continue")) }
            }
            .buttonStyle(PrimaryButtonStyle())
            .disabled(model.authLoading)

            Button(L10n.t("register_back_to_login")) { dismiss() }
                .font(.system(size: 14, weight: .semibold))
                .frame(maxWidth: .infinity)
        }
    }

    private var detailsStep: some View {
        VStack(alignment: .leading, spacing: 16) {
            ScreenHeader(title: L10n.t("register_details_title"), subtitle: L10n.t("register_details_subtitle"))

            if !model.invitedUnits.isEmpty {
                VStack(alignment: .leading, spacing: 8) {
                    Text(L10n.t("register_assigned")).font(.system(size: 13, weight: .bold)).foregroundStyle(Theme.emerald700)
                    ForEach(model.invitedUnits, id: \.self) { unit in
                        Label {
                            Text(L10n.t("unit_in_condominium", unit.unitNumber ?? "—", unit.condominiumName ?? L10n.t("condominium")))
                                .font(.system(size: 14, weight: .semibold)).foregroundStyle(Theme.slate900)
                        } icon: {
                            Image(systemName: "building.2.fill").foregroundStyle(Theme.emerald600)
                        }
                    }
                }
                .card(background: Theme.emerald50, border: Theme.emerald500.opacity(0.3))
            }

            LabeledField(title: L10n.t("field_full_name")) {
                TextField("", text: $fullName).textContentType(.name)
            }
            LabeledField(title: L10n.t("field_dni")) {
                TextField("", text: $dni).keyboardType(.numberPad)
            }
            LabeledField(title: L10n.t("field_mobile")) {
                TextField("", text: $phone).keyboardType(.phonePad).textContentType(.telephoneNumber)
            }
            Toggle(isOn: $acceptedPrivacy) {
                Text(L10n.t("register_privacy")).font(.system(size: 13)).foregroundStyle(Theme.slate700)
            }
            .tint(Theme.emerald600)

            Button {
                Task {
                    await model.completeRegistration(email: email, password: password, fullName: fullName,
                                                     dni: dni, phone: phone, acceptedPrivacy: acceptedPrivacy)
                }
            } label: {
                if model.authLoading { ProgressView().tint(.white) } else { Text(L10n.t("action_create_account")) }
            }
            .buttonStyle(PrimaryButtonStyle())
            .disabled(model.authLoading)

            Button(L10n.t("action_back")) { model.backToRegisterStep1() }
                .font(.system(size: 14, weight: .semibold))
                .frame(maxWidth: .infinity)
        }
    }
}

/// Campo de formulario con su etiqueta, sobre fondo blanco.
struct LabeledField<Content: View>: View {
    let title: String
    var hint: String?
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title).font(.system(size: 13, weight: .semibold)).foregroundStyle(Theme.slate700)
            content
                .font(.system(size: 16))
                .padding(.horizontal, 14)
                .frame(minHeight: 48)
                .background(.white, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(Theme.slate200, lineWidth: 1))
            if let hint {
                Text(hint).font(.system(size: 11)).foregroundStyle(Theme.slate500)
            }
        }
    }
}
