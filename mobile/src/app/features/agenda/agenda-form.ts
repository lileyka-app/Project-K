import { Component, ElementRef, OnDestroy, afterRenderEffect, computed, effect, inject, input, signal, untracked } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonCheckbox,
  IonContent,
  IonDatetime,
  IonDatetimeButton,
  IonHeader,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonModal,
  IonNote,
  IonRadio,
  IonRadioGroup,
  IonSearchbar,
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonSkeletonText,
  IonTextarea,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { GlassEffects } from '../../ui/glass';
import {
  AgendaAssignTargets,
  AgendaCategoryDto,
  AgendaCompletionMode,
  AgendaItemDto,
  AgendaItemKind,
  AgendaTargetInput,
  AgendaTargetType,
  CreateAgendaItemRequest,
  RecurrenceFrequency,
} from './agenda.models';
import {
  COMPLETION_MODES,
  FREQUENCIES,
  WEEKDAY_BITS,
  addMinutes,
  dayKey,
  defaultStart,
  toLocalValue,
  toWire,
  utcMidnight,
} from './agenda.labels';
import { AgendaService } from './agenda.service';

/** One row of «Для кого»: a target the API lets this person address. */
export interface TargetRow {
  id: string;
  targetType: AgendaTargetType;
  targetKey: string;
  label: string;
}

export interface TargetSection {
  title: string;
  rows: TargetRow[];
}

/**
 * The web's agenda-assign-select tree, flattened for a phone list: the kurin and its offices, then
 * each гурток with itself, its провід and its people. The API sends only what this person may
 * address; a heading with nothing to pick under it is left out.
 */
export function targetSections(tree: AgendaAssignTargets): TargetSection[] {
  const sections: TargetSection[] = [];
  const kurin: TargetRow[] = [];
  if (tree.canTargetKurin) kurin.push(row('Kurin', tree.kurinKey, 'Увесь курінь'));
  for (const office of tree.kurinLeaderships) {
    if (office.canTarget) kurin.push(row('Leadership', office.leadershipKey, office.label));
  }
  if (kurin.length) sections.push({ title: tree.kurinLabel, rows: kurin });
  for (const group of tree.groups) {
    const rows: TargetRow[] = [];
    if (group.canTargetGroup) rows.push(row('Group', group.groupKey, 'Увесь гурток'));
    if (group.leadership?.canTarget) rows.push(row('Leadership', group.leadership.leadershipKey, group.leadership.label));
    for (const member of group.members) {
      if (member.canTarget) rows.push(row('Member', member.memberKey, member.fullName));
    }
    if (rows.length) sections.push({ title: group.name, rows });
  }
  return sections;
}

/** What a chosen target is called in the form's summary («Соколи», not «Увесь гурток»). */
export function targetNames(tree: AgendaAssignTargets): Map<string, string> {
  const names = new Map<string, string>();
  names.set(targetId('Kurin', tree.kurinKey), tree.kurinLabel);
  for (const office of tree.kurinLeaderships) names.set(targetId('Leadership', office.leadershipKey), office.label);
  for (const group of tree.groups) {
    names.set(targetId('Group', group.groupKey), group.name);
    if (group.leadership) names.set(targetId('Leadership', group.leadership.leadershipKey), group.leadership.label);
    for (const member of group.members) names.set(targetId('Member', member.memberKey), member.fullName);
  }
  return names;
}

/** Sections narrowed by a search: a matching heading keeps all its rows. */
export function filterSections(sections: TargetSection[], query: string): TargetSection[] {
  const needle = query.trim().toLocaleLowerCase('uk');
  if (!needle) return sections;
  return sections
    .map((section) =>
      section.title.toLocaleLowerCase('uk').includes(needle)
        ? section
        : { ...section, rows: section.rows.filter((r) => r.label.toLocaleLowerCase('uk').includes(needle)) },
    )
    .filter((section) => section.rows.length);
}

export function targetId(type: AgendaTargetType, key: string): string {
  return `${type}:${key}`;
}

function row(targetType: AgendaTargetType, targetKey: string, label: string): TargetRow {
  return { id: targetId(targetType, targetKey), targetType, targetKey, label };
}

