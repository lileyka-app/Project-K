import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { AuthService } from "../services/auth-service/auth.service";
import { PermissionService } from "../services/permission-service/permission.service";
import { map, take } from "rxjs";

/**
 * What a route needs the caller to be able to do. Backend permissions are the real gate; this only
 * steers navigation so a member is not shown a page they would be refused anyway.
 *
 * It used to take free-form strings ('Admin', 'Manager', 'Mentor') compared in lower case, so a typo
 * silently disabled the check and the names still came from the job titles the office model replaced.
 */
export type RouteCapability = 'admin' | 'kurinManagement' | 'groupLeadership' | 'journal';

export const capabilityGuard = (...capabilities: RouteCapability[]): CanActivateFn => {
    return () => {
        const authService = inject(AuthService);
        const permissionService = inject(PermissionService);
        const router = inject(Router);

        return authService.getAuthState().pipe(
            take(1),
            map(() => {
                const allowed =
                    (capabilities.includes('admin') && permissionService.isAdmin()) ||
                    (capabilities.includes('kurinManagement') && permissionService.canManageWholeKurin()) ||
                    (capabilities.includes('groupLeadership') && permissionService.canLeadGroups()) ||
                    (capabilities.includes('journal') && permissionService.canReadJournal());

                return allowed ? true : router.createUrlTree(['/forbidden']);
            })
        );
    };
}
