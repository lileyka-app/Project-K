import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageTab, PageTabsComponent } from '../../../../shared/page-tabs/page-tabs';
import { PageGroups } from '../../../kurinModule/services/page-groups/page-groups.service';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { combineLatest } from 'rxjs';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { ScorePeriodSelectComponent } from '../../components/score-period-select/score-period-select';
import { periodParams, periodQueryFromParams, points, score } from '../../functions/score-format.function';
import { KurinScoreDto, ScoreGroupRowDto, ScorePeriodQuery } from '../../models/score.dto';
import { SCORE_ALGORITHM_HINTS, SCORE_ALGORITHM_LABELS, ScoreAlgorithm } from '../../models/score.enums';
import { ScoreService } from '../../services/score-service/score.service';

/**
 * The table of гуртки — the one page of точкування the whole kurin sees. A гурток's row opens its
 * own page for those who score it. The period lives in the query string so links keep it.
 */
@Component({
  selector: 'app-kurin-score',
  imports: [RouterLink, PageTabsComponent, ButtonModule, SkeletonModule, TooltipModule, EmptyStateComponent, ScorePeriodSelectComponent],
  templateUrl: './kurin-score.html',
  styleUrl: './kurin-score.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinScoreComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scores = inject(ScoreService);
  private readonly pageGroups = inject(PageGroups);
  /** Таблиця, Книга КВ і Налаштування — одне місце в меню, вкладки вгорі. */
  tabs: PageTab[] = [];

  readonly score = score;
  readonly points = points;
  readonly algorithmLabels = SCORE_ALGORITHM_LABELS;
  readonly algorithmHints = SCORE_ALGORITHM_HINTS;

  kurinKey = '';
  readonly period = signal<ScorePeriodQuery>({});
  readonly data = signal<KurinScoreDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  readonly periodParams = computed(() => periodParams(this.period()));

  /** The КВ's own book lives behind its own grant, not behind the table's. */

  readonly leader = computed(() => Math.max(0, ...(this.data()?.groups ?? []).map(g => g.score)));

  readonly isAverage = computed(() => this.data()?.algorithm === ScoreAlgorithm.Average);

  readonly scored = computed(() => (this.data()?.groups ?? []).filter(g => g.score !== 0 || g.youthPoints !== 0).length);

  ngOnInit(): void {
    combineLatest([this.route.paramMap, this.route.queryParamMap]).subscribe(([params, query]) => {
      this.kurinKey = params.get('kurinKey') ?? '';
      this.tabs = this.kurinKey ? this.pageGroups.score(this.kurinKey) : [];
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
    this.scores.getKurinScore(this.kurinKey, this.period()).subscribe({
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

  /** How far the bar goes: the leader fills it, the rest in proportion, a zero or a minus shows nothing. */
  barWidth(group: ScoreGroupRowDto): string {
    const leader = this.leader();
    return leader > 0 && group.score > 0 ? `${Math.round((group.score / leader) * 100)}%` : '0%';
  }

  otherLabel(group: ScoreGroupRowDto): string {
    return this.isAverage() ? `сума ${score(group.otherScore)}` : `середнє ${score(group.otherScore)}`;
  }
}
