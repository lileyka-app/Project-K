import { Component, ElementRef, OnDestroy, afterRenderEffect, computed, effect, inject, signal, untracked } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSkeletonText,
  IonTitle,
  IonToggle,
  IonToolbar,
  ViewWillEnter,
  IonMenuButton,
} from '@ionic/angular';
import { HeaderActions } from '../../nav/header-actions';
import { addIcons } from 'ionicons';
import { add, chevronBack, chevronForward, time } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { FAILED_TEXT, Loaded } from '../../core/loaded';
import { GlassEffects } from '../../ui/glass';
import { AgendaItemDto } from '../agenda/agenda.models';
import {
  WEEKDAY_HEADERS,
  addDays,
  canManageAgenda,
  dayKey,
  dayTimeline,
  fromDayKey,
  gridWindow,
  itemsByDay,
  longDayLabel,
  monthGrid,
  monthTitle,
  occurrenceId,
  rowTime,
  scheduleOf,
  statusLabel,
  targetsLabel,
  weekOf,
  weekTitle,
  MIN_BLOCK_MINUTES,
} from '../agenda/agenda.labels';
import { AgendaService } from '../agenda/agenda.service';

const SCHEDULES_KEY = 'lil.agenda.showSchedules';
const VIEW_KEY = 'lil.agenda.calendarView';
/** Height of an hour on the day's grid, in px (iOS Calendar draws about this). */
const HOUR_PX = 52;

type CalendarView = 'month' | 'week' | 'day' | 'list';
const VIEWS: ReadonlySet<CalendarView> = new Set<CalendarView>(['month', 'week', 'day', 'list']);
/** What prev/next steps through in each view, for its buttons' names. */
const STEP_NAMES: Record<Exclude<CalendarView, 'list'>, [string, string]> = {
  month: ['Попередній місяць', 'Наступний місяць'],
  week: ['Попередній тиждень', 'Наступний тиждень'],
  day: ['Попередній день', 'Наступний день'],
};
/** How far ahead the list view reads. */
const LIST_DAYS = 60;

/**
 * The kurin's calendar as a phone shows one (iOS Calendar), in the web's month/week/day plus a list
 * of what is coming: a month of days with a dot under each day that has something and the chosen
 * day's agenda below it; a week as its seven days one under another; a day as an hour grid with
 * all-day items on top and its week as a strip to hop between days. The chosen view and
 * «Графіки гуртків» (every гурток's schedule, as on the web) are remembered per device.
 */
