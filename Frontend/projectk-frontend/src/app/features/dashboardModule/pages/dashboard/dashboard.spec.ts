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
import { MyDutyDto, MyGroupDto } from '../../models/me.dto';
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

  it('says «Сходин не було» through the sheet\'s endpoint and drops the row', () => {
    const kurin = { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true };
    const meeting: MyDutyDto = {
      kind: 'EventWithoutAttendance', kurin, count: 1, groupKey: null, groupName: null,
      agendaItemKey: 'e1', occurrenceStartUtc: '2026-10-05T14:00:00Z', title: 'Сходини',
      key: 'EventWithoutAttendance:k1:-:e1:20261005T140000Z', fingerprint: '1'
    };
    const badges: MyDutyDto = { ...meeting, kind: 'BadgesToReview', count: 2, agendaItemKey: null, occurrenceStartUtc: null, title: null, key: 'BadgesToReview:k1:-:-:-', fingerprint: '2' };
    component.duties.set([badges, meeting]);

    component.markNotHeld(meeting);
    const request = TestBed.inject(HttpTestingController)
      .expectOne(r => r.url.endsWith(`/kurin/k1/score/events/e1/${encodeURIComponent('2026-10-05T14:00:00Z')}/not-held`));
    expect(request.request.method).toBe('PUT');
    request.flush({});

    expect(component.duties()).toEqual([badges]);
  });

  it('hides a duty for this person alone, and brings the hidden ones back read afresh', () => {
    const http = TestBed.inject(HttpTestingController);
    const kurin = { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true };
    const badges: MyDutyDto = {
      kind: 'BadgesToReview', kurin, count: 2, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null,
      key: 'BadgesToReview:k1:-:-:-', fingerprint: '2'
    };
    component.duties.set([badges]);

    component.hideDuty(badges);
    const dismiss = http.expectOne(r => r.url.endsWith('/me/duties/dismissed') && r.method === 'POST');
    expect(dismiss.request.body).toEqual({ key: 'BadgesToReview:k1:-:-:-', fingerprint: '2' });
    dismiss.flush({});
    expect(component.duties()).toEqual([]);
    expect(component.dutiesHidden()).toBe(1);

    component.restoreDuties();
    http.expectOne(r => r.url.endsWith('/me/duties/dismissed') && r.method === 'DELETE').flush({});
    http.match(r => r.url.endsWith('/me/duties/board')).forEach(r => r.flush({ items: [badges], hiddenCount: 0 }));
    expect(component.duties()).toEqual([badges]);
    expect(component.dutiesHidden()).toBe(0);
  });

  it('reads the bare list an older server or the static demo sends as nothing hidden', () => {
    TestBed.inject(HttpTestingController).match(r => r.url.endsWith('/me/duties/board')).forEach(r => r.flush([
      { kind: 'BadgesToReview', kurin: { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true }, count: 2, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null }
    ]));

    expect(component.duties()[0].key).toBe('');
    expect(component.dutiesHidden()).toBe(0);
  });
});
