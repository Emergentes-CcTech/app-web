package pe.iobuild.resident.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.viewmodel.ResidentViewModel

@Composable
fun LoginScreen(
    viewModel: ResidentViewModel,
    onNavigateToRegister: () -> Unit,
    onLoginSuccess: () -> Unit
) {
    var email by remember { mutableStateOf("cflores@gmail.com") }
    var password by remember { mutableStateOf("Resident123!") }
    var passwordVisible by remember { mutableStateOf(false) }
    var showServerDialog by remember { mutableStateOf(false) }
    var serverUrlInput by remember { mutableStateOf(viewModel.sessionManager.apiBaseUrl) }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(Color(0xFF18181B), Color(0xFF0F172A), Color(0xFF064E3B))
                )
            )
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 36.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            // Badge de App Propietario
            Surface(
                color = IoBuildEmerald500.copy(alpha = 0.18f),
                shape = RoundedCornerShape(50),
                border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildEmerald500.copy(alpha = 0.4f))
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 14.dp, vertical = 6.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.Apartment,
                        contentDescription = null,
                        tint = IoBuildEmerald500,
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(Modifier.width(6.dp))
                    Text(
                        text = "IoBuild Mobile · Vista Propietario",
                        color = Color(0xFFA7F3D0),
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            }

            Spacer(Modifier.height(20.dp))

            // Tarjeta principal de Login
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(24.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                elevation = CardDefaults.cardElevation(defaultElevation = 10.dp)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Box(
                        modifier = Modifier
                            .size(56.dp)
                            .clip(CircleShape)
                            .background(IoBuildEmerald50),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Default.HomeWork,
                            contentDescription = "IoBuild Logo",
                            tint = IoBuildEmerald600,
                            modifier = Modifier.size(30.dp)
                        )
                    }

                    Spacer(Modifier.height(12.dp))

                    Text(
                        text = "Iniciar Sesión",
                        fontSize = 24.sp,
                        fontWeight = FontWeight.ExtraBold,
                        color = IoBuildSlate900
                    )
                    Text(
                        text = "Consulta el consumo de tu condominio, tu estado de cuenta y el desglose legal (Ley N.° 27157).",
                        fontSize = 13.sp,
                        color = IoBuildSlate500,
                        textAlign = TextAlign.Center,
                        modifier = Modifier.padding(top = 4.dp, bottom = 20.dp)
                    )

                    OutlinedTextField(
                        value = email,
                        onValueChange = {
                            email = it
                            viewModel.clearAuthError()
                        },
                        label = { Text("Correo Electrónico") },
                        leadingIcon = {
                            Icon(Icons.Default.Email, contentDescription = null, tint = IoBuildEmerald600)
                        },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email),
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    )

                    Spacer(Modifier.height(12.dp))

                    OutlinedTextField(
                        value = password,
                        onValueChange = {
                            password = it
                            viewModel.clearAuthError()
                        },
                        label = { Text("Contraseña") },
                        leadingIcon = {
                            Icon(Icons.Default.Lock, contentDescription = null, tint = IoBuildEmerald600)
                        },
                        trailingIcon = {
                            IconButton(onClick = { passwordVisible = !passwordVisible }) {
                                Icon(
                                    imageVector = if (passwordVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                    contentDescription = "Mostrar contraseña"
                                )
                            }
                        },
                        visualTransformation = if (passwordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    )

                    // Botones rápidos de credenciales de prueba
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 8.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        AssistChip(
                            onClick = {
                                email = "cflores@gmail.com"
                                password = "Resident123!"
                                viewModel.clearAuthError()
                            },
                            label = { Text("Demo .NET (Dpto. 402)", fontSize = 11.sp) },
                            leadingIcon = {
                                Icon(Icons.Default.VerifiedUser, null, modifier = Modifier.size(14.dp))
                            }
                        )
                        AssistChip(
                            onClick = {
                                email = "carla.f@email.com"
                                password = "Demo1234!"
                                viewModel.clearAuthError()
                            },
                            label = { Text("Demo Mock", fontSize = 11.sp) }
                        )
                    }

                    if (!viewModel.authError.isNullOrBlank()) {
                        Spacer(Modifier.height(12.dp))
                        Surface(
                            color = IoBuildRed50,
                            shape = RoundedCornerShape(10.dp),
                            border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildRed500.copy(alpha = 0.4f)),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Row(
                                modifier = Modifier.padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Icon(Icons.Default.ErrorOutline, null, tint = IoBuildRed500, modifier = Modifier.size(18.dp))
                                Spacer(Modifier.width(8.dp))
                                Text(
                                    text = viewModel.authError ?: "",
                                    color = Color(0xFFB91C1C),
                                    fontSize = 12.sp
                                )
                            }
                        }
                    }

                    Spacer(Modifier.height(18.dp))

                    Button(
                        onClick = { viewModel.signIn(email, password, onLoginSuccess) },
                        enabled = !viewModel.authLoading,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(50.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600)
                    ) {
                        if (viewModel.authLoading) {
                            CircularProgressIndicator(
                                color = Color.White,
                                strokeWidth = 2.dp,
                                modifier = Modifier.size(20.dp)
                            )
                            Spacer(Modifier.width(10.dp))
                        }
                        Text("Iniciar Sesión", fontWeight = FontWeight.Bold, fontSize = 15.sp)
                    }

                    HorizontalDivider(modifier = Modifier.padding(vertical = 18.dp))

                    Text(
                        text = "¿Tu administración ya registró tu correo en el padrón?",
                        fontSize = 12.sp,
                        color = IoBuildSlate500
                    )
                    Spacer(Modifier.height(8.dp))

                    OutlinedButton(
                        onClick = {
                            viewModel.clearAuthError()
                            onNavigateToRegister()
                        },
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Icon(Icons.Default.PersonAdd, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(8.dp))
                        Text("Soy dueño de departamento (Registrarme)")
                    }
                }
            }

            Spacer(Modifier.height(16.dp))

            // Configuración de URL del Backend BFF
            TextButton(onClick = {
                serverUrlInput = viewModel.sessionManager.apiBaseUrl
                showServerDialog = true
            }) {
                Icon(Icons.Default.SettingsEthernet, contentDescription = null, tint = Color(0xFFA7F3D0), modifier = Modifier.size(16.dp))
                Spacer(Modifier.width(6.dp))
                Text(
                    text = "Servidor API/BFF: ${viewModel.sessionManager.apiBaseUrl}",
                    color = Color(0xFFA7F3D0),
                    fontSize = 12.sp
                )
            }
        }
    }

    if (showServerDialog) {
        AlertDialog(
            onDismissRequest = { showServerDialog = false },
            title = { Text("Configurar URL del Backend / BFF", fontWeight = FontWeight.Bold) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "En el emulador Android, 10.0.2.2 apunta al localhost de tu PC.",
                        fontSize = 12.sp,
                        color = IoBuildSlate500
                    )
                    OutlinedTextField(
                        value = serverUrlInput,
                        onValueChange = { serverUrlInput = it },
                        label = { Text("Base URL (/api/v1/)") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                        AssistChip(
                            onClick = { serverUrlInput = "https://iobuild.me/api/v1/" },
                            label = { Text("Prod HTTPS", fontSize = 11.sp) }
                        )
                        AssistChip(
                            onClick = { serverUrlInput = "http://10.0.2.2:8080/api/v1/" },
                            label = { Text(".NET :8080", fontSize = 11.sp) }
                        )
                        AssistChip(
                            onClick = { serverUrlInput = "http://10.0.2.2:80/api/v1/" },
                            label = { Text("Nginx :80", fontSize = 11.sp) }
                        )
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    viewModel.updateServerBaseUrl(serverUrlInput)
                    showServerDialog = false
                }) {
                    Text("Guardar")
                }
            },
            dismissButton = {
                TextButton(onClick = { showServerDialog = false }) {
                    Text("Cancelar")
                }
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

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(IoBuildBackground)
            .verticalScroll(rememberScrollState())
            .padding(20.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = {
                viewModel.backToRegisterStep1()
                onBackToLogin()
            }) {
                Icon(Icons.Default.ArrowBack, contentDescription = "Volver")
            }
            Column {
                Text(
                    text = "Registro de Propietario",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                Text(
                    text = "Paso ${viewModel.registerStep} de 2 · Vinculación con tu departamento",
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }
        }

        Spacer(Modifier.height(16.dp))

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White)
        ) {
            Column(modifier = Modifier.padding(20.dp)) {
                if (viewModel.registerStep == 1) {
                    Text(
                        text = "1. Información de tu cuenta",
                        fontWeight = FontWeight.Bold,
                        fontSize = 16.sp,
                        color = IoBuildSlate900
                    )
                    Text(
                        text = "Ingresa el correo electrónico con el que la administración de tu condominio te asignó tu departamento.",
                        fontSize = 13.sp,
                        color = IoBuildSlate500,
                        modifier = Modifier.padding(top = 4.dp, bottom = 16.dp)
                    )

                    OutlinedTextField(
                        value = email,
                        onValueChange = { email = it; viewModel.clearAuthError() },
                        label = { Text("Correo Electrónico *") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email),
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(12.dp))

                    OutlinedTextField(
                        value = password,
                        onValueChange = { password = it; viewModel.clearAuthError() },
                        label = { Text("Contraseña *") },
                        visualTransformation = PasswordVisualTransformation(),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(12.dp))

                    OutlinedTextField(
                        value = confirmPassword,
                        onValueChange = { confirmPassword = it; viewModel.clearAuthError() },
                        label = { Text("Confirmar Contraseña *") },
                        visualTransformation = PasswordVisualTransformation(),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )

                    if (!viewModel.authError.isNullOrBlank()) {
                        Spacer(Modifier.height(12.dp))
                        Text(
                            text = viewModel.authError ?: "",
                            color = IoBuildRed500,
                            fontSize = 12.sp
                        )
                    }

                    Spacer(Modifier.height(20.dp))

                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        OutlinedButton(
                            onClick = onBackToLogin,
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Cancelar")
                        }
                        Button(
                            onClick = { viewModel.verifyInvitationForStep2(email, password, confirmPassword) },
                            enabled = !viewModel.authLoading,
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600)
                        ) {
                            Text(if (viewModel.authLoading) "Verificando..." else "Siguiente")
                        }
                    }
                } else {
                    if (viewModel.invitedUnits.isNotEmpty()) {
                        Surface(
                            color = IoBuildEmerald50,
                            shape = RoundedCornerShape(10.dp),
                            border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildEmerald500),
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(bottom = 16.dp)
                        ) {
                            val unitsText = viewModel.invitedUnits.joinToString(", ") {
                                "Dpto. ${it.unitNumber ?: "—"} (${it.condominiumName ?: "Condominio"})"
                            }
                            Text(
                                text = "✓ Unidad pre-asignada encontrada: $unitsText",
                                color = IoBuildEmerald700,
                                fontSize = 13.sp,
                                fontWeight = FontWeight.SemiBold,
                                modifier = Modifier.padding(12.dp)
                            )
                        }
                    }

                    Text(
                        text = "2. Datos personales del titular",
                        fontWeight = FontWeight.Bold,
                        fontSize = 16.sp,
                        color = IoBuildSlate900
                    )
                    Spacer(Modifier.height(12.dp))

                    OutlinedTextField(
                        value = fullName,
                        onValueChange = { fullName = it; viewModel.clearAuthError() },
                        label = { Text("Nombre completo *") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(12.dp))

                    OutlinedTextField(
                        value = dni,
                        onValueChange = { if (it.length <= 8) dni = it; viewModel.clearAuthError() },
                        label = { Text("DNI (8 dígitos) *") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(12.dp))

                    OutlinedTextField(
                        value = phoneNumber,
                        onValueChange = { phoneNumber = it; viewModel.clearAuthError() },
                        label = { Text("Teléfono *") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(12.dp))

                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Checkbox(
                            checked = acceptPrivacy,
                            onCheckedChange = { acceptPrivacy = it; viewModel.clearAuthError() }
                        )
                        Text(
                            text = "Acepto las políticas de privacidad y de datos de telemetría comunal.",
                            fontSize = 12.sp,
                            color = IoBuildSlate700
                        )
                    }

                    if (!viewModel.authError.isNullOrBlank()) {
                        Spacer(Modifier.height(8.dp))
                        Text(
                            text = viewModel.authError ?: "",
                            color = IoBuildRed500,
                            fontSize = 12.sp
                        )
                    }

                    Spacer(Modifier.height(16.dp))

                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        OutlinedButton(
                            onClick = { viewModel.backToRegisterStep1() },
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Atrás")
                        }
                        Button(
                            onClick = {
                                viewModel.completeResidentRegistration(
                                    email = email,
                                    password = password,
                                    fullName = fullName,
                                    dni = dni,
                                    phoneNumber = phoneNumber,
                                    acceptedPrivacy = acceptPrivacy,
                                    onSuccess = onRegisterSuccess
                                )
                            },
                            enabled = !viewModel.authLoading,
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600)
                        ) {
                            Text(if (viewModel.authLoading) "Registrando..." else "Completar Registro")
                        }
                    }
                }
            }
        }
    }
}
