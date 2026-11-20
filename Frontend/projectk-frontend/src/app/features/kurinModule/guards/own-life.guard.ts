import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../../authModule/services/auth-service/auth.service';

/**
 * «Пластовий життєпис» бачить лише сама людина. Чуже посилання веде на картку людини: там те, що
 * про неї можна бачити іншим.
 */
export const ownLifeGuard: CanActivateFn = route => {
  const memberKey = route.paramMap.get('memberKey');
  const own = inject(AuthService).getAuthStateValue()?.memberKey;
  if (memberKey && own && own.toLowerCase() === memberKey.toLowerCase()) {
    return true;
  }

  const elsewhere = memberKey ? ['/member', memberKey] : ['/panel'];
  return inject(Router).createUrlTree(elsewhere);
};
