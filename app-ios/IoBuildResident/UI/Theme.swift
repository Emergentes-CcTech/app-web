import SwiftUI

/// Paleta de IoBuild (la misma de la web y de la app Android).
enum Theme {
    static let emerald500 = Color(hex: 0x10B981)
    static let emerald600 = Color(hex: 0x059669)
    static let emerald700 = Color(hex: 0x047857)
    static let emerald50 = Color(hex: 0xECFDF5)
    static let emerald100 = Color(hex: 0xD1FAE5)
    static let teal500 = Color(hex: 0x14B8A6)
    static let amber500 = Color(hex: 0xF59E0B)
    static let amber50 = Color(hex: 0xFFFBEB)
    static let red500 = Color(hex: 0xEF4444)
    static let red50 = Color(hex: 0xFEF2F2)
    static let slate900 = Color(hex: 0x0F172A)
    static let slate800 = Color(hex: 0x1E293B)
    static let slate700 = Color(hex: 0x334155)
    static let slate500 = Color(hex: 0x64748B)
    static let slate200 = Color(hex: 0xE2E8F0)
    static let slate100 = Color(hex: 0xF1F5F9)
    static let background = Color(hex: 0xF6F8F7)
}

extension Color {
    init(hex: UInt32) {
        self.init(
            red: Double((hex >> 16) & 0xFF) / 255,
            green: Double((hex >> 8) & 0xFF) / 255,
            blue: Double(hex & 0xFF) / 255
        )
    }
}

/// Tarjeta blanca con borde suave, base de casi todas las pantallas.
struct CardModifier: ViewModifier {
    var background: Color = .white
    var border: Color = Theme.slate200
    var padding: CGFloat = 16

    func body(content: Content) -> some View {
        content
            .padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(background, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).stroke(border, lineWidth: 1))
    }
}

extension View {
    func card(background: Color = .white, border: Color = Theme.slate200, padding: CGFloat = 16) -> some View {
        modifier(CardModifier(background: background, border: border, padding: padding))
    }
}

/// Botón principal verde de ancho completo.
struct PrimaryButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 15, weight: .semibold))
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity, minHeight: 48)
            .background(Theme.emerald600.opacity(isEnabled ? (configuration.isPressed ? 0.85 : 1) : 0.45),
                        in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }
}
