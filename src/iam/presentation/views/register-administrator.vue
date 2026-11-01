<template>
  <div class="auth-container">
    <!-- Left Side - Form -->
    <div class="auth-form-side">
      <!-- Green Fragment Decoration -->
      <div class="green-fragment"></div>

      <div class="form-content">
        <div class="form-wrapper">
          <h2 class="form-title">{{ $t('iam.registerAdministrator.title') }}</h2>
          <p class="form-subtitle">{{ $t('iam.registerAdministrator.subtitle') }}</p>

          <pv-stepper v-model:value="currentStep" linear role="group">
            <pv-step-list role="tablist" :aria-label="$t('iam.registerAdministrator.title')">
              <pv-step :value="1" :pt="{ root: { 'aria-current': null }, header: { 'aria-selected': currentStep === 1 } }">{{ $t('iam.registerAdministrator.userInfoSection') }}</pv-step>
              <pv-step :value="2" :pt="{ root: { 'aria-current': null }, header: { 'aria-selected': currentStep === 2 } }">{{ $t('iam.registerAdministrator.profileInfoSection') }}</pv-step>
            </pv-step-list>

            <pv-step-panels>
              <!-- Step 1: Account Information -->
              <pv-step-panel :value="1">
                <div class="step-content">
          <!-- Email -->
          <div class="mb-3">
            <label for="email" class="block mb-2">{{ $t('iam.registerAdministrator.email') }} *</label>
            <pv-input-text
              id="email"
              v-model="registerForm.email"
              type="email"
              :placeholder="$t('iam.registerAdministrator.emailPlaceholder')"
              :invalid="!!fieldErrors.email"
              class="w-full"
              @blur="onEmailBlur"
            />
            <small v-if="fieldErrors.email" class="p-error block mt-1">
              {{ fieldErrors.email }}
              <a v-if="isEmailAlreadyRegistered" href="#" @click.prevent="goToLogin" class="text-green-500 font-semibold underline ml-1">{{ $t('iam.login.submitButton') }}</a>
            </small>
          </div>

          <!-- Password -->
          <div class="mb-3">
            <label for="password" class="block mb-2">{{ $t('iam.registerAdministrator.password') }} *</label>
            <pv-password
              inputId="password"
              v-model="registerForm.password"
              :placeholder="$t('iam.registerAdministrator.passwordPlaceholder')"
              :invalid="!!fieldErrors.password"
              toggleMask
              class="w-full"
              inputClass="w-full"
            />
            <small v-if="fieldErrors.password" class="p-error block mt-1">{{ fieldErrors.password }}</small>
          </div>

          <!-- Confirm Password -->
          <div class="mb-3">
            <label for="confirmPassword" class="block mb-2">{{ $t('iam.registerAdministrator.confirmPassword') }} *</label>
            <pv-password
              inputId="confirmPassword"
              v-model="registerForm.confirmPassword"
              :placeholder="$t('iam.registerAdministrator.confirmPasswordPlaceholder')"
              :invalid="!!fieldErrors.confirmPassword"
              :feedback="false"
              toggleMask
              class="w-full"
              inputClass="w-full"
            />
            <small v-if="fieldErrors.confirmPassword" class="p-error block mt-1">{{ fieldErrors.confirmPassword }}</small>
          </div>

          <!-- Error Message -->
          <pv-message v-if="errorMessage" severity="error" :closable="false" class="mb-3">
            {{ errorMessage }}
          </pv-message>

                <div class="flex gap-2 pt-4">
                  <pv-button
                    type="button"
                    :label="$t('iam.registerAdministrator.cancelButton')"
                    severity="secondary"
                    outlined
                    @click="goToLogin"
                    class="flex-1"
                  />
                  <pv-button
                    :label="$t('iam.actions.next')"
                    icon="pi pi-arrow-right"
                    iconPos="right"
                    :loading="checkingEmail"
                    @click="goToStep2"
                    class="flex-1"
                  />
                </div>
              </div>
            </pv-step-panel>

            <!-- Step 2: Company Information -->
            <pv-step-panel :value="2">
              <div class="step-content">
          <!-- Photo URL -->
          <div class="mb-3">
            <label for="photoUrl" class="block mb-2">{{ $t('iam.registerAdministrator.photoUrl') }}</label>
            <input
              type="file"
              ref="fileInput"
              accept="image/*"
              style="display: none;"
              @change="handleLocalFileUpload"
            />
            <pv-button
              type="button"
              :label="registerForm.photoUrl ? $t('iam.actions.changePhoto') : $t('iam.registerAdministrator.uploadPhoto')"
              icon="pi pi-cloud-upload"
              @click="openUploadModal"
              severity="secondary"
              outlined
              class="w-full mb-2"
            />
            <div v-if="registerForm.photoUrl" class="mt-2 text-center">
              <img
                :src="registerForm.photoUrl"
                :alt="$t('iam.actions.photoPreviewAlt')"
                class="uploaded-image"
              />
              <div class="flex justify-content-center align-items-center gap-2 mt-2">
                <span class="text-sm text-green-600 font-medium">✓ {{ $t('iam.actions.photoSelected') }}</span>
                <pv-button
                  type="button"
                  icon="pi pi-trash"
                  text
                  rounded
                  severity="danger"
                  size="small"
                  @click="registerForm.photoUrl = ''"
                  :title="$t('iam.actions.removePhoto')"
                />
              </div>
            </div>
          </div>

          <!-- companyName -->
          <div class="mb-3">
            <label for="companyName" class="block mb-2">{{ $t('iam.registerAdministrator.companyName') }} *</label>
            <pv-input-text
              id="companyName"
              v-model="registerForm.companyName"
              :placeholder="$t('iam.registerAdministrator.companyNamePlaceholder')"
              :invalid="!!fieldErrors.companyName"
              class="w-full"
            />
            <small v-if="fieldErrors.companyName" class="p-error block mt-1">{{ fieldErrors.companyName }}</small>
          </div>

          <!-- legalRepresentative -->
          <div class="mb-3">
            <label for="legalRepresentative" class="block mb-2">{{ $t('iam.registerAdministrator.legalRepresentative') }} *</label>
            <pv-input-text
              id="legalRepresentative"
              v-model="registerForm.legalRepresentative"
              :placeholder="$t('iam.registerAdministrator.legalRepresentativePlaceholder')"
              :invalid="!!fieldErrors.legalRepresentative"
              class="w-full"
            />
            <small v-if="fieldErrors.legalRepresentative" class="p-error block mt-1">{{ fieldErrors.legalRepresentative }}</small>
          </div>

          <!-- phoneNumber -->
          <div class="mb-3">
            <label for="phoneNumber" class="block mb-2">{{ $t('iam.registerAdministrator.phoneNumber') }} *</label>
            <pv-input-text
              id="phoneNumber"
              v-model="registerForm.phoneNumber"
              :placeholder="$t('iam.registerAdministrator.phoneNumberPlaceholder')"
              :invalid="!!fieldErrors.phoneNumber"
              class="w-full"
            />
            <small v-if="fieldErrors.phoneNumber" class="p-error block mt-1">{{ fieldErrors.phoneNumber }}</small>
          </div>

          <!-- address -->
          <div class="mb-3">
            <label for="address" class="block mb-2">{{ $t('iam.registerAdministrator.address') }} *</label>
            <pv-input-text
              id="address"
              v-model="registerForm.address"
              :placeholder="$t('iam.registerAdministrator.addressPlaceholder')"
              :invalid="!!fieldErrors.address"
              class="w-full"
            />
            <small v-if="fieldErrors.address" class="p-error block mt-1">{{ fieldErrors.address }}</small>
          </div>

          <!-- Error/Success Messages -->
          <pv-message v-if="errorMessage" severity="error" :closable="false" class="mb-3">
            {{ errorMessage }}
          </pv-message>

          <pv-message v-if="successMessage" severity="success" :closable="false" class="mb-3">
            {{ successMessage }}
          </pv-message>

                <div class="flex gap-2 pt-4">
                  <pv-button
                    :label="$t('iam.actions.back')"
                    severity="secondary"
                    icon="pi pi-arrow-left"
                    @click="currentStep = 1"
                    class="flex-1"
                  />
                  <pv-button
                    :label="$t('iam.registerAdministrator.submitButton')"
                    icon="pi pi-check"
                    :loading="isLoading"
                    @click="handleRegister"
                    class="flex-1"
                  />
                </div>
              </div>
            </pv-step-panel>
          </pv-step-panels>
        </pv-stepper>
        </div>
      </div>
    </div>

    <!-- Right Side - Image -->
    <div class="auth-image-side"></div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { useIamStore } from '../../application/iam.store.js';
