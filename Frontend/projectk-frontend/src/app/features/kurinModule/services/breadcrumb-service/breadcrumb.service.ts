import { inject, Injectable } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Route, Router } from '@angular/router';
import { MenuItem } from '@openng/optimus-ui/api';
import { BehaviorSubject, Observable, catchError, filter, of } from 'rxjs';
import { authenticatedHomeRoute } from '../../../authModule/functions/authenticated-home-route';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { isUsableKey } from '../../../../shared/functions/is-usable-key.function';
import {
  TitleContextType,
  formatGroupTitle,
  formatKurinTitle,
  formatMemberTitle
} from '../../../systemModule/services/page-title.format';
import { GroupService } from '../group-service/group.service';
import { KurinService } from '../kurin-service/kurin.service';
import { MemberService } from '../member-service/member.service';

interface EntityTarget {
  type: TitleContextType;
  key: string;
}

/**
 * A route path as a whole-string matcher where each `:param` stands for one segment. Everything else
 * is escaped, so a `.` or `*` in a path matches only itself.
 */
export function routePathRegExp(path: string): RegExp {
  const escaped = path.replaceAll(/[.*+?^${}()|[\]\\/]/g, String.raw`\$&`);
  return new RegExp(`^${escaped.replaceAll(/:[a-zA-Z0-9]+/g, '[^/]+')}$`);
}

@Injectable({
  providedIn: 'root'
})
export class BreadcrumbService {
  private readonly breadcrumbsSubject = new BehaviorSubject<MenuItem[]>([]);
  public breadcrumbs$: Observable<MenuItem[]> = this.breadcrumbsSubject.asObservable();
  private readonly homeSubject = new BehaviorSubject<MenuItem>({ icon: 'pi pi-home' });
  public home$: Observable<MenuItem> = this.homeSubject.asObservable();
  // A Map, not an object keyed by route-parameter names: the names come from route definitions,
  // but a plain object makes a key such as "__proto__" a write to the prototype.
  private readonly paramCache = new Map<string, string>();
  private readonly entityTargets = new Map<string, EntityTarget>();
  private readonly entityLabels = new Map<string, string>();
  private readonly requestedEntities = new Set<string>();
  private navigationToken = 0;
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly permissionService = inject(PermissionService);
  private readonly authService = inject(AuthService);
  private readonly kurinService = inject(KurinService);
  private readonly groupService = inject(GroupService);
  private readonly memberService = inject(MemberService);

  constructor() {
    if (this.router?.events) {
      this.router.events.pipe(
        filter(event => event instanceof NavigationEnd)
      ).subscribe(() => this.refresh());
    }

    // The service is built on first injection, which is whenever the component holding
    // the breadcrumb first renders. If that is after the initial navigation — the
    // toolbar only appears once auth resolves — the NavigationEnd above never fires for
    // the page the user actually landed on, and the trail would stay empty until they
    // navigated somewhere else.
    this.refresh();
  }

  private refresh(): void {
    this.updateParamCache();
    // Names are dropped on every navigation so a rename never survives as a stale crumb;
    // ClientCacheService keeps the refetch off the network for the next minute anyway.
    this.entityLabels.clear();
    this.requestedEntities.clear();
    this.navigationToken++;
    this.homeSubject.next(this.createHome());
    this.breadcrumbsSubject.next(this.createBreadcrumbs());
    this.requestEntityLabels();
  }

  private createHome(): MenuItem {
    const state = this.authService.getAuthStateValue?.() ?? null;
    const label = this.homeLabel(state);

    return {
      icon: 'pi pi-home',
      routerLink: authenticatedHomeRoute(state),
      title: label,
      tooltipOptions: { tooltipLabel: label, tooltipPosition: 'bottom' }
    };
  }

  // Mirrors authenticatedHomeRoute: the icon has to name the place it actually leads to.
  private homeLabel(state: AuthState | null): string {
    if (isUsableKey(state?.memberKey)) {
      return 'Головна';
    }

    if (isUsableKey(state?.kurinKey)) {
      return 'Курінь';
    }

    return this.permissionService.isAdmin() ? 'Адміністрація' : 'На початок';
  }

  public setParam(key: string, value: string): void {
    if (!isUsableKey(value) || this.paramCache.get(key) === value) {
      return;
    }

    this.paramCache.set(key, value);
    const breadcrumbs = this.createBreadcrumbs();
    this.breadcrumbsSubject.next(breadcrumbs);
    this.requestEntityLabels();
  }

  // A mentor with no group carries Guid.Empty as their groupKey. Caching it would build a
  // crumb linking to /group/00000000-…, which every access check rejects — so placeholder
  // keys never enter the cache and the crumb that needs them is simply not built.
  private cacheParams(params: Record<string, string>): void {
    for (const [key, value] of Object.entries(params)) {
      if (isUsableKey(value)) {
        this.paramCache.set(key, value);
      }
    }
  }

