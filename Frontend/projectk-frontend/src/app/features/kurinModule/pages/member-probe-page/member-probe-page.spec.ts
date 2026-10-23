import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { MemberService } from '../../services/member-service/member.service';
import { MemberAbilitiesDto, NO_ABILITIES } from '../../models/member-abilities.dto';
import { ProbesCatalogService } from '../../services/probes-and-badges/probes-catalog.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { ProbeProgressStatus } from '../../models/enums/probe-progress-status.enum';
import { PlastLevel } from '../../models/enums/plast-level.enum';
import { MemberProbePageComponent } from './member-probe-page';
import { ConfirmationService } from '@openng/optimus-ui/api';

describe('MemberProbePageComponent', () => {
  let fixture: ComponentFixture<MemberProbePageComponent>;
  let component: MemberProbePageComponent;

  let memberServiceSpy: MockedObject<MemberService>;
  let probesCatalogServiceSpy: MockedObject<ProbesCatalogService>;
  let memberProgressServiceSpy: MockedObject<MemberProgressService>;
  let authServiceSpy: MockedObject<AuthService>;
  let routerSpy: MockedObject<Router>;
  let paramMapSubject: BehaviorSubject<ParamMap>;

  const memberKey = 'member-1';
  const probeId = 'probe-1';

  function createProbeProgress(status: ProbeProgressStatus) {
    return {
      probeProgressKey: null,
      memberKey,
      kurinKey: 'kurin-1',
      probeId,
      status,
      completedAtUtc: status === ProbeProgressStatus.Completed ? '2026-04-16T10:00:00Z' : null,
      completedByUserKey: null,
      completedByName: status === ProbeProgressStatus.Completed ? 'Впорядник' : null,
      completedByRole: status === ProbeProgressStatus.Completed ? 'Mentor' : null,
      verifiedAtUtc: null,
      verifiedByUserKey: null,
      verifiedByName: null,
      verifiedByRole: null,
      auditTrail: [],
      pointSignatures: []
    };
  }

  const signer: MemberAbilitiesDto = { ...NO_ABILITIES, probes: { sign: true, unsign: true } };

  beforeEach(async () => {
    memberServiceSpy = {
      getByKey: vi.fn(),
      getMemberships: vi.fn(),
      getAbilities: vi.fn()
    } as MockedObject<MemberService>;
    memberServiceSpy.getAbilities.mockReturnValue(of(signer));
    memberServiceSpy.getMemberships.mockReturnValue(of([]));
    probesCatalogServiceSpy = {
      getGroupedById: vi.fn()
    } as MockedObject<ProbesCatalogService>;
    memberProgressServiceSpy = {
      getProbeProgress: vi.fn(),
      updateProbeProgressStatus: vi.fn(),
      signProbePoint: vi.fn(),
      unsignProbePoint: vi.fn()
    } as MockedObject<MemberProgressService>;
    authServiceSpy = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    routerSpy = {
      navigate: vi.fn()
    } as MockedObject<Router>;
    paramMapSubject = new BehaviorSubject(convertToParamMap({ memberKey, probeId }));

    memberServiceSpy.getByKey.mockReturnValue(of({
      memberKey,
      groupKey: 'group-1',
      kurinKey: 'kurin-1',
      firstName: 'John',
      middleName: 'M',
      lastName: 'Doe',
      email: 'john@example.com',
      phoneNumber: '0990000000',
      dateOfBirth: null,
      profilePhotoUrl: null,
      plastLevelHistories: [],
      leadershipHistories: []
    }));

    probesCatalogServiceSpy.getGroupedById.mockReturnValue(of({
      id: probeId,
      title: 'Перша проба',
      pointsCount: 2,
      sectionsCount: 1,
      sections: [
        {
          id: 'sec-1',
          code: 'A',
          title: 'Розділ A',
          points: [
            { id: 'point-1', title: 'Точка 1' },
            { id: 'point-2', title: 'Точка 2' }
          ]
        }
      ]
    }));

    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of(createProbeProgress(ProbeProgressStatus.InProgress)));
    memberProgressServiceSpy.signProbePoint.mockReturnValue(of(createProbeProgress(ProbeProgressStatus.InProgress)));
    memberProgressServiceSpy.unsignProbePoint.mockReturnValue(of(createProbeProgress(ProbeProgressStatus.InProgress)));
    memberProgressServiceSpy.updateProbeProgressStatus.mockReturnValue(of(createProbeProgress(ProbeProgressStatus.Completed)));
    authServiceSpy.getAuthStateValue.mockReturnValue({
      userKey: 'user-1',
      memberKey: 'test-member-key',
      email: 'mentor@example.com',
      isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy'],
      kurinKey: 'kurin-1',
      accessToken: 'token'
    });

    await TestBed.configureTestingModule({
      imports: [MemberProbePageComponent],
      providers: [
        { provide: AuthService, useValue: authServiceSpy },
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: ProbesCatalogService, useValue: probesCatalogServiceSpy },
        { provide: MemberProgressService, useValue: memberProgressServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: { paramMap: paramMapSubject.asObservable() } }
      ]
    }).compileComponents();
  });

  function createComponent() {
    fixture = TestBed.createComponent(MemberProbePageComponent);
    component = fixture.componentInstance;
  }

  it('should create and load probe sections', () => {
    createComponent();
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.probeSections).toHaveLength(1);
    expect(component.probeSections[0].points).toHaveLength(2);
    expect(component.probeSections[0].points[0].isSigned).toBe(false);
  });

  it('should detect reviewer role access', () => {
    createComponent();
    fixture.detectChanges();

    expect(component.canManageProbePoints).toBe(true);
  });

  it('у старшого пластуна проба — архів: видно, але не підписати', () => {
    memberServiceSpy.getByKey.mockReturnValue(of({
      memberKey,
      groupKey: 'group-1',
      kurinKey: 'kurin-1',
      firstName: 'John',
      middleName: 'M',
      lastName: 'Doe',
      email: 'john@example.com',
      phoneNumber: '0990000000',
      dateOfBirth: null,
      profilePhotoUrl: null,
      latestPlastLevel: PlastLevel.Starshoplastun,
      plastLevelHistories: [{ plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2024-05-01' }],
      leadershipHistories: []
    }));
    createComponent();
    fixture.detectChanges();

    expect(component.isArchive).toBe(true);
    expect(component.canManageProbePoints).toBe(false);
    expect(component.probeSections[0].points).toHaveLength(2);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Архів юнацтва');

    component.onSignPoint(component.probeSections[0].points[0]);
    expect(memberProgressServiceSpy.signProbePoint).not.toHaveBeenCalled();
  });

  it('onSignPoint should sign point for reviewer without confirm dialog', () => {
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirmSpy = vi.spyOn(confirmationService, 'confirm').mockReturnThis();

    const point = component.probeSections[0].points[0];
    component.onSignPoint(point);

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(memberProgressServiceSpy.signProbePoint).toHaveBeenCalledWith(memberKey, probeId, point.pointId, {
      note: null
    });
  });

  it('signs but does not unsign when the kurin gives only signing', () => {
    memberServiceSpy.getAbilities.mockReturnValue(of({ ...NO_ABILITIES, probes: { sign: true, unsign: false } }));
    createComponent();
    fixture.detectChanges();

    expect(component.canSignProbePoints).toBe(true);
    expect(component.canUnsignProbePoints).toBe(false);
    const signed = { ...component.probeSections[0].points[0], isSigned: true };
    component.onUnsignPoint(signed);
    expect(memberProgressServiceSpy.unsignProbePoint).not.toHaveBeenCalled();
  });

  it('onSignPoint should do nothing for non-reviewer role', () => {
    memberServiceSpy.getAbilities.mockReturnValue(of(NO_ABILITIES));
    authServiceSpy.getAuthStateValue.mockReturnValue({
      userKey: 'user-1',
      memberKey: 'test-member-key',
      email: 'member@example.com',
      isAdmin: false, permissions: [], roles: ['Member'],
      kurinKey: 'kurin-1',
      accessToken: 'token'
    });
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirmSpy = vi.spyOn(confirmationService, 'confirm').mockReturnThis();

    const point = component.probeSections[0].points[0];
    component.onSignPoint(point);

    expect(component.canManageProbePoints).toBe(false);
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(memberProgressServiceSpy.signProbePoint).not.toHaveBeenCalled();
  });

  it('onUnsignPoint should confirm and unsign point for reviewer', () => {
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of(createProbeProgress(ProbeProgressStatus.Completed)));
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of({
      ...createProbeProgress(ProbeProgressStatus.InProgress),
      pointSignatures: [
        {
          probePointProgressKey: 'pp1',
          pointId: 'point-1',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:00:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        }
      ]
    }));
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirmSpy = vi.spyOn(confirmationService, 'confirm').mockImplementation((options) => {
      options.accept?.();
      return confirmationService;
    });

    const point = component.probeSections[0].points[0];
    component.onUnsignPoint(point);

    expect(confirmSpy).toHaveBeenCalled();
    expect(memberProgressServiceSpy.unsignProbePoint).toHaveBeenCalledWith(memberKey, probeId, point.pointId, {
      note: null
    });
  });

  // A system role is shown by its Ukrainian office name; a bare pre-офіс name like «Mentor» has none,
  // so it is left out rather than shown in English.
  it('getProbePointSignerLabel should fallback to userKey + office when signer name is absent', () => {
    createComponent();
    fixture.detectChanges();

    const point = {
      sectionId: 'sec-1',
      sectionCode: 'A',
      sectionTitle: 'Розділ A',
      pointId: 'point-1',
      pointTitle: 'Точка 1',
      isSigned: true,
      signedByUserKey: '4f36d9a1-7b8a-4faf-ae0d-f4d38a9fd111',
      signedByName: null,
      signedByRole: 'KV.Zvyazkovyi',
      signedAtUtc: null
    };

    expect(component.getProbePointSignerLabel(point)).toMatch(/^4f36d9a1-7b8a-4faf-ae0d-f4d38a9fd111 \([^A-Za-z)]+\)$/);
    expect(component.getProbePointSignerLabel({ ...point, signedByRole: 'Mentor' }))
      .toBe('4f36d9a1-7b8a-4faf-ae0d-f4d38a9fd111');
  });

  it('onUnsignPoint should show conflict message when API returns 409', () => {
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of({
      ...createProbeProgress(ProbeProgressStatus.InProgress),
      pointSignatures: [
        {
          probePointProgressKey: 'pp1',
          pointId: 'point-1',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:00:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        }
      ]
    }));
    memberProgressServiceSpy.unsignProbePoint.mockReturnValue(throwError(() => ({
      status: 409,
      error: ['conflict', createProbeProgress(ProbeProgressStatus.InProgress)]
    })));
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    vi.spyOn(confirmationService, 'confirm').mockImplementation((options) => {
      options.accept?.();
      return confirmationService;
    });

    const point = component.probeSections[0].points[0];
    component.onUnsignPoint(point);

    expect(component.reviewerActionErrorMessage).toContain('Конфлікт оновлення');
  });

  it('should allow closing probe when all points are signed', () => {
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of({
      ...createProbeProgress(ProbeProgressStatus.InProgress),
      pointSignatures: [
        {
          probePointProgressKey: 'pp1',
          pointId: 'point-1',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:00:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        },
        {
          probePointProgressKey: 'pp2',
          pointId: 'point-2',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:05:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        }
      ]
    }));

    createComponent();
    fixture.detectChanges();

    expect(component.canCloseProbe).toBe(true);
  });

  it('keeps points signed under earlier requirements as signed earlier', () => {
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of({
      ...createProbeProgress(ProbeProgressStatus.InProgress),
      pointSignatures: [
        {
          probePointProgressKey: 'pp1',
          pointId: 'point-1',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:00:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        },
        {
          probePointProgressKey: 'pp-old',
          pointId: 'old-point',
          isSigned: true,
          signedAtUtc: '2025-09-01T10:00:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        }
      ]
    }));

    createComponent();
    fixture.detectChanges();

    expect(component.signedEarlierPoints.map(point => point.pointId)).toEqual(['old-point']);
    expect(component.getSignedEarlierLabel(component.signedEarlierPoints[0])).toContain('Точка old-point · Впорядник');
    expect(component.canCloseProbe).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Підписано раніше');
  });

  it('onCloseProbe should call updateProbeProgressStatus with Completed', () => {
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of({
      ...createProbeProgress(ProbeProgressStatus.InProgress),
      pointSignatures: [
        {
          probePointProgressKey: 'pp1',
          pointId: 'point-1',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:00:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        },
        {
          probePointProgressKey: 'pp2',
          pointId: 'point-2',
          isSigned: true,
          signedAtUtc: '2026-04-16T10:05:00Z',
          signedByUserKey: 'user-1',
          signedByName: 'Впорядник',
          signedByRole: 'Mentor'
        }
      ]
    }));
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirmSpy = vi.spyOn(confirmationService, 'confirm').mockImplementation((options) => {
      options.accept?.();
      return confirmationService;
    });

    component.onCloseProbe();

    expect(confirmSpy).toHaveBeenCalled();
    expect(memberProgressServiceSpy.updateProbeProgressStatus).toHaveBeenCalledWith(memberKey, probeId, {
      status: ProbeProgressStatus.Completed,
      note: null
    });
  });

  it('goBackToMember should navigate to member card route', () => {
    createComponent();
    fixture.detectChanges();

    component.goBackToMember();

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', memberKey]);
  });
});