import { useProfileStore } from '../../../profiles/application/profile.store.js';
import { IamApi } from '../../infrastructure/iam-api.js';
import { ROUTES } from '../../../shared/infrastructure/paths.js';
import {
  isValidEmail,
  isValidPhone,
  isValidName,
  isValidPassword,
  normalizeEmail
} from '../../../shared/presentation/validators.js';

const router = useRouter();
const { t } = useI18n();
const iamStore = useIamStore();
const profileStore = useProfileStore();
const iamApi = new IamApi();

const currentStep = ref(1);
const fieldErrors = ref({});

import { loadCloudinaryWidget } from "../../../shared/infrastructure/cloudinary-loader.js";
import { getAvatarUploadConfig } from "../../../shared/infrastructure/cloudinary-config.js";

// Cloudinary configuration
const cloudinaryName = import.meta.env.VITE_CLOUDINARY_CLOUD_NAME;
const cloudinaryPreset = import.meta.env.VITE_CLOUDINARY_UPLOAD_PRESET;
let openingUpload = false;

const fileInput = ref(null);

const handleLocalFileUpload = (event) => {
  const file = event.target.files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = (e) => {
    registerForm.value.photoUrl = e.target.result;
  };
  reader.readAsDataURL(file);
};

const openUploadModal = async () => {
  if (openingUpload) return;
  const hasCloudinary = cloudinaryName && cloudinaryName.trim() !== '' && cloudinaryPreset && cloudinaryPreset.trim() !== '';
  if (hasCloudinary) {
    openingUpload = true;
    try {
      const cloudinary = await loadCloudinaryWidget();
      const widgetConfig = getAvatarUploadConfig(cloudinaryName, cloudinaryPreset);
      cloudinary.openUploadWidget(
        widgetConfig,
        (error, result) => {
          if (!error && result && result.event === "success") {
            registerForm.value.photoUrl = result.info.secure_url || result.info.url;
          }
        }
      );
      return;
    } catch (err) {
      console.warn('Cloudinary widget failed, using local file picker:', err);
    } finally {
      openingUpload = false;
    }
  }

  // Fallback seguro: abrir selector de archivo nativo sin depender de Cloudinary
  if (fileInput.value) {
    fileInput.value.click();
  }
};