  private updateParamCache(): void {
    // Route parameters are navigation-scoped. Dynamic values such as a member's
    // groupKey may be injected again after the destination entity is loaded.
    this.paramCache.clear();

    const urlSegments = (this.router.url ?? '').split('/').filter(Boolean);
    const routes = this.router.config ?? [];

    for (const route of routes) {
      if (!route.path) continue;

      const extractedParams = this.matchRouteParams(route.path.split('/').filter(Boolean), urlSegments);
      if (extractedParams) {
        this.cacheParams(extractedParams);
      }
    }

    this.addParamsFromRoute(this.activatedRoute);
  }

  private matchRouteParams(routeSegments: string[], urlSegments: string[]): Record<string, string> | null {
    if (routeSegments.length > urlSegments.length) {
      return null;
    }

    const extractedParams: Record<string, string> = {};
    for (let i = 0; i < routeSegments.length; i++) {
      if (routeSegments[i].startsWith(':')) {
        extractedParams[routeSegments[i].substring(1)] = urlSegments[i];
      } else if (routeSegments[i] !== urlSegments[i]) {
        return null;
      }
    }
    return extractedParams;
  }
  
  private addParamsFromRoute(route: ActivatedRoute): void {
    if (!route?.snapshot) {
      return;
    }

    this.cacheParams(route.snapshot.params);

    if (route.firstChild) {
      this.addParamsFromRoute(route.firstChild);
    }

    (route.children ?? []).forEach(child => {
      this.addParamsFromRoute(child);
    });
  }

  private createBreadcrumbs(): MenuItem[] {
    const breadcrumbs: MenuItem[] = [];
    this.entityTargets.clear();

    let currentRoute: ActivatedRoute = this.activatedRoute;
    if (!currentRoute) {
      return breadcrumbs;
    }
    while (currentRoute.firstChild) {
      currentRoute = currentRoute.firstChild;
    }

    this.processRoute(currentRoute, breadcrumbs);

    return this.applyEntityLabels(this.dropCrumbsUpToHome(breadcrumbs));
  }

  // A crumb that stands for an entity carries the entity's own name — the same one the
  // page title shows — and falls back to the static route label until it is loaded.
  private applyEntityLabels(breadcrumbs: MenuItem[]): MenuItem[] {
    return breadcrumbs.map(item => {
      const target = this.entityTargets.get(this.normalizePath(item.routerLink));
      const label = target ? this.entityLabels.get(this.entityCacheKey(target)) : undefined;
      return label ? { ...item, label } : item;
    });
  }

  private trackEntity(path: string, data: Record<string, unknown> | undefined): void {
    const type = data?.['breadcrumbEntity'];
    if (type !== 'kurin' && type !== 'group' && type !== 'member') {
      return;
    }

    const key = this.entityKey(type);
    if (key) {
      this.entityTargets.set(this.normalizePath(path), { type, key });
    }
  }

  private entityKey(type: TitleContextType): string | null {
    switch (type) {
      case 'group':
        return this.paramCache.get('groupKey') ?? null;
      case 'member':
        return this.paramCache.get('memberKey') ?? null;
      case 'kurin': {
        const scopedKurinKey = this.authService.getAuthStateValue?.()?.kurinKey;
        return this.paramCache.get('kurinKey') ?? (isUsableKey(scopedKurinKey) ? scopedKurinKey ?? null : null);
      }
    }
  }

  private entityCacheKey(target: EntityTarget): string {
    return `${target.type}:${target.key}`;
  }

  private requestEntityLabels(): void {
    const token = this.navigationToken;

    for (const target of this.entityTargets.values()) {
      const cacheKey = this.entityCacheKey(target);
      if (this.requestedEntities.has(cacheKey)) {
        continue;
      }
      this.requestedEntities.add(cacheKey);

      switch (target.type) {
        case 'kurin':
          this.publishLabel(token, cacheKey, this.kurinService.getByKey(target.key), kurin => formatKurinTitle(kurin.number));
          break;
        case 'group':
          this.publishLabel(token, cacheKey, this.groupService.getByKey(target.key), group => formatGroupTitle(group.name));
          break;
        case 'member':
          this.publishLabel(token, cacheKey, this.memberService.getByKey(target.key), member =>
            formatMemberTitle(member.lastName, member.firstName)
          );
          break;
      }
    }
  }

  private publishLabel<T>(
    token: number,
    cacheKey: string,
    source: Observable<T>,
    format: (entity: T) => string | null
  ): void {
    source
      .pipe(catchError(() => of(null)))
      .subscribe(entity => {
        // A response from the page the user already left must not relabel the new trail.
        if (entity === null || token !== this.navigationToken) {
          return;
        }

        const label = format(entity);
        if (!label || this.entityLabels.get(cacheKey) === label) {
          return;
        }

        this.entityLabels.set(cacheKey, label);
        this.breadcrumbsSubject.next(this.createBreadcrumbs());
      });
  }

