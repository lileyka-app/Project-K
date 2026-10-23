import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SelectModule } from '@openng/optimus-ui/select';
import { TagModule } from '@openng/optimus-ui/tag';
import { DrawerModule } from '@openng/optimus-ui/drawer';
import { MessageModule } from '@openng/optimus-ui/message';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { KurinJournalService } from '../../services/kurin-journal-service/kurin-journal.service';
import { MemberService } from '../../services/member-service/member.service';
import { JournalArea, KurinJournalEntryDto } from '../../models/kurin-journal.dto';
import {
  JOURNAL_AREA_LABELS,
  journalActionLabel,
  JournalChangeView,
  journalChangeView,
  journalRoleLabel
} from '../../functions/journal-labels.function';
import { parseUtcDateTime } from '../../../../shared/functions/utc-date-time.function';

/** How far back the «Період» filter looks; the journal itself keeps a year. */
export type JournalPeriod = 'all' | 'day' | 'week' | 'month' | 'quarter';

interface Option<T> {
  label: string;
  value: T;
}

/** One entry, worded and timed for the page. */
export interface JournalEntryView {
  key: string;
  time: string;
  actor: string;
  role: string | null;
  sentence: string;
  subjectLabel: string;
  memberKey: string | null;
  memberName: string | null;
  changes: JournalChangeView[];
  note: string | null;
}

/** The entries of one day, newest first. */
export interface JournalDayView {
  key: string;
  label: string;
  entries: JournalEntryView[];
}

const PAGE_SIZE = 20;

const PERIOD_DAYS: Record<Exclude<JournalPeriod, 'all'>, number> = {
  day: 1,
  week: 7,
  month: 30,
  quarter: 91
};

const pad = (value: number): string => String(value).padStart(2, '0');

/**
 * «Журнал дій» of the kurin: who did what, to whom and what changed, newest first and grouped by
 * day. How much of it a reader sees is the kurin's policy; the server answers only with that part.
 * Filters sit above the list on a computer and in a drawer on a phone.
 */
