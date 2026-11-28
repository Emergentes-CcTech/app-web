import SwiftUI

/// "Mi perfil": datos del propietario, idioma y cierre de sesión.
struct ProfileView: View {
    @Environment(AppModel.self) private var model
    @State private var fullName = ""
    @State private var phone = ""
    @State private var secondEmail = ""
    @State private var loadedProfileKey = ""
    @State private var confirmSignOut = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                TabTopBar()
                ScreenHeader(title: L10n.t("profile_title"), subtitle: L10n.t("profile_subtitle"))

                if model.profileLoading && model.profile == nil {
                    LoadingBlock()
                } else if let error = model.profileError, model.profile == nil {
                    ErrorBanner(message: error) { Task { await model.loadProfile() } }
                } else {
                    header
                    accountForm
                    language
                    security
                }
            }
            .padding(16)
        }
        .background(Theme.background.ignoresSafeArea())
        .refreshable { await model.loadProfile() }
        .toolbar(.hidden, for: .navigationBar)
        .onAppear(perform: syncForm)
        .onChange(of: model.profile?.fullName) { _, _ in syncForm() }
        .confirmationDialog(L10n.t("sign_out_confirm"), isPresented: $confirmSignOut, titleVisibility: .visible) {
            Button(L10n.t("action_sign_out"), role: .destructive) { Task { await model.signOut() } }
            Button(L10n.t("action_cancel"), role: .cancel) {}
        }
    }

    /// Copia los datos del servidor al formulario una vez por perfil cargado (sin pisar lo que se está editando).
    private func syncForm() {
        guard let profile = model.profile else { return }
        let key = "\(profile.profileId ?? 0)|\(profile.fullName ?? "")|\(profile.phoneNumber ?? "")|\(profile.secondEmail ?? "")"
        guard key != loadedProfileKey else { return }
        loadedProfileKey = key
        fullName = profile.fullName ?? model.session.displayName
        phone = profile.phoneNumber ?? ""
        secondEmail = profile.secondEmail ?? ""
    }

    private var initials: String {
        let parts = (model.profile?.fullName ?? model.session.displayName).split(separator: " ")
        let letters = parts.prefix(2).compactMap(\.first).map(String.init).joined()
        return letters.isEmpty ? "IO" : letters.uppercased()
    }

    private var header: some View {
        HStack(spacing: 14) {
            Group {
                if let photo = model.profile?.photoUrl, let url = URL(string: photo), !photo.isEmpty {
                    AsyncImage(url: url) { image in
                        image.resizable().scaledToFill()
                    } placeholder: {
                        Text(initials)
                    }
                } else {
                    Text(initials)
                }
            }
            .font(.system(size: 20, weight: .heavy))
            .foregroundStyle(.white)
            .frame(width: 60, height: 60)
            .background(Theme.emerald600)
            .clipShape(Circle())

            VStack(alignment: .leading, spacing: 3) {
                Text(model.profile?.fullName ?? model.session.displayName)
                    .font(.system(size: 17, weight: .bold)).foregroundStyle(Theme.slate900)
                Text(L10n.t("profile_role")).font(.system(size: 12, weight: .semibold)).foregroundStyle(Theme.emerald700)
                if let unit = model.profile?.unit {
                    Text(L10n.t("profile_condo_unit", unit.condominiumName ?? "—", unit.unitNumber ?? "—"))
                        .font(.system(size: 12)).foregroundStyle(Theme.slate700)
                    Text((unit.floor.map { L10n.t("profile_floor", $0) } ?? "") + L10n.t("profile_aliquot", Fmt.aliquot(unit.aliquotPercentage)))
                        .font(.system(size: 12)).foregroundStyle(Theme.slate500)
                }
                if let userId = model.profile?.userId, userId > 0 {
                    Text(L10n.t("profile_user_id", userId)).font(.system(size: 11)).foregroundStyle(Theme.slate500)
                }
            }
            Spacer(minLength: 0)
        }
        .card()
    }

    private var accountForm: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(L10n.t("profile_account_info")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)

            LabeledField(title: L10n.t("profile_email_readonly")) {
                Text(model.profile?.email ?? model.session.email)
                    .foregroundStyle(Theme.slate500)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            LabeledField(title: L10n.t("field_full_name")) {
                TextField("", text: $fullName).textContentType(.name)
            }
            LabeledField(title: L10n.t("profile_phone")) {
                TextField("", text: $phone).keyboardType(.phonePad).textContentType(.telephoneNumber)
            }
            LabeledField(title: L10n.t("profile_second_email")) {
                TextField("", text: $secondEmail)
                    .keyboardType(.emailAddress)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
            }

            if let message = model.profileMessage {
                NoticeBanner(message: message) { model.profileMessage = nil }
            }
            if let error = model.profileError, model.profile != nil {
                NoticeBanner(message: error, isError: true) { model.profileError = nil }
            }

            Button {
                Task { await model.saveProfile(fullName: fullName, phone: phone, secondEmail: secondEmail) }
            } label: {
                if model.profileSaving { ProgressView().tint(.white) } else { Text(L10n.t("profile_save_changes")) }
            }
            .buttonStyle(PrimaryButtonStyle())
            .disabled(model.profileSaving)
        }
        .card()
    }

    /// iOS guarda el idioma de cada app en Ajustes; desde aquí se abre directamente esa pantalla.
    private var language: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(L10n.t("profile_language")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)
            Text(L10n.t("profile_language_hint")).font(.system(size: 12)).foregroundStyle(Theme.slate500)
            Button {
                if let url = URL(string: UIApplication.openSettingsURLString) {
                    UIApplication.shared.open(url)
                }
            } label: {
                Label(L10n.t("profile_language_change"), systemImage: "globe")
                    .font(.system(size: 14, weight: .semibold))
            }
        }
        .card()
    }

    private var security: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(L10n.t("profile_security")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)
            HStack(spacing: 12) {
                Image(systemName: "iphone").font(.system(size: 22)).foregroundStyle(Theme.slate700)
                VStack(alignment: .leading, spacing: 2) {
                    Text(L10n.t("profile_device")).font(.system(size: 14, weight: .semibold)).foregroundStyle(Theme.slate900)
                    Text(UIDevice.current.model + " · iOS " + UIDevice.current.systemVersion)
                        .font(.system(size: 12)).foregroundStyle(Theme.slate500)
                }
                Spacer()
                Text(L10n.t("profile_session_active"))
                    .font(.system(size: 11, weight: .bold))
                    .foregroundStyle(Theme.emerald700)
                    .padding(.horizontal, 10).padding(.vertical, 4)
                    .background(Theme.emerald100, in: Capsule())
            }
            Button(role: .destructive) {
                confirmSignOut = true
            } label: {
                Label(L10n.t("action_sign_out"), systemImage: "rectangle.portrait.and.arrow.right")
                    .font(.system(size: 15, weight: .semibold))
                    .frame(maxWidth: .infinity, minHeight: 46)
            }
            .buttonStyle(.bordered)
            .tint(Theme.red500)
        }
        .card()
    }
}
