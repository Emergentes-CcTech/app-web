package pe.iobuild.resident.ui.theme

import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

// Paleta oficial IoBuild (Esmeralda, Turquesa y Pizarra Oscura)
val IoBuildEmerald500 = Color(0xFF10B981)
val IoBuildEmerald600 = Color(0xFF059669)
val IoBuildEmerald700 = Color(0xFF047857)
val IoBuildEmerald50 = Color(0xFFECFDF5)
val IoBuildEmerald100 = Color(0xFFD1FAE5)

val IoBuildTeal500 = Color(0xFF14B8A6)
val IoBuildAmber500 = Color(0xFFF59E0B)
val IoBuildAmber50 = Color(0xFFFFFBEB)
val IoBuildRed500 = Color(0xFFEF4444)
val IoBuildRed50 = Color(0xFFFEF2F2)

val IoBuildSlate900 = Color(0xFF0F172A)
val IoBuildSlate800 = Color(0xFF1E293B)
val IoBuildSlate700 = Color(0xFF334155)
val IoBuildSlate500 = Color(0xFF64748B)
val IoBuildSlate200 = Color(0xFFE2E8F0)
val IoBuildSlate100 = Color(0xFFF1F5F9)
val IoBuildBackground = Color(0xFFF4FBF7)

private val LightColorScheme = lightColorScheme(
    primary = IoBuildEmerald600,
    onPrimary = Color.White,
    primaryContainer = IoBuildEmerald50,
    onPrimaryContainer = IoBuildEmerald700,
    secondary = IoBuildTeal500,
    onSecondary = Color.White,
    background = IoBuildBackground,
    onBackground = IoBuildSlate900,
    surface = Color.White,
    onSurface = IoBuildSlate900,
    surfaceVariant = IoBuildSlate100,
    onSurfaceVariant = IoBuildSlate700,
    error = IoBuildRed500
)

@Composable
fun IoBuildResidentTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = LightColorScheme,
        typography = Typography(),
        content = content
    )
}
