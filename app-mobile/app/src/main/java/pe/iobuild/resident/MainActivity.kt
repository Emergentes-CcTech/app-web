package pe.iobuild.resident

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
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
 * `/api/v1/mobile/resident/*` del mismo backend de `app-web`.
 */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

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
    CONSUMPTION("Mi consumo", Icons.Default.WaterDrop),
    STATEMENT("Estado cuenta", Icons.Default.ReceiptLong),
    FEE("Mi cuota", Icons.Default.Verified),
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

    Scaffold(
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = IoBuildSlate900,
                    titleContentColor = Color.White
                ),
                title = {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Box(
                            modifier = Modifier
                                .size(36.dp)
                                .clip(CircleShape)
                                .background(
                                    Brush.linearGradient(
                                        colors = listOf(IoBuildEmerald500, IoBuildEmerald700)
                                    )
                                ),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(
                                imageVector = Icons.Default.Apartment,
                                contentDescription = "IoBuild",
                                tint = Color.White,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                        Column(modifier = Modifier.weight(1f, fill = false)) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(6.dp)
                            ) {
                                Text(
                                    text = "IoBuild",
                                    fontSize = 17.sp,
                                    fontWeight = FontWeight.ExtraBold,
                                    color = Color.White
                                )
                                Surface(
                                    shape = RoundedCornerShape(999.dp),
                                    color = IoBuildEmerald500.copy(alpha = 0.2f)
                                ) {
                                    Text(
                                        text = "PROPIETARIO",
                                        fontSize = 9.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = IoBuildEmerald100,
                                        modifier = Modifier.padding(horizontal = 7.dp, vertical = 2.dp)
                                    )
                                }
                            }
                            Text(
                                text = "$residentDisplayName · $unitBadgeText",
                                fontSize = 11.sp,
                                color = Color(0xFFCBD5E1),
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )
                        }
                    }
                },
                actions = {
                    IconButton(
                        onClick = { viewModel.refreshAllResidentViews() }
                    ) {
                        Icon(
                            imageVector = Icons.Default.Sync,
                            contentDescription = "Sincronizar BFF",
                            tint = IoBuildEmerald100
                        )
                    }
                    IconButton(
                        onClick = {
                            viewModel.signOut()
                            authRoute = AuthRoute.LOGIN
                        }
                    ) {
                        Icon(
                            imageVector = Icons.Default.Logout,
                            contentDescription = "Cerrar sesión",
                            tint = Color(0xFFFCA5A5)
                        )
                    }
                }
            )
        },
        bottomBar = {
            NavigationBar(
                containerColor = Color.White,
                tonalElevation = 8.dp
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
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .background(IoBuildBackground)
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
