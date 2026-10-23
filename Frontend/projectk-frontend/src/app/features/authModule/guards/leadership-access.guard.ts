import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { LeadershipScope, PermissionService } from '../services/permission-service/permission.service';

// The КВ has no провід form: its people are seated from the КВ tile, where a гурток decides
// впорядник or інструктор.
const scopes: ReadonlySet<LeadershipScope> = new Set<LeadershipScope>(['kurin', 'group']);

/** The провід form: the `:type` in the address decides whose body it is, and the gate follows it. */
export const leadershipAccessGuard: CanActivateFn = (route) => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);

  const type = route.paramMap.get('type') as LeadershipScope | null;
  if (type && scopes.has(type) && permissionService.canSetupLeadership(type)) {
    return true;
  }

  router.navigate(['/forbidden']);
  return false;
};
