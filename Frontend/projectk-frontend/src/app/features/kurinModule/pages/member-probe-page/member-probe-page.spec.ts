import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { EntityService } from '../../../authModule/services/entity-service/entity.service';
import { MemberService } from '../../services/member-service/member.service';
import { ProbesCatalogService } from '../../services/probes-and-badges/probes-catalog.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { ProbeProgressStatus } from '../../models/enums/probe-progress-status.enum';
import { PlastLevel } from '../../models/enums/plast-level.enum';
import { MemberProbePageComponent } from './member-probe-page';
import { ConfirmationService } from '@openng/optimus-ui/api';

describe('MemberProbePageComponent', () => {
  let fixture: ComponentFixture<MemberProbePageComponent>;
  let component: MemberProbePageComponent;

  let memberServiceSpy: jasmine.SpyObj<MemberService>;
  let probesCatalogServiceSpy: jasmine.SpyObj<ProbesCatalogService>;
  let memberProgressServiceSpy: jasmine.SpyObj<MemberProgressService>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let entityServiceSpy: jasmine.SpyObj<EntityService>;
  let routerSpy: jasmine.SpyObj<Router>;
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

  beforeEach(async () => {
    memberServiceSpy = jasmine.createSpyObj<MemberService>('MemberService', ['getByKey', 'getMemberships']);
    memberServiceSpy.getMemberships.and.returnValue(of([]));
    probesCatalogServiceSpy = jasmine.createSpyObj<ProbesCatalogService>('ProbesCatalogService', ['getGroupedById']);
    memberProgressServiceSpy = jasmine.createSpyObj<MemberProgressService>('MemberProgressService', [
      'getProbeProgress',
      'updateProbeProgressStatus',
      'signProbePoint',
      'unsignProbePoint'
    ]);
    authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['getAuthStateValue']);
    entityServiceSpy = jasmine.createSpyObj<EntityService>('EntityService', ['checkEntityAccess']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    paramMapSubject = new BehaviorSubject(convertToParamMap({ memberKey, probeId }));

    memberServiceSpy.getByKey.and.returnValue(of({
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

    probesCatalogServiceSpy.getGroupedById.and.returnValue(of({
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

    memberProgressServiceSpy.getProbeProgress.and.returnValue(of(createProbeProgress(ProbeProgressStatus.InProgress)));
    memberProgressServiceSpy.signProbePoint.and.returnValue(of(createProbeProgress(ProbeProgressStatus.InProgress)));
    memberProgressServiceSpy.unsignProbePoint.and.returnValue(of(createProbeProgress(ProbeProgressStatus.InProgress)));
    memberProgressServiceSpy.updateProbeProgressStatus.and.returnValue(of(createProbeProgress(ProbeProgressStatus.Completed)));
    entityServiceSpy.checkEntityAccess.and.returnValue(of(true));
    authServiceSpy.getAuthStateValue.and.returnValue({
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
        { provide: EntityService, useValue: entityServiceSpy },
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
    expect(component.probeSections).toHaveSize(1);
    expect(component.probeSections[0].points).toHaveSize(2);
    expect(component.probeSections[0].points[0].isSigned).toBeFalse();
  });

  it('should detect reviewer role access', () => {
    createComponent();
    fixture.detectChanges();

    expect(component.canManageProbePoints).toBeTrue();
  });

  it('у старшого пластуна проба — архів: видно, але не підписати', () => {
    memberServiceSpy.getByKey.and.returnValue(of({
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

    expect(component.isArchive).toBeTrue();
    expect(component.canManageProbePoints).toBeFalse();
    expect(component.probeSections[0].points).toHaveSize(2);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Архів юнацтва');

    component.onSignPoint(component.probeSections[0].points[0]);
    expect(memberProgressServiceSpy.signProbePoint).not.toHaveBeenCalled();
  });

  it('onSignPoint should sign point for reviewer without confirm dialog', () => {
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirmSpy = spyOn(confirmationService, 'confirm');

    const point = component.probeSections[0].points[0];
    component.onSignPoint(point);

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(memberProgressServiceSpy.signProbePoint).toHaveBeenCalledWith(memberKey, probeId, point.pointId, {
      note: null
    });
  });

  it('onSignPoint should do nothing for non-reviewer role', () => {
    authServiceSpy.getAuthStateValue.and.returnValue({
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
    const confirmSpy = spyOn(confirmationService, 'confirm');

    const point = component.probeSections[0].points[0];
    component.onSignPoint(point);

    expect(component.canManageProbePoints).toBeFalse();
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(memberProgressServiceSpy.signProbePoint).not.toHaveBeenCalled();
  });

  it('onUnsignPoint should confirm and unsign point for reviewer', () => {
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of(createProbeProgress(ProbeProgressStatus.Completed)));
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of({
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
    const confirmSpy = spyOn(confirmationService, 'confirm').and.callFake((options) => {
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
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of({
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
    memberProgressServiceSpy.unsignProbePoint.and.returnValue(throwError(() => ({
      status: 409,
      error: ['conflict', createProbeProgress(ProbeProgressStatus.InProgress)]
    })));
    createComponent();
    fixture.detectChanges();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options) => {
      options.accept?.();
      return confirmationService;
    });

    const point = component.probeSections[0].points[0];
    component.onUnsignPoint(point);

    expect(component.reviewerActionErrorMessage).toContain('Конфлікт оновлення');
  });

  it('should allow closing probe when all points are signed', () => {
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of({
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

    expect(component.canCloseProbe).toBeTrue();
  });

  it('keeps points signed under earlier requirements as signed earlier', () => {
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of({
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
    expect(component.canCloseProbe).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Підписано раніше');
  });

  it('onCloseProbe should call updateProbeProgressStatus with Completed', () => {
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of({
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
    const confirmSpy = spyOn(confirmationService, 'confirm').and.callFake((options) => {
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

