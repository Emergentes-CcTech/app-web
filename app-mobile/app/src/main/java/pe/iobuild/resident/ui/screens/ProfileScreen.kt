package pe.iobuild.resident.ui.screens

import androidx.compose.ui.res.stringResource
import pe.iobuild.resident.R
import pe.iobuild.resident.ui.util.AppLocale
import androidx.compose.foundation.clickable
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.data.model.ResidentProfileBffResponse
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.util.FormatUtils

/**
 * Pantalla "Mi Perfil" del Propietario/Residente.
 * Equivale a `profile-resident.vue` + `profile-editor.vue` de la app web,
 * alimentada por `GET /api/v1/mobile/resident/profile` y `PUT /api/v1/profiles/{id}`.
 */
@Composable
fun ProfileScreen(
    loading: Boolean,
    saving: Boolean,
    error: String?,
    successMessage: String?,
    data: ResidentProfileBffResponse?,
    onRefresh: () -> Unit,
    onSaveProfile: (fullName: String, phoneNumber: String, secondEmail: String, photoUrl: String?) -> Unit,
    onClearMessages: () -> Unit,
    currentLanguage: String,
    onChangeLanguage: (String) -> Unit,
    onSignOut: () -> Unit
) {
    var editMode by remember { mutableStateOf(false) }

    var fullNameInput by remember(data) { mutableStateOf(data?.fullName.orEmpty()) }
    var phoneInput by remember(data) { mutableStateOf(data?.phoneNumber.orEmpty()) }
    var secondEmailInput by remember(data) { mutableStateOf(data?.secondEmail.orEmpty()) }

    // Preferencias de notificación del residente (RF-003)
    var notifyByEmail by remember { mutableStateOf(true) }
    var notifyOnNewSettlement by remember { mutableStateOf(true) }
    var notifyDueSoon by remember { mutableStateOf(true) }
    var notifyCondoAlerts by remember { mutableStateOf(true) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(IoBuildBackground)
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Cabecera
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.profile_title),
                    fontSize = 24.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )
                Text(
                    text = stringResource(R.string.profile_subtitle),
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }
        }

        if (loading && data == null) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 48.dp),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator(color = IoBuildEmerald600)
            }
            return@Column
        }

        if (!error.isNullOrBlank()) {
            Surface(
                shape = RoundedCornerShape(12.dp),
                color = IoBuildRed50
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Row(
                        modifier = Modifier.weight(1f),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            imageVector = Icons.Default.ErrorOutline,
                            contentDescription = null,
                            tint = IoBuildRed500,
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = error,
                            fontSize = 12.sp,
                            color = IoBuildRed500,
                            fontWeight = FontWeight.Medium
                        )
                    }
                    IconButton(onClick = onClearMessages, modifier = Modifier.size(24.dp)) {
                        Icon(Icons.Default.Close, contentDescription = null, tint = IoBuildRed500)
                    }
                }
            }
        }

        if (!successMessage.isNullOrBlank()) {
            Surface(
                shape = RoundedCornerShape(12.dp),
                color = IoBuildEmerald50
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Row(
                        modifier = Modifier.weight(1f),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            imageVector = Icons.Default.CheckCircle,
                            contentDescription = null,
                            tint = IoBuildEmerald600,
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = successMessage,
                            fontSize = 12.sp,
                            color = IoBuildEmerald700,
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                    IconButton(onClick = onClearMessages, modifier = Modifier.size(24.dp)) {
                        Icon(Icons.Default.Close, contentDescription = null, tint = IoBuildEmerald700)
                    }
                }
            }
        }

        if (data == null) return@Column

        // ── 1. Tarjeta de Identidad del Propietario ──
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(18.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                val displayName = data.fullName?.takeIf { it.isNotBlank() } ?: stringResource(R.string.owner)
                val initials = remember(displayName) {
                    displayName
                        .split(" ")
                        .filter { it.isNotBlank() }
                        .take(2)
                        .joinToString("") { it.first().uppercase() }
                        .ifBlank { "PR" }
                }

                Box(
                    modifier = Modifier
                        .size(78.dp)
                        .clip(CircleShape)
                        .background(
                            Brush.linearGradient(
                                colors = listOf(IoBuildSlate900, IoBuildEmerald600)
                            )
                        )
                        .border(3.dp, IoBuildEmerald100, CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = initials,
                        color = Color.White,
                        fontSize = 26.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                }

                Text(
                    text = displayName,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )

                Surface(
                    shape = RoundedCornerShape(999.dp),
                    color = IoBuildEmerald50
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 5.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.VerifiedUser,
                            contentDescription = null,
                            tint = IoBuildEmerald600,
                            modifier = Modifier.size(14.dp)
                        )
                        Text(
                            text = stringResource(R.string.profile_role),
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold,
                            color = IoBuildEmerald700
                        )
                    }
                }

                if (!data.email.isNullOrBlank()) {
                    Text(
                        text = data.email,
                        fontSize = 13.sp,
                        color = IoBuildSlate500
                    )
                }

                Text(
                    text = stringResource(R.string.profile_user_id, data.userId),
                    fontSize = 11.sp,
                    color = IoBuildSlate500
                )

                // Resumen del Departamento asignado al Propietario
                data.unit?.let { unit ->
                    HorizontalDivider(
                        modifier = Modifier.padding(vertical = 4.dp),
                        color = IoBuildSlate200
                    )
                    Surface(
                        shape = RoundedCornerShape(14.dp),
                        color = IoBuildSlate100,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(10.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Apartment,
                                    contentDescription = null,
                                    tint = IoBuildEmerald600
                                )
                                Column {
                                    Text(
                                        text = stringResource(R.string.profile_condo_unit, unit.condominiumName ?: stringResource(R.string.condominium), unit.unitNumber),
                                        fontSize = 13.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = IoBuildSlate900
                                    )
                                    val floorLabel = unit.floor?.let { stringResource(R.string.profile_floor, it) } ?: ""
                                    val areaLabel = unit.areaM2?.let { "${it.toInt()} m² · " } ?: ""
                                    Text(
                                        text = floorLabel + areaLabel + stringResource(R.string.profile_aliquot, FormatUtils.formatAliquot(unit.aliquotPercentage)),
                                        fontSize = 11.sp,
                                        color = IoBuildSlate500
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }

        // ── 2. Información de Cuenta (Editable) ──
        Card(
            shape = RoundedCornerShape(18.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = stringResource(R.string.profile_account_info),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = IoBuildSlate900
                    )
                    if (!editMode) {
                        TextButton(onClick = { editMode = true }) {
                            Icon(
                                imageVector = Icons.Default.Edit,
                                contentDescription = null,
                                modifier = Modifier.size(16.dp),
                                tint = IoBuildEmerald600
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(stringResource(R.string.profile_edit), fontWeight = FontWeight.Bold, color = IoBuildEmerald600)
                        }
                    }
                }

                OutlinedTextField(
                    value = fullNameInput,
                    onValueChange = { fullNameInput = it },
                    enabled = editMode && !saving,
                    label = { Text(stringResource(R.string.field_full_name)) },
                    leadingIcon = { Icon(Icons.Default.Person, contentDescription = null) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                OutlinedTextField(
                    value = data.email.orEmpty(),
                    onValueChange = {},
                    enabled = false,
                    label = { Text(stringResource(R.string.profile_email_readonly)) },
                    leadingIcon = { Icon(Icons.Default.Email, contentDescription = null) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                OutlinedTextField(
                    value = phoneInput,
                    onValueChange = { phoneInput = it },
                    enabled = editMode && !saving,
                    label = { Text(stringResource(R.string.profile_phone)) },
                    leadingIcon = { Icon(Icons.Default.Phone, contentDescription = null) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                OutlinedTextField(
                    value = secondEmailInput,
                    onValueChange = { secondEmailInput = it },
                    enabled = editMode && !saving,
                    label = { Text(stringResource(R.string.profile_second_email)) },
                    leadingIcon = { Icon(Icons.Default.AlternateEmail, contentDescription = null) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                if (editMode) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        OutlinedButton(
                            onClick = {
                                fullNameInput = data.fullName.orEmpty()
                                phoneInput = data.phoneNumber.orEmpty()
                                secondEmailInput = data.secondEmail.orEmpty()
                                editMode = false
                            },
                            enabled = !saving,
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Text(stringResource(R.string.action_cancel))
                        }
                        Button(
                            onClick = {
                                onSaveProfile(
                                    fullNameInput.trim(),
                                    phoneInput.trim(),
                                    secondEmailInput.trim(),
                                    data.photoUrl
                                )
                                editMode = false
                            },
                            enabled = !saving,
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(12.dp),
                            colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600)
                        ) {
                            if (saving) {
                                CircularProgressIndicator(
                                    color = Color.White,
                                    strokeWidth = 2.dp,
                                    modifier = Modifier.size(16.dp)
                                )
                            } else {
                                Text(stringResource(R.string.profile_save_changes), fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }
            }
        }

        // ── Idioma de la app: Sistema (sigue al teléfono), Español o English ──
        Card(
            shape = RoundedCornerShape(18.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp)
            ) {
                Text(
                    text = stringResource(R.string.profile_language),
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                Spacer(Modifier.height(4.dp))
                listOf(
                    AppLocale.SYSTEM to stringResource(R.string.profile_language_system),
                    AppLocale.SPANISH to "Español",
                    AppLocale.ENGLISH to "English"
                ).forEach { (code, label) ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable { if (code != currentLanguage) onChangeLanguage(code) }
                            .padding(vertical = 4.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        RadioButton(
                            selected = code == currentLanguage,
                            onClick = { if (code != currentLanguage) onChangeLanguage(code) },
                            colors = RadioButtonDefaults.colors(selectedColor = IoBuildEmerald600)
                        )
                        Text(text = label, fontSize = 14.sp, color = IoBuildSlate900)
                    }
                }
            }
        }

        // ── 3. Preferencias de Notificación del Propietario ──
        Card(
            shape = RoundedCornerShape(18.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    text = stringResource(R.string.profile_notifications),
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                Text(
                    text = stringResource(R.string.profile_notifications_subtitle),
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )

                NotificationToggleRow(
                    title = stringResource(R.string.notif_email),
                    subtitle = stringResource(R.string.notif_email_hint),
                    checked = notifyByEmail,
                    onCheckedChange = { notifyByEmail = it }
                )
                HorizontalDivider(color = IoBuildSlate200)
                NotificationToggleRow(
                    title = stringResource(R.string.notif_new_settlement),
                    subtitle = stringResource(R.string.notif_new_settlement_hint),
                    checked = notifyOnNewSettlement,
                    onCheckedChange = { notifyOnNewSettlement = it }
                )
                HorizontalDivider(color = IoBuildSlate200)
                NotificationToggleRow(
                    title = stringResource(R.string.notif_due),
                    subtitle = stringResource(R.string.notif_due_hint),
                    checked = notifyDueSoon,
                    onCheckedChange = { notifyDueSoon = it }
                )
                HorizontalDivider(color = IoBuildSlate200)
                NotificationToggleRow(
                    title = stringResource(R.string.notif_alerts),
                    subtitle = stringResource(R.string.notif_alerts_hint),
                    checked = notifyCondoAlerts,
                    onCheckedChange = { notifyCondoAlerts = it }
                )
            }
        }

        // ── 4. Seguridad y Cierre de Sesión ──
        Card(
            shape = RoundedCornerShape(18.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = stringResource(R.string.profile_security),
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )

                if (!data.sessions.isNullOrEmpty()) {
                    data.sessions.forEach { session ->
                        Surface(
                            shape = RoundedCornerShape(12.dp),
                            color = IoBuildSlate100
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(12.dp),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.PhoneAndroid,
                                        contentDescription = null,
                                        tint = IoBuildEmerald600
                                    )
                                    Column {
                                        Text(
                                            text = session.device ?: stringResource(R.string.profile_device),
                                            fontSize = 13.sp,
                                            fontWeight = FontWeight.SemiBold,
                                            color = IoBuildSlate900
                                        )
                                        if (!session.lastActive.isNullOrBlank()) {
                                            Text(
                                                text = session.lastActive,
                                                fontSize = 11.sp,
                                                color = IoBuildSlate500
                                            )
                                        }
                                    }
                                }
                                if (session.isCurrent) {
                                    Surface(
                                        shape = RoundedCornerShape(999.dp),
                                        color = IoBuildEmerald50
                                    ) {
                                        Text(
                                            text = stringResource(R.string.profile_session_active),
                                            fontSize = 11.sp,
                                            fontWeight = FontWeight.Bold,
                                            color = IoBuildEmerald700,
                                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp)
                                        )
                                    }
                                }
                            }
                        }
                    }
                }

                Button(
                    onClick = onSignOut,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = IoBuildRed50,
                        contentColor = IoBuildRed500
                    )
                ) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.Logout,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.action_sign_out),
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }
    }
}

@Composable
private fun NotificationToggleRow(
    title: String,
    subtitle: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                fontSize = 13.sp,
                fontWeight = FontWeight.SemiBold,
                color = IoBuildSlate900
            )
            Text(
                text = subtitle,
                fontSize = 11.sp,
                color = IoBuildSlate500
            )
        }
        Spacer(modifier = Modifier.width(8.dp))
        Switch(
            checked = checked,
            onCheckedChange = onCheckedChange,
            colors = SwitchDefaults.colors(
                checkedThumbColor = Color.White,
                checkedTrackColor = IoBuildEmerald600
            )
        )
    }
}
