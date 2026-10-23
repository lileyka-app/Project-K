import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonDatetime,
  IonHeader,
  IonIcon,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonModal,
  IonSelect,
  IonSelectOption,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
  NavController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { add, closeCircle, remove } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { ForbiddenState } from '../account/forbidden';
import { MONTHS_OF, MONTHS_SHORT, addDays, dayKey, fromDayKey } from '../agenda/agenda.labels';
import { MemberLookupDto } from '../kurin/kurin.models';
import { KurinApi } from '../kurin/kurin.service';
import { CreatePlanningRequest, DEFAULT_ROLE_WEIGHT, ROLE_WEIGHTS } from './planning.models';
import {
  busyToWire,
  canCreatePlanning,
  dayToWire,
  daysLabel,
  defaultWindow,
  windowError,
} from './planning.labels';
import { PlanningService } from './planning.service';

/** A busy range being typed: both days are included («10–15 червня» is six days). */
interface RangeDraft {
  id: number;
  from: string;
  to: string;
}

interface ParticipantDraft {
  memberKey: string;
  fullName: string;
  roleWeight: number;
  ranges: RangeDraft[];
}

/** Which date the picker is changing. */
type Picking =
  { field: 'from' | 'to' } | { field: 'busy-from' | 'busy-to'; memberKey: string; rangeId: number };

const MIN_DAYS = 1;
const MAX_DAYS = 30;

/**
 * «Нове планування» (the web's create-planning): a name, the camp's length, the window to search and
 * the kurin's КВ with each person's vote weight and the days they are busy. The server saves it and
 * works out the dates at once. Open to those the web lets (`canCreatePlanning`).
 */
