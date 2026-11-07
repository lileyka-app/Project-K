import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { parseDateOnlyString, toDateOnlyString } from '../../../kurinModule/functions/to-date-only-string.function';
import { points } from '../../functions/score-format.function';
import { KurinScoreSettingsDto, ScoreAttendanceRateDto, ScoreItemDto, ScoreRuleDto, ScoreStageDto } from '../../models/score.dto';
import { AUTOMATIC_SCORE_SOURCES, SCORE_ALGORITHM_HINTS, SCORE_ALGORITHM_LABELS, SCORE_SOURCE_LABELS, ScoreAlgorithm, ScoreSource } from '../../models/score.enums';
import { ScoreService } from '../../services/score-service/score.service';

/**
 * One automatic source as the page lists it: the rule in force today, if any, and otherwise the
 * next one to come — a rule moved to a future day is planned, not switched off.
 */
interface RuleRow {
  source: ScoreSource;
  variant: number;
  label: string;
  hint: string;
  current: ScoreRuleDto | null;
  upcoming: ScoreRuleDto | null;
}

const RULE_HINTS: Partial<Record<ScoreSource, string>> = {
  [ScoreSource.Skill]: 'за кожну підтверджену вмілість',
  [ScoreSource.ProbePoint]: 'за кожну підписану точку проби',
  [ScoreSource.Probe]: 'за закриту пробу',
  [ScoreSource.Dues]: 'за квартал вкладки, закритий без боргу',
  [ScoreSource.Warning]: 'мінус, доки пересторога чинна'
};

/**
 * How the kurin scores: the algorithm, what being at each group of events is worth, what the
 * automatic sources are worth from when, the list of positions, the stages. The суддя куреня's
 * page; every change is written at once and the table follows.
 */
