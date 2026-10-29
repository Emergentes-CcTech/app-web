import { Profile } from '../domain/model/profile.entity.js';
import { IamFacade } from './iam.facade.js';
import { normalizeRole } from '../../shared/infrastructure/roles.js';

export class ProfileAssembler {
  static toDomainFromResponse(response) {
    if (!response) {
      return { profileEntity: new Profile({}), configData: {} };
    }

    // Email y rol vienen del contexto IAM a través de la ACL
    const emailFromIam = IamFacade.getCurrentUserEmail();
    const roleFromIam = IamFacade.getCurrentUserRole();

    const profileEntity = new Profile({
      id: response.id,
      userId: response.userId || 0,
      email: emailFromIam || '',
      role: normalizeRole(roleFromIam),
      secondEmail: response.secondEmail || response.alternateEmail || '',
      photoUrl: response.photoUrl && response.photoUrl !== 'undefined'
        ? response.photoUrl
        : (response.cloudinaryReference || undefined),
      phoneNumber: response.phoneNumber || response.phone || '',
      address: response.address || '',
      companyName: response.companyName || '',
      ruc: response.ruc || '',
      legalRepresentative: response.legalRepresentative || '',
      fullName: response.fullName || '',
      dni: response.dni || '',
      notificationPrefs: response.notificationPrefs || null,
    });

    return { profileEntity, configData: {} };
  }

  /** Recurso a enviar al backend (sin email/rol, que pertenecen a IAM). */
  static toResource(profile) {
    const { email, role, displayName, ...rest } = profile;
    return rest;
  }
}