@Component({
  selector: 'app-calendar',
  imports: [
    IonMenuButton,
    HeaderActions,
    NgTemplateOutlet,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonIcon,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSegment,
    IonSegmentButton,
    IonLabel,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonNote,
    IonToggle,
    IonSkeletonText,
  ],
  styles: `
    .switch {
      padding: 4px 16px 8px;
    }
    /* Four views share a phone's width: Material's roomy padding would cut «Тиждень». */
    .switch ion-segment-button {
      --padding-start: 4px;
      --padding-end: 4px;
      min-width: 0;
    }
    .nav {
      display: flex;
      align-items: center;
      padding: 0 8px 0 20px;
    }
    :host-context(.ios) .nav {
      padding-inline-start: 16px;
    }
    .nav h2 {
      flex: 1;
      margin: 0;
      font-size: 20px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .nav h2.short {
      min-width: 0;
      font-size: 17px;
      line-height: 22px;
      text-wrap: balance;
    }
    .nav ion-button {
      min-height: 44px;
    }
    .grid {
      margin: 0 12px;
      touch-action: pan-y;
    }
    .week {
      display: grid;
      grid-template-columns: repeat(7, 1fr);
    }
    .weekdays span {
      text-align: center;
      font-size: 12px;
      font-weight: 600;
      color: var(--lk-faint);
      padding: 6px 0 2px;
    }
    .day {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
      min-height: 48px;
      padding: 4px 0 2px;
      border: 0;
      background: transparent;
      color: var(--lk-ink);
      font: inherit;
      cursor: pointer;
    }
    .num {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 34px;
      height: 34px;
      border-radius: 50%;
      font-size: 17px;
      font-weight: 500;
    }
    .day.other .num {
      color: var(--lk-faint);
    }
    .day.today .num {
      color: var(--lk-primary);
      font-weight: 700;
    }
    .day.selected .num {
      background: var(--lk-ink);
      color: var(--lk-paper);
      font-weight: 700;
    }
    .day.selected.today .num {
      background: var(--lk-primary);
      color: var(--lk-on-primary);
    }
    .dots {
      display: flex;
      gap: 3px;
      height: 6px;
    }
    .dots i {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: var(--lk-faint);
    }
    .time {
      width: 52px;
      margin-inline-end: 12px;
      font-size: 13px;
      line-height: 18px;
      color: var(--lk-ink);
      text-align: end;
      font-variant-numeric: tabular-nums;
    }
    .time small {
      display: block;
      color: var(--lk-muted);
      font-size: 13px;
    }
    .bar {
      align-self: stretch;
      width: 4px;
      margin: 10px 12px 10px 0;
      border-radius: 2px;
      background: var(--lk-primary);
    }
    .bar.task {
      background: var(--lk-accent-700);
    }
    .strip {
      margin-bottom: 4px;
    }
    .free {
      color: var(--lk-faint);
    }
    ion-list.today ion-list-header {
      color: var(--lk-primary);
    }
    ion-list-header ion-button {
      margin-inline-start: auto;
      --padding-start: 8px;
      --padding-end: 8px;
      min-height: 44px;
    }
    .hours {
      position: relative;
      margin: 20px 16px 16px;
    }
    .hour {
      position: absolute;
      left: 0;
      right: 0;
      height: 0;
    }
    .hour::after {
      content: '';
      position: absolute;
      left: 52px;
      right: 0;
      border-top: 1px solid var(--lk-line);
    }
    .hour span {
      position: absolute;
      left: 0;
      top: -8px;
      width: 44px;
      font-size: 12px;
      line-height: 16px;
      text-align: end;
      color: var(--lk-faint);
      font-variant-numeric: tabular-nums;
    }
    .now {
      position: absolute;
      left: 48px;
      right: 0;
      z-index: 2;
      border-top: 2px solid var(--lk-danger);
    }
    .now::before {
      content: '';
      position: absolute;
      left: 0;
      top: -5px;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--lk-danger);
    }
    .lane {
      position: absolute;
      top: 1px;
      bottom: 0;
      left: 56px;
      right: 0;
    }
    .block {
      --block-color: var(--lk-primary);
      position: absolute;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      overflow: hidden;
      padding: 3px 6px;
      border-radius: 6px;
      border-inline-start: 3px solid var(--block-color);
      background: color-mix(in srgb, var(--block-color) 16%, var(--lk-paper));
      color: var(--lk-ink);
      font-size: 13px;
      line-height: 17px;
      text-decoration: none;
    }
    .block.task {
      --block-color: var(--lk-accent-700);
    }
    .block.short {
      flex-direction: row;
      align-items: center;
      gap: 6px;
      padding-block: 0;
    }
    .block span {
      min-width: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .block .what {
      font-weight: 600;
    }
    .block.short .what {
      flex: 1;
    }
    .block .when {
      color: var(--lk-muted);
      font-variant-numeric: tabular-nums;
    }
    .block.short .when {
      flex: none;
    }
    .empty {
      margin: 8px 20px 0;
      color: var(--lk-muted);
    }
    :host-context(.ios) .empty {
      margin-inline: 32px;
    }
    .note {
      margin: 6px 20px 24px;
      font-size: 13px;
      color: var(--lk-muted);
    }
    :host-context(.ios) .note {
      margin-inline: 32px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-menu-button /></ion-buttons>
        <ion-title>Календар</ion-title>
        <ion-buttons slot="end">
          @if (canManage()) {
            <ion-button
              routerLink="/tabs/calendar/new"
              [queryParams]="{ date: selected() }"
              aria-label="Нова подія"
              data-testid="new-event"
            >
              <ion-icon slot="icon-only" name="add" />
            </ion-button>
          }
          <app-header-actions />
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>
      <ion-header collapse="condense">
        <ion-toolbar><ion-title size="large">Календар</ion-title></ion-toolbar>
      </ion-header>

      @if (!kurinKey) {
        <p class="empty">Ти ще не в курені, тож календаря немає.</p>
      } @else {
        <div class="switch">
          <ion-segment [value]="view()" (ionChange)="setView($event)" data-testid="views">
            <ion-segment-button value="month"><ion-label>Місяць</ion-label></ion-segment-button>
            <ion-segment-button value="week"><ion-label>Тиждень</ion-label></ion-segment-button>
            <ion-segment-button value="day"><ion-label>День</ion-label></ion-segment-button>
            <ion-segment-button value="list"><ion-label>Список</ion-label></ion-segment-button>
          </ion-segment>
        </div>

        <!-- One nav per view, so each step button is made with its own name. -->
        @for (current of steppedView(); track current) {
          <div class="nav">
            <h2 [class.short]="current !== 'month'" data-testid="period-title">{{ title() }}</h2>
            <ion-button fill="clear" size="default" (click)="goToday()">Сьогодні</ion-button>
            <ion-button fill="clear" size="default" (click)="shift(-1)" [attr.aria-label]="stepNames[current][0]">
              <ion-icon slot="icon-only" name="chevron-back" />
            </ion-button>
            <ion-button fill="clear" size="default" (click)="shift(1)" [attr.aria-label]="stepNames[current][1]">
              <ion-icon slot="icon-only" name="chevron-forward" />
            </ion-button>
          </div>
        }

        @switch (view()) {
          @case ('month') {
            <div class="grid" (touchstart)="touchStart($event)" (touchend)="touchEnd($event)" data-testid="month">
              <div class="week weekdays">
                @for (name of weekdayHeaders; track name) { <span>{{ name }}</span> }
              </div>
              @for (week of weeks(); track week[0].getTime()) {
                <div class="week">
                  @for (date of week; track date.getTime()) {
                    <ng-container *ngTemplateOutlet="dayCell; context: { $implicit: date, other: date.getMonth() !== month().getMonth() }" />
                  }
                </div>
              }
            </div>

            <ion-list [inset]="true" data-testid="day-agenda">
              <ion-list-header><ion-label>{{ selectedLabel() }}</ion-label></ion-list-header>
              <ion-item-group>
                @switch (rangeState().state) {
                  @case ('loading') {
                    <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
                  }
                  @case ('failed') {
                    <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item>
                  }
                  @default {
                    @for (item of dayItems(); track rowId(item)) {
                      <ng-container *ngTemplateOutlet="row; context: { $implicit: item, day: selected() }" />
                    } @empty {
                      <ion-item><ion-label class="ion-text-wrap">Цього дня нічого немає.</ion-label></ion-item>
                    }
                  }
                }
              </ion-item-group>
            </ion-list>
          }

          @case ('week') {
            <div (touchstart)="touchStart($event)" (touchend)="touchEnd($event)" data-testid="week">
              @switch (rangeState().state) {
                @case ('loading') { <ng-container *ngTemplateOutlet="skeleton" /> }
                @case ('failed') { <p class="empty">{{ failedText }}</p> }
                @default {
                  @for (group of weekGroups(); track group.day) {
                    <ion-list [inset]="true" [class.today]="group.day === today" [attr.data-week-day]="group.day" data-testid="week-day">
                      <ion-list-header>
                        <ion-label>{{ group.label }}</ion-label>
                        <ion-button size="default" (click)="openDay(group.day)" [attr.aria-label]="'Відкрити день: ' + group.label" data-testid="open-day">
                          <ion-icon slot="icon-only" name="chevron-forward" />
                        </ion-button>
                      </ion-list-header>
                      <ion-item-group>
                        @for (item of group.items; track rowId(item)) {
                          <ng-container *ngTemplateOutlet="row; context: { $implicit: item, day: group.day }" />
                        } @empty {
                          <ion-item><ion-label class="free">Нічого немає.</ion-label></ion-item>
                        }
                      </ion-item-group>
                    </ion-list>
                  }
                }
              }
            </div>
          }

          @case ('day') {
            <div class="grid strip" data-testid="week-strip">
              <div class="week weekdays">
                @for (name of weekdayHeaders; track name) { <span>{{ name }}</span> }
              </div>
              <div class="week">
                @for (date of weekDays(); track date.getTime()) {
                  <ng-container *ngTemplateOutlet="dayCell; context: { $implicit: date, other: false }" />
                }
              </div>
            </div>
            <div (touchstart)="touchStart($event)" (touchend)="touchEnd($event)" data-testid="day">
              @switch (rangeState().state) {
                @case ('loading') { <ng-container *ngTemplateOutlet="skeleton" /> }
                @case ('failed') { <p class="empty">{{ failedText }}</p> }
                @default {
                  @if (timeline().allDay.length) {
                    <ion-list [inset]="true" data-testid="all-day">
                      <ion-item-group>
                        @for (item of timeline().allDay; track rowId(item)) {
                          <ng-container *ngTemplateOutlet="row; context: { $implicit: item, day: selected() }" />
                        }
                      </ion-item-group>
                    </ion-list>
                  } @else if (!timeline().timed.length) {
                    <p class="empty">Цього дня нічого немає.</p>
                  }
                  <div class="hours" [style.height.px]="gridHeight()" data-testid="timeline">
                    @for (hour of hours(); track hour) {
                      <div class="hour" [style.top.px]="(hour - timeline().fromHour) * hourPx" aria-hidden="true">
                        <span>{{ hourLabel(hour) }}</span>
                      </div>
                    }
                    @if (nowTop(); as top) { <div class="now" [style.top.px]="top" aria-hidden="true"></div> }
                    <div class="lane">
                      @for (block of blocks(); track rowId(block.item)) {
                        <a
                          class="block"
                          [class.task]="block.item.kind === 'Task'"
                          [class.short]="block.height < 40"
                          [style.top.px]="block.top"
                          [style.height.px]="block.height"
                          [style.left]="block.left"
                          [style.width]="block.width"
                          [style.--block-color]="block.item.categoryColorHex"
                          [routerLink]="['/tabs/calendar/event', block.item.agendaItemKey]"
                          [queryParams]="block.item.startUtc ? { start: block.item.startUtc } : {}"
                          [attr.aria-label]="block.item.title + ', ' + block.time"
                          data-testid="day-block"
                        >
                          <span class="what">{{ block.item.title }}</span>
                          <span class="when">{{ block.time }}</span>
                        </a>
                      }
                    </div>
                  </div>
                }
              }
            </div>
          }

          @default {
            @switch (listState().state) {
              @case ('loading') { <ng-container *ngTemplateOutlet="skeleton" /> }
              @case ('failed') { <p class="empty">{{ failedText }}</p> }
              @default {
                @for (group of upcoming(); track group.day) {
                  <ion-list [inset]="true" data-testid="list-day">
                    <ion-list-header><ion-label>{{ group.label }}</ion-label></ion-list-header>
                    <ion-item-group>
                      @for (item of group.items; track rowId(item)) {
                        <ng-container *ngTemplateOutlet="row; context: { $implicit: item, day: group.day }" />
                      }
                    </ion-item-group>
                  </ion-list>
                } @empty {
                  <p class="empty">Найближчі два місяці нічого не заплановано.</p>
                }
              }
            }
          }
        }

        <ion-list [inset]="true">
          <ion-item-group>
            <ion-item>
              <ion-toggle [checked]="showSchedules()" (ionChange)="setSchedules($event)" justify="space-between" data-testid="schedules">
                Графіки гуртків
              </ion-toggle>
            </ion-item>
          </ion-item-group>
        </ion-list>
        <p class="note">Сходини всіх гуртків куреня поруч із твоїми подіями.</p>

        <ion-list [inset]="true">
          <ion-item-group>
            <ion-item [button]="true" [detail]="true" routerLink="/tabs/calendar/planning" data-testid="open-planning">
              <ion-icon class="lk-tile" slot="start" name="time" aria-hidden="true" style="--lk-tile: #30b0c7" />
              <ion-label>Планування таборів</ion-label>
            </ion-item>
          </ion-item-group>
        </ion-list>
        <p class="note">Дати табору, коли найбільше людей з КВ вільні.</p>
      }

      <ng-template #dayCell let-date let-other="other">
        <button
          type="button"
          class="day"
          [class.other]="other"
          [class.today]="key(date) === today"
          [class.selected]="key(date) === selected()"
          [attr.data-day]="key(date)"
          [attr.aria-label]="dayAria(date)"
          [attr.aria-pressed]="key(date) === selected()"
          (click)="select(date)"
        >
          <span class="num">{{ date.getDate() }}</span>
          <span class="dots" aria-hidden="true">
            @for (dot of dots(date); track $index) { <i [style.background]="dot"></i> }
          </span>
        </button>
      </ng-template>

      <ng-template #skeleton>
        <ion-list [inset]="true">
          <ion-item-group>
            <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
            <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
          </ion-item-group>
        </ion-list>
      </ng-template>

      <ng-template #row let-item let-day="day">
        <ion-item
          [button]="true"
          [detail]="true"
          [routerLink]="['/tabs/calendar/event', item.agendaItemKey]"
          [queryParams]="item.startUtc ? { start: item.startUtc } : {}"
          data-testid="agenda-row"
        >
          <div class="time" slot="start">
            {{ time(item, day).top }}
            @if (time(item, day).bottom) { <small>{{ time(item, day).bottom }}</small> }
          </div>
          <span class="bar" [class.task]="item.kind === 'Task'" [style.background]="item.categoryColorHex" aria-hidden="true"></span>
          <ion-label class="ion-text-wrap">
            <h3>{{ item.title }}</h3>
            @if (subline(item); as text) { <p>{{ text }}</p> }
          </ion-label>
          @if (item.kind === 'Task') {
            <ion-note slot="end">{{ status(item.viewerStatus) }}</ion-note>
          }
        </ion-item>
      </ng-template>
    </ion-content>
  `,
})
export class CalendarPage implements ViewWillEnter, OnDestroy {
  private readonly agenda = inject(AgendaService);
  private readonly auth = inject(AuthService);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  protected readonly kurinKey = this.auth.user()?.kurinKey ?? null;
  protected readonly canManage = computed(() => canManageAgenda(this.auth.user()));
  protected readonly today = dayKey(new Date());
  protected readonly weekdayHeaders = WEEKDAY_HEADERS;
  protected readonly failedText = FAILED_TEXT;
  protected readonly key = dayKey;
  protected readonly status = statusLabel;
  protected readonly stepNames = STEP_NAMES;
  protected readonly hourPx = HOUR_PX;

