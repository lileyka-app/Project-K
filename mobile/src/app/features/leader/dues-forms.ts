import { currencySymbol, DEFAULT_CURRENCY } from '../../me/labels';
import { Component, ElementRef, OnDestroy, OnInit, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
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
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonTextarea,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import { GlassEffects } from '../../ui/glass';
import {
  EntryDraft,
  EntryProblems,
  GROUP_KINDS,
  KIND_LABELS,
  METHOD_LABELS,
  PERSONAL_KINDS,
  STANDING_LABELS,
  dateOnly,
  KurinRateDraft,
  entryProblems,
  entryRequest,
  kurinRateRequest,
  quarterKey,
  quarterLabel,
  rateAt,
  reducedAboveFull,
} from './dues-format';
import {
  DuesAccountDto,
  DuesEntryDto,
  DuesEntryKind,
  DuesPaymentMethod,
  DuesPersonDto,
  DuesUpstream,
  KurinDuesRateDto,
  QuarterDto,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest,
} from './leader.models';

const FORM_STYLES = `
  .hint {
    margin: -8px 32px 12px;
    font-size: 13px;
    line-height: 18px;
    color: var(--lk-muted);
  }
  .error {
    margin: -8px 32px 12px;
    font-size: 13px;
    color: var(--lk-danger);
  }
  ion-segment {
    max-width: 220px;
  }
`;

/**
 * One operation of a box, new or edited: the web's dues-entry-dialog as a sheet with «Скасувати»
 * and «Записати» in the toolbar. A гурток's kinds by default, the kurin's when it passes its own.
 * It only gathers and checks; the page sends it.
 */
@Component({
  selector: 'app-dues-entry-form',
  host: { class: 'ion-page' },
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonSelect,
    IonSelectOption,
    IonInput,
    IonTextarea,
    IonSegment,
    IonSegmentButton,
  ],
  styles: FORM_STYLES,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button [disabled]="saving()" (click)="cancel.emit()">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ entry() ? 'Операція' : 'Нова операція' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="saving()" (click)="submit()" data-testid="entry-save">
            {{ entry() ? 'Зберегти' : 'Записати' }}
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-select
              label="Вид операції"
              interface="action-sheet"
              cancelText="Скасувати"
              [value]="kind()"
              (ionChange)="kind.set($any($event).detail.value)"
              data-testid="entry-kind"
            >
              @for (option of kinds(); track option) {
                <ion-select-option [value]="option">{{ kindLabels[option] }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          @if (isPersonal()) {
            <ion-item>
              <ion-select
                label="Чия вкладка"
                placeholder="Обери юнака"
                cancelText="Скасувати"
                okText="Готово"
                [value]="membershipKey()"
                (ionChange)="membershipKey.set($any($event).detail.value)"
                data-testid="entry-person"
              >
                @for (account of accounts(); track account.membershipKey) {
                  <ion-select-option [value]="account.membershipKey">{{ personLabel(account) }}</ion-select-option>
                }
              </ion-select>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>
      @if (kind() === 'Correction') {
        <p class="hint">Корекція змінює баланс людини, а гроші в касі не рухає. Мінус зменшує баланс.</p>
      }
      @if (shown().person; as message) { <p class="error" role="alert">{{ message }}</p> }

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-input
              [label]="'Сума, ' + symbol()"
              inputmode="decimal"
              placeholder="0"
              autocomplete="off"
              [value]="amount()"
              (ionInput)="amount.set($any($event).detail.value ?? '')"
              data-testid="entry-amount"
            />
          </ion-item>
          <ion-item>
            <ion-input
              label="Дата"
              type="date"
              [max]="today"
              [value]="occurredOn()"
              (ionInput)="occurredOn.set($any($event).detail.value ?? '')"
              data-testid="entry-date"
            />
          </ion-item>
          <ion-item>
            <ion-label>{{ kind() === 'Exchange' ? 'Звідки' : 'Спосіб' }}</ion-label>
            <ion-segment slot="end" [value]="method()" (ionChange)="method.set($any($event).detail.value)">
              @for (option of methods; track option) {
                <ion-segment-button [value]="option"><ion-label>{{ methodLabels[option] }}</ion-label></ion-segment-button>
              }
            </ion-segment>
          </ion-item>
          <ion-item>
            <ion-select
              label="Хто збирав"
              placeholder="Не вказано"
              cancelText="Скасувати"
              okText="Готово"
              [value]="collectedBy()"
              (ionChange)="collectedBy.set($any($event).detail.value || null)"
            >
              <ion-select-option value="">Не вказано</ion-select-option>
              @for (person of people(); track person.memberKey) {
                <ion-select-option [value]="person.memberKey">{{ person.fullName }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
        </ion-item-group>
      </ion-list>
      @if (kind() === 'Exchange') {
        <p class="hint">{{ exchangeHint() }}. Сума в касі не змінюється.</p>
      }
      @if (shown().amount; as message) { <p class="error" role="alert">{{ message }}</p> }
      @if (shown().date; as message) { <p class="error" role="alert">{{ message }}</p> }

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-textarea
              label="Нотатка"
              labelPlacement="stacked"
              placeholder="За що, звідки, кому"
              [autoGrow]="true"
              [maxlength]="500"
              [value]="note()"
              (ionInput)="note.set($any($event).detail.value ?? '')"
            />
          </ion-item>
        </ion-item-group>
      </ion-list>
      @if (error(); as message) { <p class="error" role="alert">{{ message }}</p> }
    </ion-content>
  `,
})
export class DuesEntryForm implements OnInit, OnDestroy {
  /** The kinds this box holds; the first is where a new operation starts. */
  readonly kinds = input<readonly DuesEntryKind[]>(GROUP_KINDS);
  readonly accounts = input<DuesAccountDto[]>([]);
  readonly people = input<DuesPersonDto[]>([]);
  readonly entry = input<DuesEntryDto | null>(null);
  readonly saving = input(false);
  readonly currency = input(DEFAULT_CURRENCY);
  protected readonly symbol = computed(() => currencySymbol(this.currency()));
  readonly error = input<string | null>(null);
  readonly save = output<UpsertDuesEntryRequest>();
  readonly cancel = output<void>();

  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  protected readonly kindLabels = KIND_LABELS;
  protected readonly methods: DuesPaymentMethod[] = ['Cash', 'Card'];
  protected readonly methodLabels = METHOD_LABELS;
  protected readonly today = dateOnly(new Date());

  protected readonly kind = signal<DuesEntryKind>('Contribution');
  protected readonly method = signal<DuesPaymentMethod>('Cash');
  protected readonly amount = signal('');
  protected readonly occurredOn = signal(this.today);
  protected readonly membershipKey = signal<string | null>(null);
  protected readonly collectedBy = signal<string | null>(null);
  protected readonly note = signal('');
  private readonly tried = signal(false);

  private readonly draft = computed<EntryDraft>(() => ({
    kind: this.kind(),
    method: this.method(),
    amount: this.amount(),
    occurredOn: this.occurredOn(),
    membershipKey: this.membershipKey(),
    collectedByMemberKey: this.collectedBy(),
    note: this.note(),
  }));
  /** Problems show once a save was tried, then follow the fields as they are fixed. */
  protected readonly shown = computed<EntryProblems>(() => (this.tried() ? entryProblems(this.draft()) : {}));
  protected readonly isPersonal = computed(() => PERSONAL_KINDS.has(this.kind()));
  protected readonly exchangeHint = computed(() => {
    const from = this.method();
    const to: DuesPaymentMethod = from === 'Cash' ? 'Card' : 'Cash';
    return `З ${METHOD_LABELS[from].toLowerCase()} на ${METHOD_LABELS[to].toLowerCase()}`;
  });

  constructor() {
    afterNextRender(() => this.glass.sync());
  }

  ngOnInit(): void {
    const entry = this.entry();
    if (!entry) {
      this.kind.set(this.kinds()[0] ?? 'Contribution');
      return;
    }
    this.kind.set(entry.kind);
    this.method.set(entry.method);
    this.amount.set(String(entry.amount));
    this.occurredOn.set(entry.occurredOn.slice(0, 10));
    this.membershipKey.set(entry.membershipKey);
    this.collectedBy.set(entry.collectedByMemberKey);
    this.note.set(entry.note ?? '');
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  protected personLabel(account: DuesAccountDto): string {
    const standing = STANDING_LABELS[account.standing];
    return standing ? `${account.fullName} · ${standing}` : account.fullName;
  }

  protected submit(): void {
    this.tried.set(true);
    if (this.saving() || Object.keys(entryProblems(this.draft())).length) return;
    this.save.emit(entryRequest(this.draft()));
  }
}

/**
 * The two small settings of a гурток's box that start from a quarter: a person's пільга
 * (`concession`) and the гурток's own share of the вкладка (`rate`).
 */
@Component({
  selector: 'app-dues-quarter-form',
  host: { class: 'ion-page' },
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonItemGroup,
    IonItem,
    IonSelect,
    IonSelectOption,
    IonInput,
    IonToggle,
  ],
  styles: FORM_STYLES,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button [disabled]="saving()" (click)="cancel.emit()">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ mode() === 'rate' ? 'Ставка гуртка' : 'Пільгова вкладка' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="saving() || !valid()" (click)="submit()" data-testid="quarter-save">
            Зберегти
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-select
              label="З кварталу"
              interface="action-sheet"
              cancelText="Скасувати"
              [value]="from()"
              (ionChange)="from.set($any($event).detail.value)"
            >
              @for (quarter of quarters(); track key(quarter)) {
                <ion-select-option [value]="key(quarter)">{{ label(quarter) }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          @if (mode() === 'rate') {
            <ion-item>
              <ion-input
                [label]="'На гурток за квартал, ' + symbol()"
                inputmode="decimal"
                placeholder="0"
                [value]="share()"
                (ionInput)="share.set($any($event).detail.value ?? '')"
                data-testid="rate-share"
              />
            </ion-item>
          } @else {
            <ion-item>
              <ion-toggle [checked]="on()" (ionChange)="on.set($any($event).detail.checked)" data-testid="concession-toggle">
                {{ on() ? 'Пільгова вкладка' : 'Повна вкладка' }}
              </ion-toggle>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>
      <p class="hint">{{ hint() }}</p>
      @if (error(); as message) { <p class="error" role="alert">{{ message }}</p> }
    </ion-content>
  `,
})
export class DuesQuarterForm implements OnInit {
  readonly mode = input.required<'concession' | 'rate'>();
  readonly quarters = input<QuarterDto[]>([]);
  readonly current = input.required<QuarterDto>();
  /** The rate in force now, or whether the person has a пільга now. */
  readonly startShare = input<number | null>(null);
  readonly startOn = input(true);
  readonly hint = input('');
  readonly saving = input(false);
  readonly currency = input(DEFAULT_CURRENCY);
  protected readonly symbol = computed(() => currencySymbol(this.currency()));
  readonly error = input<string | null>(null);
  readonly save = output<{ fromQuarter: QuarterDto; share: number; on: boolean }>();
  readonly cancel = output<void>();

  protected readonly from = signal('');
  protected readonly share = signal('');
  protected readonly on = signal(true);
  protected readonly key = quarterKey;
  protected readonly label = quarterLabel;

  protected readonly valid = computed(() => {
    if (this.mode() !== 'rate') return true;
    const share = Number(this.share().replace(',', '.'));
    return this.share().trim() !== '' && Number.isFinite(share) && share >= 0;
  });

  ngOnInit(): void {
    this.from.set(quarterKey(this.current()));
    const share = this.startShare();
    this.share.set(share === null ? '' : String(share));
    this.on.set(this.startOn());
  }

  protected submit(): void {
    const fromQuarter = this.quarters().find((q) => quarterKey(q) === this.from());
    if (!fromQuarter || !this.valid() || this.saving()) return;
    this.save.emit({ fromQuarter, share: Number(this.share().replace(',', '.')) || 0, on: this.on() });
  }
}

/**
 * The станиця and kurin parts of the quarterly вкладка, from a quarter on: the web's
 * kurin-rate-dialog. Starts from the rates in force now, so a change is an edit of what is.
 */
@Component({
  selector: 'app-dues-kurin-rate-form',
  host: { class: 'ion-page' },
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonItemGroup,
    IonItem,
    IonSelect,
    IonSelectOption,
    IonInput,
  ],
  styles: FORM_STYLES,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button [disabled]="saving()" (click)="cancel.emit()">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>Ставки куреня</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="saving() || !request()" (click)="submit()" data-testid="kurin-rate-save">
            Зберегти
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-select
              label="З кварталу"
              interface="action-sheet"
              cancelText="Скасувати"
              [value]="from()"
              (ionChange)="from.set($any($event).detail.value)"
            >
              @for (quarter of quarters(); track key(quarter)) {
                <ion-select-option [value]="key(quarter)">{{ label(quarter) }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
        </ion-item-group>
      </ion-list>
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-input
              [label]="up() + ', повна, ' + symbol()"
              inputmode="decimal"
              placeholder="0"
              [value]="full()"
              (ionInput)="full.set($any($event).detail.value ?? '')"
              data-testid="rate-full"
            />
          </ion-item>
          <ion-item>
            <ion-input
              [label]="up() + ', пільгова, ' + symbol()"
              inputmode="decimal"
              placeholder="як повна"
              [value]="reduced()"
              (ionInput)="reduced.set($any($event).detail.value ?? '')"
              data-testid="rate-reduced"
            />
          </ion-item>
          <ion-item>
            <ion-input
              [label]="'Курінь, ' + symbol()"
              inputmode="decimal"
              placeholder="0"
              [value]="share()"
              (ionInput)="share.set($any($event).detail.value ?? '')"
              data-testid="rate-kurin"
            />
          </ion-item>
        </ion-item-group>
      </ion-list>
      @if (reducedTooHigh()) {
        <p class="error" role="alert">Пільгова не може бути вищою за повну.</p>
      }
      <p class="hint">
        {{ upAdjective() }} й курінна частини квартальної вкладки — однакові для всіх гуртків куреня. Пільга знижує лише {{ upAdjective().toLowerCase() }}.
      </p>
      @if (error(); as message) { <p class="error" role="alert">{{ message }}</p> }
    </ion-content>
  `,
})
export class DuesKurinRateForm implements OnInit {
  readonly rates = input<KurinDuesRateDto[]>([]);
  readonly quarters = input<QuarterDto[]>([]);
  readonly current = input.required<QuarterDto>();
  readonly saving = input(false);
  readonly currency = input(DEFAULT_CURRENCY);
  protected readonly symbol = computed(() => currencySymbol(this.currency()));
  /** In a край paid straight the part that goes up is the край's, and the server names it so. */
  readonly upstream = input<DuesUpstream>('stanytsia');
  protected readonly up = computed(() => (this.upstream() === 'krai' ? 'Край' : 'Станиця'));
  protected readonly upAdjective = computed(() => (this.upstream() === 'krai' ? 'Крайова' : 'Станична'));
  readonly error = input<string | null>(null);
  readonly save = output<SetKurinDuesRateRequest>();
  readonly cancel = output<void>();

  protected readonly from = signal('');
  protected readonly full = signal('');
  protected readonly reduced = signal('');
  protected readonly share = signal('');
  protected readonly key = quarterKey;
  protected readonly label = quarterLabel;

  private readonly draft = computed<KurinRateDraft>(() => ({ full: this.full(), reduced: this.reduced(), share: this.share() }));
  protected readonly reducedTooHigh = computed(() => reducedAboveFull(this.draft()));
  protected readonly request = computed(() => {
    const fromQuarter = this.quarters().find((q) => quarterKey(q) === this.from());
    return fromQuarter ? kurinRateRequest(this.draft(), fromQuarter) : null;
  });

  ngOnInit(): void {
    const current = this.current();
    const now = rateAt(this.rates(), current);
    const text = (value: number | undefined) => (value === undefined ? '' : String(value));
    this.from.set(quarterKey(current));
    const krai = this.upstream() === 'krai';
    this.full.set(text(krai ? now?.kraiFull : now?.stanytsiaFull));
    this.reduced.set(text(krai ? (now?.kraiReduced ?? now?.kraiFull) : (now?.stanytsiaReduced ?? now?.stanytsiaFull)));
    this.share.set(text(now?.kurinShare));
  }

  protected submit(): void {
    const request = this.request();
    if (!request || this.saving()) return;
    this.save.emit(request);
  }
}
