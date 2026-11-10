package pe.iobuild.resident.ui.screens

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusDirection
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.BuildConfig
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.viewmodel.ResidentViewModel

private val FieldShape = RoundedCornerShape(14.dp)

@Composable
private fun authFieldColors() = OutlinedTextFieldDefaults.colors(
    focusedBorderColor = IoBuildEmerald600,
    unfocusedBorderColor = IoBuildSlate200,
    focusedLabelColor = IoBuildEmerald700,
    cursorColor = IoBuildEmerald600,
    focusedContainerColor = Color.White,
    unfocusedContainerColor = Color.White
)

@Composable
private fun AuthErrorMessage(message: String) {
    Surface(
        color = IoBuildRed50,
        shape = RoundedCornerShape(12.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.Top
        ) {
            Icon(Icons.Default.ErrorOutline, null, tint = IoBuildRed500, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(10.dp))
            Text(text = message, color = Color(0xFFB91C1C), fontSize = 13.sp)
        }
    }
}

@Composable
private fun PrimaryAuthButton(text: String, loading: Boolean, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        enabled = !loading,
        modifier = Modifier
            .fillMaxWidth()
            .height(52.dp),
        shape = FieldShape,
        colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600)
    ) {
        if (loading) {
            CircularProgressIndicator(color = Color.White, strokeWidth = 2.dp, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(10.dp))
        }
        Text(text, fontWeight = FontWeight.Bold, fontSize = 16.sp)
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun LoginScreen(
    viewModel: ResidentViewModel,
    onNavigateToRegister: () -> Unit,
    onLoginSuccess: () -> Unit
) {
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var passwordVisible by remember { mutableStateOf(false) }
    var showServerDialog by remember { mutableStateOf(false) }
    var serverUrlInput by remember { mutableStateOf(viewModel.sessionManager.apiBaseUrl) }
    val focusManager = LocalFocusManager.current
    val submit = {
        focusManager.clearFocus()
        viewModel.signIn(email.trim(), password, onLoginSuccess)
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.White)
            .systemBarsPadding()
            .imePadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp)
    ) {
        Spacer(Modifier.height(56.dp))

        // Logo. En builds de prueba, una pulsación larga permite elegir otro servidor
        IoBuildLogo(
            symbolSize = 44.dp,
            textSize = 30.sp,
            modifier = Modifier.combinedClickable(
                onClick = {},
                onLongClick = {
                    if (BuildConfig.DEBUG) {
                        serverUrlInput = viewModel.sessionManager.apiBaseUrl
                        showServerDialog = true
                    }
                }
            )
        )

        Spacer(Modifier.height(40.dp))

        Text(
            text = "Hola, propietario",
            fontSize = 28.sp,
            fontWeight = FontWeight.ExtraBold,
            color = IoBuildSlate900
        )
        Text(
            text = "Inicia sesión para ver tu cuota, tus pagos y el consumo de tu edificio.",
            fontSize = 15.sp,
            color = IoBuildSlate500,
            modifier = Modifier.padding(top = 6.dp)
        )

        Spacer(Modifier.height(28.dp))

        OutlinedTextField(
            value = email,
            onValueChange = { email = it; viewModel.clearAuthError() },
            label = { Text("Correo electrónico") },
            leadingIcon = { Icon(Icons.Default.Email, contentDescription = null, tint = IoBuildSlate500) },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next),
            keyboardActions = KeyboardActions(onNext = { focusManager.moveFocus(FocusDirection.Down) }),
            modifier = Modifier.fillMaxWidth(),
            shape = FieldShape,
            colors = authFieldColors()
        )

        Spacer(Modifier.height(14.dp))

        OutlinedTextField(
            value = password,
            onValueChange = { password = it; viewModel.clearAuthError() },
            label = { Text("Contraseña") },
            leadingIcon = { Icon(Icons.Default.Lock, contentDescription = null, tint = IoBuildSlate500) },
            trailingIcon = {
                IconButton(onClick = { passwordVisible = !passwordVisible }) {
                    Icon(
                        imageVector = if (passwordVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                        contentDescription = if (passwordVisible) "Ocultar contraseña" else "Mostrar contraseña",
                        tint = IoBuildSlate500
                    )
                }
            },
            visualTransformation = if (passwordVisible) VisualTransformation.None else PasswordVisualTransformation(),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
            keyboardActions = KeyboardActions(onDone = { submit() }),
            modifier = Modifier.fillMaxWidth(),
            shape = FieldShape,
            colors = authFieldColors()
        )

        if (!viewModel.authError.isNullOrBlank()) {
            Spacer(Modifier.height(14.dp))
            AuthErrorMessage(viewModel.authError ?: "")
        }

        Spacer(Modifier.height(24.dp))

        PrimaryAuthButton(text = "Iniciar sesión", loading = viewModel.authLoading, onClick = { submit() })

        Spacer(Modifier.height(32.dp))

        // Registro: solo para propietarios que la administración ya registró con su correo
        Surface(
            color = IoBuildEmerald50,
            shape = RoundedCornerShape(16.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(18.dp)) {
                Text(
                    text = "¿Es tu primera vez?",
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                Text(
                    text = "Si tu administración ya registró tu correo, crea tu cuenta y verás tu departamento al instante.",
                    fontSize = 13.sp,
                    color = IoBuildSlate700,
                    modifier = Modifier.padding(top = 4.dp, bottom = 12.dp)
                )
                OutlinedButton(
                    onClick = { viewModel.clearAuthError(); onNavigateToRegister() },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(46.dp),
                    shape = FieldShape,
                    colors = ButtonDefaults.outlinedButtonColors(containerColor = Color.White, contentColor = IoBuildEmerald700),
                    border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildEmerald500)
                ) {
                    Icon(Icons.Default.PersonAdd, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text("Crear mi cuenta", fontWeight = FontWeight.SemiBold)
                }
            }
        }

        Spacer(Modifier.height(24.dp))

        // Aviso solo en builds de prueba cuando se apunta a un servidor distinto al de producción
        if (BuildConfig.DEBUG && viewModel.sessionManager.isUsingCustomServer) {
            Text(
                text = "Servidor de prueba: ${viewModel.sessionManager.apiBaseUrl}",
                fontSize = 11.sp,
                color = IoBuildAmber500,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth()
            )
            Spacer(Modifier.height(16.dp))
        }
    }

    if (showServerDialog) {
        AlertDialog(
            onDismissRequest = { showServerDialog = false },
            title = { Text("Servidor (solo pruebas)", fontWeight = FontWeight.Bold) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text(
                        "En el emulador, 10.0.2.2 apunta a tu PC. Las versiones publicadas usan siempre producción.",
                        fontSize = 13.sp,
                        color = IoBuildSlate500
                    )
                    OutlinedTextField(
                        value = serverUrlInput,
                        onValueChange = { serverUrlInput = it },
                        label = { Text("URL base (/api/v1/)") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        AssistChip(onClick = { serverUrlInput = BuildConfig.DEFAULT_API_BASE_URL }, label = { Text("Producción", fontSize = 12.sp) })
                        AssistChip(onClick = { serverUrlInput = "http://10.0.2.2:8080/api/v1/" }, label = { Text("Local :8080", fontSize = 12.sp) })
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    viewModel.updateServerBaseUrl(serverUrlInput)
                    showServerDialog = false
                }) { Text("Guardar") }
            },
            dismissButton = {
                TextButton(onClick = { showServerDialog = false }) { Text("Cancelar") }
            }
        )
    }
}

