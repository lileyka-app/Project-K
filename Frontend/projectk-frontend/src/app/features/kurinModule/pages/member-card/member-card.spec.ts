import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MemberCardComponent } from './member-card';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from '@openng/optimus-ui/api';
import { MemberService } from '../../services/member-service/member.service';
import { MemberDto } from '../../models/member.dto';
import { KurinBranch } from '../../models/enums/kurin-branch.enum';
import { MembershipKind } from '../../models/enums/membership-kind.enum';
import { PlastLevel } from '../../models/enums/plast-level.enum';
import { BadgesCatalogService } from '../../services/probes-and-badges/badges-catalog.service';
import { ProbesCatalogService } from '../../services/probes-and-badges/probes-catalog.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { BadgeImageBlobService } from '../../services/probes-and-badges/badge-image-blob.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { EntityService } from '../../../authModule/services/entity-service/entity.service';
import { BadgeProgressStatus } from '../../models/enums/badge-progress-status.enum';
import { ProbeProgressStatus } from '../../models/enums/probe-progress-status.enum';
import { BadgeCatalogItemDto } from '../../models/probes-and-badges/badge-catalog-item.dto';
import { BadgeProgressDto } from '../../models/probes-and-badges/badge-progress.dto';
import { ProbeSummaryDto } from '../../models/probes-and-badges/probe-summary.dto';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { CardFieldsDto, DEFAULT_CARD_FIELDS } from '../../models/card-fields.dto';
import { KurinDto } from '../../models/kurin.dto';
import { MemberProfileVerificationStatus } from '../../models/enums/member-profile-verification-status.enum';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { MemberDuesDto } from '../../../duesModule/models/group-dues.dto';

