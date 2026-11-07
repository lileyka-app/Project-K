import { Injectable, inject } from '@angular/core';
import { CanActivateFn, RedirectFunction, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { KurinFeaturesService } from '../../services/kurin-features-service/kurin-features.service';
import { hasYouthProgram } from '../../models/enums/kurin-branch.enum';

/** What the kurin's «Реєстр» does with its people: one page, one mode at a time. */
export type RegistryMode = 'roster' | 'transfers' | 'archive' | 'import';

export interface RegistryModeLink {
  readonly mode: RegistryMode;
  readonly label: string;
  /** The address of the mode, so a mode can be linked to and survives a reload. */
  readonly link: string;
}

export const REGISTRY_MODES: readonly RegistryModeLink[] = [
  { mode: 'roster', label: 'Склад', link: '/kurin/registry' },
  { mode: 'transfers', label: 'Переходи', link: '/kurin/registry/transfers' },
  { mode: 'archive', label: 'Архів', link: '/kurin/registry/archive' },
  { mode: 'import', label: 'Імпорт', link: '/kurin/registry/import' }
];

/**
 * Which modes of the «Реєстр» the person has in the kurin they act in. The menu, the route guards
 * and the tabs all ask here, so a mode is never offered where its address would refuse. The rights
 * are those the four pages had before they became one; the server checks them again.
 */
@Injectable({ providedIn: 'root' })
export class RegistryModes {
  private readonly permissions = inject(PermissionService);
  private readonly features = inject(KurinFeaturesService);

  allows(mode: RegistryMode): boolean {
    switch (mode) {
      case 'roster':
        return this.permissions.canManageWholeKurin() || this.permissions.canLeadGroups();
      case 'transfers':
        return this.permissions.canManageWholeKurin();
      case 'archive':
        return this.permissions.canReadArchive();
      case 'import':
        // The import puts people into гуртки, so a senior kurin, which has none, has no import.
        return this.permissions.canManageKurinSettings() && hasYouthProgram(this.features.branch());
    }
  }

  available(): RegistryModeLink[] {
    return REGISTRY_MODES.filter(link => this.allows(link.mode));
  }
}

/** Opens a mode for whoever has it; anyone else goes to a mode they do have, or is refused. */
export const registryModeGuard = (mode: RegistryMode): CanActivateFn => () => {
  const modes = inject(RegistryModes);
  const router = inject(Router);
  if (modes.allows(mode)) {
    return true;
  }

  const other = modes.available()[0];
  return router.parseUrl(other ? other.link : '/forbidden');
};

/**
 * An address from before the «Реєстр» was one page, sent on to its mode with its query: the QR on a
 * printed card carries the person's `?code=`, and a plain `redirectTo` string would drop it.
 */
export const toRegistryMode = (mode: RegistryMode): RedirectFunction => ({ queryParams }) =>
  inject(Router).createUrlTree([REGISTRY_MODES.find(link => link.mode === mode)!.link], { queryParams });

/**
 * `/kurin/registry?former=1` was where a card sent a person just released from the kurin. The
 * former members are in the archive now; the old address still leads to them.
 */
export const formerMembersRedirect: CanActivateFn = route =>
  route.queryParamMap.get('former') === '1'
    ? inject(Router).createUrlTree(['/kurin/registry/archive'], { queryParams: { former: 1 } })
    : true;

/**
 * `/kurin/:kurinKey/archive` named its kurin; the «Реєстр» is always the kurin the person acts in.
 * An old link to another kurin switches to it when the person stands there, and is refused when not.
 */
export const legacyArchiveRedirect: CanActivateFn = route => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const archive = router.parseUrl('/kurin/registry/archive');
  const kurinKey = route.paramMap.get('kurinKey');
  if (!kurinKey || auth.getAuthStateValue()?.kurinKey === kurinKey) {
    return archive;
  }

  return auth.setKurinScope(kurinKey).pipe(
    map(() => archive),
    catchError(() => of(router.parseUrl('/forbidden')))
  );
};