@Component({
  selector: 'app-planning-form',
  imports: [
    ForbiddenState,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonInput,
    IonIcon,
    IonSelect,
    IonSelectOption,
    IonModal,
    IonDatetime,
    IonSkeletonText,
    IonSpinner,
  ],
  styles: `
    ion-item > .lk-field {
      width: 100%;
      padding: 10px 0;
    }
    .date {
      flex: none;
      min-height: 34px;
      padding: 6px 12px;
      border: 0;
      border-radius: 8px;
      background: var(--lk-secondary-bg);
      color: var(--lk-ink);
      font: inherit;
      font-size: 15px;
      font-weight: 500;
      font-variant-numeric: tabular-nums;
    }
    .date.invalid {
      color: var(--lk-danger);
    }
    .range {
      display: flex;
      align-items: center;
      gap: 8px;
      min-width: 0;
      padding: 8px 0;
      color: var(--lk-muted);
    }
    .stepper {
      display: flex;
      align-items: center;
      gap: 2px;
    }
    .stepper strong {
      min-width: 64px;
      text-align: center;
      font-weight: 600;
      color: var(--lk-ink);
      font-variant-numeric: tabular-nums;
    }
    .stepper ion-button,
    .drop {
      --padding-start: 10px;
      --padding-end: 10px;
      min-height: 44px;
      margin: 0;
    }
    .drop {
      margin-inline-end: -8px;
      --color: var(--lk-muted);
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
    .add {
      font-weight: 500;
    }
    .section {
      margin: 24px 20px 0;
      font-size: 13px;
      font-weight: 400;
      color: var(--lk-muted);
    }
    :host-context(.ios) .section {
      margin-inline: 36px;
      font-size: 16px;
      font-weight: 600;
      color: var(--ion-color-medium-tint);
    }
    :host-context(.md) .section {
      margin-inline: 33px;
      color: var(--lk-faint);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.18em;
      text-transform: uppercase;
    }
    .gap {
      height: 16px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-button (click)="cancel()">Скасувати</ion-button></ion-buttons>
        <ion-title>Новий табір</ion-title>
        @if (canCreate) {
          <ion-buttons slot="end">
            <ion-button
              [strong]="true"
              [disabled]="saving() || members().state !== 'ready'"
              (click)="save()"
              data-testid="save"
            >
              @if (saving()) {
                <ion-spinner name="crescent" />
              } @else {
                Створити
              }
            </ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      @if (!canCreate) {
        <app-forbidden message="Нове планування відкриває провід куреня." />
      } @else {
        <ion-list [inset]="true">
          <ion-list-header><ion-label>Основні налаштування</ion-label></ion-list-header>
          <ion-item-group>
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">Назва сесії</span>
                <div class="lk-input-box" [class.lk-invalid]="tried() && !name().trim()">
                  <ion-input
                    data-testid="name"
                    aria-label="Назва сесії"
                    placeholder="Напр. Літо 2026"
                    [maxlength]="200"
                    [value]="name()"
                    (ionInput)="name.set(text($event))"
                  />
                </div>
              </div>
            </ion-item>
            <ion-item>
              <ion-label>Тривалість</ion-label>
              <div class="stepper" slot="end">
                <ion-button
                  fill="clear"
                  size="default"
                  [disabled]="duration() <= minDays"
                  (click)="step(-1)"
                  aria-label="Менше днів"
                >
                  <ion-icon slot="icon-only" name="remove" />
                </ion-button>
                <strong data-testid="duration">{{ durationText() }}</strong>
                <ion-button
                  fill="clear"
                  size="default"
                  [disabled]="duration() >= maxDays"
                  (click)="step(1)"
                  aria-label="Більше днів"
                >
                  <ion-icon slot="icon-only" name="add" />
                </ion-button>
              </div>
            </ion-item>
          </ion-item-group>
        </ion-list>

        <ion-list [inset]="true">
          <ion-list-header><ion-label>Вікно пошуку</ion-label></ion-list-header>
          <ion-item-group>
            <ion-item>
              <ion-label>Від</ion-label>
              <button
                type="button"
                class="date"
                slot="end"
                (click)="pick({ field: 'from' }, from())"
                [attr.aria-label]="'Початок вікна: ' + longDay(from())"
                data-testid="from"
              >
                {{ longDay(from()) }}
              </button>
            </ion-item>
            <ion-item>
              <ion-label>До</ion-label>
              <button
                type="button"
                class="date"
                slot="end"
                [class.invalid]="!!windowProblem()"
                (click)="pick({ field: 'to' }, to())"
                [attr.aria-label]="'Кінець вікна: ' + longDay(to())"
                data-testid="to"
              >
                {{ longDay(to()) }}
              </button>
            </ion-item>
          </ion-item-group>
        </ion-list>
        @if (windowProblem(); as problem) {
          <p class="hint error" role="alert">{{ problem }}</p>
        } @else {
          <p class="hint">Табір шукатиметься в цих днях; обидва краї входять.</p>
        }

        <h2 class="section">Учасники та зайнятість</h2>
        @switch (members().state) {
          @case ('loading') {
            <ion-list [inset]="true">
              <ion-item-group>
                <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
                <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
              </ion-item-group>
            </ion-list>
          }
          @case ('failed') {
            <p class="hint">{{ failedText }}</p>
          }
          @default {
            @if (participants().length) {
              <p class="hint">Зазнач дні, коли хтось має інші плани: табір їх обминатиме, зважаючи на важливість голосу.</p>
              @for (p of participants(); track p.memberKey) {
                <ion-list [inset]="true" data-testid="participant">
                  <ion-list-header><ion-label>{{ p.fullName }}</ion-label></ion-list-header>
                  <ion-item-group>
                    <ion-item>
                      <ion-select
                        label="Важливість голосу"
                        interface="action-sheet"
                        cancelText="Скасувати"
                        [value]="p.roleWeight"
                        (ionChange)="setWeight(p.memberKey, $event)"
                        data-testid="weight"
                      >
                        @for (option of weights; track option.value) {
                          <ion-select-option [value]="option.value">{{ option.label }}</ion-select-option>
                        }
                      </ion-select>
                    </ion-item>
                    @for (range of p.ranges; track range.id) {
                      <ion-item data-testid="busy-range">
                        <div class="range">
                          <button
                            type="button"
                            class="date"
                            (click)="
                              pick(
                                { field: 'busy-from', memberKey: p.memberKey, rangeId: range.id },
                                range.from
                              )
                            "
                            [attr.aria-label]="'Зайнятий з ' + longDay(range.from)"
                          >
                            {{ shortDay(range.from) }}
                          </button>
                          <span aria-hidden="true">–</span>
                          <button
                            type="button"
                            class="date"
                            [class.invalid]="range.to < range.from"
                            (click)="
                              pick(
                                { field: 'busy-to', memberKey: p.memberKey, rangeId: range.id },
                                range.to
                              )
                            "
                            [attr.aria-label]="'Зайнятий до ' + longDay(range.to)"
                          >
                            {{ shortDay(range.to) }}
                          </button>
                        </div>
                        <ion-button
                          slot="end"
                          fill="clear"
                          size="default"
                          class="drop"
                          (click)="removeRange(p.memberKey, range.id)"
                          aria-label="Прибрати період"
                        >
                          <ion-icon slot="icon-only" name="close-circle" />
                        </ion-button>
                      </ion-item>
                    }
                    <ion-item
                      [button]="true"
                      [detail]="false"
                      (click)="addRange(p.memberKey)"
                      data-testid="add-range"
                    >
                      <ion-icon slot="start" name="add" color="primary" aria-hidden="true" />
                      <ion-label color="primary" class="add">Додати період</ion-label>
                    </ion-item>
                  </ion-item-group>
                </ion-list>
              }
              @if (rangeProblem()) {
                <p class="hint error" role="alert">
                  Кінець періоду не може бути раніше за його початок.
                </p>
              }
            } @else {
              <p class="hint">У КВ куреня поки нікого немає — табір шукатиметься лише у вікні.</p>
            }
          }
        }
        <p class="hint">Після створення Лілейка одразу розрахує найкращі дати.</p>
        <div class="gap"></div>

        <ion-modal
          class="ion-datetime-button-overlay"
          [isOpen]="!!picking()"
          (didDismiss)="picking.set(null)"
        >
          <ng-template>
            <ion-datetime
              presentation="date"
              locale="uk-UA"
              [firstDayOfWeek]="1"
              [value]="pickValue()"
              [showDefaultButtons]="true"
              doneText="Готово"
              cancelText="Скасувати"
              (ionChange)="picked($event)"
              (ionCancel)="picking.set(null)"
              data-testid="date-picker"
            />
          </ng-template>
        </ion-modal>
      }
    </ion-content>
  `,
})
export class PlanningFormPage implements OnInit {
  private readonly nav = inject(NavController);
  private readonly auth = inject(AuthService);
  private readonly kurin = inject(KurinApi);
  private readonly planning = inject(PlanningService);
  private readonly toasts = inject(Toasts);