@Component({
  selector: 'app-kurin-journal',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    FormsModule,
    ButtonModule,
    SelectModule,
    TagModule,
    DrawerModule,
    MessageModule,
    SkeletonModule,
    EmptyStateComponent
  ],
  templateUrl: './kurin-journal.html',
  styleUrl: './kurin-journal.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinJournalComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly journal = inject(KurinJournalService);
  private readonly members = inject(MemberService);

  protected readonly areaOptions: Option<JournalArea | null>[] = [
    { label: 'Усі розділи', value: null },
    ...(Object.entries(JOURNAL_AREA_LABELS) as [JournalArea, string][]).map(([value, label]) => ({ label, value }))
  ];

  protected readonly periodOptions: Option<JournalPeriod>[] = [
    { label: 'За весь час', value: 'all' },
    { label: 'Сьогодні', value: 'day' },
    { label: 'За тиждень', value: 'week' },
    { label: 'За місяць', value: 'month' },
    { label: 'За три місяці', value: 'quarter' }
  ];

  protected kurinKey = '';

  readonly area = signal<JournalArea | null>(null);
  readonly memberKey = signal<string | null>(null);
  readonly actorUserKey = signal<string | null>(null);
  readonly period = signal<JournalPeriod>('all');

  readonly entries = signal<KurinJournalEntryDto[]>([]);
  readonly hasMore = signal(false);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly loadFailed = signal(false);
  readonly filtersOpen = signal(false);

  private readonly people = signal<{ memberKey: string; userKey: string | null; name: string }[]>([]);

  readonly personOptions = computed<Option<string>[]>(() => this.people()
    .map(person => ({ label: person.name, value: person.memberKey })));

  /** Whoever acted: the kurin's people with an account, and anyone else the loaded entries name. */
  readonly actorOptions = computed<Option<string>[]>(() => {
    const options = new Map<string, string>();
    for (const person of this.people()) {
      if (person.userKey) {
        options.set(person.userKey.toLowerCase(), person.name);
      }
    }
    for (const entry of this.entries()) {
      const key = entry.actorUserKey?.toLowerCase();
      if (key && entry.actorName && !options.has(key)) {
        options.set(key, entry.actorName);
      }
    }
    return [...options.entries()]
      .map(([value, label]) => ({ label, value }))
      .sort((left, right) => left.label.localeCompare(right.label, 'uk'));
  });

  readonly activeFilterCount = computed(() =>
    [this.area(), this.memberKey(), this.actorUserKey()].filter(value => !!value).length
    + (this.period() === 'all' ? 0 : 1));

  readonly days = computed<JournalDayView[]>(() => groupByDay(this.entries(), new Date()));

  ngOnInit(): void {
    this.kurinKey = this.route.snapshot.paramMap.get('kurinKey') ?? '';
    this.memberKey.set(this.route.snapshot.queryParamMap.get('member'));
    this.loadPeople();
    this.reload();
  }

  setArea(value: JournalArea | null): void {
    this.area.set(value ?? null);
    this.reload();
  }

  setMember(value: string | null): void {
    this.memberKey.set(value ?? null);
    this.reload();
  }

  setActor(value: string | null): void {
    this.actorUserKey.set(value ?? null);
    this.reload();
  }

  setPeriod(value: JournalPeriod | null): void {
    this.period.set(value ?? 'all');
    this.reload();
  }

  resetFilters(): void {
    this.area.set(null);
    this.memberKey.set(null);
    this.actorUserKey.set(null);
    this.period.set('all');
    this.reload();
  }

  /** The first page for the current filters, replacing whatever was shown. */
  reload(): void {
    if (!this.kurinKey) {
      this.loading.set(false);
      this.loadFailed.set(true);
      return;
    }

    this.loading.set(true);
    this.loadFailed.set(false);
    this.journal.getPage(this.kurinKey, this.query(0)).subscribe({
      next: page => {
        this.entries.set(page.items);
        this.hasMore.set(page.hasMore);
        this.loading.set(false);
      },
      error: () => {
        this.entries.set([]);
        this.hasMore.set(false);
        this.loadFailed.set(true);
        this.loading.set(false);
      }
    });
  }

  loadMore(): void {
    if (this.loadingMore() || !this.hasMore()) {
      return;
    }

    this.loadingMore.set(true);
    this.journal.getPage(this.kurinKey, this.query(this.entries().length)).subscribe({
      next: page => {
        this.entries.update(entries => [...entries, ...page.items]);
        this.hasMore.set(page.hasMore);
        this.loadingMore.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loadingMore.set(false);
      }
    });
  }

  private query(skip: number) {
    const days = this.period() === 'all' ? null : PERIOD_DAYS[this.period() as Exclude<JournalPeriod, 'all'>];
    return {
      area: this.area(),
      memberKey: this.memberKey(),
      actorUserKey: this.actorUserKey(),
      fromUtc: days === null ? null : new Date(Date.now() - days * 86_400_000).toISOString(),
      skip,
      take: PAGE_SIZE
    };
  }

  /** The kurin's people for the «Людина» and «Хто» lists; without them the filters still take a link's member. */
  private loadPeople(): void {
    if (!this.kurinKey) {
      return;
    }
    this.members.getAll(undefined, this.kurinKey).pipe(catchError(() => of([]))).subscribe(members => {
      this.people.set(members
        .map(member => ({
          memberKey: member.memberKey,
          userKey: member.userKey ?? null,
          name: [member.lastName, member.firstName].filter(Boolean).join(' ')
        }))
        .sort((left, right) => left.name.localeCompare(right.name, 'uk')));
    });
  }
}

/** Entries by the reader's calendar day, in the order they came (newest first). */
export function groupByDay(entries: KurinJournalEntryDto[], now: Date): JournalDayView[] {
  const days: JournalDayView[] = [];
  for (const entry of entries) {
    const moment = parseUtcDateTime(entry.occurredAtUtc) ?? new Date(entry.occurredAtUtc);
    const key = `${moment.getFullYear()}-${pad(moment.getMonth() + 1)}-${pad(moment.getDate())}`;
    let day = days.at(-1);
    if (day?.key !== key) {
      day = { key, label: dayLabel(moment, now), entries: [] };
      days.push(day);
    }
    day.entries.push(entryView(entry, moment));
  }
  return days;
}

function entryView(entry: KurinJournalEntryDto, moment: Date): JournalEntryView {
  return {
    key: entry.key,
    time: `${pad(moment.getHours())}:${pad(moment.getMinutes())}`,
    actor: entry.actorName?.trim() || 'Система',
    role: journalRoleLabel(entry.actorRole),
    sentence: journalActionLabel(entry),
    subjectLabel: entry.subjectLabel,
    memberKey: entry.subjectMemberKey,
    memberName: entry.subjectMemberName,
    changes: (entry.changes ?? []).map(journalChangeView),
    note: entry.note?.trim() || null
  };
}

function dayLabel(moment: Date, now: Date): string {
  const startOf = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  const daysAgo = Math.round((startOf(now) - startOf(moment)) / 86_400_000);
  if (daysAgo === 0) {
    return 'Сьогодні';
  }
  if (daysAgo === 1) {
    return 'Вчора';
  }
  return new Intl.DateTimeFormat('uk-UA', {
    day: 'numeric',
    month: 'long',
    year: moment.getFullYear() === now.getFullYear() ? undefined : 'numeric'
  }).format(moment);
}
