import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ProgressBarModule } from '@openng/optimus-ui/progressbar';
import { AccordionModule } from '@openng/optimus-ui/accordion';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TagModule } from '@openng/optimus-ui/tag';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { MemberService } from '../../services/member-service/member.service';
import { ProbesCatalogService } from '../../services/probes-and-badges/probes-catalog.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { MemberDto } from '../../models/member.dto';
import { MembershipDto } from '../../models/membership.dto';
import { memberBranchHere } from '../../functions/member-branch.function';
import { hasYouthProgram } from '../../models/enums/kurin-branch.enum';
import { GroupedProbeDto } from '../../models/probes-and-badges/grouped-probe.dto';
import { ProbeProgressDto } from '../../models/probes-and-badges/probe-progress.dto';
import { ProbePointProgressDto } from '../../models/probes-and-badges/probe-point-progress.dto';
import { MemberProbeDetailPointRowView } from '../../models/probes-and-badges/member-probe-detail-point-row.view';
import { buildMemberProbeDetailPointRows } from '../../functions/member-probe-details-view-mapper.function';
import { ProbeProgressStatus } from '../../models/enums/probe-progress-status.enum';
import { normalizeProbeProgressStatus } from '../../functions/member-probe-rows-view-mapper.function';
import { formatUtcDateTime } from '../../../../shared/functions/utc-date-time.function';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { parseOfficeRole } from '../../functions/system-role.function';
import { leadershipRoleDisplayName } from '../../functions/leadership-role-display.function';
import { MemberAbilitiesDto, NO_ABILITIES } from '../../models/member-abilities.dto';

interface ProbeDetailSectionView {
  sectionId: string;
  sectionCode: string;
  sectionTitle: string;
  points: MemberProbeDetailPointRowView[];
}

