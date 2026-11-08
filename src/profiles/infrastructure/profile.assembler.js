import { Profile } from '../domain/model/profile.entity.js';
import { IamFacade } from './iam.facade.js';
import { normalizeRole, ROLES } from '../../shared/infrastructure/roles.js';

export class ProfileAssembler {
  static toDomainFromResponse(response) {
    if (!response) {
      return { profileEntity: new Profile({}), configData: {} };
    }

    // Email y rol vienen del contexto IAM a través de la ACL
    const emailFromIam = IamFacade.getCurrentUserEmail();
    const roleFromIam = IamFacade.getCurrentUserRole();

    // El backend guarda la razón social (administración) o el nombre completo (residente) en "name"
    const role = normalizeRole(roleFromIam);
    const isAdministrator = role === ROLES.ADMIN;

    const profileEntity = new Profile({
      id: response.id,
      userId: response.userId || 0,
      email: emailFromIam || '',
      role,
      secondEmail: response.secondEmail || response.alternateEmail || '',
      photoUrl: response.photoUrl && response.photoUrl !== 'undefined'
        ? response.photoUrl
        : (response.cloudinaryReference || undefined),
      phoneNumber: response.phoneNumber || response.phone || '',
      address: response.address || '',
      companyName: response.companyName || (isAdministrator ? response.name || '' : ''),
      legalRepresentative: response.legalRepresentative || '',
      fullName: response.fullName || (!isAdministrator ? response.name || '' : ''),
      dni: response.dni || '',
      notificationPrefs: response.notificationPrefs || null,
    });

    return { profileEntity, configData: {} };
  }

  /** Recurso a enviar al backend (sin email/rol, que pertenecen a IAM). */
  static toResource(profile) {
    const { email, role, displayName, ...rest } = profile;
    // "name" es el campo del backend; la administración además envía companyName
    return { ...rest, name: profile.companyName || profile.fullName || undefined };
  }
}