@Composable
fun RegisterResidentScreen(
    viewModel: ResidentViewModel,
    onBackToLogin: () -> Unit,
    onRegisterSuccess: () -> Unit
) {
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var fullName by remember { mutableStateOf("") }
    var dni by remember { mutableStateOf("") }
    var phoneNumber by remember { mutableStateOf("") }
    var acceptPrivacy by remember { mutableStateOf(false) }
    val focusManager = LocalFocusManager.current
    val step = viewModel.registerStep

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.White)
            .systemBarsPadding()
            .imePadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp)
    ) {
        Spacer(Modifier.height(8.dp))
        IconButton(
            onClick = {
                if (step == 2) viewModel.backToRegisterStep1()
                else { viewModel.backToRegisterStep1(); onBackToLogin() }
            },
            modifier = Modifier.offset(x = (-12).dp)
        ) {
            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Volver", tint = IoBuildSlate900)
        }

        Spacer(Modifier.height(8.dp))
        Text(
            text = "Paso $step de 2",
            fontSize = 13.sp,
            fontWeight = FontWeight.SemiBold,
            color = IoBuildEmerald700
        )
        Spacer(Modifier.height(8.dp))
        LinearProgressIndicator(
            progress = { step / 2f },
            modifier = Modifier
                .fillMaxWidth()
                .height(6.dp),
            color = IoBuildEmerald500,
            trackColor = IoBuildSlate100,
            strokeCap = androidx.compose.ui.graphics.StrokeCap.Round
        )
        Spacer(Modifier.height(24.dp))

        if (step == 1) {
            Text("Crea tu cuenta", fontSize = 26.sp, fontWeight = FontWeight.ExtraBold, color = IoBuildSlate900)
            Text(
                text = "Usa el correo con el que tu administración te registró en el padrón del condominio.",
                fontSize = 15.sp,
                color = IoBuildSlate500,
                modifier = Modifier.padding(top = 6.dp, bottom = 24.dp)
            )

            OutlinedTextField(
                value = email,
                onValueChange = { email = it; viewModel.clearAuthError() },
                label = { Text("Correo electrónico") },
                leadingIcon = { Icon(Icons.Default.Email, null, tint = IoBuildSlate500) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next),
                keyboardActions = KeyboardActions(onNext = { focusManager.moveFocus(FocusDirection.Down) }),
                modifier = Modifier.fillMaxWidth(),
                shape = FieldShape,
                colors = authFieldColors()
            )
            Spacer(Modifier.height(14.dp))
            OutlinedTextField(
                value = password,
                onValueChange = { password = it; viewModel.clearAuthError() },
                label = { Text("Contraseña") },
                supportingText = { Text("Mínimo 8 caracteres") },
                leadingIcon = { Icon(Icons.Default.Lock, null, tint = IoBuildSlate500) },
                visualTransformation = PasswordVisualTransformation(),
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Next),
                keyboardActions = KeyboardActions(onNext = { focusManager.moveFocus(FocusDirection.Down) }),
                modifier = Modifier.fillMaxWidth(),
                shape = FieldShape,
                colors = authFieldColors()
            )
            Spacer(Modifier.height(6.dp))
            OutlinedTextField(
                value = confirmPassword,
                onValueChange = { confirmPassword = it; viewModel.clearAuthError() },
                label = { Text("Confirmar contraseña") },
                leadingIcon = { Icon(Icons.Default.Lock, null, tint = IoBuildSlate500) },
                visualTransformation = PasswordVisualTransformation(),
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
                keyboardActions = KeyboardActions(onDone = {
                    focusManager.clearFocus()
                    viewModel.verifyInvitationForStep2(email, password, confirmPassword)
                }),
                modifier = Modifier.fillMaxWidth(),
                shape = FieldShape,
                colors = authFieldColors()
            )

            if (!viewModel.authError.isNullOrBlank()) {
                Spacer(Modifier.height(14.dp))
                AuthErrorMessage(viewModel.authError ?: "")
            }

            Spacer(Modifier.height(24.dp))
            PrimaryAuthButton(
                text = if (viewModel.authLoading) "Verificando…" else "Continuar",
                loading = viewModel.authLoading,
                onClick = {
                    focusManager.clearFocus()
                    viewModel.verifyInvitationForStep2(email, password, confirmPassword)
                }
            )
        } else {
            Text("Tus datos", fontSize = 26.sp, fontWeight = FontWeight.ExtraBold, color = IoBuildSlate900)
            Text(
                text = "Los usamos para tu perfil de propietario.",
                fontSize = 15.sp,
                color = IoBuildSlate500,
                modifier = Modifier.padding(top = 6.dp, bottom = 20.dp)
            )

            if (viewModel.invitedUnits.isNotEmpty()) {
                Surface(
                    color = IoBuildEmerald50,
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 20.dp)
                ) {
                    Row(modifier = Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.CheckCircle, null, tint = IoBuildEmerald600, modifier = Modifier.size(22.dp))
                        Spacer(Modifier.width(12.dp))
                        Column {
                            Text("Tu administración te asignó", fontSize = 12.sp, color = IoBuildEmerald700)
                            Text(
                                text = viewModel.invitedUnits.joinToString("\n") {
                                    "Dpto. ${it.unitNumber ?: "—"} · ${it.condominiumName ?: "Condominio"}"
                                },
                                fontSize = 15.sp,
                                fontWeight = FontWeight.Bold,
                                color = IoBuildSlate900
                            )
                        }
                    }
                }
            }

            OutlinedTextField(
                value = fullName,
                onValueChange = { fullName = it; viewModel.clearAuthError() },
                label = { Text("Nombre completo") },
                leadingIcon = { Icon(Icons.Default.Person, null, tint = IoBuildSlate500) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Next),
                keyboardActions = KeyboardActions(onNext = { focusManager.moveFocus(FocusDirection.Down) }),
                modifier = Modifier.fillMaxWidth(),
                shape = FieldShape,
                colors = authFieldColors()
            )
            Spacer(Modifier.height(14.dp))
            OutlinedTextField(
                value = dni,
                onValueChange = { if (it.length <= 8 && it.all(Char::isDigit)) dni = it; viewModel.clearAuthError() },
                label = { Text("DNI") },
                leadingIcon = { Icon(Icons.Default.Badge, null, tint = IoBuildSlate500) },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = ImeAction.Next),
                keyboardActions = KeyboardActions(onNext = { focusManager.moveFocus(FocusDirection.Down) }),
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
                shape = FieldShape,
                colors = authFieldColors()
            )
            Spacer(Modifier.height(14.dp))
            OutlinedTextField(
                value = phoneNumber,
                onValueChange = { phoneNumber = it; viewModel.clearAuthError() },
                label = { Text("Celular") },
                leadingIcon = { Icon(Icons.Default.Phone, null, tint = IoBuildSlate500) },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone, imeAction = ImeAction.Done),
                keyboardActions = KeyboardActions(onDone = { focusManager.clearFocus() }),
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
                shape = FieldShape,
                colors = authFieldColors()
            )
            Spacer(Modifier.height(10.dp))

            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.offset(x = (-12).dp)
            ) {
                Checkbox(
                    checked = acceptPrivacy,
                    onCheckedChange = { acceptPrivacy = it; viewModel.clearAuthError() },
                    colors = CheckboxDefaults.colors(checkedColor = IoBuildEmerald600)
                )
                Text(
                    text = "Acepto la política de privacidad y el uso de datos de consumo del edificio.",
                    fontSize = 13.sp,
                    color = IoBuildSlate700
                )
            }

            if (!viewModel.authError.isNullOrBlank()) {
                Spacer(Modifier.height(10.dp))
                AuthErrorMessage(viewModel.authError ?: "")
            }

            Spacer(Modifier.height(20.dp))
            PrimaryAuthButton(
                text = if (viewModel.authLoading) "Creando tu cuenta…" else "Crear mi cuenta",
                loading = viewModel.authLoading,
                onClick = {
                    focusManager.clearFocus()
                    viewModel.completeResidentRegistration(
                        email = email,
                        password = password,
                        fullName = fullName,
                        dni = dni,
                        phoneNumber = phoneNumber,
                        acceptedPrivacy = acceptPrivacy,
                        onSuccess = onRegisterSuccess
                    )
                }
            )
        }
        Spacer(Modifier.height(32.dp))
    }
}
