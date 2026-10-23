import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { BreadcrumbService, routePathRegExp } from './breadcrumb.service';
import { ActivatedRoute, ActivatedRouteSnapshot, NavigationEnd, ParamMap, Route, Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { MenuItem } from '@openng/optimus-ui/api';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { GroupDto } from '../../models/group.dto';
import { KurinDto } from '../../models/kurin.dto';
import { MemberDto } from '../../models/member.dto';
import { GroupService } from '../group-service/group.service';
import { KurinService } from '../kurin-service/kurin.service';
import { MemberService } from '../member-service/member.service';

describe('BreadcrumbService', () => {
  let service: BreadcrumbService;
  let routerEventsSubject: Subject<NavigationEnd>;
  let mockRouter: MockedObject<Router>;
  let mockActivatedRoute: MockedObject<ActivatedRoute>;
  let permissionService: MockedObject<PermissionService>;
  let authService: MockedObject<AuthService>;
  let kurinService: MockedObject<KurinService>;
  let groupService: MockedObject<GroupService>;
  let memberService: MockedObject<MemberService>;

  const createAuthState = (overrides: Partial<AuthState> = {}): AuthState => ({
    userKey: 'user-1',
    memberKey: null,
    email: 'user@example.com',
    isAdmin: true, permissions: [], roles: ['Admin'],
    kurinKey: null,
    accessToken: 'token',
    ...overrides
  });

  const createActivatedRouteSnapshot = (data: Record<string, unknown>, params: Record<string, string>): ActivatedRouteSnapshot => {
    return {
      data,
      params,
      paramMap: {
        get: vi.fn(),
        has: vi.fn()
      } as unknown as ParamMap,
      queryParamMap: {
        get: vi.fn(),
        has: vi.fn()
      } as unknown as ParamMap,
      queryParams: {},
      url: [],
      outlet: 'primary',
      routeConfig: null,
      root: {} as ActivatedRouteSnapshot,
      parent: null,
      firstChild: null,
      children: [],
      pathFromRoot: [],
      fragment: null,
      title: undefined,
      component: null
    };
  };

  const createMockRouter = (url: string, config: Route[]): MockedObject<Router> => {
    routerEventsSubject = new Subject<NavigationEnd>();
    const router = {
      navigate: vi.fn(),
      events: routerEventsSubject.asObservable(),
      config: config
    } as MockedObject<Router>;
    Object.defineProperty(router, 'url', {
      get: () => url,
      configurable: true
    });
    return router;
  };

  const createMockActivatedRoute = (snapshot: ActivatedRouteSnapshot, firstChild: ActivatedRoute | null = null): MockedObject<ActivatedRoute> => {
    const route = {
      snapshot,
      firstChild,
      children: []
    } as unknown as MockedObject<ActivatedRoute>;
    return route;
  };

  afterEach(() => {
    if (routerEventsSubject) {
      routerEventsSubject.complete();
    }
  });

  beforeEach(() => {
    permissionService = {
      isAdmin: vi.fn(),
      canManageWholeKurin: vi.fn(),
      canLeadGroups: vi.fn()
    } as MockedObject<PermissionService>;
    permissionService.isAdmin.mockReturnValue(true);
    permissionService.isAdmin.mockReturnValue(false);
    authService = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    authService.getAuthStateValue.mockReturnValue(null);
    kurinService = {
      getByKey: vi.fn()
    } as MockedObject<KurinService>;
    kurinService.getByKey.mockReturnValue(of({ number: 12 } as unknown as KurinDto));
    groupService = {
      getByKey: vi.fn()
    } as MockedObject<GroupService>;
    groupService.getByKey.mockReturnValue(of({ name: 'Соколи' } as unknown as GroupDto));
    memberService = {
      getByKey: vi.fn()
    } as MockedObject<MemberService>;
    memberService.getByKey.mockReturnValue(of({ lastName: 'Шевченко', firstName: 'Тарас' } as unknown as MemberDto));
    TestBed.configureTestingModule({
      providers: [
        { provide: PermissionService, useValue: permissionService },
        { provide: AuthService, useValue: authService },
        { provide: KurinService, useValue: kurinService },
        { provide: GroupService, useValue: groupService },
        { provide: MemberService, useValue: memberService }
      ]
    });
  });

  it('should be created', () => {
    mockRouter = createMockRouter('/', []);
    const snapshot = createActivatedRouteSnapshot({}, {});
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    expect(service).toBeTruthy();
  });

  it('should emit empty breadcrumbs for route without breadcrumb data', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/home', [
      { path: 'home' }
    ]);
    const snapshot = createActivatedRouteSnapshot({}, {});
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(0);
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/home', '/home'));
  }));

  it('should create single breadcrumb from route with breadcrumb data', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/dashboard', [
      { path: 'dashboard', data: { breadcrumb: 'Dashboard' } }
    ]);
    const snapshot = createActivatedRouteSnapshot({ breadcrumb: 'Dashboard' }, {});
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(1);
        expect(breadcrumbs[0].label).toBe('Dashboard');
        expect(breadcrumbs[0].routerLink).toBe('/dashboard');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/dashboard', '/dashboard'));
  }));

  it('should create breadcrumb with resolved route parameters', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/users/123', [
      { path: 'users/:userId', data: { breadcrumb: 'User Profile' } }
    ]);
    const snapshot = createActivatedRouteSnapshot({ breadcrumb: 'User Profile' }, { userId: '123' });
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(1);
        expect(breadcrumbs[0].label).toBe('User Profile');
        expect(breadcrumbs[0].routerLink).toBe('/users/123');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/users/123', '/users/123'));
  }));

  it('should create breadcrumb hierarchy with parent route', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/settings/profile', [
      { path: 'settings', data: { breadcrumb: 'Settings' } },
      { path: 'settings/profile', data: { breadcrumb: 'Profile', parent: 'settings' } }
    ]);
    const snapshot = createActivatedRouteSnapshot({ breadcrumb: 'Profile', parent: 'settings' }, {});
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(2);
        expect(breadcrumbs[0].label).toBe('Settings');
        expect(breadcrumbs[0].routerLink).toBe('settings');
        expect(breadcrumbs[1].label).toBe('Profile');
        expect(breadcrumbs[1].routerLink).toBe('/settings/profile');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/settings/profile', '/settings/profile'));
  }));

  it('should resolve parameters in parent path', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/projects/42/tasks/99', [
      { path: 'projects/:projectId', data: { breadcrumb: 'Project Details' } },
      { path: 'projects/:projectId/tasks/:taskId', data: { breadcrumb: 'Task Details', parent: 'projects/:projectId' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Task Details', parent: 'projects/:projectId' },
      { projectId: '42', taskId: '99' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(2);
        expect(breadcrumbs[0].label).toBe('Project Details');
        expect(breadcrumbs[0].routerLink).toBe('projects/42');
        expect(breadcrumbs[1].label).toBe('Task Details');
        expect(breadcrumbs[1].routerLink).toBe('/projects/42/tasks/99');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/projects/42/tasks/99', '/projects/42/tasks/99'));
  }));

  it('should handle multiple route parameters', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/org/10/team/20/member/30', [
      { path: 'org/:orgId/team/:teamId/member/:memberId', data: { breadcrumb: 'Member Details' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Member Details' },
      { orgId: '10', teamId: '20', memberId: '30' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(1);
        expect(breadcrumbs[0].label).toBe('Member Details');
        expect(breadcrumbs[0].routerLink).toBe('/org/10/team/20/member/30');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/org/10/team/20/member/30', '/org/10/team/20/member/30'));
  }));

  it('should create multi-level breadcrumb hierarchy', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/level1/level2/level3', [
      { path: 'level1', data: { breadcrumb: 'Level 1' } },
      { path: 'level1/level2', data: { breadcrumb: 'Level 2', parent: 'level1' } },
      { path: 'level1/level2/level3', data: { breadcrumb: 'Level 3', parent: 'level1/level2' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Level 3', parent: 'level1/level2' },
      {}
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(3);
        expect(breadcrumbs[0].label).toBe('Level 1');
        expect(breadcrumbs[0].routerLink).toBe('level1');
        expect(breadcrumbs[1].label).toBe('Level 2');
        expect(breadcrumbs[1].routerLink).toBe('level1/level2');
        expect(breadcrumbs[2].label).toBe('Level 3');
        expect(breadcrumbs[2].routerLink).toBe('/level1/level2/level3');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/level1/level2/level3', '/level1/level2/level3'));
  }));

  it('should omit an admin-only breadcrumb parent for a manager', () => new Promise<void>(done => {
    permissionService.canManageWholeKurin.mockReturnValue(true);
    mockRouter = createMockRouter('/group/group-42', [
      { path: 'panel', data: { breadcrumb: 'Panel' } },
      { path: 'kurin', data: { breadcrumb: 'Kurin', parent: '/panel', parentRoles: ['Admin'] } },
      { path: 'group/:groupKey', data: { breadcrumb: 'Group', parent: '/kurin' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Group', parent: '/kurin' },
      { groupKey: 'group-42' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs.map(item => item.label)).toEqual(['Kurin', 'Group']);
        expect(breadcrumbs.map(item => item.routerLink)).toEqual(['/kurin', '/group/group-42']);
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/group/group-42', '/group/group-42'));
  }));

  it('should handle child routes in activated route tree', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/parent/child/555', [
      { path: 'parent/child/:childId', data: { breadcrumb: 'Child Page' } }
    ]);
    
    const parentSnapshot = createActivatedRouteSnapshot({}, {});
    const childSnapshot = createActivatedRouteSnapshot({ breadcrumb: 'Child Page' }, { childId: '555' });
    const childRoute = createMockActivatedRoute(childSnapshot);
    mockActivatedRoute = createMockActivatedRoute(parentSnapshot, childRoute);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(1);
        expect(breadcrumbs[0].label).toBe('Child Page');
        expect(breadcrumbs[0].routerLink).toBe('/parent/child/555');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/parent/child/555', '/parent/child/555'));
  }));

  it('should cache and reuse parameters across navigation', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/category/electronics/product/laptop', [
      { path: 'category/:categoryId', data: { breadcrumb: 'Category' } },
      { path: 'category/:categoryId/product/:productId', data: { breadcrumb: 'Product', parent: 'category/:categoryId' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Product', parent: 'category/:categoryId' },
      { categoryId: 'electronics', productId: 'laptop' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(2);
        expect(breadcrumbs[0].label).toBe('Category');
        expect(breadcrumbs[0].routerLink).toBe('category/electronics');
        expect(breadcrumbs[1].label).toBe('Product');
        expect(breadcrumbs[1].routerLink).toBe('/category/electronics/product/laptop');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/category/electronics/product/laptop', '/category/electronics/product/laptop'));
  }));

  it('should resolve parameters dynamically injected via setParam', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/member/789', [
      { path: 'group/:groupKey', data: { breadcrumb: 'Group Details' } },
      { path: 'member/:memberKey', data: { breadcrumb: 'Member Profile', parent: '/group/:groupKey' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Member Profile', parent: '/group/:groupKey' },
      { memberKey: '789' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, '/member/789', '/member/789'));

    // The group link cannot be built yet, so it is left out rather than rendered dead.
    expect(latest.map(item => item.routerLink)).toEqual(['/member/789']);

    // Dynamically set missing parameter
    service.setParam('groupKey', 'group-42');

    expect(latest.map(item => item.label)).toEqual(['Group Details', 'Member Profile']);
    expect(latest.map(item => item.routerLink)).toEqual(['/group/group-42', '/member/789']);
    done();
  }));

  it('should fall back to kurin for a member without groupKey', () => new Promise<void>(done => {
    permissionService.canManageWholeKurin.mockReturnValue(true);
    mockRouter = createMockRouter('/member/mentor-1', [
      { path: 'kurin', data: { breadcrumb: 'Kurin' } },
      { path: 'group/:groupKey', data: { breadcrumb: 'Group' } },
      {
        path: 'member/:memberKey',
        data: {
          breadcrumb: 'Member Card',
          parent: '/group/:groupKey',
          parentFallback: '/kurin'
        }
      }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      {
        breadcrumb: 'Member Card',
        parent: '/group/:groupKey',
        parentFallback: '/kurin'
      },
      { memberKey: 'mentor-1' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs.map(item => item.label)).toEqual(['Kurin', 'Member Card']);
        expect(breadcrumbs.map(item => item.routerLink)).toEqual(['/kurin', '/member/mentor-1']);
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/member/mentor-1', '/member/mentor-1'));
  }));

  it('should not reuse a previous member groupKey for a groupless mentor', () => {
    permissionService.canManageWholeKurin.mockReturnValue(true);
    mockRouter = createMockRouter('/member/mentor-1', [
      { path: 'kurin', data: { breadcrumb: 'Kurin' } },
      { path: 'group/:groupKey', data: { breadcrumb: 'Group' } },
      {
        path: 'member/:memberKey',
        data: {
          breadcrumb: 'Member Card',
          parent: '/group/:groupKey',
          parentFallback: '/kurin'
        }
      }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      {
        breadcrumb: 'Member Card',
        parent: '/group/:groupKey',
        parentFallback: '/kurin'
      },
      { memberKey: 'mentor-1' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    service.setParam('groupKey', 'previous-group');
    routerEventsSubject.next(new NavigationEnd(1, '/member/mentor-1', '/member/mentor-1'));

    expect(latest.map(item => item.routerLink)).toEqual(['/kurin', '/member/mentor-1']);
  });

  it('should point home at the kurin and drop the crumbs above it in kurin scope', () => new Promise<void>(done => {
    authService.getAuthStateValue.mockReturnValue(createAuthState({ kurinKey: 'kurin-1' }));
    mockRouter = createMockRouter('/group/group-42', [
      { path: 'panel', data: { breadcrumb: 'Panel' } },
      { path: 'kurin', data: { breadcrumb: 'Kurin', parent: '/panel', parentRoles: ['Admin'] } },
      { path: 'group/:groupKey', data: { breadcrumb: 'Group', parent: '/kurin' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Group', parent: '/kurin' },
      { groupKey: 'group-42' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let home: MenuItem | null = null;
    service.home$.subscribe(item => home = item);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs.map(item => item.label)).toEqual(['Group']);
        expect(home!.routerLink).toEqual(['/kurin']);
        expect(home!.title).toBe('Курінь');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/group/group-42', '/group/group-42'));
  }));

  it('should point home at the admin panel outside kurin scope', () => new Promise<void>(done => {
    permissionService.isAdmin.mockReturnValue(true);
    authService.getAuthStateValue.mockReturnValue(createAuthState());
    mockRouter = createMockRouter('/users', [
      { path: 'panel', data: { breadcrumb: 'Panel' } },
      { path: 'users', data: { breadcrumb: 'Users', parent: '/panel' } }
    ]);
    const snapshot = createActivatedRouteSnapshot({ breadcrumb: 'Users', parent: '/panel' }, {});
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let home: MenuItem | null = null;
    service.home$.subscribe(item => home = item);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs.map(item => item.label)).toEqual(['Users']);
        expect(home!.routerLink).toEqual(['/panel']);
        expect(home!.title).toBe('Адміністрація');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/users', '/users'));
  }));

  it('should emit an empty trail on the home page itself', () => new Promise<void>(done => {
    authService.getAuthStateValue.mockReturnValue(createAuthState({ kurinKey: 'kurin-1' }));
    mockRouter = createMockRouter('/kurin', [
      { path: 'panel', data: { breadcrumb: 'Panel' } },
      { path: 'kurin', data: { breadcrumb: 'Kurin', parent: '/panel' } }
    ]);
    const snapshot = createActivatedRouteSnapshot({ breadcrumb: 'Kurin', parent: '/panel' }, {});
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(0);
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/kurin', '/kurin'));
  }));

  it('should omit an admin-only parent of the page the user is standing on', () => new Promise<void>(done => {
    permissionService.canManageWholeKurin.mockReturnValue(true);
    authService.getAuthStateValue.mockReturnValue(createAuthState({ memberKey: 'member-7' }));
    mockRouter = createMockRouter('/kurin', [
      { path: 'panel', data: { breadcrumb: 'Panel' } },
      { path: 'kurin', data: { breadcrumb: 'Kurin', parent: '/panel', parentRoles: ['Admin'] } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Kurin', parent: '/panel', parentRoles: ['Admin'] },
      {}
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs.map(item => item.label)).toEqual(['Kurin']);
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/kurin', '/kurin'));
  }));

  it('should label entity crumbs with the same names the page title shows', () => {
    authService.getAuthStateValue.mockReturnValue(createAuthState({ kurinKey: 'kurin-1' }));
    mockRouter = createMockRouter('/member/member-1', [
      { path: 'kurin', data: { breadcrumb: 'Kurin', breadcrumbEntity: 'kurin' } },
      { path: 'group/:groupKey', data: { breadcrumb: 'Гурток', parent: '/kurin', breadcrumbEntity: 'group' } },
      {
        path: 'member/:memberKey',
        data: {
          breadcrumb: 'Картка учасника',
          parent: '/group/:groupKey',
          parentFallback: '/kurin',
          breadcrumbEntity: 'member'
        }
      }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      {
        breadcrumb: 'Картка учасника',
        parent: '/group/:groupKey',
        parentFallback: '/kurin',
        breadcrumbEntity: 'member'
      },
      { memberKey: 'member-1', groupKey: 'group-42' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, '/member/member-1', '/member/member-1'));

    expect(latest.map(item => item.label)).toEqual(['г. Соколи', 'Шевченко Тарас']);
    expect(groupService.getByKey).toHaveBeenCalledWith('group-42');
    expect(memberService.getByKey).toHaveBeenCalledWith('member-1');
  });

  it('should keep the static label when the entity lookup fails', () => {
    memberService.getByKey.mockReturnValue(throwError(() => new Error('404')));
    authService.getAuthStateValue.mockReturnValue(createAuthState({ kurinKey: 'kurin-1' }));
    mockRouter = createMockRouter('/member/member-1', [
      { path: 'member/:memberKey', data: { breadcrumb: 'Картка учасника', breadcrumbEntity: 'member' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Картка учасника', breadcrumbEntity: 'member' },
      { memberKey: 'member-1' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, '/member/member-1', '/member/member-1'));

    expect(latest.map(item => item.label)).toEqual(['Картка учасника']);
  });

  it('should label a kurin crumb from the route parameter', () => {
    permissionService.canManageWholeKurin.mockReturnValue(true);
    authService.getAuthStateValue.mockReturnValue(createAuthState({ memberKey: 'member-7' }));
    mockRouter = createMockRouter('/kurin/kurin-1/settings', [
      { path: 'kurin', data: { breadcrumb: 'Курінь', breadcrumbEntity: 'kurin' } },
      { path: 'kurin/:kurinKey/settings', data: { breadcrumb: 'Налаштування куреня', parent: '/kurin' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Налаштування куреня', parent: '/kurin' },
      { kurinKey: 'kurin-1' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, '/kurin/kurin-1/settings', '/kurin/kurin-1/settings'));

    expect(latest.map(item => item.label)).toEqual(['к. ч. 12', 'Налаштування куреня']);
  });

  it('should label the group crumb once its key arrives through setParam', () => {
    authService.getAuthStateValue.mockReturnValue(createAuthState({ kurinKey: 'kurin-1' }));
    mockRouter = createMockRouter('/member/member-1', [
      { path: 'group/:groupKey', data: { breadcrumb: 'Гурток', breadcrumbEntity: 'group' } },
      { path: 'member/:memberKey', data: { breadcrumb: 'Картка учасника', parent: '/group/:groupKey' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Картка учасника', parent: '/group/:groupKey' },
      { memberKey: 'member-1' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, '/member/member-1', '/member/member-1'));
    expect(latest.map(item => item.label)).toEqual(['Картка учасника']);

    service.setParam('groupKey', 'group-42');

    expect(latest.map(item => item.label)).toEqual(['г. Соколи', 'Картка учасника']);
    expect(latest.map(item => item.routerLink)).toEqual(['/group/group-42', '/member/member-1']);
  });

  it('should ignore an empty guid pushed through setParam', () => {
    permissionService.canLeadGroups.mockReturnValue(true);
    authService.getAuthStateValue.mockReturnValue(createAuthState({ memberKey: 'mentor-1' }));
    mockRouter = createMockRouter('/member/member-1', [
      { path: 'kurin', data: { breadcrumb: 'Kurin' } },
      { path: 'group/:groupKey', data: { breadcrumb: 'Group' } },
      {
        path: 'member/:memberKey',
        data: { breadcrumb: 'Member Card', parent: '/group/:groupKey', parentFallback: '/kurin' }
      }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Member Card', parent: '/group/:groupKey', parentFallback: '/kurin' },
      { memberKey: 'member-1' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, '/member/member-1', '/member/member-1'));
    service.setParam('groupKey', '00000000-0000-0000-0000-000000000000');

    expect(latest.map(item => item.routerLink)).toEqual(['/kurin', '/member/member-1']);
    expect(groupService.getByKey).not.toHaveBeenCalled();
  });

  it('should not build a parent crumb from an empty guid in the url', () => {
    permissionService.canLeadGroups.mockReturnValue(true);
    authService.getAuthStateValue.mockReturnValue(createAuthState({ memberKey: 'mentor-1' }));
    const url = '/group/00000000-0000-0000-0000-000000000000/member/upsert';
    mockRouter = createMockRouter(url, [
      { path: 'group/:groupKey', data: { breadcrumb: 'Group' } },
      { path: 'group/:groupKey/member/upsert', data: { breadcrumb: 'New Member', parent: '/group/:groupKey' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'New Member', parent: '/group/:groupKey' },
      { groupKey: '00000000-0000-0000-0000-000000000000' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);
    let latest: MenuItem[] = [];
    service.breadcrumbs$.subscribe(breadcrumbs => latest = breadcrumbs);

    routerEventsSubject.next(new NavigationEnd(1, url, url));

    expect(latest.map(item => item.label)).toEqual(['New Member']);
  });

  it('should handle complex nested route structure with multiple children', () => new Promise<void>(done => {
    mockRouter = createMockRouter('/admin/users/123/edit', [
      { path: 'admin', data: { breadcrumb: 'Admin' } },
      { path: 'admin/users/:userId', data: { breadcrumb: 'User Details', parent: 'admin' } },
      { path: 'admin/users/:userId/edit', data: { breadcrumb: 'Edit User', parent: 'admin/users/:userId' } }
    ]);
    const snapshot = createActivatedRouteSnapshot(
      { breadcrumb: 'Edit User', parent: 'admin/users/:userId' },
      { userId: '123' }
    );
    mockActivatedRoute = createMockActivatedRoute(snapshot);

    TestBed.configureTestingModule({
      providers: [
        BreadcrumbService,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    service = TestBed.inject(BreadcrumbService);

    let emissionCount = 0;
    service.breadcrumbs$.subscribe((breadcrumbs: MenuItem[]) => {
      emissionCount++;
      if (emissionCount === 2) {
        expect(breadcrumbs).toHaveLength(3);
        expect(breadcrumbs[0].label).toBe('Admin');
        expect(breadcrumbs[1].label).toBe('User Details');
        expect(breadcrumbs[1].routerLink).toBe('admin/users/123');
        expect(breadcrumbs[2].label).toBe('Edit User');
        done();
      }
    });

    routerEventsSubject.next(new NavigationEnd(1, '/admin/users/123/edit', '/admin/users/123/edit'));
  }));
});

describe('routePathRegExp', () => {
  it('lets a :param stand for one segment and nothing more', () => {
    expect(routePathRegExp('member/:memberKey').test('member/42')).toBe(true);
    expect(routePathRegExp('member/:memberKey').test('member/42/edit')).toBe(false);
  });

  it('matches every other character only as itself', () => {
    expect(routePathRegExp('docs/v1.0').test('docs/v1.0')).toBe(true);
    expect(routePathRegExp('docs/v1.0').test('docs/v1x0')).toBe(false);
    expect(routePathRegExp('a+b').test('aab')).toBe(false);
  });
});