const registerForm = ref({
  email: '',
  password: '',
  confirmPassword: '',
  photoUrl: '',
  companyName: '',
  legalRepresentative: '',
  phoneNumber: '',
  address: ''
});

const isLoading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const isEmailAlreadyRegistered = ref(false);
const checkingEmail = ref(false);

async function onEmailBlur() {
  const email = (registerForm.value.email || '').trim();
  if (email && isValidEmail(email)) {
    try {
      const res = await iamApi.checkInvitation(email);
      if (res?.data?.alreadyRegistered) {
        isEmailAlreadyRegistered.value = true;
        fieldErrors.value.email = t('iam.validation.emailAlreadyRegistered');
        errorMessage.value = t('iam.validation.emailAlreadyRegistered');
      } else {
        isEmailAlreadyRegistered.value = false;
        if (fieldErrors.value.email === t('iam.validation.emailAlreadyRegistered')) {
          fieldErrors.value.email = null;
          errorMessage.value = '';
        }
      }
    } catch (_) {}
  }
}

async function goToStep2() {
  errorMessage.value = '';
  fieldErrors.value = {};

  const email = normalizeEmail(registerForm.value.email);
  const password = registerForm.value.password || '';
  const confirmPassword = registerForm.value.confirmPassword || '';

  if (!email) {
    fieldErrors.value.email = t('iam.validation.emailRequired');
  } else if (!isValidEmail(email)) {
    fieldErrors.value.email = t('iam.validation.emailInvalid');
  }

  if (!password) {
    fieldErrors.value.password = t('iam.validation.passwordRequired');
  } else if (!isValidPassword(password)) {
    fieldErrors.value.password = t('iam.validation.passwordMinLength');
  }

  if (!confirmPassword) {
    fieldErrors.value.confirmPassword = t('iam.validation.confirmPasswordRequired');
  } else if (password !== confirmPassword) {
    fieldErrors.value.confirmPassword = t('iam.validation.passwordMismatch');
  }

  if (Object.keys(fieldErrors.value).length > 0) {
    errorMessage.value = t('iam.validation.completeAccountInfo');
    return;
  }

  try {
    checkingEmail.value = true;
    const res = await iamApi.checkInvitation(email);
    if (res?.data?.alreadyRegistered) {
      isEmailAlreadyRegistered.value = true;
      fieldErrors.value.email = t('iam.validation.emailAlreadyRegistered');
      errorMessage.value = t('iam.validation.emailAlreadyRegistered');
      return;
    }
    isEmailAlreadyRegistered.value = false;
  } catch (_) {
  } finally {
    checkingEmail.value = false;
  }

  registerForm.value.email = email;
  currentStep.value = 2;
}

