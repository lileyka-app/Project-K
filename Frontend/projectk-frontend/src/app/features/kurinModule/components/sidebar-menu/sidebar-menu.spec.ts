import type { MockedObject } from 'vitest';
import { MessageService } from '@openng/optimus-ui/api';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SidebarMenuComponent } from './sidebar-menu';
import { provideHttpClient } from '@angular/common/http';
import { Event, Router } from '@angular/router';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { SimpleChange } from '@angular/core';
import { MenuItem } from '@openng/optimus-ui/api';

describe('SidebarMenuComponent', () => {
  let component: SidebarMenuComponent;
  let fixture: ComponentFixture<SidebarMenuComponent>;
  let mockRouter: MockedObject<Router>;
  let mockAuthService: MockedObject<AuthService>;
  let authStateSubject: BehaviorSubject<AuthState | null>;
  let routerEvents: Subject<Event>;

  beforeEach(async () => {
    authStateSubject = new BehaviorSubject<AuthState | null>(null);
    routerEvents = new Subject<Event>();

    // The menu recomputes the current item on NavigationEnd, so the double needs a
    // usable events stream and url alongside navigate().
    mockRouter = {
      navigate: vi.fn(),
      events: routerEvents.asObservable(),
      url: '/kurin'
    } as MockedObject<Router>;
    mockAuthService = {
      getAuthState: vi.fn(),
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    mockAuthService.getAuthState.mockReturnValue(authStateSubject.asObservable());

    await TestBed.configureTestingModule({
      imports: [SidebarMenuComponent],
      providers: [
        provideHttpClient(),
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        MessageService
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(SidebarMenuComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnChanges', () => {
    it('should update items$ when state$ changes', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        expect(items.length).toBeGreaterThan(0);
        expect(items.some(item => item.label === 'Курінь')).toBe(true);
        done();
      });
    }));

    it('should update email$ when state$ changes', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.email$.subscribe(email => {
        expect(email).toBe('test@example.com');
        done();
      });
    }));

    it('should name the office rather than the tier it grants', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.roleTag$.subscribe(roleTag => {
        expect(roleTag.label).toBe("Зв'язковий");
        expect(roleTag.severity).toBe('danger');
        done();
      });
    }));

    it('rebuilds the menu when a refresh changes the permissions', async () => {
      const youth: AuthState = {
        userKey: 'user-1',
        memberKey: 'member-1',
        email: 'youth@example.com',
        isAdmin: false, permissions: [], roles: ['Member'],
        kurinKey: 'kurin-1',
        accessToken: 'token-1'
      };
      authStateSubject.next(youth);
      mockAuthService.getAuthStateValue.mockImplementation(() => authStateSubject.value);
      fixture.componentRef.setInput('state$', authStateSubject.asObservable());
      component.ngOnChanges({ state$: new SimpleChange(null, component.state$(), true) });

      const builds: MenuItem[][] = [];
      const settingsShown = new Promise<void>(resolve => {
        component.items$.subscribe(items => {
          builds.push(items);
          if (builds.length === 1) {
            authStateSubject.next({ ...youth, permissions: ['Kurin:Update:KurinWide'], roles: ['KV.Zvyazkovyi'] });
          } else if (items.some(item => item.label === 'Налаштування куреня')) {
            resolve();
          }
        });
      });

      await settingsShown;
      expect(builds[0].some(item => item.label === 'Налаштування куреня')).toBe(false);
    });

    it('should set email$ to null when state is null', () => new Promise<void>(done => {
      fixture.componentRef.setInput('state$', of(null));
      mockAuthService.getAuthStateValue.mockReturnValue(null);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.email$.subscribe(email => {
        expect(email).toBeNull();
        done();
      });
    }));
  });

  describe('current route highlight', () => {
    const managerState: AuthState = {
      userKey: 'user-1',
      memberKey: 'member-1',
      email: 'manager@projectk.com',
      isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
      kurinKey: 'kurin-1',
      accessToken: 'token'
    };

    function itemsAt(url: string): Promise<MenuItem[]> {
      Object.defineProperty(mockRouter, 'url', { value: url, configurable: true });

      fixture.componentRef.setInput('state$', of(managerState));
      mockAuthService.getAuthStateValue.mockReturnValue(managerState);
      component.ngOnChanges({ state$: new SimpleChange(null, component.state$(), true) });

      return new Promise(resolve => component.items$.subscribe(resolve));
    }

    it('marks the item matching the current url', async () => {
      const items = await itemsAt('/kurin');

      const kurin = items.find(item => item.label === 'Курінь');
      expect(kurin?.styleClass).toBe('lil-menu-item--current');
    });

    it('marks only one item, preferring the longest matching path', async () => {
      const items = await itemsAt('/kurin/kurin-1/settings');

      const marked = items.filter(item => item.styleClass === 'lil-menu-item--current');
      expect(marked).toHaveLength(1);
      expect(marked[0].label).toBe('Налаштування куреня');
    });

    it('ignores query strings when matching', async () => {
      const items = await itemsAt('/planning/kurin-1?tab=all');

      const planning = items.find(item => item.label === 'Планування');
      expect(planning?.styleClass).toBe('lil-menu-item--current');
    });

    it('marks nothing when the url belongs to no menu item', async () => {
      const items = await itemsAt('/some/unrelated/page');

      expect(items.every(item => item.styleClass === undefined)).toBe(true);
    });
  });

  describe('buildItems', () => {
    it('should build menu items with kurinKey (Manager view)', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const kurinItem = items.find(item => item.label === 'Курінь');
        const skillsReviewItem = items.find(item => item.label === 'Модерація вмілостей');
        const panelItem = items.find(item => item.label === 'Адміністрація');
        const usersItem = items.find(item => item.label === 'Користувачі');

        expect(kurinItem).toBeDefined();
        expect(kurinItem?.disabled).toBe(false);
        expect(skillsReviewItem).toBeDefined();
        
        expect(panelItem).toBeUndefined();
        expect(usersItem).toBeUndefined();
        done();
      });
    }));

    it('should hide skills moderation item for non-reviewer role', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-999',
        memberKey: 'test-member-key',
        email: 'user@example.com',
        isAdmin: false, permissions: [], roles: ['Member'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const skillsReviewItem = items.find(item => item.label === 'Модерація вмілостей');
        expect(skillsReviewItem).toBeUndefined();
        done();
      });
    }));

    it('should show kurin settings item for manager role', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'manager@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const settingsItem = items.find(item => item.label === 'Налаштування куреня');
        expect(settingsItem).toBeDefined();
        expect(settingsItem?.routerLink).toEqual(['/kurin', 'kurin-456', 'settings']);
        done();
      });
    }));

    it('should show kurin settings item for admin role with selected kurin', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'admin@example.com',
        isAdmin: true, permissions: [], roles: ['Admin'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const settingsItem = items.find(item => item.label === 'РќР°Р»Р°С€С‚СѓРІР°РЅРЅСЏ РєСѓСЂРµРЅСЏ');
        void settingsItem;
        const settingsLinkItem = items.find(item => JSON.stringify(item.routerLink) === JSON.stringify(['/kurin', 'kurin-456', 'settings']));
        expect(settingsLinkItem).toBeDefined();
        expect(settingsLinkItem?.routerLink).toEqual(['/kurin', 'kurin-456', 'settings']);
        done();
      });
    }));

    it('should hide kurin settings item for non-manager role', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-999',
        memberKey: 'test-member-key',
        email: 'mentor@example.com',
        isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const settingsItem = items.find(item => item.label === 'Налаштування куреня');
        expect(settingsItem).toBeUndefined();
        done();
      });
    }));

    it('should build menu items without kurinKey (Admin view)', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'admin@example.com',
        isAdmin: true, permissions: [], roles: ['Admin'],
        kurinKey: null,
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const kurinItem = items.find(item => item.label === 'Курінь');
        const panelItem = items.find(item => item.label === 'Адміністрація');
        const usersItem = items.find(item => item.label === 'Користувачі');
        const globalSettingsItem = items.find(item => item.label === 'Системні налаштування');

        expect(kurinItem).toBeUndefined();
        expect(panelItem).toBeDefined();
        expect(usersItem).toBeDefined();
        expect(globalSettingsItem).toBeDefined();
        expect(globalSettingsItem?.disabled).toBeFalsy();
        expect(globalSettingsItem?.routerLink).toEqual(['/system-settings']);
        done();
      });
    }));

    it('should disable kurin-related items when kurinKey is null and NOT show them if they rely on kurinKey', () => new Promise<void>(done => {
      fixture.componentRef.setInput('state$', of(null));
      mockAuthService.getAuthStateValue.mockReturnValue(null);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        // Without kurinKey, they shouldn't even be pushed, except maybe if admin
        const kurinItem = items.find(item => item.label === 'Курінь');
        expect(kurinItem).toBeUndefined();
        done();
      });
    }));

    it('should navigate to /kurin when Kurin menu item is clicked', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const kurinItem = items.find(item => item.label === 'Курінь');
        
        if (kurinItem?.command) {
          kurinItem.command({});
          expect(mockRouter.navigate).toHaveBeenCalledWith(['/kurin']);
        }
        done();
      });
    }));

    it('should navigate to /panel when Panel menu item is clicked', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'admin@example.com',
        isAdmin: true, permissions: [], roles: ['Admin'],
        kurinKey: null,
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const panelItem = items.find(item => item.label === 'Адміністрація');
        
        if (panelItem?.command) {
          panelItem.command({});
          expect(mockRouter.navigate).toHaveBeenCalledWith(['/panel']);
        }
        done();
      });
    }));

    it('should navigate to /users when Users menu item is clicked', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'admin@example.com',
        isAdmin: true, permissions: [], roles: ['Admin'],
        kurinKey: null,
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const usersItem = items.find(item => item.label === 'Користувачі');
        
        if (usersItem?.command) {
          usersItem.command({});
          expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
        }
        done();
      });
    }));

    it('should navigate to skills review route when moderation item is clicked', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'mentor@example.com',
        isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const skillsReviewItem = items.find(item => item.label === 'Модерація вмілостей');

        if (skillsReviewItem?.command) {
          skillsReviewItem.command({});
          expect(mockRouter.navigate).toHaveBeenCalledWith(['/kurin', 'kurin-456', 'review', 'skills']);
        }
        done();
      });
    }));
  });

  describe('close', () => {
    it('should set visible to false', () => {
      fixture.componentRef.setInput('visible', true);
      component.close();
      expect(component.visible()).toBe(false);
    });

    it('should emit visibleChange event', () => {
      let emittedClose: boolean | undefined;
      component.visible.subscribe(v => emittedClose = v);
      fixture.componentRef.setInput('visible', true);
      component.close();
      expect(emittedClose).toBe(false);
    });

    it('should close sidebar when menu item is clicked', () => new Promise<void>(done => {
      const mockState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      vi.spyOn(component, 'close').mockReturnValue(undefined);
      fixture.componentRef.setInput('state$', of(mockState));
      mockAuthService.getAuthStateValue.mockReturnValue(mockState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        const kurinItem = items.find(item => item.label === 'Курінь');
        
        if (kurinItem?.command) {
          kurinItem.command({});
          expect(component.close).toHaveBeenCalled();
        }
        done();
      });
    }));
  });

  describe('the footer tag', () => {
    // The office decides the label and the colour. The access tiers below it are only reached by
    // accounts that hold no office, so every case here seeds `roles` deliberately.
    function tagFor(state: Partial<AuthState>): Promise<{ label: string; severity: string }> {
      const full = {
        userKey: 'u', memberKey: 'm', email: 'e', isAdmin: false, permissions: [], roles: [],
        kurinKey: 'k', accessToken: 't', ...state
      } as AuthState;
      mockAuthService.getAuthStateValue.mockReturnValue(full);
      fixture.componentRef.setInput('state$', of(full));
      component.ngOnChanges({ state$: new SimpleChange(null, component.state$(), true) });

      return new Promise(resolve => component.roleTag$.subscribe(resolve));
    }

    it('names the office, not the tier it grants', async () => {
      expect(await tagFor({ roles: ['KV.Zvyazkovyi'], permissions: ['Group:Manage:KurinWide'] }))
        .toEqual({ label: "Зв'язковий", severity: 'danger' });
    });

    it('picks the most senior office when an account holds several', async () => {
      expect((await tagFor({ roles: ['Group.Pysar', 'KV.Zvyazkovyi'] })).label).toBe("Зв'язковий");
    });

    it('calls an admin an admin', async () => {
      expect(await tagFor({ isAdmin: true })).toEqual({ label: 'Адміністратор', severity: 'danger' });
    });

    it('falls back to the tier for a whole-kurin manager holding no office', async () => {
      expect(await tagFor({ permissions: ['Group:Manage:KurinWide'] }))
        .toEqual({ label: 'Провід куреня', severity: 'warn' });
    });

    it('falls back to the tier for a group leader holding no office', async () => {
      expect(await tagFor({ permissions: ['Group:Update:OwnGroups'] }))
        .toEqual({ label: 'Гуртковий провід', severity: 'success' });
    });

    it('calls an account with neither office nor permissions a member', async () => {
      expect(await tagFor({ permissions: [] })).toEqual({ label: 'Учасник', severity: 'info' });
    });
  });

  describe('Input/Output bindings', () => {
    it('should have visible input property', () => {
      fixture.componentRef.setInput('visible', true);
      expect(component.visible()).toBe(true);
      
      fixture.componentRef.setInput('visible', false);
      expect(component.visible()).toBe(false);
    });

    it('should have state$ input property', () => {
      const mockState$ = of(null);
      fixture.componentRef.setInput('state$', mockState$);
      expect(component.state$()).toBe(mockState$);
    });

    it('should emit visibleChange when changed', () => {
      let emittedValue: boolean | undefined;
      component.visible.subscribe(value => {
        emittedValue = value;
      });

      fixture.componentRef.setInput('visible', true);
      component.close();
      expect(emittedValue).toBe(false);
    });
  });

  describe('Integration scenarios', () => {
    it('should update menu items when switching from Manager to Admin', () => new Promise<void>(done => {
      const managerState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'manager@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      fixture.componentRef.setInput('state$', of(managerState));
      mockAuthService.getAuthStateValue.mockReturnValue(managerState);
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      component.items$.subscribe(items => {
        expect(items.find(item => item.label === 'Курінь')).toBeDefined();
        expect(items.find(item => item.label === 'Адміністрація')).toBeUndefined();

        const adminState: AuthState = {
          ...managerState,
          isAdmin: true, permissions: [], roles: ['Admin'],
          kurinKey: null
        };

        fixture.componentRef.setInput('state$', of(adminState));
      mockAuthService.getAuthStateValue.mockReturnValue(adminState);
        component.ngOnChanges({
          state$: new SimpleChange(of(managerState), component.state$(), false)
        });

        component.items$.subscribe(newItems => {
          expect(newItems.find(item => item.label === 'Курінь')).toBeUndefined();
          expect(newItems.find(item => item.label === 'Адміністрація')).toBeDefined();
          done();
        });
      });
    }));

    it('should show correct menu items based on auth state changes', () => new Promise<void>(done => {
      fixture.componentRef.setInput('state$', authStateSubject.asObservable());
      component.ngOnChanges({
        state$: new SimpleChange(null, component.state$(), true)
      });

      const subscription = component.items$.subscribe(items => {
        if (authStateSubject.value === null) {
          expect(items.find(item => item.label === 'Адміністрація')).toBeUndefined();
        } else {
          expect(items.find(item => item.label === 'Курінь')).toBeDefined();
        }
      });

      authStateSubject.next({
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      });

      setTimeout(() => {
        subscription.unsubscribe();
        done();
      }, 0);
    }));
  });
});
