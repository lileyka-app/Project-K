import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { ProgressBarModule } from '@openng/optimus-ui/progressbar';
import { MemberDto } from '../../models/member.dto';
import { MemberService } from '../../services/member-service/member.service';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { catchError, finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { SelectModule } from '@openng/optimus-ui/select';
import { MessageService } from '@openng/optimus-ui/api';
import { BadgesCatalogService } from '../../services/probes-and-badges/badges-catalog.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { ProbesCatalogService } from '../../services/probes-and-badges/probes-catalog.service';
import { buildMemberSkillsSummary, normalizeBadgeProgressStatus, resolveBadgeImageUrl } from '../../functions/member-skills-view-mapper.function';
import { buildMemberProbeRows } from '../../functions/member-probe-rows-view-mapper.function';
import { MemberSkillsSummaryView } from '../../models/probes-and-badges/member-skills-summary.view';
import { BadgeCatalogItemDto } from '../../models/probes-and-badges/badge-catalog-item.dto';
import { BadgeProgressDto } from '../../models/probes-and-badges/badge-progress.dto';
import { ProbeSummaryDto } from '../../models/probes-and-badges/probe-summary.dto';
import { ProbeProgressDto } from '../../models/probes-and-badges/probe-progress.dto';
import { MemberProbeRowView } from '../../models/probes-and-badges/member-probe-row.view';
import { MemberSkillItemView } from '../../models/probes-and-badges/member-skill-item.view';
import { BadgeProgressStatus } from '../../models/enums/badge-progress-status.enum';
import { ProbeProgressStatus } from '../../models/enums/probe-progress-status.enum';
import { SkillMiniCardComponent } from './components/skill-mini-card/skill-mini-card';
import { BadgeDetailsDialogComponent } from '../../components/badge-details-dialog/badge-details-dialog';
import { BadgeImageBlobService } from '../../services/probes-and-badges/badge-image-blob.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb-service/breadcrumb.service';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { MemberWarningDto } from '../../models/member-warning.dto';
import { MemberWarningLevel } from '../../models/enums/member-warning-level.enum';
import { MemberAwardsTileComponent } from './components/member-awards-tile/member-awards-tile';
import { MemberMembershipsTileComponent } from './components/member-memberships-tile/member-memberships-tile';
import { MembershipDto } from '../../models/membership.dto';
import { hasYouthProgram, KurinBranch } from '../../models/enums/kurin-branch.enum';
import { LeadershipHistoryDto } from '../../models/requests/leadership/leadership.dto';
import { leadershipRoleDisplayName, leadershipRoleSeverityForRole, RoleSeverity } from '../../functions/leadership-role-display.function';
import { memberBranchHere } from '../../functions/member-branch.function';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { MemberDuesDto } from '../../../duesModule/models/group-dues.dto';
import { duesAmountLabel, money, quarterLabel } from '../../../duesModule/functions/dues-format.function';
import { DUES_ENTRY_KIND_LABELS, DUES_STANDING_LABELS } from '../../../duesModule/models/dues.enums';
import { DuesEntryDto } from '../../../duesModule/models/group-dues.dto';
import { MemberAwardService, UpsertMemberAwardRequest } from '../../services/member-award-service/member-award.service';
import { EntityService } from '../../../authModule/services/entity-service/entity.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { getBadgeProgressStatusLabel, getProbeProgressStatusLabel } from '../../functions/progress-status-labels.function';
import { isUsableKey } from '../../../../shared/functions/is-usable-key.function';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { cardExtraLines, cardFieldMode, CardFieldsDto, DEFAULT_CARD_FIELDS, StandardCardField } from '../../models/card-fields.dto';
import { GroupService } from '../../services/group-service/group.service';
import { MembershipService } from '../../services/membership-service/membership.service';
import { GroupDto } from '../../models/group.dto';
import { ProfileVerificationBadgeComponent } from '../../components/profile-verification-badge/profile-verification-badge';
import { formatUtcDateTime, parseUtcDateTime } from '../../../../shared/functions/utc-date-time.function';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { emailHref, phoneHref } from '../../../../shared/functions/contact-href.function';
import { formatPhone } from '../../../../shared/functions/phone.function';
import { TileBoardComponent } from '../../../../shared/tile-board/tile-board';
import { TileDefDirective } from '../../../../shared/tile-board/tile-def.directive';
import { kurinLabel, kurinName } from '../../../../shared/functions/kurin-label.function';
import { initials } from '../../../dashboardModule/functions/greeting.function';
import { MemberWarningsDialogComponent } from './components/member-warnings-dialog/member-warnings-dialog';
import { MemberProfileVerificationStatus } from '../../models/enums/member-profile-verification-status.enum';

@Component({
  selector: 'app-member-card',
  imports: [
    DatePipe,
    SkeletonModule,
    ProgressBarModule,
    ButtonModule,
    TagModule,
    DialogModule,
    ConfirmDialogModule,
    FormsModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
    SelectModule,
    TooltipModule,
    SkillMiniCardComponent,
    BadgeDetailsDialogComponent,
    MemberAwardsTileComponent,
    MemberMembershipsTileComponent,
    ProfileVerificationBadgeComponent,
    MemberWarningsDialogComponent,
    TileBoardComponent,
    TileDefDirective,
    RouterLink
  ],
  providers: [ConfirmationService],
  templateUrl: './member-card.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-card.css'
})
export class MemberCardComponent implements OnInit {
  route = inject(ActivatedRoute);
  router = inject(Router);
  memberService = inject(MemberService);
  duesService = inject(DuesService);
  badgesCatalogService = inject(BadgesCatalogService);
  probesCatalogService = inject(ProbesCatalogService);
  memberProgressService = inject(MemberProgressService);
  badgeImageBlobService = inject(BadgeImageBlobService);
  authService = inject(AuthService);
  entityService = inject(EntityService);
  permissionService = inject(PermissionService);
  kurinService = inject(KurinService);
  groupService = inject(GroupService);
  membershipService = inject(MembershipService);
  messageService = inject(MessageService);
  confirmationService = inject(ConfirmationService);
  breadcrumbService = inject(BreadcrumbService);
  memberAwardService = inject(MemberAwardService);

  member: MemberDto | null = null;
  /** What the member's край asks of the card; the card as it always was until it loads. */
  cardFields: CardFieldsDto = DEFAULT_CARD_FIELDS;
  memberKey: string | null = null;
  skillsSummary: MemberSkillsSummaryView = this.createEmptySkillsSummary();
  probeRows: MemberProbeRowView[] = this.createEmptyProbeRows();
  allBadgesCatalog: BadgeCatalogItemDto[] = [];
  /** The вмілості the member's kurin is offered by its край; the whole catalogue stays for what they already have. */
  offeredBadges: BadgeCatalogItemDto[] = [];
  memberships: MembershipDto[] = [];
  /** Інші курені того, хто дивиться, куди з членств можна перейти. Читаються, лише коли людина стоїть ще деінде. */
  reachableKurinKeys: string[] = [];
  switchingKurinTo: string | null = null;
  groupOptions: GroupDto[] = [];
  selectedGroupKey: string | null = null;
  movingMembership: MembershipDto | null = null;
  isMoveToGroupDialogVisible = false;
  isMovingToGroup = false;
  badgeProgresses: BadgeProgressDto[] = [];

  isMembershipsLoading = false;
  membershipsLoadFailed = false;

  /** Their вкладка here; null until read, and stays null when they have none or it is not ours to see. */
  dues: MemberDuesDto | null = null;
  isDuesLoading = false;
  readonly money = (amount: number): string => money(amount, this.dues?.currency, this.dues?.numberLocale);
  readonly quarterLabel = quarterLabel;
  private memberLoaded = false;
  private membershipsSettled = false;
  private youthProgressRequested = false;
  isSkillsLoading = false;
  skillsLoadFailed = false;
  isProbesLoading = false;
  probesLoadFailed = false;
  isAllSkillsDialogVisible = false;
  isAddSkillDialogVisible = false;
  isBadgeDetailsVisible = false;
  badgeDetailsId: string | null = null;
  isSubmittingSkill = false;
  submittingBadgeId: string | null = null;
  inlineModerationBadgeId: string | null = null;

  addSkillSearchTerm = '';
  addSkillVisibleCount = 12;
  addSkillErrorMessage: string | null = null;
  addSkillSuccessMessage: string | null = null;
  inlineModerationMessage: string | null = null;
  inlineModerationSeverity: 'success' | 'warn' | 'error' = 'success';
  canManageMemberActions = false;
  profileVerificationEnabled = false;
  profileVerificationSubmitting = false;
  isWarningsDialogVisible = false;

  readonly addSkillPageSize = 12;
  readonly warningLevels = [
    MemberWarningLevel.Level1,
    MemberWarningLevel.Level2,
    MemberWarningLevel.Level3
  ];
  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.memberKey = params.get('memberKey');
      this.refreshData();
    });
  }

  refreshData(): void {
    if (!this.memberKey) {
      return;
    }

    const memberKey = this.memberKey;
    this.memberLoaded = false;
    this.membershipsSettled = false;
    this.youthProgressRequested = false;

    this.memberService.getByKey(memberKey).subscribe({
      next: (member) => {
        this.member = member;
        this.memberLoaded = true;
        this.tryLoadYouthProgress(memberKey);
        if (isUsableKey(member.groupKey)) {
          this.breadcrumbService.setParam('groupKey', member.groupKey);
        }
        this.updateMemberAccess(member.memberKey ?? this.memberKey);
        this.loadProfileVerificationToggle(member);
        this.loadCardFields(member);
      },
      error: (error) => {
        console.error('Error fetching member:', error);
        if (isUsableKey(this.member?.groupKey)) {
          this.router.navigate(['/group', this.member?.groupKey], { replaceUrl: true });
        }
        else this.router.navigate(['/panel'], { replaceUrl: true });
      }
    });

    this.loadMemberships(memberKey);
    this.loadDues(memberKey);
  }

  /**
   * Quietly: a youth may read only their own, so for anyone else's card the server answers 403 and
   * the tile simply does not appear — that is not an error the viewer needs told about.
   */
  private loadDues(memberKey: string): void {
    this.isDuesLoading = true;
    this.dues = null;
    this.duesService.getMemberDues(memberKey).subscribe({
      next: dues => {
        this.dues = dues.hasAccount ? dues : null;
        this.isDuesLoading = false;
      },
      error: () => {
        this.dues = null;
        this.isDuesLoading = false;
      }
    });
  }

  openGroupDues(groupKey: string): void {
    this.router.navigate(['/group', groupKey, 'dues']);
  }

  get duesEntriesPreview(): DuesEntryDto[] {
    return (this.dues?.entries ?? []).slice(0, 3);
  }

  duesKindLabel(entry: DuesEntryDto): string {
    return DUES_ENTRY_KIND_LABELS[entry.kind];
  }

  readonly duesAmountLabel = (entry: DuesEntryDto): string => duesAmountLabel(entry, this.dues?.currency, this.dues?.numberLocale);
  readonly duesStandingLabels = DUES_STANDING_LABELS;

  /**
   * Проби й вмілості чекають і на людину, і на її членства: гілку дають ступінь і курінь. Старшим
   * цього на картці немає — їхнє юнацтво житиме в пластовому життєписі, — тож і питати не варто.
   */
  private tryLoadYouthProgress(memberKey: string): void {
    if (!this.memberLoaded || !this.membershipsSettled || this.youthProgressRequested) {
      return;
    }

    this.youthProgressRequested = true;
    if (!this.hasYouthProgram) {
      return;
    }

    this.loadSkills(memberKey);
    this.loadProbes(memberKey);
  }

  private loadMemberships(memberKey: string): void {
    this.isMembershipsLoading = true;
    this.membershipsLoadFailed = false;
    this.memberService.getMemberships(memberKey).subscribe({
      next: memberships => {
        this.memberships = memberships;
        this.isMembershipsLoading = false;
        this.membershipsSettled = true;
        this.tryLoadYouthProgress(memberKey);
        this.loadReachableKurins(memberships);
      },
      error: () => {
        this.memberships = [];
        this.membershipsLoadFailed = true;
        this.isMembershipsLoading = false;
        this.membershipsSettled = true;
        this.tryLoadYouthProgress(memberKey);
      }
    });
  }

  private loadReachableKurins(memberships: MembershipDto[]): void {
    if (!memberships.some(m => m.isCurrent && m.kurinKey !== this.scopedKurinKey)) {
      this.reachableKurinKeys = [];
      return;
    }
    this.authService.getKurinScopeOptions().subscribe({
      next: options => this.reachableKurinKeys = options.map(o => o.kurinKey),
      error: () => this.reachableKurinKeys = []
    });
  }

  /** Інший курінь відкривається після перемикання токена: сторінка куреня показує той, у якому діє сесія. */
  openMembershipKurin(membership: MembershipDto): void {
    if (this.switchingKurinTo) {
      return;
    }
    this.switchingKurinTo = membership.kurinKey;
    this.authService.setKurinScope(membership.kurinKey).subscribe({
      next: () => {
        this.switchingKurinTo = null;
        this.router.navigate(['/kurin']);
      },
      error: (error: unknown) => {
        this.switchingKurinTo = null;
        this.messageService.add({
          severity: 'error',
          summary: 'Не вдалося перейти',
          detail: failureDetail(error, `${kurinName(membership.kurinNumber)} лишився недосяжним. Спробуй ще раз.`)
        });
      }
    });
  }

  /** The phone as it reads: +420 777 123 456. */
  get phoneText(): string {
    return formatPhone(this.member?.phoneNumber);
  }

  get phoneLink(): string | null {
    return phoneHref(this.member?.phoneNumber);
  }

  /** Mother's and father's lines on the card, only those with something filled in. */
  get parentLines(): { label: string; name: string; phone: string; phoneLink: string | null }[] {
    const member = this.member;
    if (!member || !this.showsField('parents')) {
      return [];
    }

    return [
      { label: 'Мама', first: member.motherFirstName, last: member.motherLastName, phone: member.motherPhone },
      { label: 'Тато', first: member.fatherFirstName, last: member.fatherLastName, phone: member.fatherPhone }
    ]
      .map(parent => ({
        label: parent.label,
        name: [parent.first, parent.last].filter(Boolean).join(' '),
        phone: formatPhone(parent.phone),
        phoneLink: phoneHref(parent.phone)
      }))
      .filter(parent => parent.name || parent.phone);
  }

  get emailLink(): string | null {
    return emailHref(this.member?.email);
  }

  get personalBranch(): KurinBranch {
    return memberBranchHere(this.member, this.memberships);
  }

  /** Проби, вмілості й відзначення УПЮ ведуться лише юнацтву. */
  get hasYouthProgram(): boolean {
    return hasYouthProgram(this.personalBranch);
  }

  /**
   * Чинні ролі в КВ — там, де людина у виховному проводі: впорядник, зв'язковий. Ролі куреня й
   * гуртка сюди не йдуть — це юнацькі ролі.
   */
  get staffOffices(): LeadershipHistoryDto[] {
    return (this.member?.leadershipHistories ?? [])
      .filter(office => !office.endDate && office.leadershipType?.toLowerCase() === 'kv');
  }

  /** Гуртки, за якими людина закріплена виховником у курені, де її дивимось. */
  get mentoredGroupNames(): string[] {
    return this.member?.mentoredGroupNames ?? [];
  }

  /**
   * Ті самі гуртки для показу. Ключ лишається тільки тоді, коли гуртки з куреня, у якому діє той,
   * хто дивиться: сторінка гуртка іншого куреня його не впустить.
   */
  get mentoredGroups(): { groupKey: string | null; name: string }[] {
    const linkable = !!this.member?.kurinKey && this.member.kurinKey === this.scopedKurinKey;
    const groups = this.member?.mentoredGroups;
    if (groups?.length) {
      return groups.map(g => ({ groupKey: linkable ? g.groupKey : null, name: g.name }));
    }
    return this.mentoredGroupNames.map(name => ({ groupKey: null, name }));
  }

  officeLabel(role: string): string {
    return leadershipRoleDisplayName(role);
  }

  officeSeverity(role: string): RoleSeverity {
    return leadershipRoleSeverityForRole(role);
  }

  /**
   * Курінь, у якому здобуто, — за ключем. Порожньо, поки людина знає лише один курінь: підписувати
   * кожну нагороду тим самим числом означало б додати шуму й нічого не пояснити.
   */
  get kurinStamps(): Record<string, string> {
    if (this.memberships.length < 2) {
      return {};
    }

    return this.memberships.reduce<Record<string, string>>((stamps, membership) => {
      stamps[membership.kurinKey] = kurinLabel(membership.kurinNumber);
      return stamps;
    }, {});
  }

  /** Курінь, у якому дивиться той, хто дивиться. Дії над членством можливі тільки в ньому. */
  get scopedKurinKey(): string | null {
    return this.authService.getAuthStateValue()?.kurinKey ?? null;
  }

  openMoveToGroup(membership: MembershipDto): void {
    this.movingMembership = membership;
    this.selectedGroupKey = membership.groupKey ?? null;
    this.isMoveToGroupDialogVisible = true;
    this.groupService.getAllByKurinKey(membership.kurinKey).subscribe({
      next: groups => this.groupOptions = groups,
      error: () => this.groupOptions = []
    });
  }

  saveMoveToGroup(): void {
    const membership = this.movingMembership;
    if (!membership || !this.memberKey) {
      return;
    }

    this.isMovingToGroup = true;
    this.membershipService
      .moveToGroup(membership.kurinKey, this.memberKey, this.selectedGroupKey)
      .subscribe({
        next: () => {
          this.isMovingToGroup = false;
          this.isMoveToGroupDialogVisible = false;
          this.refreshData();
        },
        error: (error: unknown) => {
          this.isMovingToGroup = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Не вдалося перевести',
            detail: failureDetail(error)
          });
        }
      });
  }

  confirmLeaveKurin(membership: MembershipDto): void {
    this.confirmationService.confirm({
      header: 'Вивести з куреня',
      // Це не видалення людини, і про це варто сказати прямо: саме страх втратити історію
      // змушував провід тримати в курені тих, хто давно пішов.
      message: `Членство (${kurinLabel(membership.kurinNumber)}) буде закрите. Людина, її проби, `
        + 'вмілості й відзначення лишаються — вони належать їй, а не куреню.',
      icon: 'pi pi-sign-out',
      acceptLabel: 'Вивести',
      rejectLabel: 'Скасувати',
      acceptButtonProps: { label: 'Вивести', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => this.leaveKurin(membership)
    });
  }

  private leaveKurin(membership: MembershipDto): void {
    if (!this.memberKey) {
      return;
    }

    this.membershipService.leave(membership.kurinKey, this.memberKey).subscribe({
      next: () => this.refreshData(),
      error: (error: unknown) => this.messageService.add({
        severity: 'error',
        summary: 'Не вдалося вивести',
        detail: failureDetail(error)
      })
    });
  }

  get hasAnySkills(): boolean {
    return this.confirmedSkillsCount > 0 || this.pendingSkillsCount > 0;
  }

  get canEditMember(): boolean {
    return this.canManageMemberActions;
  }

  /** The card of whoever is looking: nobody verifies or warns themselves. */
  get isOwnCard(): boolean {
    const auth = this.authService.getAuthStateValue?.();
    return !!this.member
      && ((!!auth?.memberKey && auth.memberKey === this.member.memberKey)
        || (!!auth?.userKey && auth.userKey === this.member.userKey));
  }

  get currentUserKey(): string | null {
    return this.authService.getAuthStateValue?.()?.userKey ?? null;
  }

  get canManageProfileVerification(): boolean {
    return this.canEditMember && this.profileVerificationEnabled && !this.isOwnCard && this.permissionService.isReviewer();
  }

  get canManageWarnings(): boolean {
    return this.canEditMember && !this.isOwnCard && this.permissionService.canManageWarnings();
  }

  get isProfileVerified(): boolean {
    return this.member?.profileVerificationStatus === MemberProfileVerificationStatus.VerifiedCurrent;
  }

  onProfileVerificationAction(): void {
    const member = this.member;
    if (!member || !this.canManageProfileVerification || this.profileVerificationSubmitting) {
      return;
    }

    const verified = this.isProfileVerified;
    this.confirmationService.confirm({
      header: verified ? 'Зняти верифікацію' : 'Верифікувати профіль',
      message: verified
        ? 'Профіль знову чекатиме перевірки.'
        : 'Підтвердити, що дані в профілі правильні?',
      icon: verified ? 'pi pi-exclamation-triangle' : 'pi pi-verified',
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', variant: 'text' },
      acceptButtonProps: verified
        ? { label: 'Зняти', severity: 'danger' }
        : { label: 'Верифікувати' },
      accept: () => {
        this.profileVerificationSubmitting = true;
        const request = verified
          ? this.memberService.resetProfileVerification(member.memberKey)
          : this.memberService.verifyProfile(member.memberKey, null);
        request.pipe(finalize(() => this.profileVerificationSubmitting = false)).subscribe({
          next: updated => {
            this.member = { ...member, ...updated };
            this.messageService.add({ severity: 'success', summary: verified ? 'Верифікацію знято.' : 'Профіль верифіковано.' });
          },
          error: (error: unknown) => this.messageService.add({
            severity: 'error',
            summary: verified ? 'Верифікацію не знято' : 'Профіль не верифіковано',
            detail: (error as { status?: number })?.status === 400
              ? 'Верифікацію профілів вимкнено для цього куреня.'
              : failureDetail(error)
          })
        });
      }
    });
  }

  onWarningsChanged(): void {
    if (!this.memberKey) {
      return;
    }
    this.memberService.getByKey(this.memberKey).subscribe(member => this.member = member);
  }

  get photoInitials(): string {
    return initials(this.member?.firstName, this.member?.lastName);
  }

  /** Переводити в гурток і виводити з куреня — справа Звʼязкового, не впорядника. */
  get canManagePlacement(): boolean {
    return this.permissionService.canManageWholeKurin();
  }

  get profileVerifiedAtDisplay(): string | null {
    return formatUtcDateTime(this.member?.profileVerifiedAtUtc);
  }

  private updateMemberAccess(memberKey: string | null): void {
    if (!memberKey) {
      this.canManageMemberActions = false;
      return;
    }

    this.entityService.checkEntityAccess('member', memberKey, 'Update').subscribe({
      next: (canUpdate) => {
        this.canManageMemberActions = canUpdate;
      },
      error: () => {
        this.canManageMemberActions = false;
      }
    });
  }

  private loadCardFields(member: MemberDto): void {
    this.cardFields = DEFAULT_CARD_FIELDS;
    if (isUsableKey(member.kurinKey)) {
      this.kurinService.getCardFields(member.kurinKey!).subscribe(card => this.cardFields = card);
    }
  }

  /** A field the member's край leaves off the card is not shown as «не вказано» either. */
  showsField(field: StandardCardField): boolean {
    return cardFieldMode(this.cardFields, field) !== 'hidden';
  }

  get cardExtras(): { key: string; label: string; value: string }[] {
    return cardExtraLines(this.cardFields, this.member?.cardExtras);
  }

  private loadProfileVerificationToggle(member: MemberDto): void {
    this.profileVerificationEnabled = false;
    if (!member.kurinKey) {
      return;
    }

    this.kurinService.getByKey(member.kurinKey).subscribe({
      next: (kurin) => {
        this.profileVerificationEnabled = kurin.profileVerificationEnabled ?? false;
      },
      error: () => {
        this.profileVerificationEnabled = false;
      }
    });
  }

  get confirmedSkillsCount(): number {
    return this.skillsSummary.recentConfirmed.length;
  }

  get pendingSkillsCount(): number {
    return this.skillsSummary.pendingConfirmation.length;
  }

  get completedProbesCount(): number {
    return this.probeRows.filter(row => !row.isDisabled && row.isCompleted).length;
  }

  get activeProbesCount(): number {
    return this.probeRows.filter(row => !row.isDisabled).length;
  }

  get filteredAddSkillCandidates(): BadgeCatalogItemDto[] {
    const normalizedQuery = this.addSkillSearchTerm.trim().toLowerCase();
    const source = [...this.offeredBadges].sort((left, right) => left.title.localeCompare(right.title, 'uk'));

    if (!normalizedQuery) {
      return source;
    }

    return source.filter(item =>
      item.title.toLowerCase().includes(normalizedQuery)
      || item.specialization.toLowerCase().includes(normalizedQuery)
      || item.country.toLowerCase().includes(normalizedQuery)
    );
  }

  get visibleAddSkillCandidates(): BadgeCatalogItemDto[] {
    return this.filteredAddSkillCandidates.slice(0, this.addSkillVisibleCount);
  }

  get hasMoreAddSkillCandidates(): boolean {
    return this.filteredAddSkillCandidates.length > this.addSkillVisibleCount;
  }

  get hasActiveWarnings(): boolean {
    return this.getActiveWarnings().length > 0;
  }

  get activeWarningLevel(): MemberWarningLevel | null {
    const activeWarnings = this.getActiveWarnings();
    if (!activeWarnings.length) {
      return null;
    }

    return activeWarnings
      .map(warning => warning.level)
      .sort((left, right) => this.getWarningLevelWeight(right) - this.getWarningLevelWeight(left))[0];
  }

  get warningTooltip(): string {
    const activeWarning = this.getActiveWarnings()
      .sort((left, right) => this.getWarningLevelWeight(right.level) - this.getWarningLevelWeight(left.level))[0];

    if (!activeWarning) {
      return '';
    }

    const levelLabel = this.getWarningLevelLabel(activeWarning.level);
    const daysLeft = this.getWarningDaysLeft(activeWarning.expiresAtUtc);
    return `Поточна пересторога: ${levelLabel}. Залишилось днів: ${daysLeft}.`;
  }

  getWarningDotActive(level: MemberWarningLevel): boolean {
    if (!this.activeWarningLevel) {
      return false;
    }

    return this.getWarningLevelWeight(level) <= this.getWarningLevelWeight(this.activeWarningLevel);
  }

  private getWarningLevelWeight(level: MemberWarningLevel): number {
    switch (level) {
      case MemberWarningLevel.Level3: return 3;
      case MemberWarningLevel.Level2: return 2;
      case MemberWarningLevel.Level1: return 1;
      default: return 0;
    }
  }

  private getActiveWarnings(): MemberWarningDto[] {
    const warnings = this.member?.warnings ?? [];
    const now = Date.now();

    return warnings.filter(warning =>
      !warning.revokedAtUtc &&
      this.parseUtcDate(warning.expiresAtUtc) > now
    );
  }

  private parseUtcDate(value: string | null | undefined): number {
    if (!value) {
      return 0;
    }

    return parseUtcDateTime(value)?.getTime() ?? 0;
  }

  private getWarningLevelLabel(level: MemberWarningLevel): string {
    switch (level) {
      case MemberWarningLevel.Level1:
        return 'Перша';
      case MemberWarningLevel.Level2:
        return 'Друга';
      case MemberWarningLevel.Level3:
        return 'Третя';
      default:
        return 'Пересторога';
    }
  }

  private getWarningDaysLeft(expiresAtUtc: string): number {
    const expiresAt = this.parseUtcDate(expiresAtUtc);
    const diffMs = expiresAt - Date.now();
    return Math.max(0, Math.ceil(diffMs / 86400000));
  }

  openAllSkillsDialog(): void {
    this.addSkillSuccessMessage = null;
    this.addSkillErrorMessage = null;
    this.inlineModerationMessage = null;
    this.isAllSkillsDialogVisible = true;
  }

  openAddSkillDialog(): void {
    this.addSkillErrorMessage = null;
    this.addSkillSuccessMessage = null;
    this.addSkillSearchTerm = '';
    this.addSkillVisibleCount = this.addSkillPageSize;
    this.isAddSkillDialogVisible = true;
  }

  onAddSkillSearchTermChange(): void {
    this.addSkillVisibleCount = this.addSkillPageSize;
  }

  loadMoreAddSkillCandidates(): void {
    this.addSkillVisibleCount += this.addSkillPageSize;
  }

  get canOpenSkillsReview(): boolean {
    const kurinKey = this.member?.kurinKey || this.authService.getAuthStateValue()?.kurinKey;
    return this.permissionService.canReviewSkills() && this.canManageMemberActions && !!kurinKey;
  }

  get canInlineModerateSkills(): boolean {
    return this.canOpenSkillsReview && !!this.memberKey;
  }

  get isInlineModerationBusy(): boolean {
    return this.inlineModerationBadgeId !== null;
  }

  canSubmitBadge(badgeId: string): boolean {
    const existing = this.badgeProgresses.find(item => item.badgeId === badgeId);
    if (!existing) {
      return true;
    }

    const normalizedStatus = normalizeBadgeProgressStatus(existing.status);
    return normalizedStatus === BadgeProgressStatus.Rejected || normalizedStatus === BadgeProgressStatus.Draft;
  }

  getExistingBadgeStatusLabel(badgeId: string): string | null {
    const existing = this.badgeProgresses.find(item => item.badgeId === badgeId);
    if (!existing) {
      return null;
    }

    const normalizedStatus = normalizeBadgeProgressStatus(existing.status);
    return getBadgeProgressStatusLabel(normalizedStatus);
  }

  getSubmitBadgeButtonLabel(badgeId: string): string {
    if (this.submittingBadgeId === badgeId) {
      return 'Додаємо...';
    }

    const existing = this.badgeProgresses.find(item => item.badgeId === badgeId);
    if (!existing) {
      return 'Додати';
    }

    const normalizedStatus = normalizeBadgeProgressStatus(existing.status);
    if (normalizedStatus === BadgeProgressStatus.Rejected) {
      return 'Подати знову';
    }

    return 'Додати';
  }

  openBadgeDetails(badgeId: string): void {
    this.badgeDetailsId = badgeId;
    this.isBadgeDetailsVisible = true;
  }

  getCatalogBadgeImageUrl(imagePath: string): string | null {
    return this.badgeImageBlobService.resolveBadgeImageForDisplay(resolveBadgeImageUrl(imagePath));
  }

  getSkillBadgeImageUrl(imageUrl: string | null): string | null {
    return this.badgeImageBlobService.resolveBadgeImageForDisplay(imageUrl);
  }

  getProbeStatusLabel(status: ProbeProgressStatus): string {
    return getProbeProgressStatusLabel(status);
  }

  getProbeSummaryMeta(row: MemberProbeRowView): string {
    if (row.probeId === 'probe-2' && row.isDisabled) {
      return 'Відкриється після закриття першої проби';
    }

    if (row.probeId === 'probe-3' && row.isDisabled) {
      return 'Третя проба буде реалізована окремим етапом';
    }

    if (row.completedAtUtc) {
      return `Завершено: ${this.formatDate(row.completedAtUtc)}`;
    }

    const statusLabel = this.getProbeStatusLabel(row.status);
    if (row.pointsCount === null) {
      return statusLabel;
    }

    return `${statusLabel} · ${row.pointsCount} точок`;
  }

  openProbeDetails(row: MemberProbeRowView): void {
    if (!this.memberKey || row.isDisabled || !row.canOpenDetails) {
      return;
    }

    this.router.navigate(['/member', this.memberKey, 'probe', row.probeId]);
  }

  submitBadge(badgeId: string): void {
    if (!this.memberKey || this.isSubmittingSkill || !this.canSubmitBadge(badgeId)) {
      return;
    }

    this.isSubmittingSkill = true;
    this.submittingBadgeId = badgeId;
    this.addSkillErrorMessage = null;
    this.addSkillSuccessMessage = null;

    this.memberProgressService
      .submitBadgeProgress(this.memberKey, badgeId, { note: null })
      .subscribe({
        next: () => {
          this.isSubmittingSkill = false;
          this.submittingBadgeId = null;
          this.isAddSkillDialogVisible = false;
          this.addSkillSuccessMessage = 'Вмілість успішно подана на підтвердження.';
          this.loadSkills(this.memberKey!);
        },
        error: (error) => {
          console.error('Error submitting badge:', error);
          this.isSubmittingSkill = false;
          this.submittingBadgeId = null;
          this.addSkillErrorMessage = failureDetail(error, 'Не вдалося подати вмілість. Спробуй ще раз.');
        }
      });
  }

  isInlineModerationInProgress(badgeId: string): boolean {
    return this.inlineModerationBadgeId === badgeId;
  }

  approvePendingSkill(skill: MemberSkillItemView): void {
    this.inlineModerateSkill(skill, true);
  }

  removeConfirmedSkill(skill: MemberSkillItemView): void {
    this.inlineModerateSkill(skill, false);
  }

  openSkillsReviewFromDialog(): void {
    const kurinKey = this.member?.kurinKey || this.authService.getAuthStateValue()?.kurinKey;
    if (!kurinKey || !this.canOpenSkillsReview) {
      return;
    }

    this.isAllSkillsDialogVisible = false;
    this.router.navigate(['/kurin', kurinKey, 'review', 'skills']);
  }

  private inlineModerateSkill(skill: MemberSkillItemView, isApproved: boolean): void {
    if (!this.memberKey || !this.canInlineModerateSkills || this.isInlineModerationBusy) {
      return;
    }

    if (isApproved) {
      this.executeInlineModeration(skill, true);
      return;
    }

    this.confirmationService.confirm({
      header: 'Видалення підтвердженої вмілості',
      message: `Видалити підтверджену вмілість "${skill.title}"?`,
      icon: 'pi pi-exclamation-triangle',
      rejectLabel: 'Скасувати',
      rejectButtonProps: {
        label: 'Скасувати',
        severity: 'secondary',
        outlined: true
      },
      acceptButtonProps: {
        label: 'Видалити',
        severity: 'danger'
      },
      accept: () => {
        this.executeInlineModeration(skill, false);
      }
    });
  }

  private executeInlineModeration(skill: MemberSkillItemView, isApproved: boolean): void {
    if (!this.memberKey || this.isInlineModerationBusy) {
      return;
    }

    this.inlineModerationBadgeId = skill.badgeId;
    this.inlineModerationMessage = null;

    this.memberProgressService
      .reviewBadgeProgress(this.memberKey, skill.badgeId, { isApproved, note: null })
      .pipe(finalize(() => {
        this.inlineModerationBadgeId = null;
      }))
      .subscribe({
        next: () => {
          this.inlineModerationSeverity = 'success';
          this.inlineModerationMessage = isApproved
            ? `Вмілість "${skill.title}" підтверджено.`
            : `Вмілість "${skill.title}" видалено з підтверджених.`;
          this.loadSkills(this.memberKey!);
        },
        error: (error) => {
          if (error?.status === 409) {
            this.inlineModerationSeverity = 'warn';
            this.inlineModerationMessage = 'Стан вмілості вже змінено в іншому запиті. Дані оновлено.';
            this.loadSkills(this.memberKey!);
            return;
          }

          if (error?.status === 403) {
            this.inlineModerationSeverity = 'error';
            this.inlineModerationMessage = failureDetail(error, 'Немає доступу для цієї дії модерації.');
            return;
          }

          this.inlineModerationSeverity = 'error';
          this.inlineModerationMessage = failureDetail(error, 'Не вдалося виконати дію. Спробуй ще раз.');
        }
      });
  }

  private loadSkills(memberKey: string): void {
    this.isSkillsLoading = true;
    this.skillsLoadFailed = false;

    let hasLoadError = false;

    forkJoin({
      badges: this.badgesCatalogService.getAll(1000).pipe(
        catchError((error) => {
          console.error('Error fetching badges catalog:', error);
          hasLoadError = true;
          return of([] as BadgeCatalogItemDto[]);
        })
      ),
      offered: (isUsableKey(this.member?.kurinKey) ? this.badgesCatalogService.getForKurin(this.member!.kurinKey!) : of([] as BadgeCatalogItemDto[])).pipe(
        catchError((error) => {
          console.error('Error fetching the kurin badges:', error);
          hasLoadError = true;
          return of([] as BadgeCatalogItemDto[]);
        })
      ),
      progresses: this.memberProgressService.getBadgeProgresses(memberKey).pipe(
        catchError((error) => {
          console.error('Error fetching badge progresses:', error);
          hasLoadError = true;
          return of([] as BadgeProgressDto[]);
        })
      )
    }).subscribe(({ badges, offered, progresses }) => {
      this.allBadgesCatalog = badges;
      this.offeredBadges = offered;
      this.badgeProgresses = progresses;
      this.skillsSummary = buildMemberSkillsSummary(progresses, badges);
      this.skillsLoadFailed = hasLoadError;
      this.isSkillsLoading = false;
    });
  }

  private loadProbes(memberKey: string): void {
    this.isProbesLoading = true;
    this.probesLoadFailed = false;

    let hasLoadError = false;

    this.probesCatalogService
      .getAll()
      .pipe(
        catchError((error) => {
          console.error('Error fetching probes catalog:', error);
          hasLoadError = true;
          return of([] as ProbeSummaryDto[]);
        }),
        switchMap((probes) => {
          if (probes.length === 0) {
            return of({ probes, progresses: [] as ProbeProgressDto[] });
          }

          return forkJoin(
            probes.map(probe =>
              this.memberProgressService.getProbeProgress(memberKey, probe.id).pipe(
                catchError((error) => {
                  console.error(`Error fetching probe progress for ${probe.id}:`, error);
                  hasLoadError = true;
                  return of(null);
                })
              )
            )
          ).pipe(
            map(progresses => ({
              probes,
              progresses: progresses.filter((progress): progress is ProbeProgressDto => progress !== null)
            }))
          );
        })
      )
      .subscribe(({ probes, progresses }) => {
        this.probeRows = buildMemberProbeRows(probes, progresses);
        this.probesLoadFailed = hasLoadError;
        this.isProbesLoading = false;
      });
  }

  private createEmptySkillsSummary(): MemberSkillsSummaryView {
    return {
      recentConfirmed: [],
      pendingConfirmation: [],
      orderedPreview: []
    };
  }

  private createEmptyProbeRows(): MemberProbeRowView[] {
    return buildMemberProbeRows([], []);
  }

  private formatDate(value: string): string {
    return formatUtcDateTime(value, { year: 'numeric', month: '2-digit', day: '2-digit' }) ?? value;
  }

  onEditMember() {
    const editRoute = this.resolveEditRoute();
    if (!editRoute) {
      console.error('Cannot edit member without a group or kurin scope:', this.memberKey);
      return;
    }

    this.router.navigate(editRoute, { state: { fromMember: true } });
  }

  // A member with no group is edited in the kurin scope — the group route would carry
  // Guid.Empty as its :groupKey and lead the user into a dead page.
  private resolveEditRoute(): unknown[] | null {
    if (isUsableKey(this.member?.groupKey)) {
      return ['/group', this.member?.groupKey, 'member', 'upsert', this.memberKey];
    }

    if (isUsableKey(this.member?.kurinKey)) {
      return ['/kurin', this.member?.kurinKey, 'member', 'upsert', this.memberKey];
    }

    return null;
  }

  onSaveAward(request: UpsertMemberAwardRequest): void {
    if (!this.memberKey) return;
    this.memberAwardService.upsertAward(this.memberKey, request).subscribe({
      next: () => {
        this.refreshData();
      },
      error: (error) => {
        console.error('Error saving award:', error);
      }
    });
  }

  onDeleteAward(awardKey: string): void {
    if (!this.memberKey) return;
    this.memberAwardService.deleteAward(this.memberKey, awardKey).subscribe({
      next: () => {
        this.refreshData();
      },
      error: (error) => {
        console.error('Error deleting award:', error);
      }
    });
  }

  onReviewAward(request: { awardKey: string, isApproved: boolean }): void {
    if (!this.memberKey) return;
    this.memberAwardService.reviewAward(this.memberKey, request.awardKey, { isApproved: request.isApproved, note: null }).subscribe({
      next: () => {
        this.refreshData();
      },
      error: (error) => {
        console.error('Error reviewing award:', error);
      }
    });
  }
}
