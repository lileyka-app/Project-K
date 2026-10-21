import { Component, ElementRef, OnDestroy, OnInit, afterRenderEffect, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
  IonProgressBar,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
  ToastController,
  ViewWillEnter,
  IonMenuButton,
} from '@ionic/angular';
import { HeaderActions } from '../nav/header-actions';
import { addIcons } from 'ionicons';
import { arrowDown, checkbox, chevronForward, star, wallet } from 'ionicons/icons';
import { AuthService } from '../auth/auth.service';
import { GlassEffects } from '../ui/glass';
import { dayLabel, greeting, money, timeLabel, todayLabel } from '../me/labels';
import {
  AgendaItemStatus,
  AgendaRsvpStatus,
  MyDuesDto,
  MyDutyDto,
  MyEventDto,
  MyGrowthDto,
  MyScoreDto,
  MyTaskDto,
} from '../me/me.models';
import { MeService } from '../me/me.service';
import { HomeTile, TileLayout, boardLayout, homeTiles, readStoredLayout, writeStoredLayout } from '../me/home-layout';
import { InstallCard } from '../pwa/install-card';
import { dutiesSpanKurins, dutyRows } from '../features/leader/duties';
import { NotificationsService } from '../features/leader/notifications.service';

/** A section's data: still loading, here, or failed (the rest of the screen still shows). */
type Loaded<T> = { state: 'loading' } | { state: 'ready'; value: T } | { state: 'failed' };

const RSVP: { value: AgendaRsvpStatus; label: string }[] = [
  { value: 'Going', label: 'Іду' },
  { value: 'Maybe', label: 'Можливо' },
  { value: 'NotGoing', label: 'Не йду' },
];