  protected readonly view = signal<CalendarView>(readView());
  /** The chosen day; the month, week or day on screen is the one it falls in. */
  protected readonly selected = signal(this.today);
  protected readonly showSchedules = signal(readSchedules());
  /** The view that steps back and forth (all but the list), as a one-item list for the nav's @for. */
  protected readonly steppedView = computed(() => {
    const view = this.view();
    return view === 'list' ? [] : [view];
  });

  /** Windows already read stay, so going back and forth does not flash empty. */
  private readonly ranges = signal(new Map<string, Loaded<AgendaItemDto[]>>());
  protected readonly listState = signal<Loaded<AgendaItemDto[]>>({ state: 'loading' });

  protected readonly month = computed(() => firstOfMonth(fromDayKey(this.selected())));
  protected readonly weeks = computed(() => monthGrid(this.month()));
  protected readonly weekDays = computed(() => weekOf(fromDayKey(this.selected())));
  protected readonly title = computed(() => {
    switch (this.view()) {
      case 'week':
        return weekTitle(this.weekDays());
      case 'day':
        return capitalize(longDayLabel(fromDayKey(this.selected())));
      default:
        return monthTitle(this.month());
    }
  });
  /** The month's grid, or the week (the day view reads its whole week for the strip's dots). */
  private readonly rangeKey = computed(() => {
    const view = this.view();
    if (view === 'list') return `list|${this.showSchedules()}`;
    const { fromUtc, toUtc } = gridWindow(view === 'month' ? this.weeks() : [this.weekDays()]);
    return `${fromUtc}|${toUtc}|${this.showSchedules()}`;
  });
  protected readonly rangeState = computed<Loaded<AgendaItemDto[]>>(
    () => this.ranges().get(this.rangeKey()) ?? { state: 'loading' },
  );
  private readonly byDay = computed(() => {
    const state = this.rangeState();
    return itemsByDay(state.state === 'ready' ? state.value : []);
  });
  protected readonly dayItems = computed(() => this.byDay().get(this.selected()) ?? []);
  protected readonly selectedLabel = computed(() => capitalize(longDayLabel(fromDayKey(this.selected()))));
  protected readonly weekGroups = computed(() =>
    this.weekDays().map((date) => {
      const day = dayKey(date);
      const label = capitalize(longDayLabel(date));
      return { day, label: day === this.today ? `Сьогодні · ${longDayLabel(date)}` : label, items: this.byDay().get(day) ?? [] };
    }),
  );
  protected readonly timeline = computed(() => dayTimeline(this.dayItems(), this.selected()));
  protected readonly hours = computed(() => {
    const { fromHour, toHour } = this.timeline();
    return Array.from({ length: toHour - fromHour + 1 }, (_, i) => fromHour + i);
  });
  protected readonly gridHeight = computed(() => (this.timeline().toHour - this.timeline().fromHour) * HOUR_PX);
  /** The timed blocks placed on the grid: px from its top, lanes as shares of its width. */
  protected readonly blocks = computed(() => {
    const { timed, fromHour, toHour } = this.timeline();
    const day = this.selected();
    return timed.map((block) => {
      const end = Math.min(toHour * 60, Math.max(block.endMinute, block.startMinute + MIN_BLOCK_MINUTES));
      const share = 100 / block.lanes;
      return {
        item: block.item,
        top: ((block.startMinute - fromHour * 60) / 60) * HOUR_PX,
        height: Math.max(20, ((end - block.startMinute) / 60) * HOUR_PX - 2),
        left: `${block.lane * share}%`,
        width: `calc(${share}% - 2px)`,
        time: blockTime(block.item, day),
      };
    });
  });
  /** Where now is on today's grid; null on any other day. */
  protected readonly nowTop = computed(() => {
    if (this.selected() !== this.today) return null;
    const { fromHour, toHour } = this.timeline();
    const now = new Date();
    const minute = now.getHours() * 60 + now.getMinutes();
    if (minute < fromHour * 60 || minute > toHour * 60) return null;
    return ((minute - fromHour * 60) / 60) * HOUR_PX || null;
  });
  protected readonly upcoming = computed(() => {
    const state = this.listState();
    if (state.state !== 'ready') return [];
    const byDay = itemsByDay(state.value);
    return [...byDay.keys()]
      .filter((day) => day >= this.today)
      .sort((a, b) => a.localeCompare(b))
      .map((day) => ({ day, label: dayHeading(day, this.today), items: byDay.get(day)! }));
  });

