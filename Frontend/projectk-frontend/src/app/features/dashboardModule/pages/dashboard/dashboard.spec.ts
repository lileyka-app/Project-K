import type { MockedObject } from 'vitest';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { of } from 'rxjs';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinBranch } from '../../../kurinModule/models/enums/kurin-branch.enum';
import { MembershipKind } from '../../../kurinModule/models/enums/membership-kind.enum';
import { KurinScopeOption } from '../../../kurinModule/models/kurin-scope-option.model';
import { MemberDto } from '../../../kurinModule/models/member.dto';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import { MyGroupDto } from '../../models/me.dto';
import { DashboardComponent } from './dashboard';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let auth: MockedObject<AuthService>;
  let members: MockedObject<MemberService>;
  let router: Router;

  const state: AuthState = {
    userKey: 'u1', memberKey: 'm1', email: 'o@x', isAdmin: false, permissions: [], roles: [], kurinKey: 'k1', accessToken: 't'
  };

  const member = {
    memberKey: 'm1', firstName: 'Оксана', lastName: 'Паливода', middleName: '', email: 'o@x', phoneNumber: '', dateOfBirth: null,
    groupKey: 'g1', groupName: 'Соколи', kurinKey: 'k1', latestPlastLevelDisplay: 'учасниця', plastLevelHistories: [],
    leadershipHistories: [
      { leadershipHistoryKey: 'h1', leadershipKey: 'l1', role: 'Suddya', leadershipType: 'Group', groupName: 'Соколи', startDate: '2026-09-01', endDate: null, member: { memberKey: 'm1', fullName: 'Оксана' } },
      { leadershipHistoryKey: 'h2', leadershipKey: 'l2', role: 'Pysar', leadershipType: 'Kurin', groupName: null, startDate: '2025-09-01', endDate: '2026-08-31', member: { memberKey: 'm1', fullName: 'Оксана' } }
    ],
    profilePhotoUrl: null
  } as unknown as MemberDto;

  const kurins: KurinScopeOption[] = [
    { kurinKey: 'k2', kurinNumber: 7, branch: KurinBranch.USP, namedAfter: null, kind: MembershipKind.Staff },
    { kurinKey: 'k1', kurinNumber: 1, branch: KurinBranch.UPYu, namedAfter: 'ім. Івана Богуна', kind: MembershipKind.Youth }
  ];

  const groups: MyGroupDto[] = [
    { groupKey: 'g1', kurin: { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true }, name: 'Соколи', silhouetteUrl: null, isOwn: true, isLed: false },
    { groupKey: 'g7', kurin: { kurinKey: 'k2', kurinNumber: 7, namedAfter: null, isCurrent: false }, name: 'Кельти', silhouetteUrl: null, isOwn: false, isLed: true }
  ];

  beforeEach(() => {
    auth = {
      getAuthStateValue: vi.fn(),
      getKurinScopeOptions: vi.fn(),
      setKurinScope: vi.fn(),
      getAuthState: vi.fn()
    } as MockedObject<AuthService>;
    auth.getAuthStateValue.mockReturnValue(state);
    auth.getAuthState.mockReturnValue(of(state));
    auth.getKurinScopeOptions.mockReturnValue(of(kurins));
    auth.setKurinScope.mockReturnValue(of({ ...state, kurinKey: 'k2' }));
    members = {
      getByKey: vi.fn()
    } as MockedObject<MemberService>;
    members.getByKey.mockReturnValue(of(member));

    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: MemberService, useValue: members },
        { provide: MessageService, useValue: {
            add: vi.fn()
          } }
      ]
    });
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).match(r => r.url.endsWith('/me/groups')).forEach(r => r.flush(groups));
    fixture.detectChanges();
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('greets the person by name and shows both tiles', () => {
    expect(text()).toContain('Оксана');
    expect(text()).toContain('Мій профіль');
    expect(text()).toContain('Мої курені та гуртки');
    expect(text()).toContain('Соколи');
    expect(text()).toContain('Кельти');
  });

  it('lists the offices held now in the kurin acted in, not the ones that ended', () => {
    expect(component.currentOffices()).toEqual(['Суддя · Соколи']);
  });

  it('opens the current kurin as it is, and switches the scope for another', () => {
    component.openKurin(kurins[1]);
    expect(auth.setKurinScope).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/kurin']);

    component.openKurin(kurins[0]);
    expect(auth.setKurinScope).toHaveBeenCalledWith('k2');
    expect(router.navigate).toHaveBeenCalledTimes(2);
  });

  it('opens a гурток of the kurin acted in at once, and one of another kurin after a switch', () => {
    component.openGroup(groups[0]);
    expect(auth.setKurinScope).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/group', 'g1']);

    component.openGroup(groups[1]);
    expect(auth.setKurinScope).toHaveBeenCalledWith('k2');
    expect(router.navigate).toHaveBeenCalledWith(['/group', 'g7']);
  });
});
