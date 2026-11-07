import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MemberCardComponent } from './member-card';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from '@openng/optimus-ui/api';
import { MemberService } from '../../services/member-service/member.service';
import { MemberDto } from '../../models/member.dto';
import { MembershipDto } from '../../models/membership.dto';
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
import { MemberWarningDto } from '../../models/member-warning.dto';
import { MemberWarningLevel } from '../../models/enums/member-warning-level.enum';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { MemberDuesDto } from '../../../duesModule/models/group-dues.dto';
import { MemberAbilitiesDto, NO_ABILITIES } from '../../models/member-abilities.dto';
import { actInKurin, rememberKurinRoleNames } from '../../functions/leadership-role-display.function';

describe('MemberCardComponent', () => {
  let fixture: ComponentFixture<MemberCardComponent>;
  let component: MemberCardComponent;

  let memberServiceSpy: MockedObject<MemberService>;
  let kurinServiceSpy: MockedObject<KurinService>;
  let badgesCatalogServiceSpy: MockedObject<BadgesCatalogService>;
  let probesCatalogServiceSpy: MockedObject<ProbesCatalogService>;
  let memberProgressServiceSpy: MockedObject<MemberProgressService>;
  let badgeImageBlobServiceSpy: MockedObject<BadgeImageBlobService>;
  let authServiceSpy: MockedObject<AuthService>;
  let entityServiceSpy: MockedObject<EntityService>;
  let routerSpy: MockedObject<Router>;
  let duesServiceSpy: MockedObject<DuesService>;
  let paramMapSubject: BehaviorSubject<ParamMap>;
  let routeStub: { paramMap: Observable<ParamMap>; snapshot: { data: Record<string, unknown>; params: Record<string, string> } };

  const memberKey = 'abc123';
  /** What the kurin's defaults give a впорядник over his own гурток. */
  const leaderAbilities: MemberAbilitiesDto = {
    warnings: { give: [1, 2, 3], cancel: 'OwnIssued' },
    awards: { confirm: [1, 2, 3, 4], remove: false },
    probes: { sign: true, unsign: true },
    badges: { review: true },
    journal: false
  };
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
    memberServiceSpy = {
      getByKey: vi.fn(),
      getMemberships: vi.fn(),
      getAbilities: vi.fn(),
      verifyProfile: vi.fn(),
      resetProfileVerification: vi.fn()
    } as MockedObject<MemberService>;
    kurinServiceSpy = {
      getByKey: vi.fn(),
      getCardFields: vi.fn()
    } as MockedObject<KurinService>;
    kurinServiceSpy.getCardFields.mockReturnValue(of(DEFAULT_CARD_FIELDS));
    badgesCatalogServiceSpy = {
      getAll: vi.fn(),
      getForKurin: vi.fn()
    } as MockedObject<BadgesCatalogService>;
    probesCatalogServiceSpy = {
      getAll: vi.fn()
    } as MockedObject<ProbesCatalogService>;
    memberProgressServiceSpy = {
      getBadgeProgresses: vi.fn(),
      submitBadgeProgress: vi.fn(),
      reviewBadgeProgress: vi.fn(),
      getProbeProgress: vi.fn(),
      // No line route by default: the card falls back to the catalogue, as against an older API.
      getProbeLine: vi.fn()
    } as MockedObject<MemberProgressService>;
    memberProgressServiceSpy.getProbeLine.mockReturnValue(throwError(() => new Error('No line route')));
    badgeImageBlobServiceSpy = {
      resolveBadgeImageForDisplay: vi.fn()
    } as MockedObject<BadgeImageBlobService>;
    authServiceSpy = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    entityServiceSpy = {
      checkEntityAccess: vi.fn()
    } as MockedObject<EntityService>;
    routerSpy = {
      navigate: vi.fn()
    } as MockedObject<Router>;
    duesServiceSpy = {
      getMemberDues: vi.fn()
    } as MockedObject<DuesService>;
    duesServiceSpy.getMemberDues.mockReturnValue(of({ hasAccount: false } as MemberDuesDto));
    paramMapSubject = new BehaviorSubject(convertToParamMap({ memberKey }));
    routeStub = { paramMap: paramMapSubject.asObservable(), snapshot: { data: {}, params: { memberKey } } };

    badgeImageBlobServiceSpy.resolveBadgeImageForDisplay.mockImplementation((url: string | null) => url);
    authServiceSpy.getAuthStateValue.mockReturnValue({
      userKey: 'user-mentor',
      memberKey: 'test-member-key',
      email: 'mentor@example.com',
      isAdmin: false, permissions: ['Group:Update:OwnGroups', 'BadgeProgress:Update:OwnGroups'], roles: ['KV.Vykhovnyk'],
      kurinKey: member.kurinKey,
      accessToken: 'token'
    });
    memberServiceSpy.getAbilities.mockReturnValue(of(leaderAbilities));

    badgesCatalogServiceSpy.getAll.mockReturnValue(of([]));
    badgesCatalogServiceSpy.getForKurin.mockReturnValue(of([]));
    kurinServiceSpy.getByKey.mockReturnValue(of({
      kurinKey: member.kurinKey,
      number: 1,
      profileVerificationEnabled: true
    } as KurinDto));
    probesCatalogServiceSpy.getAll.mockReturnValue(of([]));
    memberProgressServiceSpy.getBadgeProgresses.mockReturnValue(of([]));
    memberProgressServiceSpy.getProbeProgress.mockReturnValue(of({
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
    entityServiceSpy.checkEntityAccess.mockReturnValue(of(true));
    memberProgressServiceSpy.submitBadgeProgress.mockReturnValue(of({
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
    memberProgressServiceSpy.reviewBadgeProgress.mockReturnValue(of({
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

    memberServiceSpy.getMemberships.mockReturnValue(of([]));

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
        { provide: ActivatedRoute, useValue: routeStub }
      ]
    }).compileComponents();
  });

  function createComponent() {
    fixture = TestBed.createComponent(MemberCardComponent);
    component = fixture.componentInstance;
  }

  it('should create', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should read memberKey from route params and call service', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();
    expect(component.memberKey).toBe(memberKey);
    expect(memberServiceSpy.getByKey).toHaveBeenCalledWith(memberKey);
    expect(memberProgressServiceSpy.getBadgeProgresses).toHaveBeenCalledWith(memberKey);
  });

  it('should build probe rows and keep a probe without points disabled', () => {
    const probes: ProbeSummaryDto[] = [
      { id: 'probe-1', title: 'Перша проба', pointsCount: 10, sectionsCount: 3 },
      { id: 'probe-2', title: 'Друга проба', pointsCount: 12, sectionsCount: 4 },
      { id: 'probe-3', title: 'Третя проба', pointsCount: 0, sectionsCount: 0 }
    ];

    memberServiceSpy.getByKey.mockReturnValue(of(member));
    probesCatalogServiceSpy.getAll.mockReturnValue(of(probes));
    memberProgressServiceSpy.getProbeProgress.mockImplementation((_mk, probeId) => of({
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
    expect(component.probeRows[0].isCompleted).toBe(true);
    expect(component.probeRows[2].isDisabled).toBe(true);
    expect(component.completedProbesCount).toBe(1);
  });

  it('openProbeDetails should navigate only for enabled probe rows', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
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
      lockedBehindTitle: null,
      hasNoPoints: false,
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
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();
    expect(component.member).toEqual(member);
  });

  it('should list the parents with something filled in, and none when the край hides them', () => {
    memberServiceSpy.getByKey.mockReturnValue(of({
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
    kurinServiceSpy.getCardFields.mockReturnValue(of({
      fields: { middleName: 'hidden', phoneNumber: 'required' },
      extra: [
        { key: 'visaType', label: 'Тип візи', type: 'text', required: true },
        { key: 'visaUntil', label: 'Віза до', type: 'date', required: false },
        { key: 'permit', label: 'Дозвіл', type: 'text', required: false }
      ]
    } as CardFieldsDto));
    memberServiceSpy.getByKey.mockReturnValue(of({ ...member, cardExtras: { visaType: 'D', visaUntil: '2027-06-30' } }));
    createComponent();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Тип візи: D');
    expect(text).toContain('Віза до: 30.06.2027');
    expect(text).not.toContain('Дозвіл');
    expect(text).not.toContain('По батькові не вказано');
  });

  it('should leave out what the kurin hides, and give the birthday without the year', () => {
    memberServiceSpy.getByKey.mockReturnValue(of({
      ...member,
      email: '',
      phoneNumber: '',
      dateOfBirth: null,
      birthday: '05-17',
      address: null,
      school: null,
      hiddenData: ['contacts', 'birthDate', 'address', 'parents', 'extras']
    } as MemberDto));
    createComponent();
    fixture.detectChanges();

    const text = ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
    expect(text).not.toContain('Email:');
    expect(text).not.toContain('Телефон:');
    expect(text).toContain('Дата народження: 17.05');
    expect(text).not.toContain('Адреса:');
    expect(text).not.toContain('Мама:');
    expect(text).not.toContain('Не вказано');
  });

  it('should leave out the birth date when the kurin hides even the birthday', () => {
    memberServiceSpy.getByKey.mockReturnValue(of({
      ...member, dateOfBirth: null, birthday: null, hiddenData: ['birthDate', 'birthday']
    } as MemberDto));
    createComponent();
    fixture.detectChanges();

    const text = ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
    expect(text).not.toContain('Дата народження:');
  });

  it('should still say «Не вказано» for what is simply empty', () => {
    memberServiceSpy.getByKey.mockReturnValue(of({ ...member, email: '', dateOfBirth: null, hiddenData: [] } as MemberDto));
    createComponent();
    fixture.detectChanges();

    const text = ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
    expect(text).toContain('Email: Не вказано');
  });

  it('should display profile verification timestamp without timezone as local time from UTC', () => {
    memberServiceSpy.getByKey.mockReturnValue(of({
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
    memberServiceSpy.getByKey.mockReturnValue(throwError(() => new Error('Not found')));
    createComponent();
    fixture.detectChanges();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/panel'], { replaceUrl: true });
  });

  it('should navigate to group page on load error when existing member with groupKey set', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();
    memberServiceSpy.getByKey.mockReturnValue(throwError(() => new Error('Network')));
    component.member = { ...member };
    component.refreshData();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/group', member.groupKey], { replaceUrl: true });
  });

  it('onEditMember should navigate to edit route', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
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
    memberServiceSpy.getByKey.mockReturnValue(of(grouplessMember));
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
    memberServiceSpy.getByKey.mockReturnValue(of({ ...member, groupKey: emptyGuid, kurinKey: emptyGuid }));
    createComponent();
    fixture.detectChanges();
    routerSpy.navigate.mockClear();
    component.onEditMember();
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('should fall back to panel on load error when the group key is an empty guid', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();
    memberServiceSpy.getByKey.mockReturnValue(throwError(() => new Error('Network')));
    component.member = { ...member, groupKey: '00000000-0000-0000-0000-000000000000' };
    component.refreshData();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/panel'], { replaceUrl: true });
  });

  it('refreshData should use latest paramMap value if it changes', () => {
    memberServiceSpy.getByKey.mockReturnValueOnce(of(member)).mockReturnValueOnce(of({ ...member, memberKey: 'newKey' }));
    memberProgressServiceSpy.getBadgeProgresses.mockReturnValueOnce(of([])).mockReturnValueOnce(of([]));
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

    memberServiceSpy.getByKey.mockReturnValue(of(member));
    badgesCatalogServiceSpy.getAll.mockReturnValue(of(badges));
    memberProgressServiceSpy.getBadgeProgresses.mockReturnValue(of(progresses));

    createComponent();
    fixture.detectChanges();

    expect(component.skillsSummary.orderedPreview.map(skill => skill.badgeId)).toEqual(['badge-1', 'badge-2']);
    expect(component.pendingSkillsCount).toBe(1);
  });

  it('openAllSkillsDialog should set dialog visibility to true', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.openAllSkillsDialog();

    expect(component.isAllSkillsDialogVisible).toBe(true);
  });

  it('openAddSkillDialog should open dialog and reset search state', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.addSkillSearchTerm = 'старий пошук';
    component.addSkillVisibleCount = 99;
    component.openAddSkillDialog();

    expect(component.isAddSkillDialogVisible).toBe(true);
    expect(component.addSkillSearchTerm).toBe('');
    expect(component.addSkillVisibleCount).toBe(component.addSkillPageSize);
  });

  it('filteredAddSkillCandidates should filter by title and reset visible count on search change', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    badgesCatalogServiceSpy.getAll.mockReturnValue(of([
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
    badgesCatalogServiceSpy.getForKurin.mockReturnValue(of([
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
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();

    const initial = component.addSkillVisibleCount;
    component.loadMoreAddSkillCandidates();

    expect(component.addSkillVisibleCount).toBe(initial + component.addSkillPageSize);
  });

  it('submitBadge should call API and close add-skill dialog on success', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.isAddSkillDialogVisible = true;
    component.submitBadge('badge-new');

    expect(memberProgressServiceSpy.submitBadgeProgress).toHaveBeenCalledWith(memberKey, 'badge-new', { note: null });
    expect(component.isAddSkillDialogVisible).toBe(false);
    expect(component.addSkillSuccessMessage).toContain('подана');
  });

  it('canSubmitBadge should allow re-submission for rejected badge', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    memberProgressServiceSpy.getBadgeProgresses.mockReturnValue(of([
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

    expect(component.canSubmitBadge('badge-rejected')).toBe(true);
    expect(component.getSubmitBadgeButtonLabel('badge-rejected')).toContain('знову');
  });

  it('openSkillsReviewFromDialog should navigate to kurin skills review route for reviewer', () => {
    memberServiceSpy.getByKey.mockReturnValue(of(member));
    createComponent();
    fixture.detectChanges();

    component.canManageMemberActions = true;
    component.isAllSkillsDialogVisible = true;
    component.openSkillsReviewFromDialog();

    expect(component.isAllSkillsDialogVisible).toBe(false);
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

    memberServiceSpy.getByKey.mockReturnValue(of(member));
    badgesCatalogServiceSpy.getAll.mockReturnValue(of(badges));
    memberProgressServiceSpy.getBadgeProgresses.mockReturnValue(of(progresses));

    createComponent();
    fixture.detectChanges();

    component.canManageMemberActions = true;
    const confirmSpy = vi.spyOn(component.confirmationService, 'confirm').mockReturnThis();

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

    memberServiceSpy.getByKey.mockReturnValue(of(member));
    badgesCatalogServiceSpy.getAll.mockReturnValue(of(badges));
    memberProgressServiceSpy.getBadgeProgresses.mockReturnValue(of(progresses));

    createComponent();
    fixture.detectChanges();

    component.canManageMemberActions = true;
    const confirmSpy = vi.spyOn(component.confirmationService, 'confirm').mockImplementation((options) => {
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

  describe('пластовий життєпис', () => {
    const seniorHere = {
      membershipKey: 'ms-now',
      kurinKey: member.kurinKey,
      kurinNumber: 5,
      branch: KurinBranch.USP,
      kurinNamedAfter: 'Орден Хрестоносців',
      groupKey: null,
      groupName: null,
      kind: MembershipKind.Youth,
      joinedAtUtc: '2022-09-15T00:00:00Z',
      leftAtUtc: null,
      isCurrent: true
    };
    const youthBefore = { ...seniorHere, membershipKey: 'ms-before', kurinKey: 'kurin-youth', kurinNumber: 1, branch: KurinBranch.UPYu, kurinNamedAfter: 'Івана Богуна', leftAtUtc: '2022-06-30T00:00:00Z', isCurrent: false };
    const senior: MemberDto = {
      ...member,
      plastLevelHistories: [
        { plastLevel: PlastLevel.Uchasnyk, dateAchieved: '2016-10-08' },
        { plastLevel: PlastLevel.Skob, dateAchieved: '2020-07-18' },
        { plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2022-10-01' }
      ],
      latestPlastLevel: PlastLevel.Starshoplastun,
      leadershipHistories: [
        { leadershipHistoryKey: 'h1', leadershipKey: 'l1', role: 'Pysar', leadershipType: 'Kurin', kurinKey: 'kurin-youth', startDate: '2020-09-01', endDate: '2022-06-30', member: null as never }
      ]
    };

    function openLife(asZvyazkovyi: boolean): void {
      routeStub.snapshot.data = { view: 'life' };
      authServiceSpy.getAuthStateValue.mockReturnValue({
        userKey: 'user-zv',
        memberKey: 'someone-else',
        email: 'zv@example.com',
        isAdmin: false,
        permissions: asZvyazkovyi ? ['Group:Manage:KurinWide', 'Member:Update:KurinWide'] : ['Group:Update:OwnGroups'],
        roles: [],
        kurinKey: member.kurinKey,
        accessToken: 'token'
      });
      memberServiceSpy.getByKey.mockReturnValue(of(senior));
      memberServiceSpy.getMemberships.mockReturnValue(of([seniorHere, youthBefore]));
      createComponent();
      fixture.detectChanges();
    }

    it('показує юнацьке старшого, якого картка не показує, і запитує його', () => {
      openLife(true);

      expect(component.hasYouthProgram).toBe(false);
      expect(component.showsYouthRecord).toBe(true);
      expect(memberProgressServiceSpy.getBadgeProgresses).toHaveBeenCalledWith(memberKey);
      expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="life-head"]')).not.toBeNull();
      expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="senior-kurin-tile"]')).toBeNull();
    });

    it('розкладає ступені по етапах і називає ролі куренем, де їх тримали', () => {
      openLife(true);

      expect(component.lifeStages.map(stage => [stage.branch, stage.levels.map(level => level.title)])).toEqual([
        ['УПЮ', ['пл. уч.', 'пл. скоб']],
        ['УСП', ['ст. пл.']]
      ]);
      expect(component.lifeOffices.map(office => office.kurin)).toEqual(['к. ч. 1']);
    });

    it('юнацьке старшого виправляє лише Звʼязковий куреня, де той стоїть зараз', () => {
      openLife(true);
      expect(component.youthRecordLocked).toBe(false);
    });

    it('впорядникові юнацьке старшого лише для перегляду', () => {
      openLife(false);
      expect(component.youthRecordLocked).toBe(true);
    });

    it('відзначення УПЮ старшого Звʼязковий не додає й не править, лише розглядає', () => {
      openLife(true);
      expect(component.youthRecordLocked).toBe(false);
      expect(component.canEditYouthAwards).toBe(false);
    });

    it('поки юнацьке членство в курені УПЮ відкрите, людина ще юнак', () => {
      routeStub.snapshot.data = { view: 'life' };
      memberServiceSpy.getByKey.mockReturnValue(of(senior));
      memberServiceSpy.getMemberships.mockReturnValue(of([seniorHere, { ...youthBefore, leftAtUtc: null, isCurrent: true }]));
      createComponent();
      fixture.detectChanges();

      expect(component.isClosedYouthRecord).toBe(false);
      expect(component.youthRecordLocked).toBe(false);
    });

    it('невдале збереження відзначення показує помилку', () => {
      openLife(true);
      vi.spyOn(component.memberAwardService, 'upsertAward').mockReturnValue(throwError(() => new Error('boom')));
      const add = vi.spyOn(component.messageService, 'add');

      component.onSaveAward({} as never);

      expect(add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error', summary: 'Не вдалося зберегти відзначення' }));
    });

    it('у юнака лише етап УПЮ', () => {
      routeStub.snapshot.data = { view: 'life' };
      memberServiceSpy.getByKey.mockReturnValue(of({ ...member, plastLevelHistories: [{ plastLevel: PlastLevel.Uchasnyk, dateAchieved: '2024-10-01' }] }));
      createComponent();
      fixture.detectChanges();

      expect(component.lifeStages.map(stage => stage.branch)).toEqual(['УПЮ']);
      expect(component.youthRecordLocked).toBe(false);
    });

    it('посилання під тегами веде в життєпис', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      createComponent();
      fixture.detectChanges();

      const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('[data-testid="member-life"]')!;
      expect(link.getAttribute('href')).toBe(`/member/${memberKey}/life`);
      link.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', memberKey, 'life']);
    });

    it('лишає браузерові клік із Ctrl — життєпис відкривається в новій вкладці', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      createComponent();
      fixture.detectChanges();

      const click = new MouseEvent('click', { cancelable: true, button: 0, ctrlKey: true });
      component.openLife(click);
      expect(click.defaultPrevented).toBe(false);
      expect(routerSpy.navigate).not.toHaveBeenCalled();
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
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      memberServiceSpy.getMemberships.mockReturnValue(of([membershipIn(KurinBranch.USP)]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBe(false);
      expect(badgesCatalogServiceSpy.getAll).not.toHaveBeenCalled();
      expect(memberProgressServiceSpy.getProbeProgress).not.toHaveBeenCalled();
    });

    it('у курені УСП картка своя: курінь і роль, відзначення, перестороги — без юнацьких плиток і «Впорядництва»', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...member,
        leadershipHistories: [
          { leadershipHistoryKey: 'h1', leadershipKey: 'l1', role: 'Pysar', leadershipType: 'Kurin', startDate: '2025-09-01', endDate: null, member: null as never },
          { leadershipHistoryKey: 'h2', leadershipKey: 'l2', role: 'Hurtkoviy', leadershipType: 'Group', startDate: '2025-09-01', endDate: null, member: null as never }
        ]
      }));
      memberServiceSpy.getMemberships.mockReturnValue(of([{
        ...membershipIn(KurinBranch.USP),
        kurinNamedAfter: 'Орден Хрестоносців',
        numbering: [{ branch: KurinBranch.USP, number: 5 }, { branch: KurinBranch.UPS, number: 20 }]
      }]));

      createComponent();
      fixture.detectChanges();

      const element = fixture.nativeElement as HTMLElement;
      expect(component.isSeniorKurinCard).toBe(true);
      expect(component.seniorKurinName).toBe('Орден Хрестоносців');
      expect(component.seniorKurinBranches).toBe('5-й курінь УСП · 20-й курінь УПС');
      expect(component.currentOffices.map(o => o.role)).toEqual(['Pysar']);
      expect(element.querySelector('[data-testid="senior-kurin-tile"]')).not.toBeNull();
      expect(element.querySelector('[data-testid="senior-awards-tile"]')).not.toBeNull();
      expect(element.querySelector('[data-testid="senior-warnings-tile"]')).not.toBeNull();
    });

    it('ролі з іншого куреня на картці немає, а свою курінь називає своїм словом', () => {
      rememberKurinRoleNames('kurin1', { Pysar: 'Скриптор' });
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...member,
        leadershipHistories: [
          { leadershipHistoryKey: 'h1', leadershipKey: 'l1', role: 'Pysar', leadershipType: 'Kurin', kurinKey: 'kurin1', startDate: '2025-09-01', endDate: null, member: null as never },
          { leadershipHistoryKey: 'h2', leadershipKey: 'l2', role: 'Kurinnuy', leadershipType: 'Kurin', kurinKey: 'kurin2', startDate: '2025-09-01', endDate: null, member: null as never }
        ]
      }));
      memberServiceSpy.getMemberships.mockReturnValue(of([membershipIn(KurinBranch.USP)]));

      createComponent();
      fixture.detectChanges();

      expect(component.currentOffices.map(o => o.role)).toEqual(['Pysar']);
      expect(component.officeLabel(component.currentOffices[0])).toBe('Скриптор');
      actInKurin(null);
    });

    it('у юнацькому курені все на місці', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      memberServiceSpy.getMemberships.mockReturnValue(of([membershipIn(KurinBranch.UPYu)]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBe(true);
      expect(component.isSeniorKurinCard).toBe(false);
      expect(badgesCatalogServiceSpy.getAll).toHaveBeenCalled();
    });

    it('гілку бере членство в тому курені, де ми дивимось людину', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      memberServiceSpy.getMemberships.mockReturnValue(of([
        { ...membershipIn(KurinBranch.UPYu), kurinKey: 'kurin-elsewhere', membershipKey: 'ms-2' },
        membershipIn(KurinBranch.USP)
      ]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBe(false);
    });

    const renderedText = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

    it('старший пластун-впорядник у курені УПЮ — без юнацького й без жодних заглушок', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...member,
        latestPlastLevel: PlastLevel.Starshoplastun,
        plastLevelHistories: [{ plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2024-05-01' }]
      }));
      memberServiceSpy.getMemberships.mockReturnValue(of([{ ...membershipIn(KurinBranch.UPYu), kind: MembershipKind.Staff }]));

      createComponent();
      fixture.detectChanges();

      expect(component.hasYouthProgram).toBe(false);
      expect(badgesCatalogServiceSpy.getAll).not.toHaveBeenCalled();
      expect(memberProgressServiceSpy.getBadgeProgresses).not.toHaveBeenCalled();
      expect(memberProgressServiceSpy.getProbeProgress).not.toHaveBeenCalled();
      expect(renderedText()).not.toContain('Здобуті вмілості');
      expect(renderedText()).not.toContain('Відзначення УПЮ');
      expect(renderedText().toLowerCase()).not.toContain('скоро');
    });

    describe('сеніор, що веде курінь УПЮ', () => {
      const zvyazkovyi = {
        ...member,
        latestPlastLevel: PlastLevel.Senior,
        plastLevelHistories: [
          { plastLevel: PlastLevel.Skob, dateAchieved: '2002-06-01' },
          { plastLevel: PlastLevel.Senior, dateAchieved: '2015-04-26' }
        ]
      };

      function openAs(memberKey: string): void {
        authServiceSpy.getAuthStateValue.mockReturnValue({
          userKey: 'user-viewer',
          memberKey,
          email: 'viewer@example.com',
          isAdmin: false,
          permissions: ['Group:Manage:KurinWide', 'Member:Update:KurinWide'],
          roles: [],
          kurinKey: member.kurinKey,
          accessToken: 'token'
        });
        memberServiceSpy.getByKey.mockReturnValue(of(zvyazkovyi));
        memberServiceSpy.getMemberships.mockReturnValue(of([{ ...membershipIn(KurinBranch.UPYu), kind: MembershipKind.Staff }]));
        createComponent();
        fixture.detectChanges();
      }

      it('показує поточний ступінь — сеніора, а не скоба 2002 року', () => {
        openAs(member.memberKey);

        const highest = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="degree-highest"]');
        expect(highest?.textContent).toContain('пл. сен.');
        expect(highest?.textContent).not.toContain('скоб');
      });

      it('веде свої старші відзначення сам, і в курені УПЮ', () => {
        openAs(member.memberKey);

        const element = fixture.nativeElement as HTMLElement;
        expect(component.isSeniorKurinCard).toBe(false);
        expect(component.showsSeniorAwards).toBe(true);
        expect(element.querySelector('[data-testid="senior-awards-tile"]')).not.toBeNull();
        expect(element.querySelector('[data-testid="senior-award-add"]')).not.toBeNull();
      });

      it('провід куреня УПЮ бачить його старші відзначення, але не вносить їх', () => {
        openAs('someone-else');

        const element = fixture.nativeElement as HTMLElement;
        expect(element.querySelector('[data-testid="senior-awards-tile"]')).not.toBeNull();
        expect(element.querySelector('[data-testid="senior-award-add"]')).toBeNull();
      });
    });

    it('юнак у курені УПЮ бачить відзначення УПЮ, а впорядництва в нього немає', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      memberServiceSpy.getMemberships.mockReturnValue(of([membershipIn(KurinBranch.UPYu)]));

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
      owedToLeave: 0,
      quarterRate: { stanytsia: 240, krai: 0, kurin: 15, group: 45, total: 300 },
      isConcessionNow: false,
      concessionNamesNow: [],
      currentGroupKey: member.groupKey,
      currentGroupName: 'Соколи',
      canOpenGroupDues: false,
      accounts: [],
      entries: [],
      ...over
    });

    it('показує борг і ставку за квартал, без кнопки каси для юнака', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      duesServiceSpy.getMemberDues.mockReturnValue(of(dues({})));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('−300 ₴');
      expect(renderedText()).toContain('300 ₴ за квартал');
      expect(renderedText()).toContain('борг');
      expect(renderedText()).not.toContain('Каса гуртка');
    });

    it('сплачено — зелена плашка; впорядникові є кнопка в касу гуртка', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      duesServiceSpy.getMemberDues.mockReturnValue(of(dues({ balance: 0, canOpenGroupDues: true })));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('Сплачено');
      expect(renderedText()).toContain('Каса гуртка');
    });

    // Борг з людиною не йде: «Вивести» пояснює, що спершу треба сплатити, і не пропонує вивести.
    it('із боргом «Вивести» каже про вкладку замість підтвердження', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      duesServiceSpy.getMemberDues.mockReturnValue(of(dues({ balance: -600, owedToLeave: 300 })));
      createComponent();
      fixture.detectChanges();
      const confirm = vi.spyOn(component.confirmationService, 'confirm').mockReturnThis();

      component.confirmLeaveKurin({ kurinKey: member.kurinKey, kurinNumber: 1 } as MembershipDto);

      const options = confirm.mock.calls[0][0];
      expect(options.header).toBe('Спершу — вкладка');
      expect(options.message).toContain('300 ₴');
      expect(options.rejectVisible).toBe(false);
      expect(options.accept).toBeUndefined();
    });

    it('без боргу «Вивести» питає підтвердження', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      duesServiceSpy.getMemberDues.mockReturnValue(of(dues({ balance: 0 })));
      createComponent();
      fixture.detectChanges();
      const confirm = vi.spyOn(component.confirmationService, 'confirm').mockReturnThis();

      component.confirmLeaveKurin({ kurinKey: member.kurinKey, kurinNumber: 1 } as MembershipDto);

      expect(confirm.mock.calls[0][0].header).toBe('Вивести з куреня');
    });

    it('старший курінь у підтвердженні й на відзначеннях названо його іменем, юнацький — номером', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      duesServiceSpy.getMemberDues.mockReturnValue(of(dues({ balance: 0 })));
      const orden = { kurinKey: member.kurinKey, kurinNumber: 5, branch: KurinBranch.USP, kurinNamedAfter: 'Орден Хрестоносців' } as MembershipDto;
      const upyu = { kurinKey: 'kurin9', kurinNumber: 47, branch: KurinBranch.UPYu, kurinNamedAfter: 'Івана Богуна' } as MembershipDto;
      memberServiceSpy.getMemberships.mockReturnValue(of([orden, upyu]));
      createComponent();
      fixture.detectChanges();
      const confirm = vi.spyOn(component.confirmationService, 'confirm').mockReturnThis();

      component.confirmLeaveKurin(orden);

      expect(confirm.mock.calls[0][0].message).toContain('Членство (Орден Хрестоносців)');
      expect(component.kurinStamps).toEqual({ [member.kurinKey]: 'Орден Хрестоносців', kurin9: 'к. ч. 47' });
    });

    // Чужа вкладка юнакові не показується: сервер відмовляє, і плитки просто немає.
    it('без доступу або без рахунку плитки немає', () => {
      memberServiceSpy.getByKey.mockReturnValue(of(member));
      duesServiceSpy.getMemberDues.mockReturnValue(throwError(() => ({ status: 403 })));

      createComponent();
      fixture.detectChanges();

      expect(component.dues).toBeNull();
      expect(renderedText()).not.toContain('за квартал');
    });
  });

  describe('впорядництво', () => {
    const renderedText = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

    it('показує чинні ролі КВ і закріплені гуртки, без юнацьких і минулих ролей', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
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
      memberServiceSpy.getByKey.mockReturnValue(of({ ...member, mentoredGroupNames: ['Gurtok 1'] }));

      createComponent();
      fixture.detectChanges();

      expect(renderedText()).toContain('Впорядник гуртка:');
    });
  });

  describe('verification and перестороги', () => {
    const activeWarning: MemberWarningDto = {
      memberWarningKey: 'w-1',
      memberKey,
      level: MemberWarningLevel.Level1,
      issuedAtUtc: '2026-01-01T00:00:00Z',
      expiresAtUtc: '2999-01-01T00:00:00Z',
      issuedByUserKey: 'someone'
    };

    const headButtons = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.member-actions button'));
    const headButtonLabels = () => headButtons().map(button => button.textContent?.trim() ?? '').filter(text => !!text);
    const headButton = (ariaLabel: string) => headButtons().find(button => button.getAttribute('aria-label') === ariaLabel) ?? null;
    const menuLabels = () => component.moreMenuItems.map(item => item.label);

    function createAsReviewer(loaded: MemberDto) {
      memberServiceSpy.getByKey.mockReturnValue(of(loaded));
      createComponent();
      vi.spyOn(component.permissionService, 'isReviewer').mockReturnValue(true);
      fixture.detectChanges();
    }

    it('offers both to a reviewer who may edit the member', () => {
      createAsReviewer(member);
      expect(component.canManageProfileVerification).toBe(true);
      expect(component.canManageWarnings).toBe(true);
    });

    it('offers neither on the reviewer’s own card', () => {
      createAsReviewer({ ...member, memberKey: 'test-member-key' });
      expect(component.canManageProfileVerification).toBe(false);
      expect(component.canManageWarnings).toBe(false);
    });

    it('offers neither to someone who may not edit the member', () => {
      entityServiceSpy.checkEntityAccess.mockReturnValue(of(false));
      memberServiceSpy.getAbilities.mockReturnValue(of(NO_ABILITIES));
      createAsReviewer(member);
      expect(component.canManageProfileVerification).toBe(false);
      expect(component.canManageWarnings).toBe(false);
    });

    it('offers перестороги by the kurin policy, even without the right to edit the card', () => {
      entityServiceSpy.checkEntityAccess.mockReturnValue(of(false));
      memberServiceSpy.getAbilities.mockReturnValue(of({ ...NO_ABILITIES, warnings: { give: [1], cancel: 'OwnIssued' } }));
      createAsReviewer(member);

      expect(component.canEditMember).toBe(false);
      expect(component.canManageWarnings).toBe(true);
      expect(menuLabels()).toEqual(['Перестороги']);
      expect(headButton('Ще')).not.toBeNull();
    });

    it('offers the journal filtered to the member when the kurin lets the viewer read it', () => {
      memberServiceSpy.getAbilities.mockReturnValue(of({ ...leaderAbilities, journal: true }));
      createAsReviewer(member);

      expect(component.journalKurinKey).toBe(member.kurinKey);
      component.openJournal();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/kurin', member.kurinKey, 'journal'], { queryParams: { member: memberKey } });
    });

    it('hides the journal from whoever may not read it', () => {
      createAsReviewer(member);

      expect(component.journalKurinKey).toBeNull();
      expect(menuLabels()).not.toContain('Журнал дій');
    });

    it('keeps «Редагувати» as the one main action and the rarer ones under «Ще»', () => {
      memberServiceSpy.getAbilities.mockReturnValue(of({ ...leaderAbilities, journal: true }));
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.Unverified });

      expect(headButtonLabels()).toEqual(['Редагувати', 'Верифікувати']);
      expect(headButton('Ще')).not.toBeNull();
      expect(menuLabels()).toEqual(['Перестороги', 'Журнал дій']);
    });

    it('moves verification under «Ще» once the profile is verified', () => {
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent });

      expect(headButtonLabels()).toEqual(['Редагувати']);
      expect(menuLabels()[0]).toBe('Зняти верифікацію');
    });

    it('asks to verify again when the data changed after verification', () => {
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.VerifiedStale });

      expect(headButtonLabels()).toContain('Верифікувати');
      expect(menuLabels()).not.toContain('Зняти верифікацію');
    });

    it('shows no «Ще» and no buttons to whoever may do nothing to the member', () => {
      entityServiceSpy.checkEntityAccess.mockReturnValue(of(false));
      memberServiceSpy.getAbilities.mockReturnValue(of(NO_ABILITIES));
      createAsReviewer(member);

      expect(component.hasHeadActions).toBe(false);
      expect((fixture.nativeElement as HTMLElement).querySelector('.member-actions')).toBeNull();
    });

    it('opens перестороги from the dots for whoever gives them', () => {
      createAsReviewer({ ...member, warnings: [activeWarning] });

      const dots = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.member-warning-indicator');
      expect(dots?.getAttribute('aria-label')).toBe('Перестороги');
      dots!.click();
      expect(component.isWarningsDialogVisible).toBe(true);
    });

    it('keeps the dots a mark for whoever does not give перестороги', () => {
      memberServiceSpy.getAbilities.mockReturnValue(of(NO_ABILITIES));
      createAsReviewer({ ...member, warnings: [activeWarning] });

      const host = fixture.nativeElement as HTMLElement;
      expect(host.querySelector('.member-warning-indicator')).not.toBeNull();
      expect(host.querySelector('button.member-warning-indicator')).toBeNull();
    });

    it('verifies the profile after asking', () => {
      memberServiceSpy.verifyProfile.mockReturnValue(of({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent }));
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.Unverified });
      const confirm = vi.spyOn(component.confirmationService, 'confirm').mockImplementation(options => {
        options.accept?.();
        return component.confirmationService;
      });

      component.onProfileVerificationAction();

      expect(confirm).toHaveBeenCalled();
      expect(memberServiceSpy.verifyProfile).toHaveBeenCalledWith(memberKey, null);
      expect(component.isProfileVerified).toBe(true);
    });

    it('removes the verification after asking', () => {
      memberServiceSpy.resetProfileVerification.mockReturnValue(of({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.Unverified }));
      createAsReviewer({ ...member, profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent });
      vi.spyOn(component.confirmationService, 'confirm').mockImplementation(options => {
        options.accept?.();
        return component.confirmationService;
      });

      component.onProfileVerificationAction();

      expect(memberServiceSpy.resetProfileVerification).toHaveBeenCalledWith(memberKey);
      expect(component.isProfileVerified).toBe(false);
    });
  });
});
