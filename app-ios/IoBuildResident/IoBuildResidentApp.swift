import SwiftUI

/// App de IoBuild para propietarios de departamento (iOS).
/// Consume el BFF móvil del backend: /api/v1/mobile/resident/*
@main
struct IoBuildResidentApp: App {
    @State private var model = AppModel()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(model)
                .tint(Theme.emerald600)
        }
    }
}
