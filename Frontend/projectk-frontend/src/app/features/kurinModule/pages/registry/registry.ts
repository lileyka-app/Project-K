import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from '@openng/optimus-ui/table';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { TagModule } from '@openng/optimus-ui/tag';

import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';

import { MemberService } from '../../services/member-service/member.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { FormerMemberDto, MembershipService } from '../../services/membership-service/membership.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { MemberDto } from '../../models/member.dto';
import { KurinBranch, KURIN_BRANCH_LABELS } from '../../models/enums/kurin-branch.enum';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { PlastLevel } from '../../models/enums/plast-level.enum';
import { PLAST_LEVEL_COLUMN_LABELS, defaultLevelsFor } from '../../models/enums/plast-ladder';
import { REGISTRY_COLUMNS, RegistryColumn, defaultColumnIdsFor, staffColumnsOf } from './registry-columns';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { emailHref, phoneHref } from '../../../../shared/functions/contact-href.function';

/** Рядок таблиці чисельності: скільки юнаків стоїть на цьому ступені. */
export interface TallyRow {
  readonly label: string;
  readonly count: number;
  /** Підсумок і «без ступеня» малюються інакше — вони не ступені драбини. */
  readonly kind: 'level' | 'rest' | 'total';
}

/** Ключ вибору колонок. На курінь, бо в різних гілках потрібне різне. */
const columnChoiceKey = (kurinKey: string) => `registry:columns:${kurinKey}`;