async function handleRegister() {
  isLoading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  fieldErrors.value = {};

  const companyName = (registerForm.value.companyName || '').trim();
  const legalRepresentative = (registerForm.value.legalRepresentative || '').trim();
  const phoneNumber = (registerForm.value.phoneNumber || '').trim();
  const address = (registerForm.value.address || '').trim();

  if (!companyName) {
    fieldErrors.value.companyName = t('iam.validation.companyNameRequired');
  } else if (!isValidName(companyName, 3, 120)) {
    fieldErrors.value.companyName = t('iam.validation.companyNameMinLength');
  }
  if (!legalRepresentative) {
    fieldErrors.value.legalRepresentative = t('iam.validation.legalRepresentativeRequired');
  } else if (!isValidName(legalRepresentative)) {
    fieldErrors.value.legalRepresentative = t('iam.validation.nameInvalid');
  }
  if (!phoneNumber) {
    fieldErrors.value.phoneNumber = t('iam.validation.phoneRequired');
  } else if (!isValidPhone(phoneNumber)) {
    fieldErrors.value.phoneNumber = t('iam.validation.phoneInvalid');
  }
  if (!isValidName(address, 5, 200)) {
    fieldErrors.value.address = t('iam.validation.addressRequired');
  }

  if (Object.keys(fieldErrors.value).length > 0) {
    errorMessage.value = t('iam.validation.completeProfileInfo');
    isLoading.value = false;
    return;
  }

  try {
    // 1. Crear usuario con rol administrador
    // El backend solo admite su nombre de dominio para el rol (PropertyManager); "administrator" es el rol de la interfaz
    await iamStore.signUp({
      email: registerForm.value.email,
      password: registerForm.value.password,
      role: 'PropertyManager'
    });

    // 2. Iniciar sesión para obtener el ID de usuario y el token
    const authenticatedUser = await iamStore.signIn(registerForm.value.email, registerForm.value.password);
    if (!authenticatedUser || !authenticatedUser.id) {
      throw new Error('Failed to retrieve user ID after sign in');
    }

    // 3. Crear el perfil corporativo
    const profileData = {
      userId: authenticatedUser.id,
      companyName,
      legalRepresentative,
      phoneNumber,
      address,
      photoUrl: registerForm.value.photoUrl || ''
    };
    await profileStore.createProfile(profileData);
    iamStore.updateUserProfile({ name: companyName, photoUrl: profileData.photoUrl });

    successMessage.value = t('iam.messages.registrationSuccess');

    // Sin suscripción activa no puede registrar condominios: elegir plan
    setTimeout(() => {
      router.push(ROUTES.SUBSCRIPTION_DETAIL);
    }, 2000);
  } catch (error) {
    console.error('Registration error:', error);
    if (error.message?.includes('user ID')) {
      errorMessage.value = t('iam.messages.profileCreationFailed');
    } else if (error.response?.status === 409 || error.response?.data?.error?.includes('already exists')) {
      errorMessage.value = t('iam.validation.emailAlreadyRegistered');
      fieldErrors.value.email = t('iam.validation.emailAlreadyRegistered');
      isEmailAlreadyRegistered.value = true;
      currentStep.value = 1;
    } else {
      const apiMessage = error.response?.data?.message || error.response?.data?.error;
      errorMessage.value = apiMessage === 'Invalid registration data.'
        ? t('iam.validation.registrationDataInvalid')
        : t('iam.validation.registrationFailed');
    }
  } finally {
    isLoading.value = false;
  }
}

