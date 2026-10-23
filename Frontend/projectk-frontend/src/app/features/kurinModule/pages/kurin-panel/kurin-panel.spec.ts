import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { KurinPanelComponent } from './kurin-panel';
import { GroupService } from '../../services/group-service/group.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { GroupDto } from '../../models/group.dto';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { MemberService } from '../../services/member-service/member.service';
import { UserService } from '../../../adminModule/services/user-service/user.service';

describe('KurinPanelComponent', () => {
  let fixture: ComponentFixture<KurinPanelComponent>;
  let component: KurinPanelComponent;

  let groupService: MockedObject<GroupService>;
  let kurinService: MockedObject<KurinService>;
  let memberService: MockedObject<MemberService>;
  let userService: MockedObject<UserService>;
  let authService: MockedObject<AuthService>;
  let router: MockedObject<Router>;
  let authState$: BehaviorSubject<AuthState | null>;

  const mockAuthState: AuthState = {
    userKey: 'u1',
      memberKey: 'test-member-key',
    email: 'test@example.com',
    isAdmin: true, permissions: [], roles: ['Admin'],
    kurinKey: 'k1',
    accessToken: 'token'
  };

  const mockGroups: GroupDto[] = [
    { groupKey: 'g1', name: 'Alpha', kurinKey: 'k1', kurinNumber: 10 },
    { groupKey: 'g2', name: 'Beta',  kurinKey: 'k1', kurinNumber: 10 }
  ];

  beforeEach(async () => {
    authState$ = new BehaviorSubject<AuthState | null>(mockAuthState);

    groupService = {
      getAllByKurinKey: vi.fn(),
      getMentorAssignments: vi.fn(),
      assignMentor: vi.fn(),
      revokeMentor: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      delete: vi.fn()
    } as MockedObject<GroupService>;
    kurinService = {
      getByKey: vi.fn(),
      downloadReportPdf: vi.fn()
    } as MockedObject<KurinService>;
    memberService = {
      getAll: vi.fn(),
      getKVMembers: vi.fn(),
      getMentorCandidates: vi.fn()
    } as MockedObject<MemberService>;
    userService = {
      changeUserRole: vi.fn()
    } as MockedObject<UserService>;
    authService = {
      getAuthState: vi.fn(),
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    router = {
      navigate: vi.fn()
    } as MockedObject<Router>;

    groupService.getAllByKurinKey.mockReturnValue(of(mockGroups));
    groupService.getMentorAssignments.mockReturnValue(of([]));
    memberService.getKVMembers.mockReturnValue(of([]));
    memberService.getMentorCandidates.mockReturnValue(of([]));
    memberService.getAll.mockReturnValue(of([]));
    kurinService.getByKey.mockReturnValue(of({ kurinKey: 'k1', number: 10 }));
    authService.getAuthState.mockReturnValue(authState$.asObservable());
    authService.getAuthStateValue.mockReturnValue(mockAuthState);

    await TestBed.configureTestingModule({
      imports: [KurinPanelComponent],
      providers: [
        { provide: ActivatedRoute, useValue: {} },
        { provide: GroupService, useValue: groupService },
        { provide: KurinService, useValue: kurinService },
        { provide: MemberService, useValue: memberService },
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KurinPanelComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    authState$.complete();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('initial state before ngOnInit', () => {
    expect(component.kurinKey).toBe('');
    expect(component.groups).toEqual([]);
    expect(component.groupPanelVisible).toBe(false);
    expect(component.groupPanelParameter).toBe('undef');
  });

  it('on init gets kurinKey from authState and calls refreshData', () => {
    fixture.detectChanges(); // triggers ngOnInit
    expect(authService.getAuthState).toHaveBeenCalled();
    expect(component.kurinKey).toBe('k1');
    expect(groupService.getAllByKurinKey).toHaveBeenCalledWith('k1');
    expect(kurinService.getByKey).toHaveBeenCalledWith('k1');
    expect(component.groups).toEqual(mockGroups);
  });

  it('authState emission updates kurinKey', () => {
    fixture.detectChanges();
    groupService.getAllByKurinKey.mockClear();
    const newAuthState: AuthState = {
      ...mockAuthState,
      kurinKey: 'k2'
    };
    authState$.next(newAuthState);
    expect(component.kurinKey).toBe('k2');
  });

  it('does not allow mentor to add kurin-level members', () => {
    const mentorState = {
      ...mockAuthState,
      isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy']
    };
    authService.getAuthStateValue.mockReturnValue(mentorState);
    authState$.next(mentorState);
    fixture.detectChanges();

    expect(component.canManageMembers).toBe(false);
  });

  it('manual refresh uses current kurinKey from component', () => {
    fixture.detectChanges();
    component.kurinKey = 'k2';
    groupService.getAllByKurinKey.mockClear();
    kurinService.getByKey.mockClear();
    component.refreshData();
    expect(groupService.getAllByKurinKey).toHaveBeenCalledWith('k2');
    expect(kurinService.getByKey).toHaveBeenCalledWith('k2');
  });

  describe('prepareItemActions', () => {
    it('sets Update/Delete actions', () => {
      component.prepareItemActions(mockGroups[0]);
      expect(component.actions).toHaveLength(2);
      expect(component.actions[0].label).toBe('Редагувати');
      expect(component.actions[1].label).toBe('Видалити');
    });
  });

  describe('kurinEditMenuItems', () => {
    it('exposes kurin profile and report export actions', () => {
      component.kurinKey = 'k1';
      component.canEditKurinProfile = true;
      component.canManageGroups = true;

      expect(component.kurinEditMenuItems.map(item => item.label)).toEqual([
        'Редагування даних куреня',
        'Експорт звіту куреня'
      ]);
    });

    it('keeps same reference until menu state changes', () => {
      component.kurinKey = 'k1';
      component.canEditKurinProfile = true;
      component.canManageGroups = true;

      const first = component.kurinEditMenuItems;
      const second = component.kurinEditMenuItems;

      expect(second).toBe(first);

      component.reportDownloading = true;

      expect(component.kurinEditMenuItems).not.toBe(first);
    });

    it('exposes kurin settings action for managers', () => {
      component.kurinKey = 'k1';
      component.canManageKurinSettings = true;

      const settingsItem = component.kurinEditMenuItems.find(item => item.label === 'Налаштування куреня');

      expect(settingsItem).toBeTruthy();
      settingsItem?.command?.({ originalEvent: new Event('click'), item: settingsItem });

      expect(router.navigate).toHaveBeenCalledWith(['/kurin', 'k1', 'settings']);
    });
  });

  describe('onGroupActionClick', () => {
    it('create: resets selectedGroup', () => {
      component.onGroupActionClick(null, 'create');
      expect(component.selectedGroup).toBeNull();
      expect(component.groupPanelParameter).toBe('create');
      expect(component.groupPanelVisible).toBe(true);
    });

    it('update: sets selectedGroup', () => {
      component.onGroupActionClick(mockGroups[0], 'update');
      expect(component.selectedGroup).toBe(mockGroups[0]);
      expect(component.groupPanelParameter).toBe('update');
      expect(component.groupPanelVisible).toBe(true);
    });
  });

  describe('onGroupManage', () => {
    beforeEach(() => {
      component.kurinKey = 'k1';
      groupService.create.mockReturnValue(of(mockGroups[0]));
      groupService.update.mockReturnValue(of(mockGroups[0]));
      groupService.delete.mockReturnValue(of(void 0));
    });

    it('create calls service.create with full entity', () => {
      const entity = { groupKey: '', name: 'Gamma', kurinKey: 'k1', kurinNumber: 10 };
      component.onGroupManage({ action: 'create', entity });
      expect(groupService.create).toHaveBeenCalledWith(entity);
      expect(groupService.getAllByKurinKey).toHaveBeenCalled();
    });

    it('update calls service.update with key + entity', () => {
      const entity = { groupKey: 'g1', name: 'Alpha*', kurinKey: 'k1', kurinNumber: 10 };
      component.onGroupManage({ action: 'update', entity });
      expect(groupService.update).toHaveBeenCalledWith('g1', entity);
      expect(groupService.getAllByKurinKey).toHaveBeenCalled();
    });

    it('delete calls service.delete with key', () => {
      const entity = { groupKey: 'g1', name: 'Alpha', kurinKey: 'k1', kurinNumber: 10 };
      component.onGroupManage({ action: 'delete', entity });
      expect(groupService.delete).toHaveBeenCalledWith('g1');
      expect(groupService.getAllByKurinKey).toHaveBeenCalled();
    });
  });

  describe('groupPanelConfig', () => {
    it('groupKey field rules', () => {
      const f = component.groupPanelConfig.fields.find(x => x.name === 'groupKey')!;
      expect(f.hiddenOn).toContain('create');
      expect(f.disabledOn).toContain('update');
    });
    it('kurinKey hidden on update', () => {
      const f = component.groupPanelConfig.fields.find(x => x.name === 'kurinKey')!;
      expect(f.hiddenOn).toContain('update');
    });
    it('name visible on create', () => {
      const f = component.groupPanelConfig.fields.find(x => x.name === 'name')!;
      expect((f.hiddenOn || []).includes('create')).toBe(false);
    });
  });
});