@Component({
  selector: 'app-kurin-score-settings',
  imports: [
    DatePipe, FormsModule, RouterLink, ButtonModule, SelectButtonModule, InputTextModule, DatePickerModule, DialogModule, TagModule,
    ToggleSwitchModule, TooltipModule, SkeletonModule, ConfirmDialogModule, EmptyStateComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './kurin-score-settings.html',
  styleUrl: './kurin-score-settings.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinScoreSettingsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly scores = inject(ScoreService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);

  readonly points = points;
  readonly algorithmLabels = SCORE_ALGORITHM_LABELS;
  readonly algorithmHints = SCORE_ALGORITHM_HINTS;
  readonly algorithmOptions = [ScoreAlgorithm.Average, ScoreAlgorithm.Sum].map(value => ({ value, label: SCORE_ALGORITHM_LABELS[value] }));
  readonly today = new Date();

  kurinKey = '';
  readonly data = signal<KurinScoreSettingsDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly savingAlgorithm = signal(false);

  // Attendance rates: a draft per category, saved one at a time.
  readonly rateDrafts = signal<Record<string, number>>({});
  readonly savingRate = signal<string | null>(null);

  readonly rules = computed<RuleRow[]>(() => {
    const d = this.data();
    const rows: RuleRow[] = [];
    for (const source of AUTOMATIC_SCORE_SOURCES) {
      const variants = source === ScoreSource.Warning ? [1, 2, 3] : [0];
      for (const variant of variants) {
        rows.push({
          source,
          variant,
          label: source === ScoreSource.Warning ? `Пересторога ${['I', 'II', 'III'][variant - 1]}` : SCORE_SOURCE_LABELS[source],
          hint: RULE_HINTS[source] ?? '',
          current: this.currentRule(d, source, variant),
          upcoming: this.upcomingRule(d, source, variant)
        });
      }
    }
    return rows;
  });

  readonly activeItems = computed(() => (this.data()?.items ?? []).filter(i => !i.isArchived));
  readonly archivedItems = computed(() => (this.data()?.items ?? []).filter(i => i.isArchived));

  readonly ruleDialogVisible = signal(false);
  readonly ruleRow = signal<RuleRow | null>(null);
  readonly rulePoints = signal<number | null>(null);
  readonly ruleFrom = signal<Date | null>(new Date());
  readonly savingRule = signal(false);

  readonly itemDialogVisible = signal(false);
  readonly itemToEdit = signal<ScoreItemDto | null>(null);
  readonly itemName = signal('');
  readonly itemPoints = signal<number | null>(null);
  readonly savingItem = signal(false);
  readonly itemError = signal<string | null>(null);

  readonly stageDialogVisible = signal(false);
  readonly stageToEdit = signal<ScoreStageDto | null>(null);
  readonly stageName = signal('');
  readonly stageFrom = signal<Date | null>(null);
  readonly stageTo = signal<Date | null>(null);
  readonly savingStage = signal(false);
  readonly stageError = signal<string | null>(null);
  readonly busyStage = signal<string | null>(null);

  readonly ruleValid = computed(() => {
    const value = Number(this.rulePoints());
    const row = this.ruleRow();
    if (!row || !Number.isFinite(value) || !this.ruleFrom()) {
      return false;
    }
    return row.source === ScoreSource.Warning ? value <= 0 : value >= 0;
  });

  readonly itemValid = computed(() => {
    const value = Number(this.itemPoints());
    return this.itemName().trim().length > 0 && Number.isFinite(value) && value !== 0;
  });

  readonly stageValid = computed(() => {
    const from = this.stageFrom();
    const to = this.stageTo();
    return this.stageName().trim().length > 0 && !!from && !!to && to >= from;
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.kurinKey = params.get('kurinKey') ?? '';
      this.load();
    });
  }

  load(): void {
    if (!this.kurinKey) {
      return;
    }
    this.loading.set(true);
    this.loadFailed.set(false);
    this.scores.getSettings(this.kurinKey).subscribe({
      next: data => {
        this.data.set(data);
        this.rateDrafts.set(Object.fromEntries(data.attendanceRates.map(r => [r.agendaCategoryKey ?? '', r.points])));
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      }
    });
  }

  setAlgorithm(algorithm: ScoreAlgorithm): void {
    if (algorithm === this.data()?.algorithm) {
      return;
    }
    this.savingAlgorithm.set(true);
    this.scores.setAlgorithm(this.kurinKey, algorithm).subscribe({
      next: () => { this.savingAlgorithm.set(false); this.load(); },
      error: (error: unknown) => { this.savingAlgorithm.set(false); this.fail('Не вдалося змінити алгоритм', error); }
    });
  }

  rateDraft(rate: ScoreAttendanceRateDto): number {
    return this.rateDrafts()[rate.agendaCategoryKey ?? ''] ?? rate.points;
  }

  setRateDraft(rate: ScoreAttendanceRateDto, value: number | null): void {
    this.rateDrafts.set({ ...this.rateDrafts(), [rate.agendaCategoryKey ?? '']: Number(value ?? 0) });
  }

  rateChanged(rate: ScoreAttendanceRateDto): boolean {
    return this.rateDraft(rate) !== rate.points;
  }

  saveRate(rate: ScoreAttendanceRateDto): void {
    const value = this.rateDraft(rate);
    if (!Number.isFinite(value) || value < 0) {
      return;
    }
    this.savingRate.set(rate.agendaCategoryKey);
    this.scores.setAttendanceRate(this.kurinKey, { agendaCategoryKey: rate.agendaCategoryKey, agendaItemKey: null, points: value }).subscribe({
      next: () => { this.savingRate.set(null); this.load(); },
      error: (error: unknown) => { this.savingRate.set(null); this.fail('Не вдалося зберегти ставку', error); }
    });
  }

  openRuleDialog(row: RuleRow): void {
    this.ruleRow.set(row);
    this.rulePoints.set((row.current ?? row.upcoming)?.points ?? 0);
    // The day the rule in force started, not today: «today» was what a провід saved by mistake
    // after entering a year of history, and the day then had to be moved back.
    const shown = row.current ?? row.upcoming;
    this.ruleFrom.set(shown ? parseDateOnlyString(shown.fromDate) ?? new Date() : new Date());
    this.ruleDialogVisible.set(true);
  }

  saveRule(): void {
    const row = this.ruleRow();
    if (!row || !this.ruleValid()) {
      return;
    }
    this.savingRule.set(true);
    this.scores.setRule(this.kurinKey, { source: row.source, variant: row.variant, fromDate: toDateOnlyString(this.ruleFrom())!, points: Number(this.rulePoints()) }).subscribe({
      next: () => { this.savingRule.set(false); this.ruleDialogVisible.set(false); this.load(); },
      error: (error: unknown) => { this.savingRule.set(false); this.fail('Не вдалося зберегти правило', error); }
    });
  }

  openItemDialog(item: ScoreItemDto | null = null): void {
    this.itemToEdit.set(item);
    this.itemName.set(item?.name ?? '');
    this.itemPoints.set(item?.points ?? null);
    this.itemError.set(null);
    this.itemDialogVisible.set(true);
  }

  saveItem(): void {
    if (!this.itemValid()) {
      return;
    }
    const editing = this.itemToEdit();
    const request = { name: this.itemName().trim(), points: Number(this.itemPoints()), isArchived: editing?.isArchived ?? false };
    this.savingItem.set(true);
    this.itemError.set(null);
    const call = editing ? this.scores.updateItem(this.kurinKey, editing.scoreItemKey, request) : this.scores.createItem(this.kurinKey, request);
    call.subscribe({
      next: () => { this.savingItem.set(false); this.itemDialogVisible.set(false); this.load(); },
      error: (error: unknown) => { this.savingItem.set(false); this.itemError.set(failureDetail(error, 'Не вдалося зберегти. Спробуй ще раз.')); }
    });
  }

  setItemArchived(item: ScoreItemDto, isArchived: boolean): void {
    this.scores.updateItem(this.kurinKey, item.scoreItemKey, { name: item.name, points: item.points, isArchived }).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.fail('Не вдалося змінити позицію', error)
    });
  }

  openStageDialog(stage: ScoreStageDto | null = null): void {
    this.stageToEdit.set(stage);
    this.stageName.set(stage?.name ?? '');
    this.stageFrom.set(stage ? parseDateOnlyString(stage.fromDate) : null);
    this.stageTo.set(stage ? parseDateOnlyString(stage.toDate) : null);
    this.stageError.set(null);
    this.stageDialogVisible.set(true);
  }

  saveStage(): void {
    if (!this.stageValid()) {
      return;
    }
    const editing = this.stageToEdit();
    const request = { name: this.stageName().trim(), fromDate: toDateOnlyString(this.stageFrom())!, toDate: toDateOnlyString(this.stageTo())! };
    this.savingStage.set(true);
    this.stageError.set(null);
    const call = editing ? this.scores.updateStage(this.kurinKey, editing.scoreStageKey, request) : this.scores.createStage(this.kurinKey, request);
    call.subscribe({
      next: () => { this.savingStage.set(false); this.stageDialogVisible.set(false); this.load(); },
      error: (error: unknown) => { this.savingStage.set(false); this.stageError.set(failureDetail(error, 'Не вдалося зберегти. Спробуй ще раз.')); }
    });
  }

  confirmDeleteStage(stage: ScoreStageDto): void {
    this.confirmation.confirm({
      header: 'Видалити етап?',
      message: `«${stage.name}» зникне з переліку періодів. Бали не зачіпає.`,
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.busyStage.set(stage.scoreStageKey);
        this.scores.deleteStage(this.kurinKey, stage.scoreStageKey).subscribe({
          next: () => { this.busyStage.set(null); this.load(); },
          error: (error: unknown) => { this.busyStage.set(null); this.fail('Не вдалося видалити етап', error); }
        });
      }
    });
  }

  private currentRule(d: KurinScoreSettingsDto | null, source: ScoreSource, variant: number): ScoreRuleDto | null {
    const today = toDateOnlyString(new Date())!;
    return (d?.rules ?? [])
      .filter(r => r.source === source && r.variant === variant && r.fromDate <= today)
      .sort((a, b) => b.fromDate.localeCompare(a.fromDate))[0] ?? null;
  }

  private upcomingRule(d: KurinScoreSettingsDto | null, source: ScoreSource, variant: number): ScoreRuleDto | null {
    const today = toDateOnlyString(new Date())!;
    return (d?.rules ?? [])
      .filter(r => r.source === source && r.variant === variant && r.fromDate > today)
      .sort((a, b) => a.fromDate.localeCompare(b.fromDate))[0] ?? null;
  }

  private fail(summary: string, error: unknown): void {
    this.messages.add({ severity: 'error', summary, detail: failureDetail(error, 'Спробуй ще раз.') });
  }
}