  private entered = false;
  private touch: { x: number; y: number } | null = null;

  constructor() {
    addIcons({ add, chevronBack, chevronForward, time });
    effect(() => {
      this.rangeKey();
      untracked(() => void this.load());
    });
    afterRenderEffect(() => {
      this.view();
      this.glass.sync();
    });
  }

  /** Back from an item or a form: what changed there shows here. */
  ionViewWillEnter(): void {
    if (this.entered) void this.load();
    this.entered = true;
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  private load(): Promise<void> {
    return this.view() === 'list' ? this.loadList() : this.loadRange();
  }

  private async loadRange(): Promise<void> {
    if (!this.kurinKey) return;
    const cacheKey = this.rangeKey();
    const [fromUtc, toUtc] = cacheKey.split('|');
    try {
      const items = await this.agenda.calendar(this.kurinKey, fromUtc, toUtc, this.showSchedules());
      this.putRange(cacheKey, { state: 'ready', value: items });
    } catch {
      if (this.ranges().get(cacheKey)?.state !== 'ready') this.putRange(cacheKey, { state: 'failed' });
    }
  }

  private async loadList(): Promise<void> {
    if (!this.kurinKey) return;
    const from = fromDayKey(this.today);
    try {
      const items = await this.agenda.calendar(
        this.kurinKey,
        from.toISOString(),
        addDays(from, LIST_DAYS).toISOString(),
        this.showSchedules(),
      );
      this.listState.set({ state: 'ready', value: items });
    } catch {
      if (this.listState().state !== 'ready') this.listState.set({ state: 'failed' });
    }
  }

  private putRange(key: string, value: Loaded<AgendaItemDto[]>): void {
    const next = new Map(this.ranges());
    next.set(key, value);
    this.ranges.set(next);
  }

  protected setView(event: Event): void {
    const value = (event as CustomEvent<{ value?: string }>).detail.value as CalendarView | undefined;
    if (value && VIEWS.has(value)) this.changeView(value);
  }

  private changeView(view: CalendarView): void {
    this.view.set(view);
    try {
      localStorage.setItem(VIEW_KEY, view);
    } catch {
      // Blocked storage: the view still changes for this visit.
    }
  }

  protected select(date: Date): void {
    this.selected.set(dayKey(date));
  }

  /** A day of the week, in the day view. */
  protected openDay(day: string): void {
    this.selected.set(day);
    this.changeView('day');
  }

  /**
   * One month, week or day on. As iOS Calendar: a new month opens on its first day, or on today in
   * this month; a week keeps the weekday.
   */
  protected shift(steps: number): void {
    const view = this.view();
    const selected = fromDayKey(this.selected());
    if (view === 'week' || view === 'day') {
      this.selected.set(dayKey(addDays(selected, view === 'week' ? steps * 7 : steps)));
      return;
    }
    const next = new Date(selected.getFullYear(), selected.getMonth() + steps, 1);
    const today = fromDayKey(this.today);
    this.selected.set(sameMonth(next, today) ? this.today : dayKey(next));
  }

  protected goToday(): void {
    this.selected.set(this.today);
  }

  protected touchStart(event: TouchEvent): void {
    const touch = event.changedTouches[0];
    this.touch = touch ? { x: touch.clientX, y: touch.clientY } : null;
  }

  /** A sideways swipe turns the month, week or day; a scroll that drifts sideways does not. */
  protected touchEnd(event: TouchEvent): void {
    const end = event.changedTouches[0];
    const start = this.touch;
    this.touch = null;
    if (!start || !end) return;
    const dx = end.clientX - start.x;
    const dy = end.clientY - start.y;
    if (Math.abs(dx) > 60 && Math.abs(dx) > 2 * Math.abs(dy)) this.shift(dx < 0 ? 1 : -1);
  }

  protected setSchedules(event: Event): void {
    const on = (event as CustomEvent<{ checked: boolean }>).detail.checked;
    this.showSchedules.set(on);
    try {
      localStorage.setItem(SCHEDULES_KEY, on ? 'on' : 'off');
    } catch {
      // Blocked storage: the switch still works for this visit.
    }
  }

  /** Up to three dots under a day, in the colour of each item's group. */
  protected dots(date: Date): string[] {
    return (this.byDay().get(dayKey(date)) ?? []).slice(0, 3).map((item) => item.categoryColorHex ?? 'var(--lk-primary)');
  }

  protected dayAria(date: Date): string {
    const count = this.byDay().get(dayKey(date))?.length ?? 0;
    return count ? `${longDayLabel(date)}, записів: ${count}` : longDayLabel(date);
  }

  protected hourLabel(hour: number): string {
    return `${String(hour).padStart(2, '0')}:00`;
  }

  protected rowId(item: AgendaItemDto): string {
    return occurrenceId(item);
  }

  protected time(item: AgendaItemDto, day: string): { top: string; bottom: string } {
    return rowTime(item, day);
  }

  protected subline(item: AgendaItemDto): string {
    const schedule = scheduleOf(item);
    if (schedule) return `Графік куреня · ${schedule}`;
    return item.location || targetsLabel(item);
  }
}

function firstOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function sameMonth(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth();
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** «17:00–19:00», «до 02:00» on the day a late event ends, «17:00» for one without an end. */
function blockTime(item: AgendaItemDto, day: string): string {
  const { top, bottom } = rowTime(item, day);
  if (top === 'до') return `до ${bottom}`;
  return bottom ? `${top}–${bottom}` : top;
}

/** «Сьогодні», «Завтра», else «Субота, 11 жовтня». */
function dayHeading(day: string, today: string): string {
  if (day === today) return 'Сьогодні';
  if (day === dayKey(addDays(fromDayKey(today), 1))) return 'Завтра';
  return capitalize(longDayLabel(fromDayKey(day)));
}

/** The view last chosen on this device; the month until one is. */
function readView(): CalendarView {
  try {
    const stored = localStorage.getItem(VIEW_KEY) as CalendarView | null;
    return stored && VIEWS.has(stored) ? stored : 'month';
  } catch {
    return 'month';
  }
}

function readSchedules(): boolean {
  try {
    return localStorage.getItem(SCHEDULES_KEY) !== 'off';
  } catch {
    return true;
  }
}
