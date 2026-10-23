import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MemberListComponent } from './member-list';
import { MemberService } from '../../services/member-service/member.service';
import { LeadershipService } from '../../services/leadership-service/leadership.service';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { MemberDto } from '../../models/member.dto';
import { LeadershipDto, LeadershipHistoryDto } from '../../models/requests/leadership/leadership.dto';
import { LeadershipRole } from '../../models/enums/leadership-role.enum';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { AuthState } from '../../../authModule/models/auth-state.model';

describe('MemberListComponent', () => {
  let component: MemberListComponent;
  let fixture: ComponentFixture<MemberListComponent>;
  let memberServiceSpy: MockedObject<MemberService>;
  let leadershipServiceSpy: MockedObject<LeadershipService>;
  let authServiceSpy: MockedObject<AuthService>;
  let routerSpy: MockedObject<Router>;

  const mockMembers: MemberDto[] = [
    { memberKey: 'm1', firstName: 'John', lastName: 'Doe', middleName: 'M' } as MemberDto,
  ];

  const mockLeadership: LeadershipDto = {
    leadershipKey: 'l1',
    type: 'kurin',
    entityKey: 'k1',
    startDate: '2023-01-01',
    endDate: null,
    leadershipHistories: [
      {
        leadershipKey: 'l1',
        leadershipHistoryKey: 'lh1',
        role: LeadershipRole.Kurinnuy,
        startDate: '2023-01-01',
        endDate: null, // Active
        member: { memberKey: 'm1', firstName: 'John', lastName: 'Doe', middleName: 'M' }
      },
      {
        leadershipKey: 'l1',
        leadershipHistoryKey: 'lh2',
        role: LeadershipRole.Pysar,
        startDate: '2022-01-01',
        endDate: '2023-01-01', // Archived
        member: { memberKey: 'm2', firstName: 'Jane', lastName: 'Smith', middleName: 'A' }
      }
    ]
  };

  beforeEach(async () => {
    memberServiceSpy = {
      getAll: vi.fn()
    } as MockedObject<MemberService>;
    leadershipServiceSpy = {
      getLeadershipByTypeAndKey: vi.fn()
    } as MockedObject<LeadershipService>;
    authServiceSpy = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    routerSpy = {
      navigate: vi.fn()
    } as MockedObject<Router>;

    authServiceSpy.getAuthStateValue.mockReturnValue({ isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'] } as AuthState);

    await TestBed.configureTestingModule({
      imports: [MemberListComponent],
      providers: [
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: LeadershipService, useValue: leadershipServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        { provide: Router, useValue: routerSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MemberListComponent);
    component = fixture.componentInstance;
    
    // Default mocks
    memberServiceSpy.getAll.mockReturnValue(of(mockMembers));
    leadershipServiceSpy.getLeadershipByTypeAndKey.mockReturnValue(of(mockLeadership));
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should do nothing if typeKey is missing', () => {
      fixture.componentRef.setInput('typeKey', '');
      component.ngOnInit();
      expect(memberServiceSpy.getAll).not.toHaveBeenCalled();
      expect(leadershipServiceSpy.getLeadershipByTypeAndKey).not.toHaveBeenCalled();
    });

    it('should load members for group type', () => {
      fixture.componentRef.setInput('type', 'group');
      fixture.componentRef.setInput('typeKey', 'g1');
      component.ngOnInit();
      expect(memberServiceSpy.getAll).toHaveBeenCalledWith('g1');
      expect(component.membersLookup).toHaveLength(1);
      expect(component.membersLookup[0].phoneNumber).toBeUndefined();
      expect(component.membersLookup[0].fullNameSort).toBe('doe john m');
      expect(component.membersLookup[0].roleSortWeight).toBe(Number.MAX_SAFE_INTEGER);
    });

    it('should load members for kurin type', () => {
      fixture.componentRef.setInput('type', 'kurin');
      fixture.componentRef.setInput('typeKey', 'k1');
      component.ngOnInit();
      expect(memberServiceSpy.getAll).toHaveBeenCalledWith(undefined, 'k1');
      expect(component.membersLookup).toHaveLength(1);
    });

    it('should give a manager a higher status priority than a kurinnuy', () => {
      memberServiceSpy.getAll.mockReturnValue(of([
        {
          ...mockMembers[0],
          memberKey: 'manager',
          firstName: 'Manager',
          lastName: 'Member',
          userRole: 'KV.Zvyazkovyi',
          leadershipHistories: []
        },
        {
          ...mockMembers[0],
          memberKey: 'kurinnuy',
          firstName: 'Kurinnuy',
          lastName: 'Member',
          leadershipHistories: [{
            leadershipHistoryKey: 'lh-kurinnuy',
            leadershipKey: 'l1',
            role: LeadershipRole.Kurinnuy,
            startDate: '2025-01-01',
            endDate: null,
            member: {
              memberKey: 'kurinnuy',
              firstName: 'Kurinnuy',
              lastName: 'Member',
              middleName: null
            }
          }]
        }
      ]));
      fixture.componentRef.setInput('type', 'kurin');
      fixture.componentRef.setInput('typeKey', 'k1');

      component.ngOnInit();

      const manager = component.membersLookup.find(member => member.memberKey === 'manager');
      const kurinnuy = component.membersLookup.find(member => member.memberKey === 'kurinnuy');
      expect(manager!.roleSortWeight).toBeLessThan(kurinnuy!.roleSortWeight!);
    });

    it('should sort a mentor without an office among the staff, not the youth', () => {
      memberServiceSpy.getAll.mockReturnValue(of([
        { ...mockMembers[0], memberKey: 'mentor', userRole: null, leadershipHistories: [], mentoredGroupNames: ['Кельти'] },
        { ...mockMembers[0], memberKey: 'youth', userRole: null, leadershipHistories: [], mentoredGroupNames: [] }
      ]));
      fixture.componentRef.setInput('type', 'kurin');
      fixture.componentRef.setInput('typeKey', 'k1');

      component.ngOnInit();

      const mentor = component.membersLookup.find(member => member.memberKey === 'mentor');
      const youth = component.membersLookup.find(member => member.memberKey === 'youth');
      expect(mentor!.mentoredGroupNames).toEqual(['Кельти']);
      expect(mentor!.roleSortWeight).toBeLessThan(youth!.roleSortWeight!);
    });

    it('should load leadership for leadership type', () => {
      fixture.componentRef.setInput('type', 'leadership');
      fixture.componentRef.setInput('leadershipType', 'kurin');
      fixture.componentRef.setInput('typeKey', 'k1');
      component.ngOnInit();
      expect(leadershipServiceSpy.getLeadershipByTypeAndKey).toHaveBeenCalledWith('kurin', 'k1');
      expect(component.leadership).toEqual(mockLeadership);
      expect(component.allHistories).toHaveLength(2);
    });
  });

  describe('Leadership List Logic', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('type', 'leadership');
      fixture.componentRef.setInput('typeKey', 'k1');
      component.ngOnInit(); // Loads mockLeadership
    });

    it('should filter archived rows by default', () => {
      expect(component.showArchived).toBe(false);
      expect(component.leadershipHistories).toHaveLength(1);
      expect(component.leadershipHistories[0].role).toBe(LeadershipRole.Kurinnuy);
    });

    it('should show all rows when showArchived is true', () => {
      component.showArchived = true;
      component.refreshList();
      expect(component.leadershipHistories).toHaveLength(2);
    });

    it('should sort active rows first', () => {
      component.showArchived = true;
      component.refreshList();
      const rows = component.leadershipHistories;
      expect(rows[0].endDate).toBeFalsy(); // Active
      expect(rows[1].endDate).toBeTruthy(); // Archived
    });

    it('should sort active leadership rows by fixed role order before start date', () => {
      component.allHistories = [
        {
          leadershipKey: 'l1',
          leadershipHistoryKey: 'lh-pysar',
          role: LeadershipRole.Pysar,
          startDate: '2026-01-01',
          endDate: null,
          member: { memberKey: 'm3', firstName: 'Petro', lastName: 'Later', middleName: null }
        },
        {
          leadershipKey: 'l1',
          leadershipHistoryKey: 'lh-kurinnuy',
          role: LeadershipRole.Kurinnuy,
          startDate: '2020-01-01',
          endDate: null,
          member: { memberKey: 'm4', firstName: 'Ivan', lastName: 'Earlier', middleName: null }
        },
        {
          leadershipKey: 'l1',
          leadershipHistoryKey: 'lh-suddya',
          role: LeadershipRole.Suddya,
          startDate: '2025-01-01',
          endDate: null,
          member: { memberKey: 'm5', firstName: 'Stepan', lastName: 'Middle', middleName: null }
        }
      ];

      component.refreshList();

      expect(component.leadershipHistories.map(row => row.role)).toEqual([
        LeadershipRole.Kurinnuy,
        LeadershipRole.Suddya,
        LeadershipRole.Pysar
      ]);
    });
  });

  describe('Navigation', () => {
    it('onMemberSelect should navigate to member profile', () => {
      const member = { memberKey: 'm1' } as MemberDto;
      component.onMemberSelect(member);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', 'm1']);
    });

    it('onLeadershipSettingsSelect should navigate to edit if leadership exists', () => {
      component.leadership = mockLeadership;
      fixture.componentRef.setInput('leadershipType', 'kurin');
      fixture.componentRef.setInput('typeKey', 'k1');
      
      component.onLeadershipSettingsSelect();
      
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/leadership', 'l1', 'kurin', 'k1']);
    });

    it('onLeadershipSettingsSelect should navigate to create if leadership is null', () => {
      component.leadership = null;
      fixture.componentRef.setInput('type', 'leadership'); // Ensure context is correct
      fixture.componentRef.setInput('leadershipType', 'kurin');
      fixture.componentRef.setInput('typeKey', 'k1');

      component.onLeadershipSettingsSelect();

      expect(routerSpy.navigate).toHaveBeenCalledWith(['/leadership/create', 'kurin', 'k1']);
    });
  });

  describe('Helpers', () => {
    it('getRoleSeverity should return secondary for archived', () => {
      const history = { endDate: '2023-01-01' } as LeadershipHistoryDto;
      expect(component.getRoleSeverity(history)).toBe('secondary');
    });

    it('getRoleSeverity should return danger for Kurinnuy', () => {
      const history = { role: LeadershipRole.Kurinnuy, endDate: null } as LeadershipHistoryDto;
      expect(component.getRoleSeverity(history)).toBe('danger');
    });

    it('getRoleSeverity should return info for unknown roles', () => {
      const history = { role: LeadershipRole.Pysar, endDate: null } as LeadershipHistoryDto;
      expect(component.getRoleSeverity(history)).toBe('info');
    });
    
    it('getRoleDisplayName should return role name', () => {
        const role = LeadershipRole.Kurinnuy;
        expect(component.getRoleDisplayName(role)).toBeTruthy();
    });

    it('getMemberRoleTags should name the office once, from the leadership history', () => {
      const tags = component.getMemberRoleTags({
        memberKey: 'm1',
        firstName: 'John',
        lastName: 'Doe',
        middleName: 'M',
        // The system role mirrors the office below. Reading both used to produce the tag twice.
        userRole: 'KV.Vykhovnyk',
        leadershipHistories: [{
          leadershipHistoryKey: 'lh-1',
          leadershipKey: 'l1',
          role: LeadershipRole.Vykhovnyk,
          leadershipType: 'KV',
          startDate: '2025-01-01',
          endDate: null,
          member: { memberKey: 'm1', firstName: 'John', lastName: 'Doe', middleName: 'M' }
        }]
      } as MemberLookupDto);

      expect(tags).toEqual([
        expect.objectContaining({ label: 'Впорядник' })
      ]);
    });

    it('getMemberRoleTags should call a mentor without an office a Впорядник of their groups', () => {
      const tags = component.getMemberRoleTags({
        memberKey: 'm1',
        firstName: 'John',
        lastName: 'Doe',
        leadershipHistories: [],
        mentoredGroupNames: ['Кельти', 'Вовки']
      } as MemberLookupDto);

      expect(tags).toEqual([
        expect.objectContaining({ label: 'Впорядник: Кельти, Вовки', severity: 'info' })
      ]);
    });

    it('getMemberRoleTags should add the groups to a seated Впорядник instead of a second tag', () => {
      const tags = component.getMemberRoleTags({
        memberKey: 'm1',
        firstName: 'John',
        lastName: 'Doe',
        leadershipHistories: [{
          leadershipHistoryKey: 'lh-1',
          leadershipKey: 'l1',
          role: LeadershipRole.Vykhovnyk,
          leadershipType: 'KV',
          startDate: '2025-01-01',
          endDate: null,
          member: { memberKey: 'm1', firstName: 'John', lastName: 'Doe', middleName: null }
        }],
        mentoredGroupNames: ['Кельти']
      } as MemberLookupDto);

      expect(tags).toEqual([
        expect.objectContaining({ label: 'Впорядник: Кельти' })
      ]);
    });
  });
  describe('Group card mode', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('type', 'group');
      fixture.componentRef.setInput('typeKey', 'g1');
      memberServiceSpy.getAll.mockReturnValue(of([
        {
          memberKey: 'm1',
          firstName: 'John',
          lastName: 'Doe',
          middleName: 'M',
          phoneNumber: '111',
          latestPlastLevelDisplay: 'пл. уч.'
        } as MemberDto,
        {
          memberKey: 'm2',
          firstName: 'Jane',
          lastName: 'Smith',
          middleName: 'A',
          phoneNumber: '222',
          latestPlastLevelDisplay: 'ст. пл. скоб'
        } as MemberDto
      ]));
      component.ngOnInit();
    });

    it('should filter members by search query', () => {
      component.memberSearchQuery = 'smith';
      expect(component.filteredMembersLookup).toHaveLength(1);
      expect(component.filteredMembersLookup[0].memberKey).toBe('m2');
    });

    it('should filter by plast level localized label', () => {
      component.memberSearchQuery = 'скоб';
      expect(component.filteredMembersLookup).toHaveLength(1);
      expect(component.filteredMembersLookup[0].memberKey).toBe('m2');
    });

    it('should restore card view state from session storage for current group key', () => {
      const getItemSpy = vi.spyOn(Storage.prototype, 'getItem').mockReturnValue('true');

      fixture.componentRef.setInput('type', 'group');
      fixture.componentRef.setInput('typeKey', 'g42');
      component.ngOnInit();

      expect(getItemSpy).toHaveBeenCalledWith('member-list:group-card-view:g42');
      expect(component.showGroupCardView).toBe(true);
    });

    it('should persist card view state to session storage on toggle change', () => {
      const setItemSpy = vi.spyOn(Storage.prototype, 'setItem').mockReturnValue(undefined);

      fixture.componentRef.setInput('type', 'group');
      fixture.componentRef.setInput('typeKey', 'g77');
      component.showGroupCardView = true;
      component.onGroupCardViewToggleChange();

      expect(setItemSpy).toHaveBeenCalledWith('member-list:group-card-view:g77', 'true');
    });

    it('should set hasUpcomingBirthdays to false when no birthdays within 30 days', () => {
      memberServiceSpy.getAll.mockReturnValue(of([
        {
          memberKey: 'm1',
          groupKey: 'g1',
          kurinKey: 'k1',
          firstName: 'John',
          lastName: 'Doe',
          middleName: 'M',
          email: 'john@example.com',
          phoneNumber: '123456789',
          dateOfBirth: null,
          plastLevelHistories: [],
          leadershipHistories: [],
          profilePhotoUrl: null
        } as MemberDto
      ]));

      fixture.componentRef.setInput('type', 'group');
      fixture.componentRef.setInput('typeKey', 'g1');
      component.ngOnInit();

      expect(component.hasUpcomingBirthdays).toBe(false);
    });
  });
});