  private readonly kurinKey = this.auth.user()?.kurinKey ?? null;
  protected readonly canCreate = canCreatePlanning(this.auth.user()) && !!this.kurinKey;
  protected readonly failedText = FAILED_TEXT;
  protected readonly weights = ROLE_WEIGHTS;
  protected readonly minDays = MIN_DAYS;
  protected readonly maxDays = MAX_DAYS;

  private readonly start = defaultWindow();
  protected readonly name = signal(this.start.name);
  protected readonly duration = signal(10);
  protected readonly from = signal(this.start.from);
  protected readonly to = signal(this.start.to);
  protected readonly members = signal<Loaded<MemberLookupDto[]>>({ state: 'loading' });
  protected readonly participants = signal<ParticipantDraft[]>([]);
  protected readonly picking = signal<Picking | null>(null);
  protected readonly pickValue = signal<string>(this.start.from);
  protected readonly tried = signal(false);
  protected readonly saving = signal(false);
  private nextRange = 1;

  protected readonly durationText = computed(() => daysLabel(this.duration()));
  protected readonly windowProblem = computed(() =>
    windowError(this.from(), this.to(), this.duration()),
  );
  protected readonly rangeProblem = computed(() =>
    this.participants().some((p) => p.ranges.some((r) => r.to < r.from)),
  );

  constructor() {
    addIcons({ add, remove, closeCircle });
  }

  ngOnInit(): void {
    if (this.canCreate) void this.loadMembers();
  }

  /** The kurin's КВ, everyone at the middle weight, as the web starts them. */
  private async loadMembers(): Promise<void> {
    try {
      const list = await this.kurin.kvMembers(this.kurinKey!);
      this.members.set({ state: 'ready', value: list });
      this.participants.set(
        list.map((m) => ({
          memberKey: m.memberKey,
          fullName: `${m.firstName} ${m.lastName}`,
          roleWeight: DEFAULT_ROLE_WEIGHT,
          ranges: [],
        })),
      );
    } catch {
      this.members.set({ state: 'failed' });
    }
  }

  protected text(event: Event): string {
    return String((event as CustomEvent<{ value?: string | null }>).detail.value ?? '');
  }