let formCount = 0;

const ERRORS: Record<string, string> = {
  AGENDA_MODE_LOCKED: 'Хтось уже почав — спосіб виконання не змінити.',
  AGENDA_TARGET_FORBIDDEN: 'Цю ціль тобі не можна призначати.',
  AGENDA_AUTHOR_FORBIDDEN: 'Курінь не дає тобі призначати події й задачі.',
  AGENDA_EDIT_FORBIDDEN: 'Це може змінити лише автор або провід.',
};

/**
 * Create or edit an event or a task: the web's agenda-item-dialog form. The page around it holds the
 * «Скасувати» and «Зберегти» buttons and calls `save()`. Dates go to the API as the web sends them:
 * all-day items as UTC midnight of the day, timed ones as their instant.
 */
@Component({
  selector: 'app-agenda-form',
  imports: [
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonInput,
    IonTextarea,
    IonToggle,
    IonSelect,
    IonSelectOption,
    IonSegment,
    IonSegmentButton,
    IonDatetime,
    IonDatetimeButton,
    IonModal,
    IonRadioGroup,
    IonRadio,
    IonButton,
    IonButtons,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonSearchbar,
    IonCheckbox,
    IonSkeletonText,
  ],
  styles: `
    .kind {
      padding: 8px 16px 0;
    }
    ion-item > .lk-field {
      width: 100%;
      padding: 10px 0;
    }
    .weekdays {
      display: flex;
      justify-content: space-between;
      width: 100%;
      padding: 8px 0;
      gap: 4px;
    }
    .weekdays button {
      flex: 1;
      min-height: 44px;
      max-width: 48px;
      border-radius: 22px;
      border: 1px solid var(--lk-line);
      background: var(--lk-paper);
      color: var(--lk-ink);
      font: inherit;
      font-size: 14px;
      font-weight: 600;
    }
    .weekdays button[aria-pressed='true'] {
      background: var(--lk-primary);
      border-color: var(--lk-primary);
      color: var(--lk-on-primary);
    }
    .hint {
      margin: 6px 20px 0;
      font-size: 13px;
      color: var(--lk-muted);
    }
    :host-context(.ios) .hint {
      margin-inline: 32px;
    }
    .error {
      color: var(--lk-danger);
    }
    .summary {
      color: var(--lk-muted);
    }
    .mode ion-radio {
      width: 100%;
    }
    .mode-hint {
      display: block;
      white-space: normal;
      font-size: 13px;
      color: var(--lk-muted);
      font-weight: 400;
    }
    ion-datetime-button {
      margin-inline-start: auto;
    }
  `,
  template: `
    @if (loading()) {
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
          <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
          <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
        </ion-item-group>
      </ion-list>
    } @else if (loadFailed()) {
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item><ion-label class="ion-text-wrap">Не вдалося завантажити. Повернись і спробуй ще раз.</ion-label></ion-item>
        </ion-item-group>
      </ion-list>
    } @else {
      <div class="kind">
        <ion-segment [value]="kind()" (ionChange)="setKind($event)" aria-label="Тип">
          <ion-segment-button value="Event"><ion-label>Подія</ion-label></ion-segment-button>
          <ion-segment-button value="Task"><ion-label>Задача</ion-label></ion-segment-button>
        </ion-segment>
      </div>

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Назва</span>
              <div class="lk-input-box" [class.lk-invalid]="tried() && !title().trim()">
                <ion-input
                  data-testid="title"
                  aria-label="Назва"
                  placeholder="Про що це?"
                  [maxlength]="200"
                  [value]="title()"
                  (ionInput)="title.set(text($event))"
                />
              </div>
            </div>
          </ion-item>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Опис</span>
              <div class="lk-input-box">
                <ion-textarea
                  data-testid="description"
                  aria-label="Опис"
                  placeholder="Деталі (необовʼязково)"
                  [autoGrow]="true"
                  [rows]="3"
                  [maxlength]="2000"
                  [value]="description()"
                  (ionInput)="description.set(text($event))"
                  style="--padding-start: 12px; --padding-end: 12px"
                />
              </div>
            </div>
          </ion-item>
          @if (kind() === 'Event') {
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">Де</span>
                <div class="lk-input-box">
                  <ion-input
                    data-testid="location"
                    aria-label="Де"
                    placeholder="Домівка, мала кімната"
                    [maxlength]="200"
                    [value]="location()"
                    (ionInput)="location.set(text($event))"
                  />
                </div>
              </div>
            </ion-item>
            @if (categories().length) {
              <ion-item>
                <ion-select
                  label="Група подій"
                  interface="action-sheet"
                  cancelText="Скасувати"
                  placeholder="Без групи"
                  [value]="categoryKey() ?? ''"
                  (ionChange)="setCategory($event)"
                  data-testid="category"
                >
                  <ion-select-option value="">Без групи</ion-select-option>
                  @for (category of categories(); track category.agendaCategoryKey) {
                    <ion-select-option [value]="category.agendaCategoryKey">{{ category.name }}</ion-select-option>
                  }
                </ion-select>
              </ion-item>
            }
          }
        </ion-item-group>
      </ion-list>

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-toggle [checked]="allDay()" (ionChange)="setAllDay($event)" justify="space-between" data-testid="all-day">
              Весь день
            </ion-toggle>
          </ion-item>
          <ion-item>
            <ion-label>Початок</ion-label>
            <ion-datetime-button [datetime]="id + '-start'" data-testid="start" />
          </ion-item>
          <ion-item>
            <ion-label>Кінець</ion-label>
            @if (end()) {
              <ion-datetime-button [datetime]="id + '-end'" data-testid="end" />
              <ion-button fill="clear" size="default" slot="end" (click)="end.set(null)" aria-label="Без кінця">✕</ion-button>
            } @else {
              <ion-button fill="clear" size="default" slot="end" (click)="addEnd()" data-testid="add-end">Додати</ion-button>
            }
          </ion-item>
          <ion-item>
            <ion-select
              label="Повторення"
              interface="action-sheet"
              cancelText="Скасувати"
              [value]="frequency()"
              (ionChange)="setFrequency($event)"
              data-testid="frequency"
            >
              @for (option of frequencies; track option.value) {
                <ion-select-option [value]="option.value">{{ option.label }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          @if (frequency() !== 'None') {
            <ion-item>
              <ion-input
                label="Кожні"
                type="number"
                inputmode="numeric"
                [min]="1"
                [value]="interval()"
                (ionInput)="setInterval($event)"
                data-testid="interval"
                style="text-align: end"
              />
              <ion-note slot="end">{{ unit() }}</ion-note>
            </ion-item>
            @if (frequency() === 'Weekly') {
              <ion-item>
                <div class="weekdays" role="group" aria-label="Дні тижня">
                  @for (day of weekdays; track day.bit) {
                    <button type="button" [attr.aria-pressed]="hasWeekday(day.bit)" (click)="toggleWeekday(day.bit)">
                      {{ day.label }}
                    </button>
                  }
                </div>
              </ion-item>
            }
            <ion-item>
              <ion-label>Повторювати до</ion-label>
              @if (until()) {
                <ion-datetime-button [datetime]="id + '-until'" />
                <ion-button fill="clear" size="default" slot="end" (click)="until.set(null)" aria-label="Без кінця">✕</ion-button>
              } @else {
                <ion-button fill="clear" size="default" slot="end" (click)="addUntil()" data-testid="add-until">Без кінця</ion-button>
              }
            </ion-item>
          }
        </ion-item-group>
      </ion-list>
      @if (dateError(); as message) { <p class="hint error" role="alert">{{ message }}</p> }
      @if (series()) { <p class="hint">Зміни стосуються всієї серії.</p> }

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item [button]="true" [detail]="true" (click)="openPicker()" data-testid="targets">
            <ion-label class="ion-text-wrap">
              <h3>Для кого</h3>
              <p [class.error]="tried() && !targets().length">{{ targetsSummary() || 'Обери курінь, гуртки або людей' }}</p>
            </ion-label>
          </ion-item>
        </ion-item-group>
      </ion-list>

      @if (hasSharedTargets()) {
        <ion-list [inset]="true" class="mode">
          <ion-list-header><ion-label>Як виконується</ion-label></ion-list-header>
          <ion-item-group>
            <ion-radio-group [value]="completionMode()" (ionChange)="setMode($event)">
              @for (mode of completionModes; track mode.value) {
                <ion-item>
                  <ion-radio [value]="mode.value" [disabled]="modeLocked()" justify="space-between">
                    <span class="ion-text-wrap">{{ mode.label }}</span>
                    <span class="mode-hint">{{ mode.hint }}</span>
                  </ion-radio>
                </ion-item>
              }
            </ion-radio-group>
          </ion-item-group>
        </ion-list>
        @if (modeLocked()) {
          <p class="hint">Хтось уже почав — спосіб виконання не змінити. Потрібен інший — створи нову задачу.</p>
        }
      }

      <!-- The pickers live in modals; the buttons above show and open them. -->
      <ion-modal [keepContentsMounted]="true">
        <ng-template>
          <ion-datetime
            [id]="id + '-start'"
            [presentation]="allDay() ? 'date' : 'date-time'"
            locale="uk-UA"
            [firstDayOfWeek]="1"
            hourCycle="h23"
            [value]="start()"
            (ionChange)="setStart($event)"
          />
        </ng-template>
      </ion-modal>
      <ion-modal [keepContentsMounted]="true">
        <ng-template>
          <ion-datetime
            [id]="id + '-end'"
            [presentation]="allDay() ? 'date' : 'date-time'"
            locale="uk-UA"
            [firstDayOfWeek]="1"
            hourCycle="h23"
            [value]="end()"
            (ionChange)="end.set(picked($event) ?? end())"
          />
        </ng-template>
      </ion-modal>
      <ion-modal [keepContentsMounted]="true">
        <ng-template>
          <ion-datetime
            [id]="id + '-until'"
            presentation="date"
            locale="uk-UA"
            [firstDayOfWeek]="1"
            [value]="until()"
            (ionChange)="until.set(picked($event) ?? until())"
          />
        </ng-template>
      </ion-modal>

      <ion-modal [isOpen]="picking()" (didDismiss)="picking.set(false)">
        <ng-template>
          <ion-header>
            <ion-toolbar>
              <ion-buttons slot="start"><ion-button (click)="picking.set(false)">Скасувати</ion-button></ion-buttons>
              <ion-title>Для кого</ion-title>
              <ion-buttons slot="end">
                <ion-button [strong]="true" (click)="applyPicker()" data-testid="targets-done">Готово</ion-button>
              </ion-buttons>
            </ion-toolbar>
            <ion-toolbar>
              <ion-searchbar placeholder="Пошук" [value]="query()" (ionInput)="query.set(text($event))" />
            </ion-toolbar>
          </ion-header>
          <ion-content>
            @switch (tree().state) {
              @case ('loading') {
                <ion-list [inset]="true">
                  <ion-item-group><ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item></ion-item-group>
                </ion-list>
              }
              @case ('failed') {
                <p class="hint">Не вдалося завантажити цілі. Спробуй ще раз.</p>
              }
              @default {
                @for (section of shownSections(); track section.title) {
                  <ion-list [inset]="true">
                    <ion-list-header><ion-label>{{ section.title }}</ion-label></ion-list-header>
                    <ion-item-group>
                      @for (target of section.rows; track target.id) {
                        <ion-item>
                          <ion-checkbox
                            justify="space-between"
                            [checked]="draft().has(target.id)"
                            (ionChange)="toggleDraft(target.id)"
                          >{{ target.label }}</ion-checkbox>
                        </ion-item>
                      }
                    </ion-item-group>
                  </ion-list>
                } @empty {
                  <p class="hint">{{ query() ? 'Нічого не знайдено.' : 'Немає доступних цілей.' }}</p>
                }
              }
            }
          </ion-content>
        </ng-template>
      </ion-modal>
    }
  `,
})
export class AgendaForm implements OnDestroy {
  private readonly agenda = inject(AgendaService);
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(Toasts);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  /** Event from the calendar, Task from the board. */
  readonly defaultKind = input<AgendaItemKind>('Event');
  /** Set to edit an item; null creates one. */
  readonly itemKey = input<string | null>(null);
  /** A fresh item starts on this day («2026-10-07»), else today. */
  readonly day = input<string | null>(null);

