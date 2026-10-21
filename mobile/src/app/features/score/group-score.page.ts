import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, OnDestroy, afterRenderEffect, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ActionSheetController,
  IonAccordion,
  IonAccordionGroup,
  IonBackButton,
  IonBadge,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonItemOption,
  IonItemOptions,
  IonItemSliding,
  IonLabel,
  IonList,
  IonModal,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { add, calendarOutline } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { FAILED_TEXT } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { GlassEffects } from '../../ui/glass';
import { EntryEditor } from './score-entry.editor';
import { ScoreEntryForm } from './score-entry.form';
import { ScorePeriodSelect } from './score-period.select';
import { periodParams, periodQueryFromParams, personSources, points, score, shortDate } from './score.format';
import {
  GroupScoreDto,
  SCORE_SOURCE_LABELS,
  ScoreEntryDto,
  ScoreEntryTarget,
  ScorePeriodQuery,
  ScorePersonRowDto,
} from './score.models';
import { ScoreService } from './score.service';

type View = { state: 'loading' } | { state: 'ready'; value: GroupScoreDto } | { state: 'failed' } | { state: 'forbidden' };
type Tab = 'people' | 'entries';
type Kind = 'all' | 'people' | 'group';

/**
 * One гурток's точкування (the web's group-score): its standing, each youth's points by source
 * (a row opens to the breakdown) and every entry given by hand. «Записати бал» and editing are
 * for those the API gives `canScore`; anyone else the server lets in reads it as it is.
 */
