import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from '@openng/optimus-ui/table';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { TagModule } from '@openng/optimus-ui/tag';

import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService } from '@openng/optimus-ui/api';

import { MemberService } from '../../../../services/member-service/member.service';
import { KurinService } from '../../../../services/kurin-service/kurin.service';
import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import { MemberDto } from '../../../../models/member.dto';
import { KurinBranch, hasGroups } from '../../../../models/enums/kurin-branch.enum';
import { EmptyStateComponent } from '../../../../../../shared/empty-state/empty-state';
import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { PLAST_LEVEL_COLUMN_LABELS, defaultLevelsFor } from '../../../../models/enums/plast-ladder';
import { REGISTRY_COLUMNS, RegistryColumn, columnsFor, defaultColumnIdsFor, staffColumnsOf } from './registry-columns';
import { emailHref, phoneHref } from '../../../../../../shared/functions/contact-href.function';
import { KurinTagService } from '../../../../services/kurin-tag-service/kurin-tag.service';
import { KurinTagChipsComponent } from '../../../../components/kurin-tag-chips/kurin-tag-chips';
import { KurinTagManagerComponent, TagCandidate } from '../../../../components/kurin-tag-manager/kurin-tag-manager';
import { RegistrySearchComponent } from '../../components/registry-search/registry-search';

/** Рядок таблиці реєстру: людина й назви її тегів, за якими таблиця сортує колонку «Теги». */
export interface RegistryRow extends MemberDto {
  readonly tagSort: string;
}

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
  selector: 'app-registry-roster',
  imports: [
    RegistrySearchComponent,
    TableModule,
    ButtonModule,
    MultiSelectModule,
    TagModule,
    ConfirmDialogModule,
    FormsModule,
    DatePipe,
    RouterLink,
    EmptyStateComponent,
    KurinTagChipsComponent,
    KurinTagManagerComponent
  ],
  templateUrl: './roster.html',
  styleUrl: './roster.css',
  providers: [ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush
})
/**
 * «Склад» — the registry's first mode: the kurin's people in rows, youth and staff apart, with every
 * field the viewer may read, tags and the export. Former members live in the archive mode.
 */
export class RosterComponent implements OnInit {
  private readonly memberService = inject(MemberService);
  private readonly router = inject(Router);
  private readonly kurinService = inject(KurinService);
  private readonly authService = inject(AuthService);
  protected readonly tagService = inject(KurinTagService);

  readonly members = signal<MemberDto[]>([]);
  readonly loading = signal(true);
  readonly branch = signal<KurinBranch | null>(null);
  readonly kurinNumber = signal<number | null>(null);
  readonly selectedColumnIds = signal<string[]>([]);
  readonly exporting = signal(false);
  readonly search = signal('');

  /**
   * Колонки, які можна вмикати. `mentoredGroups` не серед них: у таблиці впорядників вона стоїть
   * завжди, а в таблиці юнаків не значить нічого — вибирати там нічого.
   */
  readonly allColumns = computed<RegistryColumn[]>(() => columnsFor(
    REGISTRY_COLUMNS.filter(column => column.id !== 'mentoredGroups' && (column.kind !== 'tags' || this.tagService.enabled())),
    this.branch()));
  /** Чи є в курені гуртки: у старшому їх немає, і реєстр про них не питає. */
  readonly hasGroups = computed(() => hasGroups(this.branch()));

  /** Обрані колонки в порядку драбини, а не в порядку кліків. */
  readonly visibleColumns = computed<RegistryColumn[]>(() => {
    const chosen = new Set(this.selectedColumnIds());
    return this.allColumns().filter(column => chosen.has(column.id));
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
  private readonly found = computed<RegistryRow[]>(() => {
    const needle = this.search().trim().toLowerCase();
    const wanted = new Set(this.tagFilter());
    const withTags = (member: MemberDto): RegistryRow => ({
      ...member,
      tagSort: this.tagService.resolve(member.tagKeys).map(tag => tag.name).join(', ')
    });

    return this.members()
      .filter(member => !wanted.size || (member.tagKeys ?? []).some(key => wanted.has(key)))
      .filter(member => !needle || this.matches(member, needle))
      .map(withTags);
  });

  /** Обрані в фільтрі теги; людина лишається в реєстрі, якщо має бодай один із них. */
  readonly tagFilter = signal<string[]>([]);

  private matches(member: MemberDto, needle: string): boolean {
    return [
      member.lastName,
      member.firstName,
      member.middleName,
      member.email,
      member.phoneNumber,
      member.groupName,
      ...(member.mentoredGroupNames ?? []),
      ...this.tagService.resolve(member.tagKeys).map(tag => tag.name)
    ].some(field => field?.toLowerCase().includes(needle));
  }

  /** Панель «Теги куреня» — для того, кому курінь дав право ними керувати. */
  readonly tagsOpen = signal(false);

  /** Кому можна поставити тег: увесь склад, з тегами, які людина вже має. */
  readonly tagCandidates = computed<TagCandidate[]>(() => this.members().map(member => ({
    memberKey: member.memberKey,
    name: this.fullName(member),
    tagKeys: member.tagKeys ?? []
  })));

  private kurinKey = '';
  readonly kurinKeyValue = signal('');

  constructor() {
    // Теги лежать на людях: після зміни тегів склад треба перечитати, щоб плашки збіглися.
    let seen = this.tagService.revision();
    effect(() => {
      const revision = this.tagService.revision();
      if (revision !== seen && this.kurinKey) {
        seen = revision;
        untracked(() => this.loadMembers());
      }
    });
  }

  ngOnInit(): void {
    this.authService.getAuthState().subscribe(state => {
      if (!state?.kurinKey) {
        return;
      }

      this.kurinKey = state.kurinKey;
      this.kurinKeyValue.set(state.kurinKey);
      this.loadKurin();
      this.loadMembers();
      this.tagService.load(this.kurinKey).subscribe({ error: () => undefined });
    });
  }

  private loadKurin(): void {
    this.kurinService.getByKey(this.kurinKey).subscribe({
      next: kurin => {
        const branch = kurin.branch ?? KurinBranch.UPYu;
        this.branch.set(branch);
        this.kurinNumber.set(kurin.number);
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
    // The columns on screen, not every one ever chosen: a kurin with tags off has no «Теги» column to export.
    const columns = this.visibleColumns().map(column => column.id);
    this.kurinService.exportRegistry(this.kurinKey, columns).subscribe({
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

      const known = new Set(columnsFor(REGISTRY_COLUMNS, branch).map(column => column.id));
      const kept = parsed.filter((id): id is string => typeof id === 'string' && known.has(id));
      return kept.length ? kept : defaultColumnIdsFor(branch);
    } catch {
      return defaultColumnIdsFor(branch);
    }
  }
}