  /** Ids for the date pickers; two forms can be open at once in two tabs. */
  protected readonly id = `agenda-form-${++formCount}`;
  private readonly kurinKey = this.auth.user()?.kurinKey ?? null;

  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);
  private readonly editing = signal<AgendaItemDto | null>(null);
  readonly saving = signal(false);
  protected readonly tried = signal(false);

  protected readonly kind = signal<AgendaItemKind>('Event');
  protected readonly title = signal('');
  protected readonly description = signal('');
  protected readonly location = signal('');
  protected readonly categoryKey = signal<string | null>(null);
  protected readonly allDay = signal(true);
  protected readonly start = signal<string | null>(null);
  protected readonly end = signal<string | null>(null);
  protected readonly frequency = signal<RecurrenceFrequency>('None');
  protected readonly interval = signal(1);
  protected readonly weekdayMask = signal(0);
  protected readonly until = signal<string | null>(null);
  protected readonly targets = signal<AgendaTargetInput[]>([]);
  protected readonly completionMode = signal<AgendaCompletionMode>('Shared');

  protected readonly categories = signal<AgendaCategoryDto[]>([]);
  protected readonly tree = signal<{ state: 'loading' } | { state: 'ready'; value: AgendaAssignTargets } | { state: 'failed' }>({
    state: 'loading',
  });
  protected readonly picking = signal(false);
  protected readonly query = signal('');
  protected readonly draft = signal<Set<string>>(new Set());

  protected readonly frequencies = FREQUENCIES;
  protected readonly weekdays = WEEKDAY_BITS;
  protected readonly completionModes = COMPLETION_MODES;

  private readonly sections = computed(() => {
    const tree = this.tree();
    return tree.state === 'ready' ? targetSections(tree.value) : [];
  });
  protected readonly shownSections = computed(() => filterSections(this.sections(), this.query()));
  private readonly names = computed(() => {
    const tree = this.tree();
    const names = tree.state === 'ready' ? targetNames(tree.value) : new Map<string, string>();
    for (const a of this.editing()?.assignments ?? []) {
      if (a.label && !names.has(targetId(a.targetType, a.targetKey))) names.set(targetId(a.targetType, a.targetKey), a.label);
    }
    return names;
  });
  protected readonly targetsSummary = computed(() =>
    this.targets()
      .map((t) => this.names().get(targetId(t.targetType, t.targetKey)))
      .filter(Boolean)
      .join(', '),
  );

  protected readonly unit = computed(() => {
    const plural = this.interval() > 1;
    switch (this.frequency()) {
      case 'Weekly':
        return plural ? 'тижні' : 'тиждень';
      case 'Monthly':
        return plural ? 'місяці' : 'місяць';
      default:
        return plural ? 'роки' : 'рік';
    }
  });
  protected readonly series = computed(() => !!this.editing() && this.editing()!.recurrenceFrequency !== 'None');

  /** The choice only exists for a task aimed at a гурток, the kurin or a провід. */
  protected readonly hasSharedTargets = computed(
    () => this.kind() === 'Task' && this.targets().some((t) => t.targetType !== 'Member'),
  );
  /** Once anyone has moved a target, its way of being done stays — the server would refuse it. */
  protected readonly modeLocked = computed(() =>
    (this.editing()?.assignments ?? []).some(
      (a) => a.targetType !== 'Member' && (a.status !== 'Todo' || !!a.statusChangedAtUtc),
    ),
  );

  protected readonly dateError = computed(() => {
    const start = this.start();
    const end = this.end();
    if (start && end && toWire(end, this.allDay())! < toWire(start, this.allDay())!) return 'Кінець не може бути раніше за початок.';
    const until = this.until();
    if (this.frequency() !== 'None' && start && until && until.slice(0, 10) < start.slice(0, 10)) {
      return 'Повторення має закінчитися не раніше за початок.';
    }
    return null;
  });

  readonly canSave = computed(
    () => !this.loading() && !this.loadFailed() && !this.saving() && !!this.title().trim() && this.targets().length > 0 && !this.dateError(),
  );

  constructor() {
    effect(() => {
      const key = this.itemKey();
      const kind = this.defaultKind();
      const day = this.day();
      untracked(() => void this.init(key, kind, day));
    });
    afterRenderEffect(() => {
      this.loading();
      this.glass.sync();
    });
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  private async init(key: string | null, kind: AgendaItemKind, day: string | null): Promise<void> {
    if (!this.kurinKey) return;
    void this.loadReferences();
    if (!key) {
      this.kind.set(kind);
      this.allDay.set(true);
      this.start.set(`${day ?? dayKey(new Date())}T00:00`);
      return;
    }
    this.loading.set(true);
    try {
      const item = await this.agenda.item(key);
      this.editing.set(item);
      this.fill(item);
    } catch {
      this.loadFailed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadReferences(): Promise<void> {
    const kurinKey = this.kurinKey!;
    this.agenda.categories(kurinKey).then(
      (list) => this.categories.set(list.filter((c) => !c.isArchived)),
      () => undefined,
    );
    try {
      this.tree.set({ state: 'ready', value: await this.agenda.assignTargets(kurinKey) });
    } catch {
      this.tree.set({ state: 'failed' });
    }
  }

  private fill(item: AgendaItemDto): void {
    this.kind.set(item.kind);
    this.title.set(item.title);
    this.description.set(item.description ?? '');
    this.location.set(item.location ?? '');
    this.categoryKey.set(item.categoryKey);
    this.allDay.set(item.isAllDay);
    // An occurrence row carries its own start; the series is edited from its first one.
    this.start.set(toLocalValue(item.seriesStartUtc ?? item.startUtc, item.isAllDay));
    this.end.set(toLocalValue(item.seriesEndUtc ?? item.endUtc, item.isAllDay));
    this.frequency.set(item.recurrenceFrequency ?? 'None');
    this.interval.set(item.recurrenceInterval || 1);
    this.weekdayMask.set(item.recurrenceByWeekday ?? 0);
    this.until.set(item.recurrenceEndUtc ? `${item.recurrenceEndUtc.slice(0, 10)}T00:00` : null);
    this.targets.set(item.assignments.map((a) => ({ targetType: a.targetType, targetKey: a.targetKey })));
    this.completionMode.set(item.assignments.find((a) => a.targetType !== 'Member')?.completionMode ?? 'Shared');
  }

  protected text(event: Event): string {
    return String((event as CustomEvent<{ value?: string | null }>).detail.value ?? '');
  }

  /** The picker's value as «2026-10-07T17:00» (a date alone becomes that day's midnight). */
  protected picked(event: Event): string | null {
    const value = (event as CustomEvent<{ value?: string | string[] | null }>).detail.value;
    if (!value || Array.isArray(value)) return null;
    return value.length === 10 ? `${value}T00:00` : value.slice(0, 16);
  }

  protected setKind(event: Event): void {
    const kind = (event as CustomEvent<{ value?: string }>).detail.value as AgendaItemKind | undefined;
    if (kind) this.kind.set(kind);
  }

  /** On a fresh event a group lends its template description and default length. */
  protected setCategory(event: Event): void {
    const key = String((event as CustomEvent<{ value?: string }>).detail.value ?? '') || null;
    this.categoryKey.set(key);
    const category = this.categories().find((c) => c.agendaCategoryKey === key);
    if (!category || this.editing()) return;
    if (!this.description().trim() && category.defaultDescription) this.description.set(category.defaultDescription);
    const start = this.start();
    if (category.defaultDurationMinutes && start && !this.end()) {
      this.end.set(addMinutes(start, category.defaultDurationMinutes));
    }
  }

  /** Switching to hours puts a whole-day value at a usable time instead of midnight. */
  protected setAllDay(event: Event): void {
    const allDay = (event as CustomEvent<{ checked: boolean }>).detail.checked;
    this.allDay.set(allDay);
    const start = this.start();
    if (!allDay && start?.endsWith('T00:00')) {
      this.start.set(`${start.slice(0, 10)}T10:00`);
      const end = this.end();
      if (end?.endsWith('T00:00')) this.end.set(`${end.slice(0, 10)}T11:00`);
    }
  }

  protected setStart(event: Event): void {
    const value = this.picked(event);
    if (!value) return;
    const before = this.start();
    this.start.set(value);
    // The end follows the start, keeping the length, as calendars do.
    const end = this.end();
    if (before && end) {
      const length = new Date(end).getTime() - new Date(before).getTime();
      if (length >= 0) this.end.set(addMinutes(value, Math.round(length / 60_000)));
    }
  }

  protected addEnd(): void {
    const start = this.start() ?? defaultStart(this.day());
    this.end.set(this.allDay() ? start : addMinutes(start, 60));
  }

  protected addUntil(): void {
    const start = this.start() ?? `${dayKey(new Date())}T00:00`;
    const base = new Date(`${start.slice(0, 10)}T00:00`);
    this.until.set(`${dayKey(new Date(base.getFullYear(), base.getMonth() + 3, base.getDate()))}T00:00`);
  }

  protected setFrequency(event: Event): void {
    const value = (event as CustomEvent<{ value?: string }>).detail.value as RecurrenceFrequency | undefined;
    if (value) this.frequency.set(value);
  }

  protected setInterval(event: Event): void {
    const value = Number(this.text(event));
    this.interval.set(Number.isFinite(value) && value >= 1 ? Math.floor(value) : 1);
  }

  protected hasWeekday(bit: number): boolean {
    return (this.weekdayMask() & bit) !== 0;
  }

  protected toggleWeekday(bit: number): void {
    this.weekdayMask.update((mask) => mask ^ bit);
  }

  protected setMode(event: Event): void {
    const mode = (event as CustomEvent<{ value?: string }>).detail.value as AgendaCompletionMode | undefined;
    if (mode) this.completionMode.set(mode);
  }

  protected openPicker(): void {
    this.draft.set(new Set(this.targets().map((t) => targetId(t.targetType, t.targetKey))));
    this.query.set('');
    this.picking.set(true);
    if (this.tree().state === 'failed') void this.loadReferences();
  }

  protected toggleDraft(id: string): void {
    this.draft.update((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected applyPicker(): void {
    const chosen = this.draft();
    this.targets.set(
      [...chosen].map((id) => {
        const [targetType, targetKey] = id.split(':') as [AgendaTargetType, string];
        return { targetType, targetKey };
      }),
    );
    this.picking.set(false);
  }

  /** Creates or rewrites the item; true when the server took it. */
  async save(): Promise<boolean> {
    this.tried.set(true);
    if (!this.canSave() || !this.kurinKey) {
      if (!this.title().trim() || !this.targets().length) await this.toasts.show('Заповни назву та обери, для кого це.');
      return false;
    }
    this.saving.set(true);
    const kind = this.kind();
    const allDay = this.allDay();
    const frequency = this.frequency();
    const payload: CreateAgendaItemRequest = {
      kurinKey: this.kurinKey,
      kind,
      title: this.title().trim(),
      description: this.description().trim() || null,
      location: kind === 'Event' ? this.location().trim() || null : null,
      startUtc: toWire(this.start(), allDay),
      endUtc: toWire(this.end(), allDay),
      isAllDay: allDay,
      agendaCategoryKey: kind === 'Event' ? this.categoryKey() : null,
      recurrenceFrequency: frequency,
      recurrenceInterval: Math.max(1, this.interval() || 1),
      recurrenceByWeekday: frequency === 'Weekly' ? this.weekdayMask() : 0,
      recurrenceEndUtc: frequency !== 'None' && this.until() ? utcMidnight(this.until()!) : null,
      recurrenceCount: null,
      targets: this.targets().map((t) => ({
        ...t,
        completionMode: kind === 'Task' && t.targetType !== 'Member' ? this.completionMode() : 'Shared',
      })),
    };
    const editing = this.editing();
    try {
      if (editing) await this.agenda.update({ ...payload, agendaItemKey: editing.agendaItemKey });
      else await this.agenda.create(payload);
      await this.toasts.show(editing ? 'Оновлено' : 'Створено');
      return true;
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', ERRORS), 'danger');
      return false;
    } finally {
      this.saving.set(false);
    }
  }
}
