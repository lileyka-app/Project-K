import { inject, Injectable } from "@angular/core";
import { ActivatedRouteSnapshot, CanActivate, Router } from "@angular/router";
import { HttpErrorResponse } from "@angular/common/http";
import { catchError, map, Observable, of } from "rxjs";
import { EntityService } from "../services/entity-service/entity.service";
import { isMfaRequiredRefusal } from "../../../shared/functions/failure-detail.function";

@Injectable({
  providedIn: 'root'
})
export class EntityGuard implements CanActivate {
    private readonly router = inject(Router);
    private readonly entityService = inject(EntityService);

    canActivate(route: ActivatedRouteSnapshot): Observable<boolean> {
        const entityType = this.resolveEntityType(route);
        const entityKey = this.resolveEntityKey(route, entityType);
        const action = this.resolveEntityAction(route);

        // EntityGuard is a UX pre-check only; backend remains the source of truth.
        if (!entityType || !entityKey) {
            return of(true);
        }

        return this.entityService.checkEntityAccess(entityType, entityKey, action).pipe(
            map(access => {
                if (access) {
                    return true;
                } else {
                    this.router.navigate(['/forbidden']);
                    return false;
                }
            }),
            catchError((error: unknown) => {
                if (error instanceof HttpErrorResponse && this.isMfaChallenge(error)) {
                    // MFA is resolved by its own dialog; blocking here would hide it.
                    return of(true);
                }

                // status 0 is the browser's "never reached the server" (offline, CORS,
                // timeout). Anything else is an answer from the server, and an answer we
                // cannot read as "allowed" must not open the page.
                if (error instanceof HttpErrorResponse && error.status === 0) {
                    return of(true);
                }

                this.router.navigate(['/forbidden']);
                return of(false);
            })
        );
    }

    private isMfaChallenge(error: HttpErrorResponse): boolean {
        return isMfaRequiredRefusal(error);
    }

    private resolveEntityType(route: ActivatedRouteSnapshot): string | null {
        const routeType = route.data['entityType'];
        if (typeof routeType === 'string' && routeType.length > 0) {
            return routeType;
        }

        const entityTypeParam = route.data['entityTypeParam'];
        if (typeof entityTypeParam === 'string' && entityTypeParam.length > 0) {
            return route.paramMap.get(entityTypeParam);
        }

        return null;
    }

    private resolveEntityKey(route: ActivatedRouteSnapshot, entityType: string | null): string | null {
        const entityKeyParam = route.data['entityKeyParam'];
        if (typeof entityKeyParam === 'string' && entityKeyParam.length > 0) {
            return route.paramMap.get(entityKeyParam);
        }

        if (!entityType) {
            return null;
        }

        return route.paramMap.get(`${entityType}Key`);
    }

    private resolveEntityAction(route: ActivatedRouteSnapshot): string | undefined {
        const action = route.data['entityAction'];
        return typeof action === 'string' && action.length > 0 ? action : undefined;
    }
}
