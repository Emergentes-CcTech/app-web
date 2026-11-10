package pe.iobuild.resident

import android.graphics.Color as AndroidColor
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.automirrored.filled.ReceiptLong
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.viewmodel.compose.viewModel
import pe.iobuild.resident.data.local.SessionManager
import pe.iobuild.resident.data.remote.ApiClientProvider
import pe.iobuild.resident.data.repository.ResidentBffRepository
import pe.iobuild.resident.ui.screens.*
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.viewmodel.ResidentViewModel

/**
 * Actividad principal de la aplicación móvil exclusiva para el Propietario (Resident) de IoBuild.
 * Aplica el patrón Backend for Frontend (BFF) consumiendo los endpoints agregados
 * `/api/v1/mobile/resident/...` del mismo backend de `app-web`.
 */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Barras del sistema transparentes con íconos oscuros: la app es clara y la hora queda legible
        enableEdgeToEdge(
            statusBarStyle = SystemBarStyle.light(AndroidColor.TRANSPARENT, AndroidColor.TRANSPARENT),
            navigationBarStyle = SystemBarStyle.light(AndroidColor.TRANSPARENT, AndroidColor.TRANSPARENT)
        )

        val sessionManager = SessionManager(applicationContext)
        val apiProvider = ApiClientProvider(sessionManager)
        val repository = ResidentBffRepository(sessionManager, apiProvider)
        val factory = ResidentViewModel.Factory(sessionManager, repository)

        setContent {
            IoBuildResidentTheme {
                val residentViewModel: ResidentViewModel = viewModel(factory = factory)
                IoBuildResidentApp(viewModel = residentViewModel)
            }
        }
    }
}

private enum class AuthRoute {
    LOGIN,
    REGISTER
}

