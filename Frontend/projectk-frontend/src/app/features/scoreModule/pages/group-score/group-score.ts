import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { SelectModule } from '@openng/optimus-ui/select';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { combineLatest } from 'rxjs';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { ScoreEntryDialogComponent, ScoreEntryTarget } from '../../components/score-entry-dialog/score-entry-dialog';
import { ScorePeriodSelectComponent } from '../../components/score-period-select/score-period-select';
import { periodParams, periodQueryFromParams, points, score } from '../../functions/score-format.function';
import { GroupScoreDto, ScoreEntryDto, ScorePeriodQuery, ScorePersonRowDto, UpsertScoreEntryRequest } from '../../models/score.dto';
import { SCORE_ALGORITHM_LABELS, SCORE_SOURCE_LABELS, SCORE_SOURCE_ORDER, ScoreAlgorithm, ScoreSource } from '../../models/score.enums';
import { ScoreService } from '../../services/score-service/score.service';

/**
 * One гурток's точкування: its standing, each youth's points by source, and every manual entry.
 * For those who score the гурток; a youth sees their own tile on the card instead.
 */
@Component({
  selector: 'app-group-score',
  imports: [
    DatePipe, FormsModule, RouterLink, ButtonModule, TableModule, TagModule, SelectModule, TooltipModule, SkeletonModule,
    ConfirmDialogModule, EmptyStateComponent, ScorePeriodSelectComponent, ScoreEntryDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './group-score.html',
  styleUrl: './group-score.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GroupScoreComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scores = inject(ScoreService);
  private readonly auth = inject(AuthService);
  private readonly confirmation = inject(ConfirmationService);

  readonly score = score;
  readonly points = points;
  readonly sourceLabels = SCORE_SOURCE_LABELS;
  readonly algorithmLabels = SCORE_ALGORITHM_LABELS;

  groupKey = '';
  readonly period = signal<ScorePeriodQuery>({});
  readonly data = signal<GroupScoreDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  readonly periodParams = computed(() => periodParams(this.period()));
  readonly kurinKey = computed(() => this.data()?.kurinKey ?? this.auth.getAuthStateValue()?.kurinKey ?? '');
  readonly isAverage = computed(() => this.data()?.algorithm === ScoreAlgorithm.Average);

  /** Only the sources anyone in the гурток has points from: a column of zeros says nothing. */
  readonly sources = computed(() => {
    const people = this.data()?.people ?? [];
    return SCORE_SOURCE_ORDER.filter(source => people.some(p => (p.bySource[source] ?? 0) !== 0));
  });

  readonly current = computed(() => (this.data()?.people ?? []).filter(p => p.standing === 'Current'));
  readonly gone = computed(() => (this.data()?.people ?? []).filter(p => p.standing !== 'Current'));

  readonly kindFilter = signal<'all' | 'people' | 'group'>('all');
  readonly kindFilterOptions = [
    { label: 'Усі записи', value: 'all' as const },
    { label: 'Юнакам', value: 'people' as const },
    { label: 'Гуртку цілим', value: 'group' as const }
  ];
  readonly filteredEntries = computed(() => {
    const kind = this.kindFilter();
    return (this.data()?.entries ?? []).filter(e => kind === 'all' || (kind === 'group') === e.isForGroup);
  });

  readonly entryDialogVisible = signal(false);
  readonly entryTarget = signal<ScoreEntryTarget | null>(null);
  readonly entryToEdit = signal<ScoreEntryDto | null>(null);
  readonly savingEntry = signal(false);
  readonly entryError = signal<string | null>(null);
  readonly busyKey = signal<string | null>(null);

  readonly entryTargets = computed<ScoreEntryTarget[]>(() => {
    const d = this.data();
    if (!d) {
      return [];
    }
    return [
      { membershipKey: null, groupKey: d.groupKey, name: `Гурток ${d.groupName} цілим` },
      ...this.current().map(p => ({ membershipKey: p.membershipKey, groupKey: null, name: p.fullName }))
    ];
  });

  ngOnInit(): void {
    combineLatest([this.route.paramMap, this.route.queryParamMap]).subscribe(([params, query]) => {
      this.groupKey = params.get('groupKey') ?? '';
      this.period.set(periodQueryFromParams(Object.fromEntries(query.keys.map(k => [k, query.get(k)]))));
      this.load();
    });
  }

  load(): void {
    const kurinKey = this.kurinKey();
    if (!this.groupKey || !kurinKey) {
      this.loadFailed.set(true);
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.loadFailed.set(false);
    this.scores.getGroupScore(kurinKey, this.groupKey, this.period()).subscribe({
      next: data => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      }
    });
  }

  changePeriod(query: ScorePeriodQuery): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: periodParams(query) });
  }

  bySource(person: ScorePersonRowDto, source: ScoreSource): number {
    return person.bySource[source] ?? 0;
  }

  standingLabel(person: ScorePersonRowDto): string | null {
    if (person.standing === 'Current') {
      return null;
    }
    return person.standing === 'Moved' ? 'переведений' : 'вибув';
  }

  whatLabel(entry: ScoreEntryDto): string {
    return entry.itemName ?? entry.reason ?? '—';
  }

  sheetLink(entry: ScoreEntryDto): unknown[] | null {
    return entry.agendaItemKey && entry.occurrenceStartUtc
      ? ['/kurin', this.kurinKey(), 'score', 'events', entry.agendaItemKey, entry.occurrenceStartUtc]
      : null;
  }

  openEntryDialog(target: ScoreEntryTarget | null = null, entry: ScoreEntryDto | null = null): void {
    this.entryTarget.set(target);
    this.entryToEdit.set(entry);
    this.entryError.set(null);
    this.entryDialogVisible.set(true);
  }

  giveTo(person: ScorePersonRowDto): void {
    this.openEntryDialog({ membershipKey: person.membershipKey, groupKey: null, name: person.fullName });
  }

  saveEntry(request: UpsertScoreEntryRequest): void {
    const editing = this.entryToEdit();
    this.savingEntry.set(true);
    this.entryError.set(null);
    const call = editing
      ? this.scores.updateEntry(this.kurinKey(), editing.scoreEntryKey, request)
      : this.scores.createEntry(this.kurinKey(), request);
    call.subscribe({
      next: () => {
        this.savingEntry.set(false);
        this.entryDialogVisible.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.savingEntry.set(false);
        this.entryError.set(failureDetail(error, 'Не вдалося записати. Спробуй ще раз.'));
      }
    });
  }

  confirmDelete(entry: ScoreEntryDto): void {
    this.confirmation.confirm({
      header: 'Видалити запис?',
      message: `${points(entry.points)} · ${this.whatLabel(entry)} зникне з балів. Слід у історії змін лишиться.`,
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.busyKey.set(entry.scoreEntryKey);
        this.scores.deleteEntry(this.kurinKey(), entry.scoreEntryKey).subscribe({
          next: () => { this.busyKey.set(null); this.entryDialogVisible.set(false); this.load(); },
          error: () => this.busyKey.set(null)
        });
      }
    });
  }
}