  /** «1 червня 2026» */
  protected longDay(key: string): string {
    const date = fromDayKey(key);
    return `${date.getDate()} ${MONTHS_OF[date.getMonth()]} ${date.getFullYear()}`;
  }

  /** «10 чер», with the year only when it is not the window's. */
  protected shortDay(key: string): string {
    const date = fromDayKey(key);
    const year =
      date.getFullYear() === fromDayKey(this.from()).getFullYear() ? '' : ` ${date.getFullYear()}`;
    return `${date.getDate()} ${MONTHS_SHORT[date.getMonth()]}${year}`;
  }

  protected step(by: number): void {
    this.duration.update((days) => Math.min(MAX_DAYS, Math.max(MIN_DAYS, days + by)));
  }

  protected setWeight(memberKey: string, event: Event): void {
    const value = Number((event as CustomEvent<{ value?: number }>).detail.value);
    if (Number.isFinite(value))
      this.updateParticipant(memberKey, (p) => ({ ...p, roleWeight: value }));
  }

  /** A new range starts after the last one (or at the window's start) and runs a week. */
  protected addRange(memberKey: string): void {
    this.updateParticipant(memberKey, (p) => {
      const last = p.ranges.at(-1);
      const first = last ? addDays(fromDayKey(last.to), 1) : fromDayKey(this.from());
      return {
        ...p,
        ranges: [
          ...p.ranges,
          { id: this.nextRange++, from: dayKey(first), to: dayKey(addDays(first, 6)) },
        ],
      };
    });
  }

  protected removeRange(memberKey: string, rangeId: number): void {
    this.updateParticipant(memberKey, (p) => ({
      ...p,
      ranges: p.ranges.filter((r) => r.id !== rangeId),
    }));
  }

  protected pick(target: Picking, value: string): void {
    this.pickValue.set(value);
    this.picking.set(target);
  }

  /** The picker's «Готово»: the day lands in the field it was opened for. */
  protected picked(event: Event): void {
    const value = (event as CustomEvent<{ value?: string | string[] | null }>).detail.value;
    const target = this.picking();
    this.picking.set(null);
    if (!target || !value || Array.isArray(value)) return;
    const day = value.slice(0, 10);
    switch (target.field) {
      case 'from':
        this.from.set(day);
        break;
      case 'to':
        this.to.set(day);
        break;
      default: {
        const key = target.field === 'busy-from' ? 'from' : 'to';
        this.updateParticipant(target.memberKey, (p) => ({
          ...p,
          ranges: p.ranges.map((r) => {
            if (r.id !== target.rangeId) return r;
            const next = { ...r, [key]: day };
            // Moving the start past the end takes the end along, as calendars do.
            if (key === 'from' && next.to < day) next.to = day;
            return next;
          }),
        }));
      }
    }
  }

  private updateParticipant(
    memberKey: string,
    change: (p: ParticipantDraft) => ParticipantDraft,
  ): void {
    this.participants.update((list) =>
      list.map((p) => (p.memberKey === memberKey ? change(p) : p)),
    );
  }

  protected cancel(): void {
    void this.nav.back();
  }

  protected async save(): Promise<void> {
    this.tried.set(true);
    if (this.saving() || !this.kurinKey) return;
    if (!this.name().trim()) {
      await this.toasts.show('Дай плануванню назву.');
      return;
    }
    const problem =
      this.windowProblem() ??
      (this.rangeProblem() ? 'Кінець періоду не може бути раніше за його початок.' : null);
    if (problem) {
      await this.toasts.show(problem);
      return;
    }
    const request: CreatePlanningRequest = {
      name: this.name().trim(),
      kurinKey: this.kurinKey,
      durationDays: this.duration(),
      searchStart: dayToWire(this.from()),
      searchEnd: dayToWire(this.to()),
      participants: this.participants().map((p) => ({
        memberKey: p.memberKey,
        fullName: p.fullName,
        roleWeight: p.roleWeight,
        busyRanges: p.ranges.map((r) => busyToWire(r.from, r.to)),
      })),
    };
    this.saving.set(true);
    try {
      await this.planning.create(request);
      await this.toasts.show('Планування створено, дати розраховано');
      await this.nav.navigateBack('/tabs/calendar/planning');
    } catch (error) {
      await this.toasts.show(
        apiErrorText(error, 'Не вдалося створити планування. Спробуй ще раз.'),
        'danger',
      );
    } finally {
      this.saving.set(false);
    }
  }
}
