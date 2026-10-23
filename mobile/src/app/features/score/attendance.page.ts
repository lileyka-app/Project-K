import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, afterRenderEffect, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import {
  ActionSheetController,
  AlertController,
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
  IonLabel,
  IonList,
  IonListHeader,
  IonModal,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSearchbar,
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { checkmarkCircle, ellipseOutline } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { FAILED_TEXT } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { dateLabel, timeLabel } from '../../me/labels';
import { GlassEffects } from '../../ui/glass';
import { EntryEditor } from './score-entry.editor';
import { ScoreEntryForm } from './score-entry.form';
import { SheetGroupFilter, Shelf, filterSheet, notHeldLine, points, rsvpLabel, shelfOf } from './score.format';
import {
  AttendanceMarkDto,
  AttendanceSheetDto,
  SCORE_ERRORS,
  ScoreEntryEvent,
  SheetGroupDto,
  SheetPersonDto,
} from './score.models';
import { ScoreService } from './score.service';

type View = { state: 'loading' } | { state: 'ready'; value: AttendanceSheetDto } | { state: 'failed' } | { state: 'forbidden' };

const SHELVES: { value: Shelf; label: string }[] = [
  { value: 'answered', label: 'Відповіли' },
  { value: 'assigned', label: 'Призначені' },
  { value: 'others', label: 'Решта' },
];

/**
 * Who was at one occurrence of an event (the web's attendance-sheet), made for marking on the
 * spot: those who answered «Іду» or «Можливо», those the event was aimed at, and everyone else,
 * as a segment. A tap marks or unmarks at once and goes back with a toast if the server says no;
 * points go to a person or a гурток from the same sheet. The occurrence is the event's start:
 * the `start` query param for a series, the item's own start otherwise.
 */
@Component({
  selector: 'app-attendance',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonCard,
    IonCardContent,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonBadge,
    IonIcon,
    IonSpinner,
    IonSearchbar,
    IonSegment,
    IonSegmentButton,
    IonSelect,
    IonSelectOption,
    IonSkeletonText,
    IonModal,
    ScoreEntryForm,
  ],
  styles: `
    .lead {
      padding: 0 20px 4px;
    }
    :host-context(.ios) .lead {
      padding-inline: 16px;
    }
    /* No large title on Android: the event starts the page, clear of the toolbar. */
    :host-context(.md) .lead {
      padding-top: 16px;
    }
    .lead h2 {
      margin: 0 0 2px;
      font-size: 20px;
      line-height: 26px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .lead p {
      margin: 0;
      color: var(--lk-muted);
      font-size: 15px;
      line-height: 20px;
    }
    .dot {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      margin-inline-end: 6px;
      background: var(--lk-faint);
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
      text-align: center;
    }
    .stats strong {
      display: block;
      font-size: 26px;
      line-height: 32px;
      font-weight: 700;
      color: var(--lk-ink);
      font-variant-numeric: tabular-nums;
    }
    .stats .primary strong {
      color: var(--lk-primary);
    }
    .stats span {
      font-size: 12px;
      line-height: 16px;
      color: var(--lk-muted);
    }
    ion-searchbar {
      padding: 8px 16px 0;
    }
    :host-context(.md) ion-searchbar {
      padding-inline: 12px;
    }
    ion-segment {
      margin: 8px 16px 0;
      width: auto;
    }
    .bulk {
      margin: 12px 16px 0;
    }
    .person {
      --min-height: 60px;
    }
    .check {
      width: 30px;
      height: 30px;
      margin-inline-end: 14px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .check ion-icon {
      font-size: 30px;
      color: var(--lk-faint);
    }
    .check.on ion-icon {
      color: var(--lk-primary);
    }
    .check ion-spinner {
      width: 22px;
      height: 22px;
    }
    .readonly .check {
      opacity: 0.5;
    }
    ion-label h3 {
      font-weight: 600;
      color: var(--lk-ink);
    }
    ion-badge {
      margin-inline-start: 4px;
      vertical-align: middle;
    }
    .entries {
      margin-top: 2px;
    }
    .plus {
      color: var(--lk-primary);
      font-weight: 700;
    }
    .minus {
      color: var(--lk-danger);
      font-weight: 700;
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
      padding: 24px;
      text-align: center;
      color: var(--lk-muted);
    }
    .pad {
      height: 32px;
    }
    .not-held p {
      margin: 0 0 4px;
      color: var(--lk-muted);
    }
    .not-held strong {
      color: var(--lk-ink);
    }
    .not-held ion-button {
      margin: 4px 0 0;
      --padding-start: 0;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button [defaultHref]="backHref" text="Назад" /></ion-buttons>
        <ion-title>Присутність</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>
      <ion-header collapse="condense">
        <ion-toolbar>
          <ion-title size="large">Присутність</ion-title>
        </ion-toolbar>
      </ion-header>

      @switch (view().state) {
        @case ('loading') {
          <ion-card><ion-card-content><ion-skeleton-text [animated]="true" style="height: 64px" /></ion-card-content></ion-card>
          <ion-list [inset]="true">
            <ion-item-group>
              @for (row of [1, 2, 3, 4, 5]; track row) {
                <ion-item><ion-label><ion-skeleton-text [animated]="true" style="height: 32px" /></ion-label></ion-item>
              }
            </ion-item-group>
          </ion-list>
        }
        @case ('forbidden') {
          <p class="empty" data-testid="sheet-forbidden">
            Це аркуш судді. Відмічати присутність можуть судді, гуртковий, курінний, впорядники і Звʼязковий.
          </p>
        }
        @case ('failed') {
          <p class="empty">{{ failedText }} Може, подія того дня не збирається.</p>
        }
        @default {
          @if (value(); as d) {
            <div class="lead">
              <h2 data-testid="sheet-title">{{ d.title }}</h2>
              <p>
                @if (d.categoryName) {
                  <span class="dot" [style.background]="d.categoryColorHex"></span>{{ d.categoryName }} ·
                }
                {{ when() }}@if (d.isRecurring) { · серія }
              </p>
            </div>

            @if (d.notHeld; as notHeld) {
              <ion-card class="not-held" data-testid="sheet-not-held">
                <ion-card-content>
                  <p><strong>Сходин не було</strong> · {{ notHeldLine(notHeld) }}</p>
                  <p>Відмітити присутність можна, коли це повернуть.</p>
                  <ion-button fill="clear" size="small" [disabled]="savingNotHeld()" (click)="saveNotHeld(false)">Повернути</ion-button>
                </ion-card-content>
              </ion-card>
            }

            <ion-list [inset]="true">
              <ion-item-group>
                <ion-item [button]="d.canManage" [detail]="false" (click)="d.canManage && editRate()" data-testid="sheet-rate">
                  <ion-label>За присутність</ion-label>
                  <ion-note slot="end">
                    <span [class.plus]="d.attendancePoints > 0">{{ points(d.attendancePoints) }}</span>
                    @if (d.canManage) { · {{ d.hasOwnRate ? 'своя ставка' : 'змінити' }} }
                  </ion-note>
                </ion-item>
                @if (!d.notHeld && !markedCount()) {
                  <ion-item [button]="true" [detail]="false" [disabled]="savingNotHeld()" (click)="confirmNotHeld()" data-testid="sheet-say-not-held">
                    <ion-label color="primary">Сходин не було</ion-label>
                  </ion-item>
                }
              </ion-item-group>
            </ion-list>

            <ion-card>
              <ion-card-content>
                <div class="stats">
                  <div class="primary"><strong data-testid="sheet-marked">{{ markedCount() }}</strong><span>були</span></div>
                  <div><strong>{{ goingCount() }}</strong><span>відповіли «Іду»</span></div>
                  <div><strong>{{ assignedCount() }}</strong><span>призначено</span></div>
                </div>
              </ion-card-content>
            </ion-card>

            <ion-searchbar
              placeholder="Знайти юнака"
              [debounce]="0"
              [value]="search()"
              (ionInput)="search.set($any($event).detail.value ?? '')"
            />

            @if (groupOptions().length > 1) {
              <ion-list [inset]="true">
                <ion-item-group>
                  <ion-item>
                    <ion-select
                      data-testid="sheet-groups"
                      label="Гуртки"
                      interface="action-sheet"
                      cancelText="Скасувати"
                      [value]="group()"
                      (ionChange)="group.set($any($event).detail.value)"
                    >
                      @for (option of groupOptions(); track option.value) {
                        <ion-select-option [value]="option.value">{{ option.label }}</ion-select-option>
                      }
                    </ion-select>
                  </ion-item>
                </ion-item-group>
              </ion-list>
            }

            <ion-segment [value]="shelf()" (ionChange)="setShelf($event)">
              @for (option of shelves; track option.value) {
                <ion-segment-button [value]="option.value" [attr.data-testid]="'shelf-' + option.value">
                  <ion-label>{{ option.label }} {{ shelfCount(option.value) }}</ion-label>
                </ion-segment-button>
              }
            </ion-segment>

            @if (shelf() === 'answered' && answeredUnmarked().length && !d.notHeld) {
              <ion-button class="bulk" expand="block" data-testid="mark-answered" (click)="markAnswered()">
                Усі, хто відповів, були ({{ answeredUnmarked().length }})
              </ion-button>
            }

            @if (shown().length) {
              <ion-list [inset]="true">
                <ion-item-group>
                  @for (person of shown(); track person.membershipKey) {
                    <ion-item
                      class="person"
                      data-testid="sheet-person"
                      [attr.data-marked]="person.attendance ? 'yes' : 'no'"
                      [class.readonly]="!person.canScore"
                      [button]="person.canScore && !d.notHeld"
                      [detail]="false"
                      (click)="toggle(person)"
                    >
                      <div slot="start" class="check" [class.on]="!!person.attendance" aria-hidden="true">
                        @if (isBusy(person)) {
                          <ion-spinner name="crescent" />
                        } @else {
                          <ion-icon [name]="person.attendance ? 'checkmark-circle' : 'ellipse-outline'" />
                        }
                      </div>
                      <ion-label class="ion-text-wrap">
                        <h3>{{ person.fullName }}</h3>
                        <p>
                          {{ person.groupName }}
                          @if (rsvpLabel(person); as label) {
                            <ion-badge [class]="rsvpTag(person)">{{ label }}</ion-badge>
                          }
                        </p>
                        @if (person.attendance?.markedByName; as by) {
                          <p>відмітив {{ by }}</p>
                        }
                        @if (person.entries.length) {
                          <p class="entries">
                            @for (e of person.entries; track e.scoreEntryKey; let last = $last) {
                              <span [class.plus]="e.points > 0" [class.minus]="e.points < 0">{{ points(e.points) }}</span>
                              {{ e.itemName ?? e.reason }}@if (!last) { · }
                            }
                          </p>
                        }
                      </ion-label>
                      @if (person.canScore) {
                        <ion-button
                          slot="end"
                          fill="clear"
                          size="default"
                          [attr.aria-label]="'Дати бал: ' + person.fullName"
                          (click)="$event.stopPropagation(); giveTo(person)"
                          >Бал</ion-button
                        >
                      }
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
            } @else {
              <p class="empty">{{ emptyText() }}</p>
            }

            @if (scorableGroups().length) {
              <ion-list [inset]="true">
                <ion-list-header><ion-label>Гуртку цілим</ion-label></ion-list-header>
                <ion-item-group>
                  @for (g of scorableGroups(); track g.groupKey) {
                    <ion-item [button]="true" [detail]="false" data-testid="sheet-group" (click)="giveToGroup(g)">
                      <ion-label class="ion-text-wrap">
                        <h3>{{ g.groupName }}</h3>
                        @if (g.entries.length) {
                          <p>
                            @for (e of g.entries; track e.scoreEntryKey; let last = $last) {
                              <span [class.plus]="e.points > 0" [class.minus]="e.points < 0">{{ points(e.points) }}</span>
                              {{ e.itemName ?? e.reason }}@if (!last) { · }
                            }
                          </p>
                        }
                      </ion-label>
                      <ion-note slot="end" color="primary">Бал</ion-note>
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
              <p class="note">За гурткову точку, виступ, порядок у таборі — не ділиться на юнаків.</p>
            }
            <div class="pad"></div>

            <ion-modal [isOpen]="editor.open()" [initialBreakpoint]="1" [breakpoints]="[0, 1]" (didDismiss)="editor.close()">
              <ng-template>
                <app-score-entry-form
                  [target]="editor.target()"
                  [targets]="editor.targets()"
                  [event]="entryEvent()"
                  [items]="d.items"
                  [existing]="editor.existing()"
                  [entry]="editor.entry()"
                  [saving]="editor.saving()"
                  [errorMessage]="editor.error()"
                  [editable]="true"
                  (save)="editor.save($event)"
                  (remove)="editor.remove($event)"
                  (edit)="editor.edit($event)"
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
export class AttendancePage implements OnInit, OnDestroy {
  private readonly scores = inject(ScoreService);
  private readonly route = inject(ActivatedRoute);
  private readonly alerts = inject(AlertController);
  private readonly toasts = inject(Toasts);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  private readonly kurinKey = inject(AuthService).user()?.kurinKey ?? '';
  private readonly itemKey = this.route.snapshot.paramMap.get('itemKey') ?? '';
  private occurrence: string | null = this.route.snapshot.queryParamMap.get('start');

  protected readonly backHref = `/tabs/calendar/event/${this.itemKey}`;
  protected readonly failedText = FAILED_TEXT;
  protected readonly points = points;
  protected readonly rsvpLabel = rsvpLabel;
  protected readonly shelves = SHELVES;

  protected readonly view = signal<View>({ state: 'loading' });
  protected readonly value = computed(() => {
    const view = this.view();
    return view.state === 'ready' ? view.value : null;
  });
  protected readonly savingNotHeld = signal(false);
  protected readonly notHeldLine = notHeldLine;
  protected readonly search = signal('');
  protected readonly group = signal<SheetGroupFilter>('mine');
  protected readonly shelf = signal<Shelf>('answered');
  private readonly busy = signal<ReadonlySet<string>>(new Set());
  private shelfChosen = false;

  private readonly people = computed(() => this.value()?.people ?? []);
  private readonly visible = computed(() => filterSheet(this.people(), this.group(), this.search()));
  private readonly byShelf = computed(() => {
    const shelves: Record<Shelf, SheetPersonDto[]> = { answered: [], assigned: [], others: [] };
    for (const person of this.visible()) shelves[shelfOf(person)].push(person);
    return shelves;
  });
  protected readonly shown = computed(() => this.byShelf()[this.shelf()]);

  protected readonly markedCount = computed(() => this.people().filter((p) => p.attendance).length);
  protected readonly goingCount = computed(() => this.people().filter((p) => p.rsvp === 'Going').length);
  protected readonly assignedCount = computed(() => this.people().filter((p) => p.isAssigned).length);

  /** Those who answered, are mine and not yet marked: what «Усі, хто відповів» marks. */
  protected readonly answeredUnmarked = computed(() => this.byShelf().answered.filter((p) => p.canScore && !p.attendance));
  protected readonly scorableGroups = computed(() => (this.value()?.groups ?? []).filter((g) => g.canScore));

  /** «Мої гуртки» while some гуртки are not mine, «Усі гуртки», then each гурток with people here. */
  protected readonly groupOptions = computed(() => {
    const people = this.people();
    const named = new Map<string, string>();
    for (const p of people) if (p.groupKey) named.set(p.groupKey, p.groupName);
    const options = [...named].map(([value, label]) => ({ value, label }));
    const mixed = people.some((p) => !p.canScore);
    if (!mixed && named.size < 2) return [];
    return [
      ...(mixed ? [{ value: 'mine', label: 'Мої гуртки' }] : []),
      { value: mixed ? 'all' : 'mine', label: 'Усі гуртки' },
      ...options,
    ];
  });

  protected readonly when = computed(() => {
    const d = this.value();
    if (!d) return '';
    const start = new Date(d.occurrenceStartUtc);
    const end = d.occurrenceEndUtc ? new Date(d.occurrenceEndUtc) : null;
    return [dateLabel(start), timeLabel(start, end, d.isAllDay)].filter(Boolean).join(', ');
  });

  protected readonly entryEvent = computed<ScoreEntryEvent | null>(() => {
    const d = this.value();
    return d ? { agendaItemKey: d.agendaItemKey, occurrenceStartUtc: d.occurrenceStartUtc, title: d.title } : null;
  });

  protected readonly editor = new EntryEditor(
    this.scores,
    inject(ActionSheetController),
    this.toasts,
    () => this.kurinKey,
    () => this.load(),
  );

  constructor() {
    addIcons({ checkmarkCircle, ellipseOutline });
    afterRenderEffect(() => {
      this.value();
      this.glass.sync();
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected setShelf(change: Event): void {
    const value = (change as CustomEvent<{ value?: string }>).detail.value;
    if (value === 'answered' || value === 'assigned' || value === 'others') this.shelf.set(value);
  }

  protected shelfCount(shelf: Shelf): number {
    return this.byShelf()[shelf].length;
  }

  protected emptyText(): string {
    if (this.search().trim()) return 'Нікого не знайшлось.';
    switch (this.shelf()) {
      case 'answered':
        return 'Ніхто не відповідав. Або подія без RSVP, або ще рано.';
      case 'assigned':
        return 'Усі призначені відповіли, або подію ні на кого не призначали.';
      default:
        return 'Решти немає.';
    }
  }

  protected rsvpTag(person: SheetPersonDto): string {
    if (person.rsvp === 'Going') return 'lk-tag--info';
    return person.rsvp === 'Maybe' ? 'lk-tag--warn' : 'lk-tag--secondary';
  }

  protected isBusy(person: SheetPersonDto): boolean {
    return this.busy().has(person.membershipKey);
  }

  protected toggle(person: SheetPersonDto): void {
    if (!person.canScore || this.isBusy(person) || this.value()?.notHeld) return;
    if (person.attendance) void this.unmark(person);
    else void this.mark([person]);
  }

  protected markAnswered(): void {
    const people = this.answeredUnmarked().filter((p) => !this.isBusy(p));
    if (people.length) void this.mark(people);
  }

  protected giveTo(person: SheetPersonDto): void {
    this.editor.start({
      target: { membershipKey: person.membershipKey, groupKey: null, name: person.fullName },
      existing: person.entries,
    });
  }

  protected giveToGroup(group: SheetGroupDto): void {
    this.editor.start({
      target: { membershipKey: null, groupKey: group.groupKey, name: `Гурток ${group.groupName} цілим` },
      existing: group.entries,
    });
  }

  /** Cancelled сходини (the web's «Сходин не було»): off «Справи», and nobody to mark until taken back. */
  protected async confirmNotHeld(): Promise<void> {
    const d = this.value();
    if (!d) return;
    const alert = await this.alerts.create({
      header: 'Сходин не було?',
      message: `«${d.title}» цього дня не відбулися: відмічати нікого не треба, і зі «Справ» вони зникнуть. Це можна повернути.`,
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        { text: 'Сходин не було', role: 'confirm' },
      ],
    });
    await alert.present();
    const { role } = await alert.onWillDismiss();
    if (role === 'confirm') await this.saveNotHeld(true);
  }

  protected async saveNotHeld(notHeld: boolean): Promise<void> {
    const occurrence = this.occurrence;
    if (!occurrence || this.savingNotHeld()) return;
    this.savingNotHeld.set(true);
    try {
      await this.scores.setNotHeld(this.kurinKey, this.itemKey, occurrence, notHeld);
      await this.load();
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', SCORE_ERRORS), 'danger');
    } finally {
      this.savingNotHeld.set(false);
    }
  }

  /** The event's own rate (the web's «змінити» / «своя ставка»); «Як у групи» drops it. */
  protected async editRate(): Promise<void> {
    const d = this.value();
    if (!d?.canManage) return;
    const alert = await this.alerts.create({
      header: 'Бал за присутність',
      message: d.categoryName
        ? `Група подій «${d.categoryName}» дає своє; тут можна задати інше саме для цієї події. Зміна перераховує й уже поставлені відмітки.`
        : 'У події немає групи, тож без власної ставки присутність нічого не варта.',
      inputs: [
        { name: 'points', type: 'number', min: 0, max: 1000, value: d.attendancePoints, attributes: { inputmode: 'numeric' } },
      ],
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        ...(d.hasOwnRate ? [{ text: 'Як у групи', role: 'reset' }] : []),
        { text: 'Зберегти', role: 'confirm' },
      ],
    });
    await alert.present();
    const { role, data } = await alert.onWillDismiss<{ values?: { points?: string } }>();
    if (role === 'reset') {
      await this.saveRate(null);
    } else if (role === 'confirm') {
      const value = Number(data?.values?.points);
      if (!Number.isFinite(value) || value < 0 || value > 1000) {
        await this.toasts.show('Бал — число від 0 до 1000.', 'danger');
        return;
      }
      await this.saveRate(Math.round(value));
    }
  }

  private async saveRate(value: number | null): Promise<void> {
    try {
      await this.scores.setRate(this.kurinKey, { agendaCategoryKey: null, agendaItemKey: this.itemKey, points: value });
      await this.toasts.show('Збережено');
      await this.load();
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', SCORE_ERRORS), 'danger');
    }
  }

  /** Marked on the row at once; the server's answer settles who marked, a refusal rolls back. */
  private async mark(people: SheetPersonDto[]): Promise<void> {
    const occurrence = this.occurrence;
    if (!occurrence) return;
    const keys = people.map((p) => p.membershipKey);
    const before = new Map(people.map((p) => [p.membershipKey, p.attendance] as const));
    const now = new Date().toISOString();
    this.setBusy(keys, true);
    this.patch(keys, () => ({ markedByName: null, markedAtUtc: now }));
    try {
      const results = await this.scores.mark(this.kurinKey, this.itemKey, occurrence, keys);
      for (const result of results.filter((r) => r.outcome === 'AlreadyMarked')) {
        this.patch([result.membershipKey], () => ({ markedByName: result.markedByName, markedAtUtc: now }));
        const name = this.people().find((p) => p.membershipKey === result.membershipKey)?.fullName ?? '';
        void this.toasts.show(`Уже відмічено: ${name} — відмітив ${result.markedByName ?? 'хтось інший'}.`);
      }
    } catch (error) {
      this.patch(keys, (key) => before.get(key) ?? null);
      this.setBusy(keys, false);
      void this.toasts.show(apiErrorText(error, 'Не вдалося відмітити. Спробуй ще раз.', SCORE_ERRORS), 'danger');
    } finally {
      this.setBusy(keys, false);
    }
  }

  private async unmark(person: SheetPersonDto): Promise<void> {
    const occurrence = this.occurrence;
    if (!occurrence) return;
    const key = person.membershipKey;
    const before = person.attendance;
    this.setBusy([key], true);
    this.patch([key], () => null);
    try {
      await this.scores.unmark(this.kurinKey, this.itemKey, occurrence, key);
    } catch (error) {
      this.patch([key], () => before);
      this.setBusy([key], false);
      void this.toasts.show(apiErrorText(error, 'Не вдалося зняти відмітку. Спробуй ще раз.', SCORE_ERRORS), 'danger');
    } finally {
      this.setBusy([key], false);
    }
  }

  private async load(): Promise<void> {
    if (!this.kurinKey || !this.itemKey) {
      this.view.set({ state: 'failed' });
      return;
    }
    if (this.view().state !== 'ready') this.view.set({ state: 'loading' });
    try {
      this.occurrence ??= (await this.scores.agendaItem(this.itemKey)).startUtc;
      if (!this.occurrence) {
        this.view.set({ state: 'failed' });
        return;
      }
      const sheet = await this.scores.sheet(this.kurinKey, this.itemKey, this.occurrence);
      this.view.set({ state: 'ready', value: sheet });
      this.chooseShelf(sheet);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) this.view.set({ state: 'forbidden' });
      else if (this.view().state !== 'ready') this.view.set({ state: 'failed' });
    }
  }

  /** On the first read: the first shelf that has anyone on it. */
  private chooseShelf(sheet: AttendanceSheetDto): void {
    if (this.shelfChosen) return;
    this.shelfChosen = true;
    const mine = sheet.people.filter((p) => p.canScore);
    const first = SHELVES.find((s) => mine.some((p) => shelfOf(p) === s.value));
    if (first) this.shelf.set(first.value);
  }

  private patch(keys: string[], attendance: (key: string) => AttendanceMarkDto | null): void {
    const d = this.value();
    if (!d) return;
    const set = new Set(keys);
    this.view.set({
      state: 'ready',
      value: {
        ...d,
        people: d.people.map((p) => (set.has(p.membershipKey) ? { ...p, attendance: attendance(p.membershipKey) } : p)),
      },
    });
  }

  private setBusy(keys: string[], busy: boolean): void {
    const next = new Set(this.busy());
    for (const key of keys) {
      if (busy) next.add(key);
      else next.delete(key);
    }
    this.busy.set(next);
  }
}