describe('MemberCardComponent', () => {
  let fixture: ComponentFixture<MemberCardComponent>;
  let component: MemberCardComponent;

  let memberServiceSpy: jasmine.SpyObj<MemberService>;
  let kurinServiceSpy: jasmine.SpyObj<KurinService>;
  let badgesCatalogServiceSpy: jasmine.SpyObj<BadgesCatalogService>;
  let probesCatalogServiceSpy: jasmine.SpyObj<ProbesCatalogService>;
  let memberProgressServiceSpy: jasmine.SpyObj<MemberProgressService>;
  let badgeImageBlobServiceSpy: jasmine.SpyObj<BadgeImageBlobService>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let entityServiceSpy: jasmine.SpyObj<EntityService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let duesServiceSpy: jasmine.SpyObj<DuesService>;
  let paramMapSubject: BehaviorSubject<ParamMap>;

  const memberKey = 'abc123';
  const member: MemberDto = {
    memberKey,
    groupKey: 'group1',
    kurinKey: 'kurin1',
    firstName: 'John',
    middleName: 'M',
    lastName: 'Doe',
    email: 'john@example.com',
    phoneNumber: '123456789',
    dateOfBirth: null,
    profilePhotoUrl: null,
    plastLevelHistories: [],
    leadershipHistories: []
  };

  beforeEach(async () => {
    memberServiceSpy = jasmine.createSpyObj<MemberService>('MemberService', ['getByKey', 'getMemberships', 'verifyProfile', 'resetProfileVerification']);
    kurinServiceSpy = jasmine.createSpyObj<KurinService>('KurinService', ['getByKey', 'getCardFields']);
    kurinServiceSpy.getCardFields.and.returnValue(of(DEFAULT_CARD_FIELDS));
    badgesCatalogServiceSpy = jasmine.createSpyObj<BadgesCatalogService>('BadgesCatalogService', ['getAll', 'getForKurin']);
    probesCatalogServiceSpy = jasmine.createSpyObj<ProbesCatalogService>('ProbesCatalogService', ['getAll']);
    memberProgressServiceSpy = jasmine.createSpyObj<MemberProgressService>('MemberProgressService', ['getBadgeProgresses', 'submitBadgeProgress', 'reviewBadgeProgress', 'getProbeProgress']);
    badgeImageBlobServiceSpy = jasmine.createSpyObj<BadgeImageBlobService>('BadgeImageBlobService', ['resolveBadgeImageForDisplay']);
    authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['getAuthStateValue']);
    entityServiceSpy = jasmine.createSpyObj<EntityService>('EntityService', ['checkEntityAccess']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    duesServiceSpy = jasmine.createSpyObj<DuesService>('DuesService', ['getMemberDues']);
    duesServiceSpy.getMemberDues.and.returnValue(of({ hasAccount: false } as MemberDuesDto));
    paramMapSubject = new BehaviorSubject(convertToParamMap({ memberKey }));

    badgeImageBlobServiceSpy.resolveBadgeImageForDisplay.and.callFake((url: string | null) => url);
    authServiceSpy.getAuthStateValue.and.returnValue({
      userKey: 'user-mentor',
      memberKey: 'test-member-key',
      email: 'mentor@example.com',
      isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy'],
      kurinKey: member.kurinKey,
      accessToken: 'token'
    });

    badgesCatalogServiceSpy.getAll.and.returnValue(of([]));
    badgesCatalogServiceSpy.getForKurin.and.returnValue(of([]));
    kurinServiceSpy.getByKey.and.returnValue(of({
      kurinKey: member.kurinKey,
      number: 1,
      profileVerificationEnabled: true
    } as KurinDto));
    probesCatalogServiceSpy.getAll.and.returnValue(of([]));
    memberProgressServiceSpy.getBadgeProgresses.and.returnValue(of([]));
    memberProgressServiceSpy.getProbeProgress.and.returnValue(of({
      probeProgressKey: null,
      memberKey,
      kurinKey: member.kurinKey,
      probeId: 'probe-1',
      status: ProbeProgressStatus.NotStarted,
      completedAtUtc: null,
      completedByUserKey: null,
      completedByName: null,
      completedByRole: null,
      verifiedAtUtc: null,
      verifiedByUserKey: null,
      verifiedByName: null,
      verifiedByRole: null,
      auditTrail: []
    }));
    entityServiceSpy.checkEntityAccess.and.returnValue(of(true));
    memberProgressServiceSpy.submitBadgeProgress.and.returnValue(of({
      badgeProgressKey: 'new-progress',
      memberKey,
      kurinKey: member.kurinKey,
      badgeId: 'badge-new',
      status: BadgeProgressStatus.Submitted,
      submittedAtUtc: '2026-04-16T00:00:00Z',
      reviewedAtUtc: null,
      reviewedByUserKey: null,
      reviewedByName: null,
      reviewedByRole: null,
      reviewNote: null,
      auditTrail: []
    }));
    memberProgressServiceSpy.reviewBadgeProgress.and.returnValue(of({
      badgeProgressKey: 'reviewed-progress',
      memberKey,
      kurinKey: member.kurinKey,
      badgeId: 'badge-reviewed',
      status: BadgeProgressStatus.Confirmed,
      submittedAtUtc: '2026-04-16T00:00:00Z',
      reviewedAtUtc: '2026-04-16T00:10:00Z',
      reviewedByUserKey: 'reviewer-1',
      reviewedByName: 'Mentor',
      reviewedByRole: 'Mentor',
      reviewNote: null,
      auditTrail: []
    }));

    memberServiceSpy.getMemberships.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [MemberCardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService,
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: KurinService, useValue: kurinServiceSpy },
        { provide: BadgesCatalogService, useValue: badgesCatalogServiceSpy },
        { provide: ProbesCatalogService, useValue: probesCatalogServiceSpy },
        { provide: MemberProgressService, useValue: memberProgressServiceSpy },
        { provide: BadgeImageBlobService, useValue: badgeImageBlobServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        { provide: EntityService, useValue: entityServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: DuesService, useValue: duesServiceSpy },
        { provide: ActivatedRoute, useValue: { paramMap: paramMapSubject.asObservable() } }
      ]
    }).compileComponents();
  });

  function createComponent() {
    fixture = TestBed.createComponent(MemberCardComponent);
    component = fixture.componentInstance;
  }

  it('should create', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should read memberKey from route params and call service', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();
    expect(component.memberKey).toBe(memberKey);
    expect(memberServiceSpy.getByKey).toHaveBeenCalledWith(memberKey);
    expect(memberProgressServiceSpy.getBadgeProgresses).toHaveBeenCalledWith(memberKey);
  });

  it('should build probe rows and keep third probe disabled', () => {
    const probes: ProbeSummaryDto[] = [
      { id: 'probe-1', title: 'Перша проба', pointsCount: 10, sectionsCount: 3 },
      { id: 'probe-2', title: 'Друга проба', pointsCount: 12, sectionsCount: 4 },
      { id: 'probe-3', title: 'Третя проба', pointsCount: 14, sectionsCount: 5 }
    ];

    memberServiceSpy.getByKey.and.returnValue(of(member));
    probesCatalogServiceSpy.getAll.and.returnValue(of(probes));
    memberProgressServiceSpy.getProbeProgress.and.callFake((_mk, probeId) => of({
      probeProgressKey: null,
      memberKey,
      kurinKey: member.kurinKey,
      probeId,
      status: probeId === 'probe-1' ? ProbeProgressStatus.Completed : ProbeProgressStatus.NotStarted,
      completedAtUtc: probeId === 'probe-1' ? '2026-04-16T00:00:00Z' : null,
      completedByUserKey: null,
      completedByName: null,
      completedByRole: null,
      verifiedAtUtc: null,
      verifiedByUserKey: null,
      verifiedByName: null,
      verifiedByRole: null,
      auditTrail: []
    }));

    createComponent();
    fixture.detectChanges();

    expect(component.probeRows.map(row => row.probeId)).toEqual(['probe-1', 'probe-2', 'probe-3']);
    expect(component.probeRows[0].isCompleted).toBeTrue();
    expect(component.probeRows[2].isDisabled).toBeTrue();
    expect(component.completedProbesCount).toBe(1);
  });

  it('openProbeDetails should navigate only for enabled probe rows', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();

    const enabledProbe = {
      probeId: 'probe-1',
      label: 'Перша проба',
      title: 'Перша проба',
      status: ProbeProgressStatus.InProgress,
      completedAtUtc: null,
      isCompleted: false,
      isDisabled: false,
      canOpenDetails: true,
      pointsCount: 10,
      sectionsCount: 3,
      signedPointsCount: 4,
      completionPercent: 40
    };

    const disabledProbe = {
      ...enabledProbe,
      probeId: 'probe-3',
      label: 'Третя проба',
      isDisabled: true,
      canOpenDetails: false
    };

    component.openProbeDetails(enabledProbe);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', memberKey, 'probe', 'probe-1']);

    component.openProbeDetails(disabledProbe);

    expect(routerSpy.navigate).toHaveBeenCalledTimes(1);
  });

  it('should set member on successful load', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();
    expect(component.member).toEqual(member);
  });

  it('should list the parents with something filled in, and none when the край hides them', () => {
    memberServiceSpy.getByKey.and.returnValue(of({
      ...member, motherFirstName: 'Олена', motherLastName: 'Петренко', motherPhone: '+380501234567', fatherFirstName: ''
    }));
    createComponent();
    fixture.detectChanges();

    expect(component.parentLines.map(line => [line.label, line.name, line.phoneLink]))
      .toEqual([['Мама', 'Олена Петренко', 'tel:+380501234567']]);

    component.cardFields = { fields: { parents: 'hidden' }, extra: [] } as CardFieldsDto;
    expect(component.parentLines).toEqual([]);
  });

  it('should show the край\'s own fields and leave out the ones it hides', () => {
    kurinServiceSpy.getCardFields.and.returnValue(of({
      fields: { middleName: 'hidden', phoneNumber: 'required' },
      extra: [
        { key: 'visaType', label: 'Тип візи', type: 'text', required: true },
        { key: 'visaUntil', label: 'Віза до', type: 'date', required: false },
        { key: 'permit', label: 'Дозвіл', type: 'text', required: false }
      ]
    } as CardFieldsDto));
    memberServiceSpy.getByKey.and.returnValue(of({ ...member, cardExtras: { visaType: 'D', visaUntil: '2027-06-30' } }));
    createComponent();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Тип візи: D');
    expect(text).toContain('Віза до: 30.06.2027');
    expect(text).not.toContain('Дозвіл');
    expect(text).not.toContain('По батькові не вказано');
  });

  it('should display profile verification timestamp without timezone as local time from UTC', () => {
    memberServiceSpy.getByKey.and.returnValue(of({
      ...member,
      profileVerifiedAtUtc: '2026-07-04T20:53:00'
    }));
    createComponent();
    fixture.detectChanges();

    const expected = new Date('2026-07-04T20:53:00Z').toLocaleString('uk-UA', {
      dateStyle: 'medium',
      timeStyle: 'short'
    });
    expect(component.profileVerifiedAtDisplay).toBe(expected);
  });

  it('should navigate to panel on load error when member not preset', () => {
    memberServiceSpy.getByKey.and.returnValue(throwError(() => new Error('Not found')));
    createComponent();
    fixture.detectChanges();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/panel'], { replaceUrl: true });
  });

  it('should navigate to group page on load error when existing member with groupKey set', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();
    memberServiceSpy.getByKey.and.returnValue(throwError(() => new Error('Network')));
    component.member = { ...member };
    component.refreshData();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/group', member.groupKey], { replaceUrl: true });
  });

  it('onEditMember should navigate to edit route', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();
    component.onEditMember();
    expect(routerSpy.navigate).toHaveBeenCalledWith(
      ['/group', member.groupKey, 'member', 'upsert', memberKey],
      { state: { fromMember: true } }
    );
  });

  it('onEditMember should edit in the kurin scope when the member has no group', () => {
    const grouplessMember = { ...member, groupKey: '00000000-0000-0000-0000-000000000000' };
    memberServiceSpy.getByKey.and.returnValue(of(grouplessMember));
    createComponent();
    fixture.detectChanges();
    component.onEditMember();
    expect(routerSpy.navigate).toHaveBeenCalledWith(
      ['/kurin', member.kurinKey, 'member', 'upsert', memberKey],
      { state: { fromMember: true } }
    );
  });

  it('onEditMember should not navigate when the member has neither group nor kurin', () => {
    const emptyGuid = '00000000-0000-0000-0000-000000000000';
    memberServiceSpy.getByKey.and.returnValue(of({ ...member, groupKey: emptyGuid, kurinKey: emptyGuid }));
    createComponent();
    fixture.detectChanges();
    routerSpy.navigate.calls.reset();
    component.onEditMember();
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('should fall back to panel on load error when the group key is an empty guid', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();
    memberServiceSpy.getByKey.and.returnValue(throwError(() => new Error('Network')));
    component.member = { ...member, groupKey: '00000000-0000-0000-0000-000000000000' };
    component.refreshData();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/panel'], { replaceUrl: true });
  });

  it('refreshData should use latest paramMap value if it changes', () => {
    memberServiceSpy.getByKey.and.returnValues(
      of(member),
      of({ ...member, memberKey: 'newKey' })
    );
    memberProgressServiceSpy.getBadgeProgresses.and.returnValues(of([]), of([]));
    createComponent();
    fixture.detectChanges();

    paramMapSubject.next(convertToParamMap({ memberKey: 'newKey' }));

    expect(component.memberKey).toBe('newKey');
    expect(memberServiceSpy.getByKey).toHaveBeenCalledWith('newKey');
  });

  it('should build skills summary with confirmed first and pending at the end', () => {
    const badges: BadgeCatalogItemDto[] = [
      {
        id: 'badge-1',
        title: 'Badge A',
        imagePath: 'https://example.com/a.png',
        country: 'UA',
        specialization: 'Scout',
        status: 'active',
        level: 1,
        lastUpdated: '2026-04-01',
        seekerRequirements: '',
        instructorRequirements: '',
        fixNotes: []
      },
      {
        id: 'badge-2',
        title: 'Badge B',
        imagePath: 'https://example.com/b.png',
        country: 'UA',
        specialization: 'Scout',
        status: 'active',
        level: 1,
        lastUpdated: '2026-04-01',
        seekerRequirements: '',
        instructorRequirements: '',
        fixNotes: []
      }
    ];

    const progresses: BadgeProgressDto[] = [
      {
        badgeProgressKey: 'p1',
        memberKey,
        kurinKey: member.kurinKey,
        badgeId: 'badge-1',
        status: BadgeProgressStatus.Confirmed,
        submittedAtUtc: '2026-04-01T00:00:00Z',
        reviewedAtUtc: '2026-04-10T00:00:00Z',
        reviewedByUserKey: null,
        reviewedByName: null,
        reviewedByRole: null,
        reviewNote: null,
        auditTrail: []
      },
      {
        badgeProgressKey: 'p2',
        memberKey,
        kurinKey: member.kurinKey,
        badgeId: 'badge-2',
        status: BadgeProgressStatus.Submitted,
        submittedAtUtc: '2026-04-11T00:00:00Z',
        reviewedAtUtc: null,
        reviewedByUserKey: null,
        reviewedByName: null,
        reviewedByRole: null,
        reviewNote: null,
        auditTrail: []
      }
    ];

    memberServiceSpy.getByKey.and.returnValue(of(member));
    badgesCatalogServiceSpy.getAll.and.returnValue(of(badges));
    memberProgressServiceSpy.getBadgeProgresses.and.returnValue(of(progresses));

    createComponent();
    fixture.detectChanges();

    expect(component.skillsSummary.orderedPreview.map(skill => skill.badgeId)).toEqual(['badge-1', 'badge-2']);
    expect(component.pendingSkillsCount).toBe(1);
  });

  it('openAllSkillsDialog should set dialog visibility to true', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.openAllSkillsDialog();

    expect(component.isAllSkillsDialogVisible).toBeTrue();
  });

  it('openAddSkillDialog should open dialog and reset search state', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.addSkillSearchTerm = 'старий пошук';
    component.addSkillVisibleCount = 99;
    component.openAddSkillDialog();

    expect(component.isAddSkillDialogVisible).toBeTrue();
    expect(component.addSkillSearchTerm).toBe('');
    expect(component.addSkillVisibleCount).toBe(component.addSkillPageSize);
  });

  it('filteredAddSkillCandidates should filter by title and reset visible count on search change', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    badgesCatalogServiceSpy.getAll.and.returnValue(of([
      {
        id: 'pl-badge',
        title: 'Rescue Badge PL',
        imagePath: 'badges/pl.png',
        country: 'PL',
        specialization: 'Scout',
        status: 'active',
        level: 1,
        lastUpdated: '2026-04-01',
        seekerRequirements: '',
        instructorRequirements: '',
        fixNotes: []
      }
    ]));
    badgesCatalogServiceSpy.getForKurin.and.returnValue(of([
      {
        id: 'badge-1',
        title: 'Rescue Badge',
        imagePath: 'badges/a.png',
        country: 'UA',
        specialization: 'Scout',
        status: 'active',
        level: 1,
        lastUpdated: '2026-04-01',
        seekerRequirements: '',
        instructorRequirements: '',
        fixNotes: []
      },
      {
        id: 'badge-2',
        title: 'Cooking Badge',
        imagePath: 'badges/b.png',
        country: 'UA',
        specialization: 'Craft',
        status: 'active',
        level: 1,
        lastUpdated: '2026-04-01',
        seekerRequirements: '',
        instructorRequirements: '',
        fixNotes: []
      }
    ]));

    createComponent();
    fixture.detectChanges();

    component.addSkillVisibleCount = 30;
    component.addSkillSearchTerm = 'rescue';
    component.onAddSkillSearchTermChange();

    expect(component.addSkillVisibleCount).toBe(component.addSkillPageSize);
    expect(badgesCatalogServiceSpy.getForKurin).toHaveBeenCalledWith(member.kurinKey);
    expect(component.filteredAddSkillCandidates.map(item => item.id)).toEqual(['badge-1']);
  });

  it('loadMoreAddSkillCandidates should increase visible results count', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();

    const initial = component.addSkillVisibleCount;
    component.loadMoreAddSkillCandidates();

    expect(component.addSkillVisibleCount).toBe(initial + component.addSkillPageSize);
  });

  it('submitBadge should call API and close add-skill dialog on success', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.isAddSkillDialogVisible = true;
    component.submitBadge('badge-new');

    expect(memberProgressServiceSpy.submitBadgeProgress).toHaveBeenCalledWith(memberKey, 'badge-new', { note: null });
    expect(component.isAddSkillDialogVisible).toBeFalse();
    expect(component.addSkillSuccessMessage).toContain('подана');
  });

  it('canSubmitBadge should allow re-submission for rejected badge', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    memberProgressServiceSpy.getBadgeProgresses.and.returnValue(of([
      {
        badgeProgressKey: 'rejected-progress',
        memberKey,
        kurinKey: member.kurinKey,
        badgeId: 'badge-rejected',
        status: BadgeProgressStatus.Rejected,
        submittedAtUtc: '2026-04-10T00:00:00Z',
        reviewedAtUtc: '2026-04-11T00:00:00Z',
        reviewedByUserKey: 'reviewer-1',
        reviewedByName: 'Mentor',
        reviewedByRole: 'Mentor',
        reviewNote: 'needs updates',
        auditTrail: []
      }
    ]));

    createComponent();
    fixture.detectChanges();

    expect(component.canSubmitBadge('badge-rejected')).toBeTrue();
    expect(component.getSubmitBadgeButtonLabel('badge-rejected')).toContain('знову');
  });

  it('openSkillsReviewFromDialog should navigate to kurin skills review route for reviewer', () => {
    memberServiceSpy.getByKey.and.returnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.canManageMemberActions = true;
    component.isAllSkillsDialogVisible = true;
    component.openSkillsReviewFromDialog();

    expect(component.isAllSkillsDialogVisible).toBeFalse();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/kurin', member.kurinKey, 'review', 'skills']);
  });

  it('approvePendingSkill should call review API with isApproved=true', () => {
    const badges: BadgeCatalogItemDto[] = [{
      id: 'badge-1',
      title: 'Badge A',
      imagePath: 'https://example.com/a.png',
      country: 'UA',
      specialization: 'Scout',
      status: 'active',
      level: 1,
      lastUpdated: '2026-04-01',
      seekerRequirements: '',
      instructorRequirements: '',
      fixNotes: []
    }];

    const progresses: BadgeProgressDto[] = [{
      badgeProgressKey: 'p-submitted',
      memberKey,
      kurinKey: member.kurinKey,
      badgeId: 'badge-1',
      status: BadgeProgressStatus.Submitted,
      submittedAtUtc: '2026-04-11T00:00:00Z',
      reviewedAtUtc: null,
      reviewedByUserKey: null,
      reviewedByName: null,
      reviewedByRole: null,
      reviewNote: null,
      auditTrail: []
    }];

    memberServiceSpy.getByKey.and.returnValue(of(member));
    badgesCatalogServiceSpy.getAll.and.returnValue(of(badges));
    memberProgressServiceSpy.getBadgeProgresses.and.returnValue(of(progresses));

    createComponent();
    fixture.detectChanges();

    component.canManageMemberActions = true;
    const confirmSpy = spyOn(component.confirmationService, 'confirm');

    component.approvePendingSkill(component.skillsSummary.pendingConfirmation[0]);

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(memberProgressServiceSpy.reviewBadgeProgress).toHaveBeenCalledWith(memberKey, 'badge-1', {
      isApproved: true,
      note: null
    });
  });

  it('removeConfirmedSkill should call review API with isApproved=false', () => {
    const badges: BadgeCatalogItemDto[] = [{
      id: 'badge-2',
      title: 'Badge B',
      imagePath: 'https://example.com/b.png',
      country: 'UA',
      specialization: 'Scout',
      status: 'active',
      level: 1,
      lastUpdated: '2026-04-01',
      seekerRequirements: '',
      instructorRequirements: '',
      fixNotes: []
    }];

    const progresses: BadgeProgressDto[] = [{
      badgeProgressKey: 'p-confirmed',
      memberKey,
      kurinKey: member.kurinKey,
      badgeId: 'badge-2',
      status: BadgeProgressStatus.Confirmed,
      submittedAtUtc: '2026-04-10T00:00:00Z',
      reviewedAtUtc: '2026-04-11T00:00:00Z',
      reviewedByUserKey: 'reviewer-1',
      reviewedByName: 'Mentor',
      reviewedByRole: 'Mentor',
      reviewNote: null,
      auditTrail: []
    }];

    memberServiceSpy.getByKey.and.returnValue(of(member));
    badgesCatalogServiceSpy.getAll.and.returnValue(of(badges));
    memberProgressServiceSpy.getBadgeProgresses.and.returnValue(of(progresses));

    createComponent();
    fixture.detectChanges();

    component.canManageMemberActions = true;
    const confirmSpy = spyOn(component.confirmationService, 'confirm').and.callFake((options) => {
      options.accept?.();
      return component.confirmationService;
    });

    component.removeConfirmedSkill(component.skillsSummary.recentConfirmed[0]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(memberProgressServiceSpy.reviewBadgeProgress).toHaveBeenCalledWith(memberKey, 'badge-2', {
      isApproved: false,
      note: null
    });
  });

  describe('гілка куреня', () => {
    const membershipIn = (branch: KurinBranch) => ({
      membershipKey: 'ms-1',
      kurinKey: member.kurinKey,
      kurinNumber: 7,
      branch,
      kurinNamedAfter: null,
      groupKey: null,
      groupName: null,
      kind: MembershipKind.Youth,
      joinedAtUtc: '2022-09-01T00:00:00Z',
      leftAtUtc: null,
      isCurrent: true
    });

    it('у курені УСП проби й вмілості не показуються і навіть не запитуються', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      memberServiceSpy.getMemberships.and.returnValue(of([membershipIn(KurinBranch.USP)]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBeFalse();
      expect(badgesCatalogServiceSpy.getAll).not.toHaveBeenCalled();
      expect(memberProgressServiceSpy.getProbeProgress).not.toHaveBeenCalled();
    });

    it('у юнацькому курені все на місці', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      memberServiceSpy.getMemberships.and.returnValue(of([membershipIn(KurinBranch.UPYu)]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBeTrue();
      expect(badgesCatalogServiceSpy.getAll).toHaveBeenCalled();
    });

    it('гілку бере членство в тому курені, де ми дивимось людину', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      memberServiceSpy.getMemberships.and.returnValue(of([
        { ...membershipIn(KurinBranch.UPYu), kurinKey: 'kurin-elsewhere', membershipKey: 'ms-2' },
        membershipIn(KurinBranch.USP)
      ]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBeFalse();
    });

    const renderedText = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

    it('старший пластун-впорядник у курені УПЮ — без юнацького й без жодних заглушок', () => {
      memberServiceSpy.getByKey.and.returnValue(of({
        ...member,
        latestPlastLevel: PlastLevel.Starshoplastun,
        plastLevelHistories: [{ plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2024-05-01' }]
      }));
      memberServiceSpy.getMemberships.and.returnValue(of([{ ...membershipIn(KurinBranch.UPYu), kind: MembershipKind.Staff }]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBeFalse();
      expect(badgesCatalogServiceSpy.getAll).not.toHaveBeenCalled();
      expect(memberProgressServiceSpy.getBadgeProgresses).not.toHaveBeenCalled();
      expect(memberProgressServiceSpy.getProbeProgress).not.toHaveBeenCalled();
      expect(renderedText()).not.toContain('Здобуті вмілості');
      expect(renderedText()).not.toContain('Відзначення УПЮ');
      expect(renderedText().toLowerCase()).not.toContain('скоро');
    });

    it('юнак у курені УПЮ бачить відзначення УПЮ, а впорядництва в нього немає', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      memberServiceSpy.getMemberships.and.returnValue(of([membershipIn(KurinBranch.UPYu)]));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('Відзначення УПЮ');
      expect(renderedText()).not.toContain('Впорядництво');
    });
  });

  describe('вкладка', () => {
    const renderedText = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    const dues = (over: Partial<MemberDuesDto>): MemberDuesDto => ({
      hasAccount: true,
      kurinKey: member.kurinKey,
      currency: 'UAH',
      numberLocale: 'uk-UA',
      currentQuarter: { year: 2026, number: 4 },
      balance: -300,
      quarterRate: { stanytsia: 240, krai: 0, kurin: 15, group: 45, total: 300 },
      isConcessionNow: false,
      currentGroupKey: member.groupKey,
      currentGroupName: 'Соколи',
      canOpenGroupDues: false,
      accounts: [],
      entries: [],
      ...over
    });

    it('показує борг і ставку за квартал, без кнопки каси для юнака', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      duesServiceSpy.getMemberDues.and.returnValue(of(dues({})));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('−300 ₴');
      expect(renderedText()).toContain('300 ₴ за квартал');
      expect(renderedText()).toContain('борг');
      expect(renderedText()).not.toContain('Каса гуртка');
    });

    it('сплачено — зелена плашка; впорядникові є кнопка в касу гуртка', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      duesServiceSpy.getMemberDues.and.returnValue(of(dues({ balance: 0, canOpenGroupDues: true })));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('Сплачено');
      expect(renderedText()).toContain('Каса гуртка');
    });

    // Чужа вкладка юнакові не показується: сервер відмовляє, і плитки просто немає.
    it('без доступу або без рахунку плитки немає', () => {
      memberServiceSpy.getByKey.and.returnValue(of(member));
      duesServiceSpy.getMemberDues.and.returnValue(throwError(() => ({ status: 403 })));

      createComponent();
      fixture.detectChanges();

      expect(component.dues).toBeNull();
      expect(renderedText()).not.toContain('за квартал');
    });
  });

  describe('впорядництво', () => {
    const renderedText = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

    it('показує чинні ролі КВ і закріплені гуртки, без юнацьких і минулих ролей', () => {
      memberServiceSpy.getByKey.and.returnValue(of({
        ...member,
        mentoredGroupNames: ['Gurtok 1', 'Gurtok 2'],
        leadershipHistories: [
          {
          leadershipHistoryKey: 'kv-now', leadershipKey: 'l-kv-now', role: 'Zvyazkovyi', leadershipType: 'KV',
          startDate: '2023-09-01', endDate: null,
          member: { memberKey, firstName: member.firstName, middleName: member.middleName, lastName: member.lastName }
        },
          {
          leadershipHistoryKey: 'kv-past', leadershipKey: 'l-kv-past', role: 'Vykhovnyk', leadershipType: 'KV',
          startDate: '2023-09-01', endDate: '2024-06-01',
          member: { memberKey, firstName: member.firstName, middleName: member.middleName, lastName: member.lastName }
        },
          {
          leadershipHistoryKey: 'group-now', leadershipKey: 'l-group-now', role: 'Hurtkoviy', leadershipType: 'Group',
          startDate: '2023-09-01', endDate: null,
          member: { memberKey, firstName: member.firstName, middleName: member.middleName, lastName: member.lastName }
        }
        ]
      }));

      createComponent();
      fixture.detectChanges();

      expect(component.staffOffices.map(o => o.leadershipHistoryKey)).toEqual(['kv-now']);
      expect(renderedText()).toContain('Впорядництво');
      expect(renderedText()).toContain("Зв'язковий");
      expect(renderedText()).toContain('Gurtok 1');
      expect(renderedText()).toContain('Gurtok 2');
    });

    it('впорядник лише із закріпленням теж має плитку', () => {
      memberServiceSpy.getByKey.and.returnValue(of({ ...member, mentoredGroupNames: ['Gurtok 1'] }));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('Впорядник гуртка:');
    });
  });

  describe('verification and перестороги', () => {
    function createAsReviewer(loaded: MemberDto) {
      memberServiceSpy.getByKey.and.returnValue(of(loaded));
      createComponent();
      spyOn(component.permissionService, 'isReviewer').and.returnValue(true);
      spyOn(component.permissionService, 'canManageWarnings').and.returnValue(true);
      fixture.detectChanges();
    }

    it('offers both to a reviewer who may edit the member', () => {
      createAsReviewer(member);
      expect(component.canManageProfileVerification).toBeTrue();
      expect(component.canManageWarnings).toBeTrue();
    });

    it('offers neither on the reviewer’s own card', () => {
      createAsReviewer({ ...member, memberKey: 'test-member-key' });
      expect(component.canManageProfileVerification).toBeFalse();
      expect(component.canManageWarnings).toBeFalse();
    });

    it('offers neither to someone who may not edit the member', () => {
      entityServiceSpy.checkEntityAccess.and.returnValue(of(false));
      createAsReviewer(member);
      expect(component.canManageProfileVerification).toBeFalse();
      expect(component.canManageWarnings).toBeFalse();
    });

    it('verifies the profile after asking', () => {
      memberServiceSpy.verifyProfile.and.returnValue(of({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent }));
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.Unverified });
      const confirm = spyOn(component.confirmationService, 'confirm').and.callFake(options => {
        options.accept?.();
        return component.confirmationService;
      });

      component.onProfileVerificationAction();

      expect(confirm).toHaveBeenCalled();
      expect(memberServiceSpy.verifyProfile).toHaveBeenCalledWith(memberKey, null);
      expect(component.isProfileVerified).toBeTrue();
    });

    it('removes the verification after asking', () => {
      memberServiceSpy.resetProfileVerification.and.returnValue(of({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.Unverified }));
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent });
      spyOn(component.confirmationService, 'confirm').and.callFake(options => {
        options.accept?.();
        return component.confirmationService;
      });

      component.onProfileVerificationAction();

      expect(memberServiceSpy.resetProfileVerification).toHaveBeenCalledWith(memberKey);
      expect(component.isProfileVerified).toBeFalse();
    });
  });
});
