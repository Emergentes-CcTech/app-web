package pe.iobuild.resident.ui.screens

import androidx.compose.ui.res.stringResource
import pe.iobuild.resident.R
import pe.iobuild.resident.ui.util.AppLocale
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
    onLoginSuccess: () -> Unit,
    currentLanguage: String,
    onChangeLanguage: (String) -> Unit
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
            text = stringResource(R.string.login_greeting),
            fontSize = 28.sp,
            fontWeight = FontWeight.ExtraBold,
            color = IoBuildSlate900
        )
        Text(
            text = stringResource(R.string.login_subtitle),
            fontSize = 15.sp,
            color = IoBuildSlate500,
            modifier = Modifier.padding(top = 6.dp)
        )

        Spacer(Modifier.height(28.dp))

        OutlinedTextField(
            value = email,
            onValueChange = { email = it; viewModel.clearAuthError() },
            label = { Text(stringResource(R.string.field_email)) },
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
            label = { Text(stringResource(R.string.field_password)) },
            leadingIcon = { Icon(Icons.Default.Lock, contentDescription = null, tint = IoBuildSlate500) },
            trailingIcon = {
                IconButton(onClick = { passwordVisible = !passwordVisible }) {
                    Icon(
                        imageVector = if (passwordVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                        contentDescription = stringResource(if (passwordVisible) R.string.password_hide else R.string.password_show),
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

        PrimaryAuthButton(text = stringResource(R.string.action_sign_in), loading = viewModel.authLoading, onClick = { submit() })

        Spacer(Modifier.height(32.dp))

        // Registro: solo para propietarios que la administración ya registró con su correo
        Surface(
            color = IoBuildEmerald50,
            shape = RoundedCornerShape(16.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(18.dp)) {
                Text(
                    text = stringResource(R.string.login_first_time),
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                Text(
                    text = stringResource(R.string.login_first_time_body),
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
                    Text(stringResource(R.string.action_create_account), fontWeight = FontWeight.SemiBold)
                }
            }
        }

        Spacer(Modifier.height(20.dp))

        LanguageSwitch(
            currentLanguage = currentLanguage,
            onChangeLanguage = onChangeLanguage,
            modifier = Modifier.align(Alignment.CenterHorizontally)
        )

        Spacer(Modifier.height(16.dp))

        // Aviso solo en builds de prueba cuando se apunta a un servidor distinto al de producción
        if (BuildConfig.DEBUG && viewModel.sessionManager.isUsingCustomServer) {
            Text(
                text = stringResource(R.string.debug_server_in_use, viewModel.sessionManager.apiBaseUrl),
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
            title = { Text(stringResource(R.string.debug_server_title), fontWeight = FontWeight.Bold) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text(
                        stringResource(R.string.debug_server_hint),
                        fontSize = 13.sp,
                        color = IoBuildSlate500
                    )
                    OutlinedTextField(
                        value = serverUrlInput,
                        onValueChange = { serverUrlInput = it },
                        label = { Text(stringResource(R.string.debug_server_url)) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        AssistChip(onClick = { serverUrlInput = BuildConfig.DEFAULT_API_BASE_URL }, label = { Text(stringResource(R.string.debug_server_production), fontSize = 12.sp) })
                        AssistChip(onClick = { serverUrlInput = "http://10.0.2.2:8080/api/v1/" }, label = { Text("Local :8080", fontSize = 12.sp) })
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    viewModel.updateServerBaseUrl(serverUrlInput)
                    showServerDialog = false
                }) { Text(stringResource(R.string.action_save)) }
            },
            dismissButton = {
                TextButton(onClick = { showServerDialog = false }) { Text(stringResource(R.string.action_cancel)) }
            }
        )
    }
}

/** Español / English: el idioma activo (también cuando sigue al teléfono) queda resaltado. */
@Composable
fun LanguageSwitch(
    currentLanguage: String,
    onChangeLanguage: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    val active = if (currentLanguage == AppLocale.SYSTEM) {
        if (java.util.Locale.getDefault().language == "en") AppLocale.ENGLISH else AppLocale.SPANISH
    } else currentLanguage
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Icon(Icons.Default.Language, contentDescription = null, tint = IoBuildSlate500, modifier = Modifier.size(18.dp))
        listOf(AppLocale.SPANISH to "Español", AppLocale.ENGLISH to "English").forEach { (code, label) ->
            TextButton(onClick = { if (code != active) onChangeLanguage(code) }) {
                Text(
                    text = label,
                    fontSize = 14.sp,
                    fontWeight = if (code == active) FontWeight.Bold else FontWeight.Normal,
                    color = if (code == active) IoBuildEmerald700 else IoBuildSlate500
                )
            }
        }
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
            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.action_back), tint = IoBuildSlate900)
        }

        Spacer(Modifier.height(8.dp))
        Text(
            text = stringResource(R.string.register_step, step),
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
            Text(stringResource(R.string.register_title), fontSize = 26.sp, fontWeight = FontWeight.ExtraBold, color = IoBuildSlate900)
            Text(
                text = stringResource(R.string.register_subtitle),
                fontSize = 15.sp,
                color = IoBuildSlate500,
                modifier = Modifier.padding(top = 6.dp, bottom = 24.dp)
            )

            OutlinedTextField(
                value = email,
                onValueChange = { email = it; viewModel.clearAuthError() },
                label = { Text(stringResource(R.string.field_email)) },
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
                label = { Text(stringResource(R.string.field_password)) },
                supportingText = { Text(stringResource(R.string.register_password_hint)) },
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
                label = { Text(stringResource(R.string.field_confirm_password)) },
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
                text = stringResource(if (viewModel.authLoading) R.string.register_checking else R.string.action_continue),
                loading = viewModel.authLoading,
                onClick = {
                    focusManager.clearFocus()
                    viewModel.verifyInvitationForStep2(email, password, confirmPassword)
                }
            )
        } else {
            Text(stringResource(R.string.register_details_title), fontSize = 26.sp, fontWeight = FontWeight.ExtraBold, color = IoBuildSlate900)
            Text(
                text = stringResource(R.string.register_details_subtitle),
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
                            Text(stringResource(R.string.register_assigned), fontSize = 12.sp, color = IoBuildEmerald700)
                            val unitFormat = stringResource(R.string.unit_in_condominium)
                            val condominiumLabel = stringResource(R.string.condominium)
                            Text(
                                text = viewModel.invitedUnits.joinToString("\n") {
                                    String.format(unitFormat, it.unitNumber ?: "—", it.condominiumName ?: condominiumLabel)
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
                label = { Text(stringResource(R.string.field_full_name)) },
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
                label = { Text(stringResource(R.string.field_dni)) },
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
                label = { Text(stringResource(R.string.field_mobile)) },
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
                    text = stringResource(R.string.register_privacy),
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
                text = stringResource(if (viewModel.authLoading) R.string.register_creating else R.string.action_create_account),
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