@Component({
  selector: 'app-group-score',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonButton,
    IonIcon,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonCard,
    IonCardContent,
    IonSegment,
    IonSegmentButton,
    IonList,
      IonItemGroup,
    IonItem,
    IonItemSliding,
    IonItemOptions,
    IonItemOption,
    IonLabel,
    IonNote,
    IonBadge,
    IonAccordionGroup,
    IonAccordion,
    IonSelect,
    IonSelectOption,
    IonSkeletonText,
    IonModal,
    RouterLink,
    ScorePeriodSelect,
    ScoreEntryForm,
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
    .stats {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px 12px;
    }
    .stat span {
      display: block;
      font-size: 12px;
      font-weight: 600;
      color: var(--lk-faint);
    }
    .stat strong {
      display: block;
      font-size: 26px;
      line-height: 32px;
      font-weight: 700;
      color: var(--lk-ink);
      font-variant-numeric: tabular-nums;
    }
    .stat.primary strong {
      color: var(--lk-primary);
    }
    .stat small {
      display: block;
      font-size: 12px;
      line-height: 16px;
      color: var(--lk-muted);
    }
    ion-segment {
      margin: 8px 16px 0;
      width: auto;
    }
    ion-label h2,
    ion-label h3 {
      font-weight: 600;
      color: var(--lk-ink);
    }
    .total,
    .pts {
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      color: var(--lk-muted);
    }
    .total {
      font-size: 17px;
    }
    .plus {
      color: var(--lk-primary);
    }
    .minus {
      color: var(--lk-danger);
    }
    .pts {
      min-width: 40px;
      margin-inline-end: 12px;
    }
    .gone ion-label h2 {
      color: var(--lk-muted);
    }
    ion-badge {
      margin-inline-start: 6px;
      vertical-align: middle;
    }
    .sub {
      --padding-start: 32px;
      --min-height: 40px;
      font-size: 15px;
    }
    .action {
      --padding-start: 32px;
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
    ion-note ion-icon {
      vertical-align: -2px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button [defaultHref]="backHref" text="Назад" /></ion-buttons>
        <ion-title>{{ title() }}</ion-title>
        @if (value()?.canScore) {
          <ion-buttons slot="end">
            <ion-button aria-label="Записати бал" data-testid="give-score" (click)="giveToAnyone()">
              <ion-icon slot="icon-only" name="add" />
            </ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>
      <ion-header collapse="condense">
        <ion-toolbar>
          <ion-title size="large">{{ title() }}</ion-title>
        </ion-toolbar>
      </ion-header>

      @switch (view().state) {
        @case ('loading') {
          <ion-card><ion-card-content><ion-skeleton-text [animated]="true" style="height: 120px" /></ion-card-content></ion-card>
          <ion-list [inset]="true">
            <ion-item-group>
              @for (row of [1, 2, 3]; track row) {
                <ion-item><ion-label><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-label></ion-item>
              }
            </ion-item-group>
          </ion-list>
        }
        @case ('forbidden') {
          <p class="empty" data-testid="score-forbidden">Точкування цього гуртка бачать його провід і судді. Свої бали — на Головній.</p>
        }
        @case ('failed') {
          <p class="empty">{{ failedText }}</p>
        }
        @default {
          @if (value(); as d) {
            <p class="subline">{{ d.period.label }} · {{ current().length }} у складі</p>
            <app-score-period [periods]="d.periods" [period]="d.period" (periodChange)="changePeriod($event)" />

            <ion-card>
              <ion-card-content>
                <div class="stats">
                  <div class="stat primary">
                    <span>Місце</span>
                    <strong data-testid="score-place">{{ d.standing.place }}</strong>
                    <small>з {{ d.groupCount }} гуртків</small>
                  </div>
                  <div class="stat">
                    <span>Бал гуртка</span>
                    <strong>{{ score(d.standing.score) }}</strong>
                    <small>{{ d.algorithm === 'Average' ? 'середнє на юнака' : 'сума балів' }} · інакше {{ score(d.standing.otherScore) }}</small>
                  </div>
                  <div class="stat">
                    <span>Бали юнаків</span>
                    <strong>{{ score(d.standing.youthPoints) }}</strong>
                    <small>юнаків у періоді {{ score(d.standing.youthCount) }}</small>
                  </div>
                  <div class="stat">
                    <span>Гуртку цілим</span>
                    <strong>{{ points(d.standing.groupPoints) }}</strong>
                    <small>додається після, не ділиться</small>
                  </div>
                </div>
              </ion-card-content>
            </ion-card>

            <ion-segment [value]="tab()" (ionChange)="setTab($event)">
              <ion-segment-button value="people"><ion-label>Юнаки</ion-label></ion-segment-button>
              <ion-segment-button value="entries" data-testid="tab-entries"><ion-label>Дано вручну</ion-label></ion-segment-button>
            </ion-segment>

            @if (tab() === 'people') {
              @if (!d.people.length) {
                <p class="empty">У гуртку ще нікого. Юнаки зʼявляться тут, щойно їх запишуть у гурток.</p>
              } @else {
                <ion-list [inset]="true">
                  <ion-item-group>
                    <ion-accordion-group [multiple]="true">
                      @for (person of d.people; track person.membershipKey) {
                        <ion-accordion [value]="person.membershipKey" [class.gone]="person.standing !== 'Current'">
                          <ion-item slot="header" data-testid="score-person">
                            <ion-label>
                              <h2>
                                {{ person.fullName }}
                                @if (standingLabel(person); as label) {
                                  <ion-badge class="lk-tag--secondary">{{ label }}</ion-badge>
                                }
                              </h2>
                            </ion-label>
                            <span
                              slot="end"
                              class="total"
                              [class.plus]="person.total > 0"
                              [class.minus]="person.total < 0"
                              >{{ points(person.total) }}</span
                            >
                          </ion-item>
                          <div slot="content">
                            @for (row of sources(person); track row.source) {
                              <ion-item class="sub" lines="none">
                                <ion-label>{{ sourceLabels[row.source] }}</ion-label>
                                <span slot="end" class="pts" [class.minus]="row.value < 0">{{ points(row.value) }}</span>
                              </ion-item>
                            } @empty {
                              <ion-item class="sub" lines="none"><ion-label color="medium">Балів за цей період немає</ion-label></ion-item>
                            }
                            @if (d.canScore && person.standing === 'Current') {
                              <ion-item class="action" [button]="true" [detail]="false" lines="none" (click)="giveTo(person)">
                                <ion-label color="primary">Дати бал</ion-label>
                              </ion-item>
                            }
                            <ion-item class="action" [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', person.memberKey]">
                              <ion-label>Профіль</ion-label>
                            </ion-item>
                          </div>
                        </ion-accordion>
                      }
                    </ion-accordion-group>
                  </ion-item-group>
                </ion-list>
                <p class="note">Переведені й вибулі лишаються з тим, що заробили тут.</p>
              }
            } @else {
              <ion-list [inset]="true">
                <ion-item-group>
                  <ion-item>
                    <ion-select
                      label="Кому"
                      interface="action-sheet"
                      cancelText="Скасувати"
                      [value]="kind()"
                      (ionChange)="kind.set($any($event).detail.value)"
                    >
                      <ion-select-option value="all">Усі записи</ion-select-option>
                      <ion-select-option value="people">Юнакам</ion-select-option>
                      <ion-select-option value="group">Гуртку цілим</ion-select-option>
                    </ion-select>
                  </ion-item>
                </ion-item-group>
              </ion-list>
              @if (!entries().length) {
                <p class="empty">
                  {{ d.entries.length ? 'Нічого не знайшлось. Спробуй інший фільтр.' : 'Записів ще немає. Перший бал від судді — і історія почнеться.' }}
                </p>
              } @else {
                <ion-list [inset]="true">
                  <ion-item-group>
                    @for (entry of entries(); track entry.scoreEntryKey) {
                      <ion-item-sliding [disabled]="!d.canScore">
                        <ion-item data-testid="score-entry" [button]="d.canScore" [detail]="false" (click)="d.canScore && editEntry(entry)">
                          <span slot="start" class="pts" [class.plus]="entry.points > 0" [class.minus]="entry.points < 0">{{
                            points(entry.points)
                          }}</span>
                          <ion-label class="ion-text-wrap">
                            <h3>{{ entry.itemName ?? entry.reason ?? '—' }}</h3>
                            <p>{{ entryLine(entry) }}</p>
                          </ion-label>
                          @if (entry.occurrenceStartUtc) {
                            <ion-note slot="end"><ion-icon name="calendar-outline" aria-hidden="true" /> {{ shortDate(entry.occurrenceStartUtc) }}</ion-note>
                          }
                        </ion-item>
                        <ion-item-options side="end">
                          @if (entry.agendaItemKey && entry.occurrenceStartUtc) {
                            <ion-item-option color="medium" (click)="openSheet(entry)">Аркуш</ion-item-option>
                          }
                          <ion-item-option color="danger" (click)="editor.remove(entry)">Видалити</ion-item-option>
                        </ion-item-options>
                      </ion-item-sliding>
                    }
                  </ion-item-group>
                </ion-list>
                <p class="note">Позиції з переліку та бали від судді — юнакам і гуртку цілим. Присутність і автоматичні бали тут не показані.</p>
              }
            }

            <ion-modal [isOpen]="editor.open()" [initialBreakpoint]="1" [breakpoints]="[0, 1]" (didDismiss)="editor.close()">
              <ng-template>
                <app-score-entry-form
                  [target]="editor.target()"
                  [targets]="editor.targets()"
                  [items]="d.items"
                  [existing]="editor.existing()"
                  [entry]="editor.entry()"
                  [saving]="editor.saving()"
                  [errorMessage]="editor.error()"
                  (save)="editor.save($event)"
                  (remove)="editor.remove($event)"
                  (cancelled)="editor.close()"
                />
              </ng-template>
            </ion-modal>
          }
        }
      }
    </ion-content>
  `,
})
export class GroupScorePage implements OnDestroy {
  private readonly scores = inject(ScoreService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  private readonly kurinKey = inject(AuthService).user()?.kurinKey ?? '';
  private readonly groupKey = this.route.snapshot.paramMap.get('groupKey') ?? '';
  protected readonly backHref = '/tabs/kurin/score';
  protected readonly failedText = FAILED_TEXT;
  protected readonly score = score;
  protected readonly points = points;
  protected readonly shortDate = shortDate;
  protected readonly sources = personSources;
  protected readonly sourceLabels = SCORE_SOURCE_LABELS;

  private readonly period = signal<ScorePeriodQuery>({});
  protected readonly view = signal<View>({ state: 'loading' });
  protected readonly value = computed(() => {
    const view = this.view();
    return view.state === 'ready' ? view.value : null;
  });
  protected readonly title = computed(() => (this.value() ? `Гурток ${this.value()!.groupName}` : 'Точкування гуртка'));
  protected readonly tab = signal<Tab>('people');
  protected readonly kind = signal<Kind>('all');

  protected readonly current = computed(() => (this.value()?.people ?? []).filter((p) => p.standing === 'Current'));
  protected readonly entries = computed(() => {
    const kind = this.kind();
    return (this.value()?.entries ?? []).filter((e) => kind === 'all' || (kind === 'group') === e.isForGroup);
  });

  /** The гурток itself and its youth: whom «Записати бал» may go to. */
  private readonly targets = computed<ScoreEntryTarget[]>(() => {
    const d = this.value();
    if (!d) return [];
    return [
      { membershipKey: null, groupKey: d.groupKey, name: `Гурток ${d.groupName} цілим` },
      ...this.current().map((p) => ({ membershipKey: p.membershipKey, groupKey: null, name: p.fullName })),
    ];
  });

  protected readonly editor = new EntryEditor(
    this.scores,
    inject(ActionSheetController),
    inject(Toasts),
    () => this.kurinKey,
    () => this.load(),
  );

  constructor() {
    addIcons({ add, calendarOutline });
    this.route.queryParams.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      this.period.set(periodQueryFromParams(params));
      void this.load();
    });
    afterRenderEffect(() => {
      this.value();
      this.glass.sync();
    });
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected changePeriod(query: ScorePeriodQuery): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: periodParams(query), replaceUrl: true });
  }

  protected setTab(change: Event): void {
    const value = (change as CustomEvent<{ value?: string }>).detail.value;
    if (value === 'people' || value === 'entries') this.tab.set(value);
  }

  protected standingLabel(person: ScorePersonRowDto): string | null {
    if (person.standing === 'Current') return null;
    return person.standing === 'Moved' ? 'переведений' : 'вибув';
  }

  protected entryLine(entry: ScoreEntryDto): string {
    const whom = entry.isForGroup ? 'гуртку' : (entry.memberName ?? '');
    return [whom, shortDate(entry.occurredOn, true), entry.createdByName].filter(Boolean).join(' · ');
  }

  protected giveToAnyone(): void {
    this.editor.start({ targets: this.targets() });
  }

  protected giveTo(person: ScorePersonRowDto): void {
    this.editor.start({ target: { membershipKey: person.membershipKey, groupKey: null, name: person.fullName } });
  }

  protected editEntry(entry: ScoreEntryDto): void {
    this.editor.start({ entry });
  }

  protected openSheet(entry: ScoreEntryDto): void {
    void this.router.navigate(['/tabs/calendar/attendance', entry.agendaItemKey], {
      queryParams: { start: entry.occurrenceStartUtc },
    });
  }

  private async load(): Promise<void> {
    if (!this.kurinKey || !this.groupKey) {
      this.view.set({ state: 'failed' });
      return;
    }
    if (this.view().state !== 'ready') this.view.set({ state: 'loading' });
    try {
      this.view.set({ state: 'ready', value: await this.scores.group(this.kurinKey, this.groupKey, this.period()) });
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) this.view.set({ state: 'forbidden' });
      else if (this.view().state !== 'ready') this.view.set({ state: 'failed' });
    }
  }
}