@Component({
  selector: 'app-member-probe-page',
  imports: [ButtonModule, ProgressBarModule, AccordionModule, SkeletonModule, TagModule, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './member-probe-page.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-probe-page.css'
})
export class MemberProbePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly memberService = inject(MemberService);
  private readonly probesCatalogService = inject(ProbesCatalogService);
  private readonly memberProgressService = inject(MemberProgressService);

  memberKey: string | null = null;
  probeId: string | null = null;

  member: MemberDto | null = null;
  memberships: MembershipDto[] = [];
  groupedProbe: GroupedProbeDto | null = null;
  probeProgress: ProbeProgressDto | null = null;

  probeDetailPointRows: MemberProbeDetailPointRowView[] = [];
  probeSections: ProbeDetailSectionView[] = [];

  isLoading = false;
  loadFailed = false;
  hasPartialDataWarning = false;
  isUpdatingProbeStatus = false;
  reviewerActionErrorMessage: string | null = null;
  reviewerActionSuccessMessage: string | null = null;
  /** What the kurin's policy lets the viewer do to this member; signing and unsigning come from it. */
  abilities: MemberAbilitiesDto = NO_ABILITIES;

  /** Скільки точок цієї проби вже підписано. */
  get signedPointsCount(): number {
    return this.probeDetailPointRows.filter(point => point.isSigned).length;
  }

  /**
   * Скільки пройдено, у відсотках, або null коли точок немає — смуга без знаменника показувала б
   * нуль, а це інше твердження, ніж «нема чого рахувати».
   */
  get completionPercent(): number | null {
    const total = this.probeDetailPointRows.length;
    return total ? Math.round((this.signedPointsCount / total) * 100) : null;
  }

  /**
   * Старший пластун чи сеніор: проба — уже не вишкіл, а сторінка його пластового життєпису.
   * Її видно, але не підписати й не закрити.
   */
  get isArchive(): boolean {
    return this.member !== null && !hasYouthProgram(memberBranchHere(this.member, this.memberships));
  }

  /** Sign points and close the проба. */
  get canSignProbePoints(): boolean {
    return !this.isArchive && this.abilities.probes.sign;
  }

  /** Take a signature back; a kurin may give it to fewer people than signing. */
  get canUnsignProbePoints(): boolean {
    return !this.isArchive && this.abilities.probes.unsign;
  }

  get canManageProbePoints(): boolean {
    return this.canSignProbePoints || this.canUnsignProbePoints;
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.memberKey = params.get('memberKey');
      this.probeId = params.get('probeId');
      this.reviewerActionErrorMessage = null;
      this.reviewerActionSuccessMessage = null;
      this.updateMemberAccess();
      this.loadData();
    });
  }

  goBackToMember(): void {
    if (!this.memberKey) {
      this.router.navigate(['/panel']);
      return;
    }

    this.router.navigate(['/member', this.memberKey]);
  }

  getProbePointSignerLabel(point: MemberProbeDetailPointRowView): string {
    if (!point.isSigned) {
      return 'Не підписано';
    }

    const name = point.signedByName?.trim();
    const role = signerRoleLabel(point.signedByRole);
    if (name && role) {
      return `${name} (${role})`;
    }

    if (name) {
      return name;
    }

    if (point.signedByUserKey && role) {
      return `${point.signedByUserKey} (${role})`;
    }

    if (point.signedByUserKey) {
      return point.signedByUserKey;
    }

    if (role) {
      return `Невідомий користувач (${role})`;
    }

    return 'Підписант не вказаний';
  }

  /**
   * Точки, підписані за вимогами, яких у пробі вже немає. Підпис не знімається, коли вимоги
   * змінились: людина бачить чинні вимоги, а давніші підписи лишаються тут.
   */
  get signedEarlierPoints(): ProbePointProgressDto[] {
    if (!this.groupedProbe) {
      return [];
    }

    const current = new Set(this.groupedProbe.sections.flatMap(section => section.points.map(point => point.id)));
    return (this.probeProgress?.pointSignatures ?? []).filter(point => point.isSigned && !current.has(point.pointId));
  }

  getSignedEarlierLabel(point: ProbePointProgressDto): string {
    const date = point.signedAtUtc ? this.formatDate(point.signedAtUtc) : null;
    const name = point.signedByName?.trim() || null;
    return [`Точка ${point.pointId}`, name, date].filter(Boolean).join(' · ');
  }

  getProbePointSignedAtLabel(point: MemberProbeDetailPointRowView): string {
    if (!point.isSigned || !point.signedAtUtc) {
      return '—';
    }

    return this.formatDate(point.signedAtUtc);
  }

  getSectionPanelValue(section: ProbeDetailSectionView, sectionIndex: number): string {
    return section.sectionId || `section-${sectionIndex}`;
  }

  get canCloseProbe(): boolean {
    if (!this.canSignProbePoints || !this.memberKey || !this.probeId) {
      return false;
    }

    if (this.isUpdatingProbeStatus || !this.isCloseFlowSupportedProbe()) {
      return false;
    }

    if (this.isProbeClosed()) {
      return false;
    }

    return this.probeDetailPointRows.length > 0
      && this.probeDetailPointRows.every(point => point.isSigned);
  }

  get showCloseProbeInfo(): boolean {
    if (!this.probeId || !this.isCloseFlowSupportedProbe()) {
      return false;
    }

    return this.isProbeClosed();
  }

  get closedProbeDateLabel(): string {
    const closedAtUtc = this.probeProgress?.completedAtUtc ?? this.probeProgress?.verifiedAtUtc ?? null;
    if (!closedAtUtc) {
      return '—';
    }

    return this.formatDate(closedAtUtc);
  }

  onSignPoint(point: MemberProbeDetailPointRowView): void {
    if (!this.canSignProbePoints || point.isSigned || this.isUpdatingProbeStatus) {
      return;
    }

    this.updatePointSignature(point.pointId, true, 'Точку підписано.');
  }

  onUnsignPoint(point: MemberProbeDetailPointRowView): void {
    if (!this.canUnsignProbePoints || !point.isSigned || this.isUpdatingProbeStatus) {
      return;
    }

    this.confirmationService.confirm({
      header: 'Скасування підпису',
      message: `Скасувати підпис точки "${point.pointTitle}"?`,
      icon: 'pi pi-exclamation-triangle',
      rejectLabel: 'Ні',
      rejectButtonProps: {
        label: 'Ні',
        severity: 'secondary',
        outlined: true
      },
      acceptButtonProps: {
        label: 'Так, скасувати',
        severity: 'danger'
      },
      accept: () => {
        this.updatePointSignature(point.pointId, false, 'Підпис скасовано.');
      }
    });
  }

  onCloseProbe(): void {
    if (!this.memberKey || !this.probeId || !this.canCloseProbe) {
      return;
    }

    const confirmationText = 'Буде зафіксовано дату закриття, і відкриється наступна проба, якщо вона є.';

    this.confirmationService.confirm({
      header: 'Закриття проби',
      message: confirmationText,
      icon: 'pi pi-exclamation-triangle',
      rejectLabel: 'Скасувати',
      rejectButtonProps: {
        label: 'Скасувати',
        severity: 'secondary',
        outlined: true
      },
      acceptButtonProps: {
        label: 'Закрити',
        severity: 'contrast'
      },
      accept: () => {
        this.executeCloseProbe();
      }
    });
  }

  private updateMemberAccess(): void {
    this.abilities = NO_ABILITIES;
    if (!this.memberKey) {
      return;
    }

    this.memberService.getAbilities(this.memberKey).subscribe({
      next: abilities => this.abilities = abilities,
      error: () => this.abilities = NO_ABILITIES
    });
  }

  private executeCloseProbe(): void {
    if (!this.memberKey || !this.probeId || !this.canCloseProbe) {
      return;
    }

    this.isUpdatingProbeStatus = true;
    this.reviewerActionErrorMessage = null;
    this.reviewerActionSuccessMessage = null;

    this.memberProgressService
      .updateProbeProgressStatus(this.memberKey, this.probeId, {
        status: ProbeProgressStatus.Completed,
        note: null
      })
      .pipe(finalize(() => {
        this.isUpdatingProbeStatus = false;
      }))
      .subscribe({
        next: (progress) => {
          this.applyProbeProgress(progress);
          this.reviewerActionSuccessMessage = 'Пробу здано і закрито. Дату закриття зафіксовано.';
        },
        error: (error) => {
          console.error('Error closing probe:', error);
          if (error?.status === 409) {
            const conflictProgress = this.extractConflictProbeProgress(error);
            if (!conflictProgress) {
              this.reviewerActionErrorMessage = 'Не вдалося закрити пробу через конфлікт. Оновлюю дані.';
              this.loadData();
              return;
            }

            this.applyProbeProgress(conflictProgress);
            const conflictStatus = normalizeProbeProgressStatus(conflictProgress.status ?? ProbeProgressStatus.NotStarted);
            if (conflictStatus === ProbeProgressStatus.Completed || conflictStatus === ProbeProgressStatus.Verified) {
              this.reviewerActionErrorMessage = null;
              this.reviewerActionSuccessMessage = 'Пробу вже закрито іншим запитом. Дані синхронізовано.';
              this.loadData();
              return;
            }

            this.reviewerActionErrorMessage = 'Пробу не вдалося закрити: не всі точки підписані або стан уже змінено.';
            this.loadData();
          } else {
            this.reviewerActionErrorMessage = failureDetail(error, 'Не вдалося закрити пробу. Спробуй ще раз.');
          }
        }
      });
  }

  private loadData(): void {
    if (!this.memberKey || !this.probeId) {
      this.loadFailed = true;
      this.isLoading = false;
      this.hasPartialDataWarning = false;
      return;
    }

    this.isLoading = true;
    this.loadFailed = false;
    this.hasPartialDataWarning = false;

    forkJoin({
      member: this.memberService.getByKey(this.memberKey).pipe(
        catchError((error) => {
          console.error('Error fetching member for probe page:', error);
          return of(null);
        })
      ),
      memberships: this.memberService.getMemberships(this.memberKey).pipe(
        catchError(() => of([] as MembershipDto[]))
      ),
      groupedProbe: this.probesCatalogService.getGroupedById(this.probeId).pipe(
        catchError((error) => {
          console.error(`Error fetching grouped probe ${this.probeId}:`, error);
          return of(null);
        })
      ),
      progress: this.memberProgressService.getProbeProgress(this.memberKey, this.probeId).pipe(
        catchError((error) => {
          console.error(`Error fetching probe progress ${this.probeId}:`, error);
          return of(null);
        })
      )
    }).subscribe(({ member, memberships, groupedProbe, progress }) => {
      this.member = member;
      this.memberships = memberships;
      this.groupedProbe = groupedProbe;
      this.applyProbeProgress(progress);

      this.loadFailed = groupedProbe === null;
      this.hasPartialDataWarning = groupedProbe !== null && progress === null;
      this.isLoading = false;
    });
  }

  private buildProbeSections(
    groupedProbe: GroupedProbeDto | null,
    pointRows: MemberProbeDetailPointRowView[]
  ): ProbeDetailSectionView[] {
    if (!groupedProbe) {
      return [];
    }

    return groupedProbe.sections.map(section => ({
      sectionId: section.id,
      sectionCode: section.code,
      sectionTitle: section.title,
      points: pointRows.filter(row => row.sectionId === section.id)
    }));
  }

  private formatDate(value: string): string {
    return formatUtcDateTime(value, { year: 'numeric', month: '2-digit', day: '2-digit' }) ?? value;
  }

  private applyProbeProgress(progress: ProbeProgressDto | null): void {
    this.probeProgress = progress;
    this.probeDetailPointRows = buildMemberProbeDetailPointRows(this.groupedProbe, progress);
    this.probeSections = this.buildProbeSections(this.groupedProbe, this.probeDetailPointRows);
  }

  private isProbeClosed(): boolean {
    const normalizedStatus = normalizeProbeProgressStatus(this.probeProgress?.status ?? ProbeProgressStatus.NotStarted);
    return normalizedStatus === ProbeProgressStatus.Completed || normalizedStatus === ProbeProgressStatus.Verified;
  }

  /** Закрити поточково можна пробу з точками; та, що їх у каталозі не має (третя), — окремий етап. */
  private isCloseFlowSupportedProbe(): boolean {
    return (this.groupedProbe?.pointsCount ?? 0) > 0;
  }

  private updatePointSignature(pointId: string, isSigned: boolean, successMessage: string): void {
    if (!this.memberKey || !this.probeId || this.isUpdatingProbeStatus || !pointId) {
      return;
    }

    this.isUpdatingProbeStatus = true;
    this.reviewerActionErrorMessage = null;
    this.reviewerActionSuccessMessage = null;

    const request = { note: null };
    const operation$ = isSigned
      ? this.memberProgressService.signProbePoint(this.memberKey, this.probeId, pointId, request)
      : this.memberProgressService.unsignProbePoint(this.memberKey, this.probeId, pointId, request);

    operation$
      .pipe(finalize(() => {
        this.isUpdatingProbeStatus = false;
      }))
      .subscribe({
        next: (progress) => {
          this.applyProbeProgress(progress);
          this.reviewerActionSuccessMessage = successMessage;
        },
        error: (error) => {
          console.error('Error updating probe point signature:', error);
          const turn = probeTurnRefusal(error);
          if (turn) {
            this.reviewerActionErrorMessage = turn;
            return;
          }
          if (error?.status === 409) {
            const conflictProgress = this.extractConflictProbeProgress(error);
            if (!conflictProgress) {
              this.reviewerActionErrorMessage = 'Не вдалося оновити підпис точки через конфлікт. Оновлюю дані.';
              this.loadData();
              return;
            }

            this.applyProbeProgress(conflictProgress);
            this.reviewerActionErrorMessage = 'Конфлікт оновлення. Дані синхронізовано, перевір поточний стан точки.';
            this.loadData();
          } else {
            this.reviewerActionErrorMessage = failureDetail(error, 'Не вдалося оновити підпис точки. Спробуй ще раз.');
          }
        }
      });
  }

  private extractConflictProbeProgress(error: unknown): ProbeProgressDto | null {
    if (!error || typeof error !== 'object') {
      return null;
    }

    const payload = (error as { error?: unknown }).error;

    if (Array.isArray(payload) && payload.length > 1) {
      const maybeProgress = payload[1];
      if (maybeProgress && typeof maybeProgress === 'object') {
        return maybeProgress as ProbeProgressDto;
      }
    }

    if (payload && typeof payload === 'object') {
      const candidate = payload as Partial<ProbeProgressDto>;
      if (candidate.probeId && candidate.status !== undefined) {
        return candidate as ProbeProgressDto;
      }
    }

    return null;
  }
}

/** The signer's office as people say it («Впорядник»), not the system role it is stored as («KV.Vykhovnyk»). */
function signerRoleLabel(systemRole: string | null | undefined): string | null {
  if (!systemRole) {
    return null;
  }
  const office = parseOfficeRole(systemRole);
  if (office) {
    return leadershipRoleDisplayName(office.role);
  }
  if (systemRole === 'Admin') {
    return 'Адміністратор';
  }
  // Older signatures already carry the office's name.
  return /^[A-Za-z.]+$/.test(systemRole) ? null : systemRole;
}

/** What the server says when a probe is started out of its turn, in words; null for any other answer. */
function probeTurnRefusal(error: unknown): string | null {
  const code = (error as { error?: { error?: string } } | null)?.error?.error;
  switch (code) {
    case 'ProbeWaitsForPrevious':
      return 'Ця проба відкриється, коли закриють попередню.';
    case 'ProbeNotInLine':
      return 'Ця проба не з тієї, якою йде людина: вона йде пробою свого гуртка, а хто вже закрив першу — своєю.';
    default:
      return null;
  }
}