private enum class ResidentTab(
    val title: String,
    val icon: ImageVector
) {
    CONSUMPTION("Consumo", Icons.Default.WaterDrop),
    STATEMENT("Pagos", Icons.AutoMirrored.Filled.ReceiptLong),
    FEE("Mi cuota", Icons.Default.PieChart),
    PROFILE("Perfil", Icons.Default.Person)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun IoBuildResidentApp(viewModel: ResidentViewModel) {
    var authRoute by remember { mutableStateOf(AuthRoute.LOGIN) }
    var activeTab by remember { mutableStateOf(ResidentTab.CONSUMPTION) }
    val lifecycleOwner = LocalLifecycleOwner.current

    // Al regresar a la app desde el navegador (onResume), confirma cualquier sesión de pago Stripe pendiente
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                viewModel.confirmPendingCheckoutIfNeeded()
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
        }
    }

    if (!viewModel.isAuthenticated) {
        when (authRoute) {
            AuthRoute.LOGIN -> {
                LoginScreen(
                    viewModel = viewModel,
                    onNavigateToRegister = {
                        viewModel.clearAuthError()
                        authRoute = AuthRoute.REGISTER
                    },
                    onLoginSuccess = {
                        activeTab = ResidentTab.CONSUMPTION
                    }
                )
            }
            AuthRoute.REGISTER -> {
                RegisterResidentScreen(
                    viewModel = viewModel,
                    onBackToLogin = {
                        viewModel.clearAuthError()
                        authRoute = AuthRoute.LOGIN
                    },
                    onRegisterSuccess = {
                        activeTab = ResidentTab.CONSUMPTION
                    }
                )
            }
        }
        return
    }

    val unitBadgeText = viewModel.overviewData?.unit?.let { u ->
        val condoName = viewModel.overviewData?.condominium?.name
        if (!condoName.isNullOrBlank()) "Dpto. ${u.unitNumber} · $condoName" else "Dpto. ${u.unitNumber}"
    } ?: viewModel.statementData?.unit?.let { u ->
        val condoName = u.condominiumName
        if (!condoName.isNullOrBlank()) "Dpto. ${u.unitNumber} · $condoName" else "Dpto. ${u.unitNumber}"
    } ?: "Portal Propietario"

    val residentDisplayName = viewModel.profileData?.fullName?.takeIf { it.isNotBlank() }
        ?: viewModel.overviewData?.unit?.residentFullName?.takeIf { it.isNotBlank() }
        ?: viewModel.sessionManager.displayName.takeIf { it.isNotBlank() }
        ?: "Propietario"

    val initials = residentDisplayName.split(" ").filter { it.isNotBlank() }.take(2)
        .joinToString("") { it.first().uppercase() }.ifBlank { "P" }

    Scaffold(
        containerColor = IoBuildBackground,
        topBar = {
            Column {
                TopAppBar(
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = Color.White,
                        titleContentColor = IoBuildSlate900
                    ),
                    title = {
                        Column {
                            IoBuildLogo(symbolSize = 24.dp, textSize = 19.sp)
                            Text(
                                text = unitBadgeText,
                                fontSize = 12.sp,
                                color = IoBuildSlate500,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )
                        }
                    },
                    actions = {
                        IconButton(onClick = { viewModel.refreshAllResidentViews() }) {
                            Icon(
                                imageVector = Icons.Default.Refresh,
                                contentDescription = "Actualizar",
                                tint = IoBuildSlate500
                            )
                        }
                        // Avatar con las iniciales: abre el perfil (ahí está cerrar sesión)
                        Box(
                            modifier = Modifier
                                .padding(end = 12.dp)
                                .size(36.dp)
                                .clip(CircleShape)
                                .background(IoBuildEmerald600)
                                .clickable { activeTab = ResidentTab.PROFILE },
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = initials,
                                color = Color.White,
                                fontSize = 14.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                )
                HorizontalDivider(color = IoBuildSlate200, thickness = 1.dp)
            }
        },
        bottomBar = {
            Column {
                HorizontalDivider(color = IoBuildSlate200, thickness = 1.dp)
                NavigationBar(
                    containerColor = Color.White,
                    tonalElevation = 0.dp
                ) {
                    ResidentTab.entries.forEach { tab ->
                        val selected = activeTab == tab
                        NavigationBarItem(
                            selected = selected,
                            onClick = { activeTab = tab },
                            icon = {
                                Icon(
                                    imageVector = tab.icon,
                                    contentDescription = tab.title
                                )
                            },
                            label = {
                                Text(
                                    text = tab.title,
                                    fontSize = 11.sp,
                                    fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium
                                )
                            },
                            colors = NavigationBarItemDefaults.colors(
                                selectedIconColor = IoBuildEmerald600,
                                selectedTextColor = IoBuildEmerald700,
                                indicatorColor = IoBuildEmerald100,
                                unselectedIconColor = IoBuildSlate500,
                                unselectedTextColor = IoBuildSlate500
                            )
                        )
                    }
                }
            }
        }
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(IoBuildBackground)
                .padding(innerPadding)
        ) {
            when (activeTab) {
                ResidentTab.CONSUMPTION -> {
                    MyConsumptionScreen(
                        viewModel = viewModel,
                        onOpenBreakdown = { lineId ->
                            viewModel.loadFeeExplanation(lineId)
                            activeTab = ResidentTab.FEE
                        },
                        onNavigateToAccountStatement = {
                            activeTab = ResidentTab.STATEMENT
                        }
                    )
                }
                ResidentTab.STATEMENT -> {
                    AccountStatementScreen(
                        viewModel = viewModel,
                        onOpenBreakdown = { lineId ->
                            viewModel.loadFeeExplanation(lineId)
                            activeTab = ResidentTab.FEE
                        }
                    )
                }
                ResidentTab.FEE -> {
                    FeeExplanationScreen(
                        viewModel = viewModel,
                        onGoToPay = {
                            activeTab = ResidentTab.STATEMENT
                        },
                        onAskAiOnDashboard = {
                            activeTab = ResidentTab.CONSUMPTION
                        }
                    )
                }
                ResidentTab.PROFILE -> {
                    ProfileScreen(
                        loading = viewModel.profileLoading,
                        saving = viewModel.profileSaving,
                        error = viewModel.profileError,
                        successMessage = viewModel.profileSuccessMessage,
                        data = viewModel.profileData,
                        onRefresh = { viewModel.loadProfile() },
                        onSaveProfile = { fullName, phone, secondEmail, photoUrl ->
                            viewModel.saveProfileChanges(fullName, phone, secondEmail, photoUrl)
                        },
                        onClearMessages = { viewModel.clearProfileMessages() },
                        onSignOut = {
                            viewModel.signOut()
                            authRoute = AuthRoute.LOGIN
                        }
                    )
                }
            }
        }
    }
}