function goToLogin() {
  router.push({ name: 'login' });
}
</script>

<style scoped>
.auth-container {
  display: flex;
  min-height: 100vh;
  width: 100%;
  background-color: #262626;
}

/* Left Side - Form */
.auth-form-side {
  flex: 1;
  background-color: #18181b;
  background-image: 
      linear-gradient(rgba(255, 255, 255, 0.02) 1px, transparent 1px),
      linear-gradient(90deg, rgba(255, 255, 255, 0.02) 1px, transparent 1px);
  background-size: 40px 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  overflow-y: auto;
  position: relative;
  z-index: 10;
}

/* Green Fragment Decoration */
.green-fragment {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 120px;
  height: 300px;
  background: linear-gradient(to top, #10b981, transparent);
  clip-path: polygon(100% 0, 100% 100%, 0% 100%);
  opacity: 0.6;
  z-index: -1;
}

.form-content {
  width: 100%;
  max-width: 550px;
}

.form-wrapper {
  background: white;
  padding: 2rem;
  border-radius: 1rem;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
}

.form-title {
  font-size: 1.875rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 0.5rem 0;
}

.form-subtitle {
  color: #6b7280;
  margin: 0 0 1.5rem 0;
  font-size: 0.95rem;
}

.step-content {
  padding: 1rem 0;
}

.mb-3 {
  margin-bottom: 1.25rem;
}

.uploaded-image {
  max-width: 200px;
  max-height: 200px;
  border-radius: 8px;
  border: 2px solid #10b981;
  object-fit: cover;
  margin: 0 auto;
  display: block;
}

/* Right Side - Image */
.auth-image-side {
  flex: 1;
  background-image: url('https://img.freepik.com/free-photo/modern-business-building-with-glass-wall-from-empty-floor_1127-3091.jpg?semt=ais_hybrid&w=740&q=80');
  background-size: cover;
  background-position: center;
  clip-path: polygon(10% 0, 100% 0, 100% 100%, 0% 100%);
}

/* Responsive Design */
@media (max-width: 968px) {
  .auth-container {
    flex-direction: column;
  }

  .auth-image-side {
    min-height: 300px;
    order: -1;
    clip-path: polygon(0 0, 100% 0, 100% 85%, 0 100%);
  }

  .green-fragment {
    display: none;
  }
}

@media (max-width: 768px) {
  .auth-image-side {
    display: none;
  }
}

@media (max-width: 640px) {
  .auth-form-side {
    padding: 1rem;
  }

  .form-wrapper {
    padding: 1.5rem;
  }

  .form-title {
    font-size: 1.5rem;
  }
}
</style>
