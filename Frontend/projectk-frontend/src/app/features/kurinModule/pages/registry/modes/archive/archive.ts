import { ChangeDetectionStrategy, Component, computed, DestroyRef, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { SelectModule } from '@openng/optimus-ui/select';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { EmptyStateComponent } from '../../../../../../shared/empty-state/empty-state';
import { KurinArchiveService } from '../../../../services/kurin-archive-service/kurin-archive.service';
import { KurinFeaturesService } from '../../../../services/kurin-features-service/kurin-features.service';
import { FormerMemberStandingDto, MembershipService } from '../../../../services/membership-service/membership.service';
import { RegistryModes } from '../../registry-modes';
import { standsInLine } from '../../membership-asks';
import { ReturnDialogComponent } from '../../components/return-dialog/return-dialog';
import { LEADERSHIP_ROLE_MAP } from '../../../../components/leadership/leadership/leadership';
import { leadershipRoleDisplayName } from '../../../../functions/leadership-role-display.function';
import { LeadershipRole } from '../../../../models/enums/leadership-role.enum';
import { MembershipKind, membershipKindLabel } from '../../../../models/enums/membership-kind.enum';
import {
  ArchivedLeadershipType,
  ArchivedMembershipDto,
  ArchivedOfficeDto,
  ArchiveDangerZoneDto,
  DissolvedGroupDto,
  KurinArchiveDto
} from '../../../../models/kurin-archive.dto';
import { RegistrySearchComponent } from '../../components/registry-search/registry-search';

type OfficeFilter = 'former' | 'all';
type ProofMethod = 'password' | 'code';

interface Option<T> {
  label: string;
  value: T;
}

const ROLES_BY_TYPE: Record<ArchivedLeadershipType, LeadershipRole[]> = {
  Kurin: LEADERSHIP_ROLE_MAP['kurin'],
  Group: LEADERSHIP_ROLE_MAP['group'],
  KV: [LeadershipRole.Instruktor, LeadershipRole.Vykhovnyk, LeadershipRole.Zvyazkovyi]
};

const pad = (value: number): string => String(value).padStart(2, '0');

/** `2023-08-31` → «31.08.2023»; nothing for no date. Read as text: a date has no time zone. */
export function archiveDate(value: string | null | undefined): string {
  const match = value ? /^(\d{4})-(\d{2})-(\d{2})/.exec(value) : null;
  return match ? `${match[3]}.${match[2]}.${match[1]}` : '';
}

function toDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function fromDate(value: Date): string {
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
}

/**
 * «Архів» — the registry's mode for the kurin's past: who held which role and when, the memberships
 * that ended, the гуртки dissolved. Each row the viewer may correct opens a small form, and the
 * Звʼязковий takes a former member back from their row. The Звʼязковий may also turn on the danger
 * zone, where records are deleted one at a time; it lives in this mode's memory only and ends the
 * moment the mode does — on another mode, on navigation, on a closed tab, by its own clock, or by the button.
 */
@Component({
  selector: 'app-kurin-archive',
  imports: [
    RegistrySearchComponent,
    RouterLink,
    FormsModule,
    ButtonModule,
    TableModule,
    TagModule,
    DialogModule,
    SelectModule,
    SelectButtonModule,
    DatePickerModule,
    InputTextModule,
    MessageModule,
    ConfirmDialogModule,
    EmptyStateComponent,
    ReturnDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './archive.html',
  styleUrl: './archive.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinArchiveComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly membershipService = inject(MembershipService);
  private readonly archiveService = inject(KurinArchiveService);
  private readonly features = inject(KurinFeaturesService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly date = archiveDate;
  protected readonly hasGroups = this.features.hasGroups;

  protected readonly officeFilterOptions: Option<OfficeFilter>[] = [
    { label: 'Колишні', value: 'former' },
    { label: 'Усі', value: 'all' }
  ];
  protected readonly proofOptions: Option<ProofMethod>[] = [
    { label: 'Пароль', value: 'password' },
    { label: 'Код', value: 'code' }
  ];

  protected kurinKey = '';

  /** Taking a former member back is the Звʼязковий's, as on «Переходи»: a гурток's провід only reads and corrects. */
  protected readonly canReturn = inject(RegistryModes).allows('transfers');
  private scrollToFormer = this.route.snapshot.queryParamMap.get('former') === '1';

  readonly archive = signal<KurinArchiveDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly officeFilter = signal<OfficeFilter>('former');
  readonly search = signal('');

  readonly offices = computed(() => {
    const query = this.search().trim().toLowerCase();
    return (this.archive()?.offices ?? [])
      .filter(office => this.officeFilter() === 'all' || office.endDate !== null)
      .filter(office => !query || office.memberName.toLowerCase().includes(query) || this.roleLabel(office).toLowerCase().includes(query));
  });
  readonly memberships = computed(() => {
    const query = this.search().trim().toLowerCase();
    return (this.archive()?.memberships ?? []).filter(membership => !query || membership.memberName.toLowerCase().includes(query));
  });
  readonly dissolvedGroups = computed(() => this.archive()?.dissolvedGroups ?? []);
  readonly canDelete = computed(() => this.archive()?.canDelete === true);

  // ── Повернути колишнього ──
  private readonly standing = signal<Map<string, FormerMemberStandingDto>>(new Map());
  /** Former members a sent request already asks for: not asked twice. */
  readonly asked = signal<Set<string>>(new Set());
  readonly returning = signal<FormerMemberStandingDto | null>(null);
  /** A person may have left more than once; they are taken back from their latest membership only. */
  private readonly latestMembership = computed(() => {
    const latest = new Map<string, ArchivedMembershipDto>();
    for (const membership of this.archive()?.memberships ?? []) {
      const seen = latest.get(membership.memberKey);
      if (!seen || membership.leftOn > seen.leftOn) {
        latest.set(membership.memberKey, membership);
      }
    }
    return latest;
  });

  // ── Виправлення ──
  readonly editingOffice = signal<ArchivedOfficeDto | null>(null);
  readonly editingMembership = signal<ArchivedMembershipDto | null>(null);
  readonly editRole = signal<LeadershipRole | null>(null);
  readonly editKind = signal<MembershipKind>(MembershipKind.Youth);
  readonly editFrom = signal<Date | null>(null);
  readonly editTo = signal<Date | null>(null);
  readonly saving = signal(false);
  protected readonly today = new Date();

  readonly roleOptions = computed<Option<LeadershipRole>[]>(() => {
    const office = this.editingOffice();
    return office ? ROLES_BY_TYPE[office.type].map(role => ({ label: leadershipRoleDisplayName(role, this.kurinKey), value: role })) : [];
  });
  readonly kindOptions = computed<Option<MembershipKind>[]>(() =>
    [MembershipKind.Youth, MembershipKind.Staff].map(kind => ({ label: membershipKindLabel(kind, this.features.branch()), value: kind })));
  readonly canSaveEdit = computed(() => {
    const from = this.editFrom();
    const to = this.editTo();
    return !!from && !!to && from <= to && to <= this.today && (!this.editingOffice() || !!this.editRole());
  });

  // ── Небезпечний режим ──
  readonly zone = signal<ArchiveDangerZoneDto | null>(null);
  readonly entering = signal(false);
  readonly proofMethod = signal<ProofMethod>('password');
  readonly proof = signal('');
  readonly proofError = signal<string | null>(null);
  readonly sendingProof = signal(false);
  private readonly now = signal(Date.now());
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly secondsLeft = computed(() => {
    const zone = this.zone();
    return zone ? Math.max(0, Math.round((Date.parse(zone.expiresAtUtc) - this.now()) / 1000)) : 0;
  });
  readonly clock = computed(() => `${Math.floor(this.secondsLeft() / 60)}:${pad(this.secondsLeft() % 60)}`);
  readonly deletionsLeftLabel = computed(() => {
    const left = this.zone()?.deletionsLeft ?? 0;
    // «1 видалення», «2–4 видалення», «5 видалень»: the first two forms coincide for this noun.
    const lastTwo = left % 100;
    const few = left % 10 >= 1 && left % 10 <= 4 && (lastTwo < 11 || lastTwo > 14);
    return `ще ${left} ${few ? 'видалення' : 'видалень'}`;
  });

  ngOnInit(): void {
    this.kurinKey = this.auth.getAuthStateValue()?.kurinKey ?? '';
    this.destroyRef.onDestroy(() => this.stopTimer());
    this.load();
    this.loadReturns();
  }

  /** Leaving the mode ends the zone, whether for another mode or another page: that is the rule. */
  ngOnDestroy(): void {
    if (this.zone()) {
      this.zone.set(null);
      this.archiveService.endDangerZone(this.kurinKey, 'Left').subscribe({ error: () => undefined });
      this.messages.add({ severity: 'info', summary: 'Небезпечний режим вимкнено' });
    }
  }

  returnFor(membership: ArchivedMembershipDto): FormerMemberStandingDto | null {
    if (!this.canReturn || this.latestMembership().get(membership.memberKey) !== membership) {
      return null;
    }
    return this.standing().get(membership.memberKey) ?? null;
  }

  standingLine(membership: ArchivedMembershipDto): string {
    const person = this.standing().get(membership.memberKey);
    return person && this.latestMembership().get(membership.memberKey) === membership ? standsInLine(person.standsIn) : '';
  }

  reloadAfterReturn(): void {
    this.load();
    this.loadReturns();
  }

  private loadReturns(): void {
    if (!this.canReturn || !this.kurinKey) {
      return;
    }
    this.membershipService.formerStanding(this.kurinKey).subscribe({
      next: people => this.standing.set(new Map(people.map(person => [person.former.memberKey, person]))),
      error: () => this.standing.set(new Map())
    });
    this.membershipService.requests(this.kurinKey).subscribe({
      next: requests => this.asked.set(new Set(requests.outgoing.filter(r => r.status === 'Pending').map(r => r.memberKey))),
      error: () => this.asked.set(new Set())
    });
  }

  @HostListener('window:pagehide')
  onPageHide(): void {
    if (this.zone()) {
      this.zone.set(null);
      this.archiveService.endDangerZoneOnUnload(this.kurinKey);
    }
  }

  roleLabel(office: ArchivedOfficeDto): string {
    const role = leadershipRoleDisplayName(office.role, this.kurinKey);
    return office.groupName ? `${role} · ${office.groupName}` : role;
  }

  kindLabel(kind: MembershipKind): string {
    return membershipKindLabel(kind, this.features.branch());
  }

  stintsLine(membership: ArchivedMembershipDto): string {
    return membership.groups.map(stint => stint.groupName ?? 'видалений гурток').join(' → ') || '—';
  }

  groupLeaders(group: DissolvedGroupDto): string {
    return (this.archive()?.offices ?? [])
      .filter(office => office.groupKey === group.groupKey)
      .map(office => `${leadershipRoleDisplayName(office.role, this.kurinKey)}: ${office.memberName}, ${archiveDate(office.startDate)}–${archiveDate(office.endDate)}`)
      .join('; ') || '—';
  }

  canDeleteOffice(office: ArchivedOfficeDto): boolean {
    return !!this.zone() && office.endDate !== null && !office.isOwn;
  }

  canDeleteMembership(membership: ArchivedMembershipDto): boolean {
    return !!this.zone() && !membership.isOwn;
  }

  // ── Виправлення ──

  openOffice(office: ArchivedOfficeDto): void {
    this.editingMembership.set(null);
    this.editingOffice.set(office);
    this.editRole.set(office.role);
    this.editFrom.set(toDate(office.startDate));
    this.editTo.set(office.endDate ? toDate(office.endDate) : null);
  }

  openMembership(membership: ArchivedMembershipDto): void {
    this.editingOffice.set(null);
    this.editingMembership.set(membership);
    this.editKind.set(membership.kind);
    this.editFrom.set(toDate(membership.joinedOn));
    this.editTo.set(toDate(membership.leftOn));
  }

  closeEdit(): void {
    this.editingOffice.set(null);
    this.editingMembership.set(null);
  }

  saveEdit(): void {
    const from = this.editFrom();
    const to = this.editTo();
    if (!this.canSaveEdit() || !from || !to) {
      return;
    }
    const office = this.editingOffice();
    const membership = this.editingMembership();
    const request = office
      ? this.archiveService.correctOffice(this.kurinKey, office.historyKey, { role: this.editRole()!, startDate: fromDate(from), endDate: fromDate(to) })
      : this.archiveService.correctMembership(this.kurinKey, membership!.membershipKey, { kind: this.editKind(), joinedOn: fromDate(from), leftOn: fromDate(to) });
    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeEdit();
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.refuse(error);
      }
    });
  }

  // ── Небезпечний режим ──

  openEnter(): void {
    this.proof.set('');
    this.proofError.set(null);
    this.entering.set(true);
  }

  setProofMethod(method: ProofMethod): void {
    this.proofMethod.set(method);
    this.proof.set('');
    this.proofError.set(null);
  }

  enter(): void {
    const value = this.proof().trim();
    if (!value || this.sendingProof()) {
      return;
    }
    this.sendingProof.set(true);
    this.proofError.set(null);
    const proof = this.proofMethod() === 'password' ? { password: this.proof() } : { code: value };
    this.archiveService.enterDangerZone(this.kurinKey, proof).subscribe({
      next: zone => {
        this.sendingProof.set(false);
        this.entering.set(false);
        this.proof.set('');
        this.zone.set(zone);
        this.startTimer();
        this.messages.add({ severity: 'warn', summary: 'Небезпечний режим увімкнено' });
      },
      error: (error: unknown) => {
        this.sendingProof.set(false);
        this.proofError.set(this.proofErrorText(error));
      }
    });
  }

  end(): void {
    if (!this.zone()) {
      return;
    }
    this.closeZone();
    this.archiveService.endDangerZone(this.kurinKey, 'Manual').subscribe({ error: () => undefined });
    this.messages.add({ severity: 'info', summary: 'Небезпечний режим вимкнено' });
  }

  confirmDeleteOffice(office: ArchivedOfficeDto): void {
    this.confirmDelete(
      'Видалити запис ролі?',
      `${this.roleLabel(office)} · ${office.memberName}, ${archiveDate(office.startDate)} – ${archiveDate(office.endDate)}.`,
      ticket => this.archiveService.deleteOffice(this.kurinKey, office.historyKey, ticket));
  }

  confirmDeleteMembership(membership: ArchivedMembershipDto): void {
    this.confirmDelete(
      'Видалити членство?',
      `${membership.memberName}, ${archiveDate(membership.joinedOn)} – ${archiveDate(membership.leftOn)}: членство, ролі за цей час і гуртки. Проби, вмілості й вкладка лишаються.`,
      ticket => this.archiveService.deleteMembership(this.kurinKey, membership.membershipKey, ticket));
  }

  confirmDeleteGroup(group: DissolvedGroupDto): void {
    this.confirmDelete(
      'Видалити гурток назавжди?',
      `«${group.name}» з усім його проводом.`,
      ticket => this.archiveService.deleteGroup(this.kurinKey, group.groupKey, ticket));
  }

  private confirmDelete(header: string, message: string, send: (ticket: string) => ReturnType<KurinArchiveService['deleteOffice']>): void {
    this.confirmation.confirm({
      header,
      message,
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        const zone = this.zone();
        if (!zone) {
          return;
        }
        send(zone.ticket).subscribe({
          next: result => {
            if (result.deletionsLeft <= 0) {
              this.closeZone();
            } else {
              this.zone.set({ ...zone, deletionsLeft: result.deletionsLeft });
            }
            this.load();
          },
          error: (error: unknown) => {
            if (error instanceof HttpErrorResponse && error.error?.error === 'DangerZoneClosed') {
              this.closeZone();
            } else {
              this.refuse(error);
            }
            this.load();
          }
        });
      }
    });
  }

  /** The server keeps a record that dues, a held office or another membership still lean on. */
  private refuse(error: unknown): void {
    const code = error instanceof HttpErrorResponse ? error.error?.error : null;
    const summary = ({
      ArchiveMembershipHasDues: 'За членством є внески',
      ArchiveOpenOffice: 'Роль із того часу ще триває',
      ArchiveMembershipOverlap: 'Дати перетинаються з іншим членством'
    } as Record<string, string>)[code ?? ''] ?? 'Не вдалося зберегти';
    this.messages.add({ severity: 'error', summary });
  }

  private proofErrorText(error: unknown): string {
    const code = error instanceof HttpErrorResponse ? error.error?.error : null;
    switch (code) {
      case 'WrongCredentials':
        return this.proofMethod() === 'password' ? 'Неправильний пароль' : 'Неправильний код';
      case 'NoSecondFactor':
        return 'Без двофакторного входу — лише пароль';
      case 'TooManyAttempts':
        return 'Забагато спроб. Спробуй за 15 хвилин';
      default:
        return 'Не вдалося увімкнути';
    }
  }

  private startTimer(): void {
    this.stopTimer();
    this.now.set(Date.now());
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      if (this.zone() && this.secondsLeft() <= 0) {
        this.closeZone();
        this.archiveService.endDangerZone(this.kurinKey, 'Manual').subscribe({ error: () => undefined });
      }
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  private closeZone(): void {
    this.zone.set(null);
    this.stopTimer();
  }

  private load(): void {
    this.archiveService.get(this.kurinKey).subscribe({
      next: archive => {
        this.archive.set(archive);
        this.loading.set(false);
        this.loadFailed.set(false);
        // `?former=1`: a card that just released someone sends the Звʼязковий to the former members.
        if (this.scrollToFormer) {
          this.scrollToFormer = false;
          setTimeout(() => document.getElementById('former')?.scrollIntoView({ block: 'start' }));
        }
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      }
    });
  }
}
