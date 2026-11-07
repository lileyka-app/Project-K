import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { combineLatest } from 'rxjs';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { parseDateOnlyString, toDateOnlyString } from '../../../kurinModule/functions/to-date-only-string.function';
import { ScorePeriodSelectComponent } from '../../components/score-period-select/score-period-select';
import { periodParams, periodQueryFromParams, points } from '../../functions/score-format.function';
import { PrivateScoreCriterionDto, PrivateScoreDto, PrivateScoreEntryDto, PrivateScorePersonDto } from '../../models/private-score.dto';
import { ScorePeriodQuery } from '../../models/score.dto';
import { ScoreService } from '../../services/score-service/score.service';

/**
 * The КВ's private book: the Звʼязковий and the впорядники score youths by their own criteria, hidden
 * from the table and the youths. Nothing here ever becomes a public point.
 */
@Component({
  selector: 'app-private-score',
  imports: [
    DatePipe, FormsModule, RouterLink, ButtonModule, TableModule, TagModule, SelectModule, DialogModule, InputTextModule, TextareaModule,
    DatePickerModule, TooltipModule, SkeletonModule, ConfirmDialogModule, EmptyStateComponent, ScorePeriodSelectComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './private-score.html',
  styleUrl: './private-score.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PrivateScoreComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scores = inject(ScoreService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);

  readonly points = points;
  readonly today = new Date();

  kurinKey = '';
  readonly period = signal<ScorePeriodQuery>({});
  readonly data = signal<PrivateScoreDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  readonly periodParams = computed(() => periodParams(this.period()));
  readonly activeCriteria = computed(() => (this.data()?.criteria ?? []).filter(c => !c.isArchived));
  readonly hasUncategorised = computed(() => (this.data()?.people ?? []).some(p => p.uncategorised !== 0));
  readonly written = computed(() => (this.data()?.people ?? []).filter(p => p.privateTotal !== 0).length);

  readonly search = signal('');
  readonly people = computed(() => {
    const term = this.search().trim().toLowerCase();
    return (this.data()?.people ?? []).filter(p => !term || p.fullName.toLowerCase().includes(term) || p.groupName.toLowerCase().includes(term));
  });

  readonly entryDialogVisible = signal(false);
  readonly entryToEdit = signal<PrivateScoreEntryDto | null>(null);
  readonly entryPerson = signal<string | null>(null);
  readonly entryCriterion = signal<string | null>(null);
  readonly entryPoints = signal<number | null>(null);
  readonly entryNote = signal('');
  readonly entryDate = signal<Date | null>(new Date());
  readonly savingEntry = signal(false);
  readonly entryError = signal<string | null>(null);
  readonly busyKey = signal<string | null>(null);

  readonly personOptions = computed(() => (this.data()?.people ?? []).map(p => ({ label: `${p.fullName} · ${p.groupName}`, value: p.membershipKey })));
  readonly criterionOptions = computed(() => this.activeCriteria().map(c => ({ label: c.name, value: c.privateScoreCriterionKey })));
  readonly entryPersonName = computed(() => {
    const key = this.entryToEdit()?.membershipKey ?? this.entryPerson();
    return (this.data()?.people ?? []).find(p => p.membershipKey === key)?.fullName ?? null;
  });
  readonly entryValid = computed(() => {
    const amount = Number(this.entryPoints());
    return !!(this.entryToEdit()?.membershipKey ?? this.entryPerson())
      && Number.isFinite(amount) && amount !== 0
      && (!!this.entryCriterion() || this.entryNote().trim().length > 0)
      && !!this.entryDate();
  });

  readonly criteriaDialogVisible = signal(false);
  readonly newCriterion = signal('');
  readonly savingCriterion = signal(false);

  ngOnInit(): void {
    combineLatest([this.route.paramMap, this.route.queryParamMap]).subscribe(([params, query]) => {
      this.kurinKey = params.get('kurinKey') ?? '';
      this.period.set(periodQueryFromParams(Object.fromEntries(query.keys.map(k => [k, query.get(k)]))));
      this.load();
    });
  }

  load(): void {
    if (!this.kurinKey) {
      return;
    }
    this.loading.set(true);
    this.loadFailed.set(false);
    this.scores.getPrivateScore(this.kurinKey, this.period()).subscribe({
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

  byCriterion(person: PrivateScorePersonDto, criterion: PrivateScoreCriterionDto): number {
    return person.byCriterion[criterion.privateScoreCriterionKey] ?? 0;
  }

  openEntryDialog(person: PrivateScorePersonDto | null = null, entry: PrivateScoreEntryDto | null = null): void {
    this.entryToEdit.set(entry);
    this.entryPerson.set(person?.membershipKey ?? entry?.membershipKey ?? null);
    this.entryCriterion.set(entry?.privateScoreCriterionKey ?? null);
    this.entryPoints.set(entry?.points ?? null);
    this.entryNote.set(entry?.note ?? '');
    this.entryDate.set(entry ? parseDateOnlyString(entry.occurredOn) : new Date());
    this.entryError.set(null);
    this.entryDialogVisible.set(true);
  }

  saveEntry(): void {
    const editing = this.entryToEdit();
    const membershipKey = editing?.membershipKey ?? this.entryPerson();
    if (!membershipKey || !this.entryValid()) {
      return;
    }
    const request = {
      membershipKey,
      privateScoreCriterionKey: this.entryCriterion(),
      points: Number(this.entryPoints()),
      note: this.entryNote().trim() || null,
      occurredOn: toDateOnlyString(this.entryDate())!
    };
    this.savingEntry.set(true);
    this.entryError.set(null);
    const call = editing
      ? this.scores.updatePrivateEntry(this.kurinKey, editing.privateScoreEntryKey, request)
      : this.scores.createPrivateEntry(this.kurinKey, request);
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

  confirmDelete(entry: PrivateScoreEntryDto): void {
    this.confirmation.confirm({
      header: 'Видалити запис?',
      message: `${points(entry.points)} · ${entry.criterionName ?? entry.note} для ${entry.memberName} зникне з книги. Слід у історії змін лишиться.`,
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.busyKey.set(entry.privateScoreEntryKey);
        this.scores.deletePrivateEntry(this.kurinKey, entry.privateScoreEntryKey).subscribe({
          next: () => { this.busyKey.set(null); this.entryDialogVisible.set(false); this.load(); },
          error: (error: unknown) => { this.busyKey.set(null); this.fail('Не вдалося видалити', error); }
        });
      }
    });
  }

  addCriterion(): void {
    const name = this.newCriterion().trim();
    if (!name) {
      return;
    }
    this.savingCriterion.set(true);
    this.scores.createPrivateCriterion(this.kurinKey, { name, isArchived: false }).subscribe({
      next: () => { this.savingCriterion.set(false); this.newCriterion.set(''); this.load(); },
      error: (error: unknown) => { this.savingCriterion.set(false); this.fail('Не вдалося додати критерій', error); }
    });
  }

  setCriterionArchived(criterion: PrivateScoreCriterionDto, isArchived: boolean): void {
    this.scores.updatePrivateCriterion(this.kurinKey, criterion.privateScoreCriterionKey, { name: criterion.name, isArchived }).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.fail('Не вдалося змінити критерій', error)
    });
  }

  private fail(summary: string, error: unknown): void {
    this.messages.add({ severity: 'error', summary, detail: failureDetail(error, 'Спробуй ще раз.') });
  }
}
