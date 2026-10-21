import { Component, ElementRef, OnDestroy, afterRenderEffect, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonTextarea,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { GlassEffects } from '../../ui/glass';
import { dateOnly, parseDay, points } from './score.format';
import { ScoreEntryDto, ScoreEntryEvent, ScoreEntryTarget, ScoreItemDto, UpsertScoreEntryRequest } from './score.models';

type Mode = 'item' | 'free';

/**
 * «Записати бал» (the web's score-entry-dialog) as the body of a sheet: a position from the
 * kurin's list, or an amount with a reason, and the day. Shows what the target already has at
 * this event so a second суддя sees it; tapping one of those edits it instead.
 */
@Component({
  selector: 'app-score-entry-form',
  // The modal lays out its child as a page: header on top, content filling the rest.
  host: { class: 'ion-page' },
  imports: [
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
    IonNote,
    IonInput,
    IonTextarea,
    IonSelect,
    IonSelectOption,
    IonSegment,
    IonSegmentButton,
    IonSpinner,
  ],
  styles: `
    ion-content {
      --background: var(--lk-surface);
    }
    :host-context(.ios) ion-content {
      --background: var(--lk-ios-ground);
    }
    ion-segment {
      margin: 16px 16px 0;
      width: auto;
    }
    ion-item > .lk-field {
      padding: 10px 0 12px;
      width: 100%;
    }
    .lk-input-box ion-textarea {
      --background: transparent;
      --color: var(--lk-ink);
      --placeholder-color: var(--lk-faint);
      --placeholder-opacity: 1;
      --padding-start: 12px;
      --padding-end: 12px;
      --highlight-height: 0;
      --border-width: 0;
      font-size: 16px;
    }
    .hint {
      margin: 6px 20px 0;
      font-size: 13px;
      color: var(--lk-muted);
    }
    .error {
      margin: 12px 20px 0;
      color: var(--lk-danger);
      font-size: 14px;
    }
    .plus {
      color: var(--lk-primary);
      font-weight: 700;
    }
    .minus {
      color: var(--lk-danger);
      font-weight: 700;
    }
    .remove {
      text-align: center;
    }
    .pad {
      height: 24px;
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button [disabled]="saving()" (click)="cancelled.emit()">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ title() }}</ion-title>
        <ion-buttons slot="end">
          <ion-button strong data-testid="entry-save" [disabled]="!valid() || saving()" (click)="submit()">
            @if (saving()) { <ion-spinner name="crescent" /> } @else { {{ entry() ? 'Зберегти' : 'Записати' }} }
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          @if (targetName(); as name) {
            <ion-item>
              <ion-label class="ion-text-wrap">
                <h3>{{ name }}</h3>
                @if (event(); as ev) { <p>{{ ev.title }}</p> }
              </ion-label>
            </ion-item>
          } @else if (canPickTarget()) {
            <ion-item>
              <ion-select
                data-testid="entry-target"
                label="Кому"
                interface="action-sheet"
                placeholder="Обери"
                cancelText="Скасувати"
                [value]="targetValue()"
                (ionChange)="targetValue.set($any($event).detail.value)"
              >
                @for (t of targetOptions(); track t.value) {
                  <ion-select-option [value]="t.value">{{ t.label }}</ion-select-option>
                }
              </ion-select>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>

      @if (others().length) {
        <ion-list [inset]="true">
          <ion-list-header><ion-label>Уже є на цій події</ion-label></ion-list-header>
          <ion-item-group>
            @for (e of others(); track e.scoreEntryKey) {
              <ion-item [button]="editable()" [detail]="editable()" (click)="editable() && edit.emit(e)">
                <span slot="start" [class.plus]="e.points > 0" [class.minus]="e.points < 0">{{ points(e.points) }}</span>
                <ion-label class="ion-text-wrap">{{ e.itemName ?? e.reason }}</ion-label>
                @if (e.createdByName) { <ion-note slot="end">{{ e.createdByName }}</ion-note> }
              </ion-item>
            }
          </ion-item-group>
        </ion-list>
      }

      @if (items().length) {
        <ion-segment [value]="mode()" (ionChange)="setMode($event)" aria-label="Що саме">
          <ion-segment-button value="item"><ion-label>З переліку</ion-label></ion-segment-button>
          <ion-segment-button value="free"><ion-label>Свій бал</ion-label></ion-segment-button>
        </ion-segment>
      }

      <ion-list [inset]="true">
        <ion-item-group>
          @if (mode() === 'item') {
            <ion-item>
              <ion-select
                data-testid="entry-item"
                label="Позиція"
                interface="action-sheet"
                placeholder="Обери"
                cancelText="Скасувати"
                [value]="itemKey()"
                (ionChange)="itemKey.set($any($event).detail.value)"
              >
                @for (option of itemOptions(); track option.value) {
                  <ion-select-option [value]="option.value" [disabled]="option.disabled">{{ option.label }}</ion-select-option>
                }
              </ion-select>
            </ion-item>
          } @else {
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">Бал</span>
                <div class="lk-input-box" [class.lk-invalid]="!!amountError()">
                  <ion-input
                    data-testid="entry-amount"
                    aria-label="Бал"
                    type="number"
                    placeholder="+3 або −1"
                    [value]="amount()"
                    (ionInput)="amount.set($any($event).detail.value)"
                  />
                </div>
                @if (amountError(); as message) { <span class="lk-field__error">{{ message }}</span> }
              </div>
            </ion-item>
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">За що</span>
                <div class="lk-input-box">
                  <ion-textarea
                    data-testid="entry-reason"
                    aria-label="За що"
                    [autoGrow]="true"
                    [rows]="2"
                    [maxlength]="500"
                    placeholder="Коротко, щоб потім згадати"
                    [value]="reason()"
                    (ionInput)="reason.set($any($event).detail.value ?? '')"
                  />
                </div>
              </div>
            </ion-item>
          }
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Дата</span>
              <div class="lk-input-box">
                <ion-input
                  data-testid="entry-date"
                  aria-label="Дата"
                  type="date"
                  [max]="today"
                  [value]="occurredOn()"
                  (ionInput)="occurredOn.set($any($event).detail.value ?? '')"
                />
              </div>
            </div>
          </ion-item>
        </ion-item-group>
      </ion-list>
      @if (mode() === 'item' && event()) {
        <p class="hint">Позицію на одній події дають один раз — зайняті вже неактивні.</p>
      }
      @if (errorMessage(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      @if (entry(); as editing) {
        <ion-list [inset]="true">
          <ion-item-group>
            <ion-item [button]="true" [detail]="false" [disabled]="saving()" (click)="remove.emit(editing)">
              <ion-label class="remove" color="danger">Видалити бал</ion-label>
            </ion-item>
          </ion-item-group>
        </ion-list>
      }
      <div class="pad"></div>
    </ion-content>
  `,
})
export class ScoreEntryForm implements OnDestroy {
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  readonly target = input<ScoreEntryTarget | null>(null);
  /** Where a target may be picked: the people of a гурток and the гурток itself. */
  readonly targets = input<ScoreEntryTarget[]>([]);
  readonly event = input<ScoreEntryEvent | null>(null);
  readonly items = input<ScoreItemDto[]>([]);
  /** What the target already has at this event, shown before anything is added. */
  readonly existing = input<ScoreEntryDto[]>([]);
  readonly entry = input<ScoreEntryDto | null>(null);
  readonly saving = input(false);
  readonly errorMessage = input<string | null>(null);
  /** Whether an entry in «Уже є» may be opened for editing. */
  readonly editable = input(false);

  readonly save = output<UpsertScoreEntryRequest>();
  readonly remove = output<ScoreEntryDto>();
  readonly edit = output<ScoreEntryDto>();
  readonly cancelled = output<void>();

  protected readonly points = points;
  protected readonly today = dateOnly(new Date());

  protected readonly mode = signal<Mode>('item');
  protected readonly targetValue = signal<string | null>(null);
  protected readonly itemKey = signal<string | null>(null);
  protected readonly amount = signal<string | number | null>(null);
  protected readonly reason = signal('');
  protected readonly occurredOn = signal(this.today);

  protected readonly title = computed(() => (this.entry() ? 'Змінити бал' : 'Записати бал'));
  protected readonly canPickTarget = computed(() => !this.target() && !this.entry() && this.targets().length > 0);
  protected readonly targetOptions = computed(() =>
    this.targets().map((t) => ({ label: t.name, value: t.membershipKey ?? `group:${t.groupKey}` })),
  );

  protected readonly itemOptions = computed(() => {
    const taken = new Set(this.existing().map((e) => e.scoreItemKey).filter(Boolean));
    const editing = this.entry()?.scoreItemKey;
    return this.items().map((item) => ({
      label: `${item.name} · ${points(item.points)}`,
      value: item.scoreItemKey,
      disabled: this.event() !== null && taken.has(item.scoreItemKey) && editing !== item.scoreItemKey,
    }));
  });

  /** What else the target has here — the entry being edited is not «else». */
  protected readonly others = computed(() => {
    const editing = this.entry()?.scoreEntryKey;
    return this.existing().filter((e) => e.scoreEntryKey !== editing);
  });

  protected readonly targetName = computed(() => {
    const editing = this.entry();
    if (editing) return editing.isForGroup ? `Гурток ${editing.groupName} цілим` : editing.memberName;
    return this.target()?.name ?? null;
  });

  private readonly amountNumber = computed(() => {
    const raw = this.amount();
    return raw === null || raw === '' ? null : Number(raw);
  });

  protected readonly amountError = computed(() =>
    this.mode() === 'free' && this.amountNumber() === 0 ? 'Нуль нічого не змінює.' : null,
  );

  protected readonly valid = computed(() => {
    if (!this.resolvedTarget() || !this.occurredOn()) return false;
    if (this.mode() === 'item') return !!this.itemKey();
    const amount = this.amountNumber();
    return amount !== null && Number.isFinite(amount) && amount !== 0 && this.reason().trim().length > 0;
  });

  constructor() {
    // A new entry to edit (tapped in «Уже є») starts the form over from it.
    effect(() => {
      const editing = this.entry();
      untracked(() => this.reset(editing));
    });
    afterRenderEffect(() => {
      this.items();
      this.glass.sync();
    });
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  protected setMode(change: Event): void {
    const value = (change as CustomEvent<{ value?: string }>).detail.value;
    if (value === 'item' || value === 'free') this.mode.set(value);
  }

  protected submit(): void {
    const target = this.resolvedTarget();
    if (!target || !this.valid() || this.saving()) return;
    const free = this.mode() === 'free';
    const editing = this.entry();
    this.save.emit({
      membershipKey: target.membershipKey,
      groupKey: target.groupKey,
      scoreItemKey: free ? null : this.itemKey(),
      points: free ? this.amountNumber() : null,
      reason: free ? this.reason().trim() : null,
      agendaItemKey: this.event()?.agendaItemKey ?? editing?.agendaItemKey ?? null,
      occurrenceStartUtc: this.event()?.occurrenceStartUtc ?? editing?.occurrenceStartUtc ?? null,
      occurredOn: this.occurredOn(),
    });
  }

  private resolvedTarget(): { membershipKey: string | null; groupKey: string | null } | null {
    const editing = this.entry();
    if (editing) return { membershipKey: editing.membershipKey, groupKey: editing.isForGroup ? editing.groupKey : null };
    const fixed = this.target();
    if (fixed) return { membershipKey: fixed.membershipKey, groupKey: fixed.groupKey };
    const value = this.targetValue();
    if (!value) return null;
    return value.startsWith('group:')
      ? { membershipKey: null, groupKey: value.slice('group:'.length) }
      : { membershipKey: value, groupKey: null };
  }

  private reset(editing: ScoreEntryDto | null): void {
    const event = this.event();
    const itemFirst = editing ? !!editing.scoreItemKey : this.items().length > 0;
    this.mode.set(itemFirst ? 'item' : 'free');
    this.itemKey.set(editing?.scoreItemKey ?? null);
    this.amount.set(editing && !editing.scoreItemKey ? editing.points : null);
    this.reason.set(editing?.reason ?? '');
    const day = editing?.occurredOn ?? event?.occurrenceStartUtc;
    this.occurredOn.set(day ? dateOnly(parseDay(day)) : this.today);
  }
}
