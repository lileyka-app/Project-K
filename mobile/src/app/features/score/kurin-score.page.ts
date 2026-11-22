import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { AuthService } from '../../auth/auth.service';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { ScorePeriodSelect } from './score-period.select';
import { periodParams, periodQueryFromParams, points, score } from './score.format';
import {
  KurinScoreDto,
  SCORE_ALGORITHM_HINTS,
  SCORE_ALGORITHM_LABELS,
  ScoreGroupRowDto,
  ScorePeriodQuery,
  ScorePersonRowDto,
} from './score.models';
import { ScoreService } from './score.service';

/**
 * The table of гуртки (the web's kurin-score): the one page of точкування the whole kurin sees.
 * A гурток's row opens its own page for those the server lets in. A senior kurin has no гуртки, so
 * its table is of people. The period lives in the query
 * string so the link to a гурток keeps it. «Точкування КВ» and «Налаштування» stay on the web.
 */
@Component({
  selector: 'app-kurin-score',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonSkeletonText,
    RouterLink,
    ScorePeriodSelect,
  ],
  styles: `
    .subline {
      margin: 0 20px 4px;
      color: var(--lk-muted);
      font-size: 15px;
      line-height: 20px;
    }
    :host-context(.ios) .subline {
      margin-inline: 16px;
    }
    /* No large title on Android: the line starts the page, clear of the toolbar. */
    :host-context(.md) .subline {
      margin-top: 16px;
    }
    .place {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 15px;
      color: var(--lk-muted);
      background: var(--lk-surface);
      border: 1px solid var(--lk-line);
      margin-inline-end: 12px;
    }
    .place.podium {
      color: var(--lk-primary);
      border-color: var(--lk-primary);
    }
    .place.first {
      color: var(--lk-on-primary);
      background: var(--lk-primary);
      border-color: var(--lk-primary);
    }
    ion-label h2 {
      font-weight: 600;
      color: var(--lk-ink);
    }
    .bar {
      margin-top: 6px;
      height: 4px;
      border-radius: 2px;
      background: var(--lk-line);
      overflow: hidden;
    }
    .bar > div {
      height: 100%;
      border-radius: 2px;
      background: var(--lk-faint);
    }
    .bar > div.lead {
      background: var(--lk-primary);
    }
    .total {
      font-size: 20px;
      font-weight: 700;
      color: var(--lk-ink);
      font-variant-numeric: tabular-nums;
    }
    .total.minus,
    .minus {
      color: var(--lk-danger);
    }
    .plus {
      color: var(--lk-primary);
    }
    .note {
      margin: 8px 20px 0;
      font-size: 13px;
      line-height: 18px;
      color: var(--lk-muted);
    }
    :host-context(.ios) .note {
      margin-inline: 36px;
    }
    .empty {
      padding: 32px 24px;
      text-align: center;
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/kurin" text="Курінь" /></ion-buttons>
        <ion-title>Точкування</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>
      <ion-header collapse="condense">
        <ion-toolbar>
          <ion-title size="large">Точкування</ion-title>
        </ion-toolbar>
      </ion-header>

      @if (!kurinKey) {
        <p class="empty">Ти ще не в курені.</p>
      } @else {
        @switch (data().state) {
          @case ('loading') {
            <ion-list [inset]="true">
              <ion-item-group>
                @for (row of [1, 2, 3, 4]; track row) {
                  <ion-item><ion-label><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-label></ion-item>
                }
              </ion-item-group>
            </ion-list>
          }
          @case ('failed') {
            <p class="empty">{{ failedText }}</p>
          }
          @default {
            @if (value(); as d) {
              <p class="subline" data-testid="score-subline">
                {{ d.period.label }}
                @if (d.hasGroups) {
                  · {{ algorithmLabels[d.algorithm].toLowerCase() }}
                } @else {
                  · людей: {{ d.people.length }}
                }
              </p>
              <app-score-period [periods]="d.periods" [period]="d.period" (periodChange)="changePeriod($event)" />

              @if (!d.hasGroups) {
                @if (!d.people.length) {
                  <p class="empty">Людей поки немає.</p>
                } @else {
                  <ion-list [inset]="true">
                    <ion-list-header><ion-label>Люди</ion-label></ion-list-header>
                    <ion-item-group>
                      @for (person of d.people; track person.membershipKey; let i = $index) {
                        <ion-item data-testid="score-person">
                          <span
                            slot="start"
                            class="place"
                            [class.first]="i === 0 && person.total > 0"
                            [class.podium]="i > 0 && i < 3 && person.total > 0"
                            >{{ i + 1 }}</span
                          >
                          <ion-label>
                            <h2>{{ person.fullName }}</h2>
                            @if (person.standing === 'Left') {
                              <p>вибув</p>
                            }
                            <div class="bar" aria-hidden="true">
                              <div [class.lead]="i === 0" [style.width]="personBarWidth(person)"></div>
                            </div>
                          </ion-label>
                          <span slot="end" class="total" [class.minus]="person.total < 0">{{ score(person.total) }}</span>
                        </ion-item>
                      }
                    </ion-item-group>
                  </ion-list>
                  @if (!peopleScored()) {
                    <p class="note">Балів за цей період ще немає. Присутність відмічають на події в календарі — там же дають додаткові бали.</p>
                  } @else if (d.viewer.canScore) {
                    <p class="note">Відмічати присутність і давати бали — на події в календарі, кнопка «Точкування».</p>
                  }
                }
              } @else if (!d.groups.length) {
                <p class="empty">Гуртків поки немає: спершу гуртки, потім бали.</p>
              } @else {
                <ion-list [inset]="true">
                  <ion-list-header><ion-label>Гуртки</ion-label></ion-list-header>
                  <ion-item-group>
                    @for (group of d.groups; track group.groupKey) {
                      <ion-item
                        data-testid="score-group"
                        [button]="group.canOpen"
                        [detail]="group.canOpen"
                        [routerLink]="group.canOpen ? ['/tabs/kurin/group', group.groupKey, 'score'] : null"
                        [queryParams]="query()"
                      >
                        <span
                          slot="start"
                          class="place"
                          [class.first]="group.place === 1 && group.score > 0"
                          [class.podium]="group.place > 1 && group.place <= 3 && group.score > 0"
                          >{{ group.place }}</span
                        >
                        <ion-label>
                          <h2>{{ group.groupName }}</h2>
                          <p>
                            юнаків {{ score(group.youthCount) }} · {{ otherLabel(group) }}
                            @if (group.groupPoints !== 0) {
                              · гуртку
                              <span [class.plus]="group.groupPoints > 0" [class.minus]="group.groupPoints < 0">{{
                                points(group.groupPoints)
                              }}</span>
                            }
                          </p>
                          <div class="bar" aria-hidden="true">
                            <div [class.lead]="group.place === 1" [style.width]="barWidth(group)"></div>
                          </div>
                        </ion-label>
                        <span slot="end" class="total" [class.minus]="group.score < 0">{{ score(group.score) }}</span>
                      </ion-item>
                    }
                  </ion-item-group>
                </ion-list>
                <p class="note">{{ algorithmHints[d.algorithm] }}</p>
                @if (!scored()) {
                  <p class="note">Балів за цей період ще немає. Присутність відмічають на події в календарі — там же дають додаткові бали.</p>
                } @else if (d.viewer.canScore) {
                  <p class="note">Відмічати присутність і давати бали — на події в календарі, кнопка «Точкування».</p>
                }
              }
            }
          }
        }
      }
    </ion-content>
  `,
})
export class KurinScorePage {
  private readonly scores = inject(ScoreService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly kurinKey = inject(AuthService).user()?.kurinKey ?? null;
  protected readonly failedText = FAILED_TEXT;
  protected readonly score = score;
  protected readonly points = points;
  protected readonly algorithmLabels = SCORE_ALGORITHM_LABELS;
  protected readonly algorithmHints = SCORE_ALGORITHM_HINTS;

  private readonly period = signal<ScorePeriodQuery>({});
  protected readonly data = signal<Loaded<KurinScoreDto>>({ state: 'loading' });
  protected readonly value = computed(() => valueOf(this.data()));
  protected readonly query = computed(() => periodParams(this.period()));
  private readonly leader = computed(() => Math.max(0, ...(this.value()?.groups ?? []).map((g) => g.score)));
  protected readonly scored = computed(
    () => (this.value()?.groups ?? []).filter((g) => g.score !== 0 || g.youthPoints !== 0).length,
  );

  private readonly peopleLeader = computed(() => Math.max(0, ...(this.value()?.people ?? []).map((p) => p.total)));
  protected readonly peopleScored = computed(() => (this.value()?.people ?? []).some((p) => p.total !== 0));

  constructor() {
    this.route.queryParams.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      this.period.set(periodQueryFromParams(params));
      void this.load();
    });
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected changePeriod(query: ScorePeriodQuery): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: periodParams(query), replaceUrl: true });
  }

  /** The leader fills the bar, the rest in proportion; a zero or a minus shows nothing. */
  protected barWidth(group: ScoreGroupRowDto): string {
    const leader = this.leader();
    return leader > 0 && group.score > 0 ? `${Math.round((group.score / leader) * 100)}%` : '0%';
  }

  protected personBarWidth(person: ScorePersonRowDto): string {
    const leader = this.peopleLeader();
    return leader > 0 && person.total > 0 ? `${Math.round((person.total / leader) * 100)}%` : '0%';
  }

  protected otherLabel(group: ScoreGroupRowDto): string {
    return this.value()?.algorithm === 'Average' ? `сума ${score(group.otherScore)}` : `середнє ${score(group.otherScore)}`;
  }

  private async load(): Promise<void> {
    if (!this.kurinKey) return;
    if (this.data().state === 'failed') this.data.set({ state: 'loading' });
    await settle(this.scores.kurin(this.kurinKey, this.period()), this.data);
  }
}