  // The house icon already leads to the user's root, so anything at or above it is dead
  // weight: a second link to the same page, and — for a kurin-scoped user — an
  // "Адміністрація" crumb that kurinAccessGuard bounces straight back to the kurin.
  private dropCrumbsUpToHome(breadcrumbs: MenuItem[]): MenuItem[] {
    const state = this.authService.getAuthStateValue?.() ?? null;
    const homePath = this.normalizePath(authenticatedHomeRoute(state));

    for (let i = breadcrumbs.length - 1; i >= 0; i--) {
      if (this.normalizePath(breadcrumbs[i].routerLink) === homePath) {
        return breadcrumbs.slice(i + 1);
      }
    }

    return breadcrumbs;
  }

  private toPath(segments: unknown[]): string {
    return segments.map(String).join('/');
  }

  private normalizePath(link: string | unknown[] | null | undefined): string {
    const raw = Array.isArray(link) ? this.toPath(link) : String(link ?? '');
    return raw.split(/[?#]/)[0].split('/').filter(Boolean).join('/');
  }

  private processRoute(route: ActivatedRoute, breadcrumbs: MenuItem[]): void {
    if (!route?.snapshot?.data) return;

    if (route.snapshot.data['breadcrumb']) {
      const currentUrl = this.router.url ?? '';
      const currentItem: MenuItem = {
        label: route.snapshot.data['breadcrumb'],
        routerLink: currentUrl
      };
      
      breadcrumbs.unshift(currentItem);
      this.trackEntity(currentUrl, route.snapshot.data);

      const parentPath = this.resolveParentPath(
        route.snapshot.data['parent'],
        route.snapshot.data['parentFallback']
      );
      if (parentPath && this.isParentAllowed(route.snapshot.data['parentRoles'])) {
        this.processParent(parentPath, breadcrumbs);
      }
    }
  }

  private resolveParentPath(parent: unknown, fallback: unknown): string | null {
    if (typeof parent !== 'string' || parent.length === 0) {
      return null;
    }

    const resolvedParent = this.resolveParameters(parent);
    if (!resolvedParent.includes(':')) {
      return parent;
    }

    return typeof fallback === 'string' && fallback.length > 0
      ? fallback
      : parent;
  }
  
  private processParent(parentPath: string, breadcrumbs: MenuItem[]): void {
    if (!parentPath) return;

    const resolvedPath = this.resolveParameters(parentPath);

    const route = this.findRouteByPattern(parentPath);
    if (route?.data?.['breadcrumb']) {
      // A path we could not fill in is a dead link — skip the crumb, but keep walking up
      // so the trail still reaches somewhere the user can actually go.
      if (!resolvedPath.includes(':')) {
        const parentItem: MenuItem = {
          label: route.data['breadcrumb'],
          routerLink: resolvedPath
        };

        breadcrumbs.unshift(parentItem);
        this.trackEntity(resolvedPath, route.data);
      }

      if (route.data['parent'] && this.isParentAllowed(route.data['parentRoles'])) {
        this.processParent(route.data['parent'], breadcrumbs);
      }
    }
  }

  private isParentAllowed(parentRoles: unknown): boolean {
    if (!Array.isArray(parentRoles) || parentRoles.length === 0) {
      return true;
    }

    return parentRoles.some(role => {
      if (typeof role !== 'string') {
        return false;
      }
      switch (role.trim().toLowerCase()) {
        case 'admin': return this.permissionService.isAdmin();
        case 'manager': return this.permissionService.canManageWholeKurin();
        case 'mentor': return this.permissionService.canLeadGroups();
        default: return false;
      }
    });
  }
  
  private resolveParameters(path: string): string {
    if (!path) return '';
    
    let result = path;
    const paramMatches = path.match(/:[a-zA-Z0-9]+/g) || [];
    
    for (const param of paramMatches) {
      const paramName = param.substring(1);
      const cached = this.paramCache.get(paramName);
      if (cached) {
        result = result.replace(param, cached);
      }
    }
    
    return result;
  }
  
  private findRouteByPattern(pathPattern: string): Route | null {
    if (!pathPattern) return null;

    const normalizedPattern = pathPattern.startsWith('/') ? pathPattern.substring(1) : pathPattern;
    
    const patternRegExp = routePathRegExp(normalizedPattern);

    for (const route of this.router.config ?? []) {
      if (!route.path) continue;

      if (route.path === normalizedPattern) {
        return route;
      }

      if (patternRegExp.test(route.path) || routePathRegExp(route.path).test(normalizedPattern)) {
        return route;
      }
    }
    
    return null;
  }
}