@Component({
  selector: 'app-home',
  imports: [
    IonMenuButton,
    HeaderActions,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardSubtitle,
    IonCardContent,
    IonButton,
    IonButtons,
    IonIcon,
    IonList,
    IonItem,
    IonNote,
    IonLabel,
    IonSegment,
    IonSegmentButton,
    IonProgressBar,
    IonSkeletonText,
    InstallCard,
    RouterLink,
  ],
  styles: `
    .subline {
      margin: 0 20px 4px;
      color: var(--lk-muted);
      font-size: 15px;
      line-height: 20px;
    }
    /* Under the large title, on its leading edge (Apple's subhead, 15/20). */
    :host-context(.ios) .subline {
      margin-inline: 16px;
    }
    .row {
      padding: 12px 0;
      border-bottom: 1px solid var(--lk-line);
    }
    .row:last-child {
      border-bottom: 0;
      padding-bottom: 0;
    }
    .row:first-child {
      padding-top: 0;
    }
    .row h3 {
      margin: 2px 0;
      font-size: 16px;
      font-weight: 600;
      color: var(--lk-ink);
    }
    .row p {
      margin: 0;
      font-size: 14px;
    }
    .open {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 44px;
      color: inherit;
      text-decoration: none;
      flex: 1;
      min-width: 0;
    }
    .open > div {
      flex: 1;
      min-width: 0;
    }
    .go {
      flex: none;
      font-size: 18px;
      color: var(--lk-faint);
    }
    .duties ion-list {
      background: transparent;
      padding: 0 0 8px;
    }
    .duties ion-item {
      --background: transparent;
    }
    /* The rows line up with the card's title; on iPhone the card gives them no inset of its own. */
    :host-context(.ios) .duties ion-item {
      --padding-start: 20px;
    }
    .duties ion-card-title .count {
      margin-inline-start: 6px;
      color: var(--lk-faint);
      font-weight: 600;
    }
    .duties h3 {
      font-size: 15px;
      font-weight: 600;
      color: var(--lk-ink);
      margin: 0;
    }
    .when {
      color: var(--lk-primary);
      font-weight: 600;
    }
    .late {
      color: var(--lk-danger);
    }
    .task {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .task > div {
      flex: 1;
      min-width: 0;
    }
    ion-segment {
      margin-top: 8px;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
      text-align: center;
    }
    .stats strong {
      display: block;
      font-size: 24px;
      color: var(--lk-ink);
    }
    .big {
      font-size: 28px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .ok {
      color: var(--lk-primary);
    }
    .debt {
      color: var(--lk-danger);
    }
    ion-progress-bar {
      margin: 8px 0;
      height: 6px;
      border-radius: 3px;
    }
    .muted {
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-menu-button /></ion-buttons>
        <ion-title>{{ hello }}</ion-title>
        <ion-buttons slot="end"><app-header-actions /></ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      <ion-header collapse="condense">
        <ion-toolbar>
          <ion-title size="large">{{ hello }}</ion-title>
        </ion-toolbar>
      </ion-header>
      <p class="subline">{{ subline() }}</p>

      <app-install-card />

      @if (!layoutReady()) {
        <ion-card><ion-card-content><ion-skeleton-text [animated]="true" style="height: 48px" /></ion-card-content></ion-card>
      }
      <!-- In the order the person chose on the web's Головна, less the tiles they hid there. -->
      @for (tile of tiles(); track tile) {
        @switch (tile) {
          @case ('my-duties') {
            <ion-card class="duties" data-testid="duties">
              <ion-card-header>
                <ion-card-subtitle>Що чекає на тебе як на провід</ion-card-subtitle>
                <ion-card-title>Справи<span class="count">{{ dutiesTotal() }}</span></ion-card-title>
              </ion-card-header>
              <ion-list lines="full">
                @for (row of duties(); track row.key; let last = $last) {
                  <ion-item
                    [button]="!!row.link"
                    [detail]="!!row.link"
                    [routerLink]="row.link"
                    [queryParams]="row.queryParams"
                    [lines]="last ? 'none' : 'full'"
                    data-testid="duty"
                  >
                    <ion-icon class="lk-tile" slot="start" [name]="row.icon" [style.--lk-tile]="row.tile" aria-hidden="true" />
                    <ion-label class="ion-text-wrap">
                      <h3>{{ row.label }}</h3>
                      @if (row.detail) { <p>{{ row.detail }}</p> }
                    </ion-label>
                    @if (row.count !== null) { <ion-note slot="end">{{ row.count }}</ion-note> }
                  </ion-item>
                }
              </ion-list>
            </ion-card>
          }
          @case ('upcoming-events') {
            <ion-card>
              <ion-card-header><ion-card-title>Найближче</ion-card-title></ion-card-header>
              <ion-card-content>
                @switch (events().state) {
                  @case ('loading') { <ion-skeleton-text [animated]="true" style="height: 48px" /> }
                  @case ('failed') { <p>{{ failedText }}</p> }
                  @default {
                    @for (event of upcoming(); track event.agendaItemKey + event.startUtc) {
                      <div class="row" data-testid="event">
                        <a
                          class="open"
                          [routerLink]="['/tabs/calendar/event', event.agendaItemKey]"
                          [queryParams]="event.isRecurring ? { start: event.startUtc } : null"
                        >
                          <div>
                            <p class="when">{{ when(event) }}</p>
                            <h3>{{ event.title }}</h3>
                            @if (event.location) { <p>{{ event.location }}</p> }
                          </div>
                          <ion-icon class="go" name="chevron-forward" aria-hidden="true" />
                        </a>
                        @if (event.rsvpRequired) {
                          <ion-segment
                            [value]="event.myResponse ?? ''"
                            [disabled]="responding() !== null"
                            (ionChange)="respond(event, $event)"
                          >
                            @for (option of rsvp; track option.value) {
                              <ion-segment-button [value]="option.value">
                                <ion-label>{{ option.label }}</ion-label>
                              </ion-segment-button>
                            }
                          </ion-segment>
                        }
                      </div>
                    } @empty {
                      <p>Найближчі два тижні подій немає.</p>
                    }
                  }
                }
              </ion-card-content>
            </ion-card>
          }
          @case ('my-tasks') {
            <ion-card>
              <ion-card-header><ion-card-title>Мої задачі</ion-card-title></ion-card-header>
              <ion-card-content>
                @switch (tasks().state) {
                  @case ('loading') { <ion-skeleton-text [animated]="true" style="height: 48px" /> }
                  @case ('failed') { <p>{{ failedText }}</p> }
                  @default {
                    @for (task of openTasks(); track task.agendaItemKey) {
                      <div class="row task" data-testid="task">
                        <a class="open" [routerLink]="['/tabs/tasks/task', task.agendaItemKey]">
                          <div>
                            <h3>{{ task.title }}</h3>
                            <p [class.late]="isLate(task)">{{ taskLine(task) }}</p>
                          </div>
                          @if (!task.canChangeStatus) { <ion-icon class="go" name="chevron-forward" aria-hidden="true" /> }
                        </a>
                        @if (task.canChangeStatus) {
                          <ion-button
                            size="small"
                            fill="outline"
                            [disabled]="moving() !== null"
                            (click)="move(task, task.status === 'Todo' ? 'InProgress' : 'Done')"
                          >
                            {{ task.status === 'Todo' ? 'Почати' : 'Зроблено' }}
                          </ion-button>
                        }
                      </div>
                    } @empty {
                      <p>Відкритих задач немає.</p>
                    }
                  }
                }
              </ion-card-content>
            </ion-card>
          }
          @case ('my-probe') {
            @if (growthValue()?.probe; as probe) {
              <ion-card>
                <ion-card-header>
                  <ion-card-subtitle>Проба</ion-card-subtitle>
                  <ion-card-title>{{ probe.title }}</ion-card-title>
                </ion-card-header>
                <ion-card-content>
                  <p>Підписано {{ probe.signedPoints }} з {{ probe.totalPoints }}</p>
                  <ion-progress-bar [value]="probe.totalPoints ? probe.signedPoints / probe.totalPoints : 0" />
                  @if (probe.nextPoints.length) {
                    <p class="muted">Далі:</p>
                    @for (point of probe.nextPoints.slice(0, 3); track point.pointId) {
                      <p>{{ point.sectionCode }} · {{ point.title }}</p>
                    }
                  }
                </ion-card-content>
              </ion-card>
            }
          }
          @case ('my-skills') {
            @if (growthValue(); as growth) {
              <ion-card>
                <ion-card-header><ion-card-title>Вмілості</ion-card-title></ion-card-header>
                <ion-card-content>
                  <div class="stats">
                    <div><strong>{{ growth.badges.onReview.length }}</strong>на перевірці</div>
                    <div><strong>{{ growth.badges.inWork.length }}</strong>у роботі</div>
                    <div><strong>{{ growth.badges.confirmedCount }}</strong>підтверджені</div>
                  </div>
                </ion-card-content>
              </ion-card>
            }
          }
          @case ('my-score') {
            @for (item of scoreValue(); track item.groupKey) {
              <ion-card>
                <ion-card-header>
                  <ion-card-subtitle>Точкування · {{ item.groupName }}</ion-card-subtitle>
                  <ion-card-title><span class="big">{{ item.total }}</span> балів</ion-card-title>
                </ion-card-header>
                <ion-card-content>
                  <p>Місце {{ item.groupPlace }} з {{ item.groupCount }} · {{ item.periodLabel }}</p>
                </ion-card-content>
              </ion-card>
            }
          }
          @case ('my-dues') {
            @for (item of duesValue(); track item.kurin.kurinKey) {
              <ion-card>
                <ion-card-header>
                  <ion-card-subtitle>Вкладка · {{ item.quarterNumber }} квартал {{ item.quarterYear }}</ion-card-subtitle>
                  <ion-card-title [class.debt]="item.balance < 0" [class.ok]="item.balance >= 0">
                    {{ duesTitle(item) }}
                  </ion-card-title>
                </ion-card-header>
                @if (item.quarterRate !== null) {
                  <ion-card-content>
                    <p>Ставка {{ money(item.quarterRate, item.currency, item.numberLocale) }} за квартал{{ item.isConcession ? ' (пільга)' : '' }}</p>
                  </ion-card-content>
                }
              </ion-card>
            }
          }
        }
      }
    </ion-content>
  `,
})
export class HomePage implements OnInit, OnDestroy, ViewWillEnter {
  private readonly me = inject(MeService);
  private readonly notifications = inject(NotificationsService);
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastController);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  private readonly now = new Date();
  protected readonly hello = greeting(this.now);
  protected readonly rsvp = RSVP;
  protected readonly money = money;
  protected readonly failedText = 'Не вдалося завантажити. Потягни вниз, щоб оновити.';

  private readonly firstName = signal<string | null>(null);
  protected readonly events = signal<Loaded<MyEventDto[]>>({ state: 'loading' });
  protected readonly tasks = signal<Loaded<MyTaskDto[]>>({ state: 'loading' });
  private readonly growth = signal<Loaded<MyGrowthDto>>({ state: 'loading' });
  private readonly score = signal<Loaded<MyScoreDto[]>>({ state: 'loading' });
  private readonly dues = signal<Loaded<MyDuesDto[]>>({ state: 'loading' });
  private readonly dutyList = signal<Loaded<MyDutyDto[]>>({ state: 'loading' });
  /** The web's last read of the layout starts the screen; the API's answer replaces it. */
  private readonly layout = signal<TileLayout | null>(readStoredLayout());
  protected readonly layoutReady = signal(this.layout() !== null);
  protected readonly responding = signal<string | null>(null);
  protected readonly moving = signal<string | null>(null);

  protected readonly subline = computed(() => {
    const name = this.firstName();
    return name ? `${name} · ${todayLabel(this.now)}` : todayLabel(this.now);
  });
  protected readonly upcoming = computed(() => valueOf(this.events())?.slice(0, 5) ?? []);
  protected readonly openTasks = computed(
    () => valueOf(this.tasks())?.filter((t) => t.status !== 'Done').slice(0, 5) ?? [],
  );
  protected readonly growthValue = computed(() => valueOf(this.growth()));
  protected readonly scoreValue = computed(() => valueOf(this.score()) ?? []);
  protected readonly duesValue = computed(() => valueOf(this.dues()) ?? []);
  /** Like the web: the card is there only while something waits (a failed read hides it too). */
  protected readonly duties = computed(() => {
    const duties = valueOf(this.dutyList()) ?? [];
    return dutyRows(duties, new Date(), dutiesSpanKurins(duties));
  });
  /** The tiles with something to show, in the phone's order; then the web's layout over them. */
  protected readonly tiles = computed<HomeTile[]>(() => {
    if (!this.layoutReady()) return [];
    const growth = this.growthValue();
    const present: HomeTile[] = [];
    if (this.duties().length) present.push('my-duties');
    present.push('upcoming-events', 'my-tasks');
    if (growth?.hasYouthProgram && growth.probe) present.push('my-probe');
    if (growth?.hasYouthProgram) present.push('my-skills');
    if (this.scoreValue().length) present.push('my-score');
    if (this.duesValue().length) present.push('my-dues');
    return homeTiles(this.layout(), present);
  });
  protected readonly dutiesTotal = computed(() => (valueOf(this.dutyList()) ?? []).reduce((sum, d) => sum + d.count, 0));

  constructor() {
    addIcons({ chevronForward, star, arrowDown, wallet, checkbox });
    // The RSVP segments come and go with the events; each gets the iOS glass lens (no-op on md).
    afterRenderEffect(() => {
      this.upcoming();
      this.glass.sync();
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  /** Home stays alive under the screens it opens; the bell is re-read each time it shows. */
  ionViewWillEnter(): void {
    void this.notifications.refreshUnread();
  }

  protected async refresh(event: Event): Promise<void> {
    await Promise.all([this.load(), this.notifications.refreshUnread()]);
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected when(event: MyEventDto): string {
    const start = new Date(event.startUtc);
    const end = event.endUtc ? new Date(event.endUtc) : null;
    return [dayLabel(start, new Date()), timeLabel(start, end, event.isAllDay)].filter(Boolean).join(' · ');
  }

  protected isLate(task: MyTaskDto): boolean {
    return task.endUtc !== null && new Date(task.endUtc) < new Date();
  }

  protected taskLine(task: MyTaskDto): string {
    const status = task.status === 'InProgress' ? 'В процесі' : 'Зробити';
    if (!task.endUtc) return status;
    const due = dayLabel(new Date(task.endUtc), new Date());
    return `${status} · ${this.isLate(task) ? 'прострочено, ' : 'до '}${due}`;
  }

  protected duesTitle(item: MyDuesDto): string {
    if (item.balance < 0) return `Борг ${money(-item.balance, item.currency, item.numberLocale)}`;
    if (item.balance > 0) return `Сплачено, +${money(item.balance, item.currency, item.numberLocale)}`;
    return 'Сплачено';
  }

  /** The answer lands on the row at once and goes back if the server refuses it. */
  protected async respond(event: MyEventDto, change: Event): Promise<void> {
    const status = (change as CustomEvent<{ value?: string }>).detail.value as AgendaRsvpStatus | undefined;
    if (!status || status === event.myResponse || this.responding()) return;
    const key = event.agendaItemKey + event.startUtc;
    this.responding.set(key);
    this.patchEvent(key, status);
    try {
      await this.me.respond(event, status);
    } catch {
      this.patchEvent(key, event.myResponse);
      await this.toast('Не вдалося відповісти. Спробуй ще раз.');
    } finally {
      this.responding.set(null);
    }
  }

  protected async move(task: MyTaskDto, status: AgendaItemStatus): Promise<void> {
    if (this.moving()) return;
    this.moving.set(task.agendaItemKey);
    try {
      await this.me.moveTask(task, status);
      const current = valueOf(this.tasks()) ?? [];
      this.tasks.set({
        state: 'ready',
        value: current.map((t) => (t.agendaItemKey === task.agendaItemKey ? { ...t, status } : t)),
      });
      if (status === 'Done') await this.toast('Зроблено');
    } catch {
      await this.toast('Не вдалося змінити задачу. Спробуй ще раз.');
    } finally {
      this.moving.set(null);
    }
  }

  private async load(): Promise<void> {
    const memberKey = this.auth.user()?.memberKey;
    await Promise.all([
      memberKey
        ? this.me.member(memberKey).then((m) => this.firstName.set(m.firstName), () => undefined)
        : Promise.resolve(),
      settle(this.me.events(), this.events),
      settle(this.me.tasks(), this.tasks),
      settle(this.me.growth(), this.growth),
      settle(this.me.score(), this.score),
      settle(this.me.dues(), this.dues),
      settle(this.me.duties(), this.dutyList),
      this.loadLayout(),
    ]);
  }

  /** Read only: what the person hid or moved on the web's Головна. No layout, or a failed read with none kept, shows every tile. */
  private async loadLayout(): Promise<void> {
    try {
      const layout = boardLayout(await this.me.layouts());
      this.layout.set(layout);
      writeStoredLayout(layout);
    } catch {
      // Keeps the layout the web left on this device, if any.
    } finally {
      this.layoutReady.set(true);
    }
  }

  private patchEvent(key: string, myResponse: AgendaRsvpStatus | null): void {
    const current = valueOf(this.events()) ?? [];
    this.events.set({
      state: 'ready',
      value: current.map((e) => (e.agendaItemKey + e.startUtc === key ? { ...e, myResponse } : e)),
    });
  }

  private async toast(message: string): Promise<void> {
    const toast = await this.toasts.create({ message, duration: 2500, position: 'top' });
    await toast.present();
  }
}

function valueOf<T>(loaded: Loaded<T>): T | null {
  return loaded.state === 'ready' ? loaded.value : null;
}

/** Fills one section; a failed read keeps what was shown before a refresh, if anything. */
async function settle<T>(request: Promise<T>, target: { set(v: Loaded<T>): void; (): Loaded<T> }): Promise<void> {
  try {
    target.set({ state: 'ready', value: await request });
  } catch {
    if (target().state !== 'ready') target.set({ state: 'failed' });
  }
}
