package pe.iobuild.resident.ui.theme

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.em
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.R

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
val IoBuildBackground = Color(0xFFF6F8F7)

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
    outline = IoBuildSlate200,
    outlineVariant = IoBuildSlate100,
    // Sin tinte de elevación: tarjetas y barras quedan blancas en vez de verdosas
    surfaceTint = Color.White,
    error = IoBuildRed500
)

/**
 * Interlineado proporcional (em) y sin espaciado entre letras: cuando una pantalla fija un tamaño
 * de letra más chico, el interlineado se reduce con él en vez de quedar "estirado" como en web.
 */
private fun TextStyle.mobile(lineHeight: Float, letterSpacing: TextUnit = 0.sp) =
    copy(lineHeight = lineHeight.em, letterSpacing = letterSpacing)

private val AppTypography = Typography().let { base ->
    Typography(
        displayLarge = base.displayLarge.mobile(1.12f),
        displayMedium = base.displayMedium.mobile(1.12f),
        displaySmall = base.displaySmall.mobile(1.15f),
        headlineLarge = base.headlineLarge.mobile(1.2f),
        headlineMedium = base.headlineMedium.mobile(1.2f),
        headlineSmall = base.headlineSmall.mobile(1.25f),
        titleLarge = base.titleLarge.mobile(1.25f),
        titleMedium = base.titleMedium.mobile(1.3f),
        titleSmall = base.titleSmall.mobile(1.3f),
        bodyLarge = base.bodyLarge.mobile(1.4f),
        bodyMedium = base.bodyMedium.mobile(1.4f),
        bodySmall = base.bodySmall.mobile(1.35f),
        labelLarge = base.labelLarge.mobile(1.25f),
        labelMedium = base.labelMedium.mobile(1.25f),
        labelSmall = base.labelSmall.mobile(1.25f, 0.2.sp)
    )
}

private val AppShapes = Shapes(
    extraSmall = RoundedCornerShape(6.dp),
    small = RoundedCornerShape(10.dp),
    medium = RoundedCornerShape(14.dp),
    large = RoundedCornerShape(20.dp),
    extraLarge = RoundedCornerShape(28.dp)
)

@Composable
fun IoBuildResidentTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = LightColorScheme,
        typography = AppTypography,
        shapes = AppShapes,
        content = content
    )
}

/** Símbolo de IoBuild (vector, nítido en cualquier densidad de pantalla). */
@Composable
fun IoBuildSymbol(size: Dp, modifier: Modifier = Modifier) {
    Image(
        painter = painterResource(R.drawable.ic_iobuild_symbol),
        contentDescription = "IoBuild",
        modifier = modifier.size(size)
    )
}

/** Logo completo: símbolo + nombre, como en la web. */
@Composable
fun IoBuildLogo(
    modifier: Modifier = Modifier,
    symbolSize: Dp = 28.dp,
    textSize: TextUnit = 22.sp,
    textColor: Color = IoBuildSlate900
) {
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        IoBuildSymbol(size = symbolSize)
        Text(
            text = "IoBuild",
            fontSize = textSize,
            fontWeight = FontWeight.ExtraBold,
            color = textColor,
            letterSpacing = (-0.5).sp
        )
    }
}
