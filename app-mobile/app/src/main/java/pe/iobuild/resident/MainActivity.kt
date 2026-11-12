package pe.iobuild.resident

import androidx.compose.runtime.saveable.rememberSaveable
import android.app.Activity
import android.content.Context
import androidx.annotation.StringRes
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import pe.iobuild.resident.ui.util.AppLocale
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
    // Las pantallas leen sus textos del contexto de la actividad, con el idioma elegido
    override fun attachBaseContext(newBase: Context) {
        super.attachBaseContext(AppLocale.wrap(newBase))
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        AppLocale.applyToApp(applicationContext)
        // Barras del sistema transparentes con íconos oscuros: la app es clara y la hora queda legible
        enableEdgeToEdge(
            statusBarStyle = SystemBarStyle.light(AndroidColor.TRANSPARENT, AndroidColor.TRANSPARENT),
            navigationBarStyle = SystemBarStyle.light(AndroidColor.TRANSPARENT, AndroidColor.TRANSPARENT)
        )

        val sessionManager = SessionManager(applicationContext)
        val apiProvider = ApiClientProvider(sessionManager)
        val repository = ResidentBffRepository(sessionManager, apiProvider, applicationContext)
        val factory = ResidentViewModel.Factory(sessionManager, repository, applicationContext)

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
    @StringRes val titleRes: Int,
    val icon: ImageVector
) {
    CONSUMPTION(R.string.tab_consumption, Icons.Default.WaterDrop),
    STATEMENT(R.string.tab_payments, Icons.AutoMirrored.Filled.ReceiptLong),
    FEE(R.string.tab_fee, Icons.Default.PieChart),
    PROFILE(R.string.tab_profile, Icons.Default.Person)
}

// Cambia el idioma, lo aplica a toda la app y recrea la pantalla para mostrar los textos nuevos
private fun changeLanguage(context: Context, language: String) {
    AppLocale.save(context, language)
    AppLocale.applyToApp(context.applicationContext)
    (context as? Activity)?.recreate()
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun IoBuildResidentApp(viewModel: ResidentViewModel) {
    var authRoute by rememberSaveable { mutableStateOf(AuthRoute.LOGIN) }
    val context = LocalContext.current
    val currentLanguage = remember { AppLocale.saved(context) }
    var activeTab by rememberSaveable { mutableStateOf(ResidentTab.CONSUMPTION) }
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
                    },
                    currentLanguage = currentLanguage,
                    onChangeLanguage = { changeLanguage(context, it) }
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

    val unitNumber = viewModel.overviewData?.unit?.unitNumber ?: viewModel.statementData?.unit?.unitNumber
    val condoName = viewModel.overviewData?.condominium?.name ?: viewModel.statementData?.unit?.condominiumName
    val unitBadgeText = when {
        unitNumber == null -> stringResource(R.string.owner_portal)
        !condoName.isNullOrBlank() -> stringResource(R.string.unit_in_condominium, unitNumber, condoName)
        else -> stringResource(R.string.unit_short, unitNumber)
    }

    val residentDisplayName = viewModel.profileData?.fullName?.takeIf { it.isNotBlank() }
        ?: viewModel.overviewData?.unit?.residentFullName?.takeIf { it.isNotBlank() }
        ?: viewModel.sessionManager.displayName.takeIf { it.isNotBlank() }
        ?: stringResource(R.string.owner)

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
                                contentDescription = stringResource(R.string.action_refresh),
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
                                    contentDescription = stringResource(tab.titleRes)
                                )
                            },
                            label = {
                                Text(
                                    text = stringResource(tab.titleRes),
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
                        currentLanguage = currentLanguage,
                        onChangeLanguage = { changeLanguage(context, it) },
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