@Component({
  selector: 'app-registry',
  imports: [
    TableModule,
    ButtonModule,
    MultiSelectModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
    TagModule,
    ConfirmDialogModule,
    FormsModule,
    DatePipe,
    RouterLink,
    EmptyStateComponent
  ],
  templateUrl: './registry.html',
  styleUrl: './registry.css',
  providers: [ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegistryComponent implements OnInit {
  private readonly memberService = inject(MemberService);
  private readonly router = inject(Router);
  private readonly kurinService = inject(KurinService);
  private readonly authService = inject(AuthService);
  private readonly membershipService = inject(MembershipService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  readonly members = signal<MemberDto[]>([]);
  readonly loading = signal(true);
  readonly branch = signal<KurinBranch | null>(null);
  readonly kurinNumber = signal<number | null>(null);
  /** Tells a підготовчий курінь (no number) from a kurin not loaded yet. */
  readonly kurinLoaded = signal(false);
  readonly selectedColumnIds = signal<string[]>([]);
  readonly exporting = signal(false);
  readonly search = signal('');

  /**
   * Кого курінь вивів. Окремо від складу й згорнуто: це не щоденний список, але без нього
   * помилковий клік прибирав людину назовсім — усі читання членства фільтрують закриті рядки, і
   * повернути її можна було лише за кодом, який тримає вона сама.
   */
  readonly former = signal<FormerMemberDto[]>([]);
  readonly formerOpen = signal(false);
  readonly returning = signal<string | null>(null);

  /**
   * Колонки, які можна вмикати. `mentoredGroups` не серед них: у таблиці впорядників вона стоїть
   * завжди, а в таблиці юнаків не значить нічого — вибирати там нічого.
   */
  readonly allColumns: RegistryColumn[] = REGISTRY_COLUMNS.filter(column => column.id !== 'mentoredGroups');

  readonly branchLabel = computed(() => {
    const branch = this.branch();
    return branch ? KURIN_BRANCH_LABELS[branch] : null;
  });

  /** Обрані колонки в порядку драбини, а не в порядку кліків. */
  readonly visibleColumns = computed<RegistryColumn[]>(() => {
    const chosen = new Set(this.selectedColumnIds());
    return this.allColumns.filter(column => chosen.has(column.id));
  });

  /**
   * Юнаки й впорядники — окремими таблицями, бо це різні питання до одного складу. Розділяє діловодство
   * у кадрі виховників, який рахує бекенд: гуртковий теж має діловодство і теж юнак.
   */
  readonly youth = computed(() => this.found().filter(member => !member.isStaff));
  readonly staff = computed(() => this.found().filter(member => member.isStaff));

  /** Ті самі колонки, але закріплення попереду й без власного гуртка. */
  readonly staffColumns = computed(() => staffColumnsOf(this.visibleColumns()));

  /**
   * Чисельність за ступенями — лише юнацтво: впорядники рахуються як кадра, не як склад юнаків.
   * Рядки — драбина цієї гілки, тож УСП- і УПС-курінь рахують свої ступені, а не юнацькі.
   */
  readonly tally = computed<TallyRow[]>(() => {
    const youth = this.youth();
    const levels = defaultLevelsFor(this.branch());
    const rows: TallyRow[] = levels.map(level => ({
      label: PLAST_LEVEL_COLUMN_LABELS[level],
      count: youth.filter(member => member.latestPlastLevel === level).length,
      kind: 'level' as const
    }));

    // Хто стоїть поза драбиною цієї гілки або взагалі без ступеня. Без цього рядка стовпчик не
    // сходився б зі складом — а саме незаписаний ступінь провід тут і шукає.
    const onLadder = new Set<string>(levels as readonly PlastLevel[]);
    const rest = youth.filter(member => !member.latestPlastLevel || !onLadder.has(member.latestPlastLevel)).length;
    if (rest) {
      rows.push({ label: 'Без ступеня / інший', count: rest, kind: 'rest' });
    }

    rows.push({ label: 'Разом', count: youth.length, kind: 'total' });
    return rows;
  });

  /**
   * Пошук живе тут, а не у фільтрі таблиці: таблиць дві, а поле одне, і людина шукає по складу —
   * не знаючи наперед, у якій із двох таблиць той, кого вона шукає.
   */
  private readonly found = computed(() => {
    const needle = this.search().trim().toLowerCase();
    if (!needle) {
      return this.members();
    }

    return this.members().filter(member =>
      [
        member.lastName,
        member.firstName,
        member.middleName,
        member.email,
        member.phoneNumber,
        member.groupName,
        ...(member.mentoredGroupNames ?? [])
      ].some(field => field?.toLowerCase().includes(needle)));
  });

  private kurinKey = '';

  ngOnInit(): void {
    this.authService.getAuthState().subscribe(state => {
      if (!state?.kurinKey) {
        return;
      }

      this.kurinKey = state.kurinKey;
      this.loadKurin();
      this.loadMembers();
      this.loadFormer();
    });
  }

  private loadKurin(): void {
    this.kurinService.getByKey(this.kurinKey).subscribe({
      next: kurin => {
        const branch = kurin.branch ?? KurinBranch.UPYu;
        this.branch.set(branch);
        this.kurinNumber.set(kurin.number);
        this.kurinLoaded.set(true);
        this.selectedColumnIds.set(this.restoreColumnChoice(branch));
      },
      // A kurin we cannot read still lists its people; the columns just fall back to the youth set.
      error: () => this.selectedColumnIds.set(this.restoreColumnChoice(KurinBranch.UPYu))
    });
  }

  private loadMembers(): void {
    this.loading.set(true);
    this.memberService.getAll(undefined, this.kurinKey).subscribe({
      next: members => {
        this.members.set([...members].sort((left, right) => this.byName(left, right)));
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private loadFormer(): void {
    // Тихо: колишні — довідка, а не робота. Курінь, у якому ще нікого не виводили, не має бачити
    // помилку через порожній список.
    this.membershipService.former(this.kurinKey).subscribe({
      next: people => this.former.set(people),
      error: () => this.former.set([])
    });
  }

  formerName(person: FormerMemberDto): string {
    return [person.lastName, person.firstName, person.middleName].filter(Boolean).join(' ');
  }

  toggleFormer(): void {
    this.formerOpen.update(open => !open);
  }

  /**
   * Повернути в курінь. Питаємо перед тим — не тому, що дія небезпечна (вона зворотна), а тому, що
   * це зміна складу, і провід має побачити, кого саме повертає.
   */
  confirmTakeBack(person: FormerMemberDto): void {
    this.confirmationService.confirm({
      header: 'Повернути в курінь',
      message: `${this.formerName(person)} знову стане членом куреня. Гурток можна буде вказати `
        + 'на її картці — повернення саме по собі нікуди її не ставить.',
      icon: 'pi pi-user-plus',
      acceptButtonProps: { label: 'Повернути' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => this.takeBack(person)
    });
  }

  private takeBack(person: FormerMemberDto): void {
    this.returning.set(person.memberKey);
    this.membershipService.takeBack(this.kurinKey, person.memberKey).subscribe({
      next: () => {
        this.returning.set(null);
        this.former.update(people => people.filter(other => other.memberKey !== person.memberKey));
        this.loadMembers();
        this.messageService.add({
          severity: 'success',
          summary: 'Повернено',
          detail: `${this.formerName(person)} знову в курені.`
        });
      },
      error: (error: unknown) => {
        this.returning.set(null);
        this.messageService.add({
          severity: 'error',
          summary: 'Не вдалося повернути',
          detail: failureDetail(error)
        });
      }
    });
  }

  private byName(left: MemberDto, right: MemberDto): number {
    return `${left.lastName} ${left.firstName}`.localeCompare(`${right.lastName} ${right.firstName}`, 'uk');
  }

  fullName(member: MemberDto): string {
    return [member.lastName, member.firstName, member.middleName].filter(Boolean).join(' ');
  }

  cell(member: MemberDto, column: RegistryColumn): string | Date | null {
    return column.value(member);
  }

  asDate(value: string | Date | null): Date | null {
    return value instanceof Date ? value : null;
  }

  contactHref(member: MemberDto, column: RegistryColumn): string | null {
    const value = column.value(member);
    if (typeof value !== 'string') {
      return null;
    }
    switch (column.kind) {
      case 'phone':
        return phoneHref(value);
      case 'email':
        return emailHref(value);
      default:
        return null;
    }
  }

  openCard(event: Event, member: MemberDto): void {
    const target = event.target instanceof Element ? event.target : null;
    if (target?.closest('a, button') || window.getSelection()?.toString()) {
      return;
    }
    void this.router.navigate(['/member', member.memberKey]);
  }

  onColumnsChange(ids: string[]): void {
    this.selectedColumnIds.set(ids);
    if (this.kurinKey) {
      localStorage.setItem(columnChoiceKey(this.kurinKey), JSON.stringify(ids));
    }
  }

  resetColumns(): void {
    const ids = defaultColumnIdsFor(this.branch());
    this.selectedColumnIds.set(ids);
    if (this.kurinKey) {
      localStorage.removeItem(columnChoiceKey(this.kurinKey));
    }
  }

  /** Вивантажує рівно те, що на екрані — і з тим самим маскуванням, бо файл робить той самий читач. */
  exportToExcel(): void {
    if (!this.kurinKey || this.exporting()) {
      return;
    }

    this.exporting.set(true);
    this.kurinService.exportRegistry(this.kurinKey, this.selectedColumnIds()).subscribe({
      next: response => {
        if (response.body) {
          this.saveBlob(response.body, this.fileNameFrom(response));
        }
        this.exporting.set(false);
      },
      error: () => this.exporting.set(false)
    });
  }

  private fileNameFrom(response: HttpResponse<Blob>): string {
    const disposition = response.headers.get('content-disposition');
    const match = disposition?.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i);
    if (match?.[1]) {
      return decodeURIComponent(match[1].replace(/"$/g, ''));
    }

    return `reyestr-kurin-${this.kurinNumber() ?? ''}.xlsx`;
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.URL.revokeObjectURL(url);
  }

  /**
   * Що людина вибрала минулого разу — або пресет гілки. Збережений вибір чиститься від колонок,
   * яких уже немає: інакше видалена колонка жила б у сховищі вічно.
   */
  private restoreColumnChoice(branch: KurinBranch): string[] {
    const stored = localStorage.getItem(columnChoiceKey(this.kurinKey));
    if (!stored) {
      return defaultColumnIdsFor(branch);
    }

    try {
      const parsed: unknown = JSON.parse(stored);
      if (!Array.isArray(parsed)) {
        return defaultColumnIdsFor(branch);
      }

      const known = new Set(this.allColumns.map(column => column.id));
      const kept = parsed.filter((id): id is string => typeof id === 'string' && known.has(id));
      return kept.length ? kept : defaultColumnIdsFor(branch);
    } catch {
      return defaultColumnIdsFor(branch);
    }
  }
}
