import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE } from '../../me/labels';
import { Component, ElementRef, OnDestroy, OnInit, afterRenderEffect, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import {
  ActionSheetButton,
  ActionSheetController,
  Config,
  IonBackButton,
  IonBadge,
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
import { add, lockClosed } from 'ionicons/icons';
import { Api, apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { GlassEffects } from '../../ui/glass';
import {
  GROUP_KINDS,
  KIND_LABELS,
  STANDING_LABELS,
  allQuarters,
  amountLabel,
  articlesAreStandard,
  balanceLabel,
  duesMoney,
  fromQuarterOptions,
  methodLabel,
  quarterKey,
  quarterLabel,
  quarterOfDate,
  rateAt,
  sameQuarter,
  shortDate,
  signedAmount,
  withPilha,
} from './dues-format';
import { DuesEntryForm, DuesKurinRateForm, DuesQuarterForm } from './dues-forms';
import {
  DuesAccountDto,
  DuesAccountQuarterDto,
  DuesEntryDto,
  DuesEntryKind,
  GroupDuesDto,
  QuarterDto,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest,
} from './leader.models';

type Part = 'box' | 'people' | 'history';

/** The API's dues error codes, in the web's words. */
const DUES_ERRORS: Record<string, string> = {
  EntryVerified: 'Операцію вже перевірено, її не змінити.',
  EntryNotFound: 'Цієї операції вже немає.',
  NotOfThisGroup: 'У цього юнака немає вкладки в цьому гуртку.',
  NotInGroup: 'Цей юнак не з цього гуртка.',
  CollectorNotInKurin: 'Той, хто збирав, має бути з цього куреня.',
  GroupNotFound: 'Такого гуртка немає.',
  DuesArticlesInUse: 'У куреня власні статті вкладки, їх змінюють у вебі.',
};

interface PersonRow {
  account: DuesAccountDto;
  cell: DuesAccountQuarterDto | null;
}

/**
 * The гурток's box on a phone (the web's group-dues): what the box holds and the rates, each
 * youth's quarter, and the history with filters. The keeper records operations and sets пільги;
 * the впорядник marks them checked; whoever may set the kurin's rates (`canSetKurinRates`) does it
 * here too, as on the web. Every write is followed by a fresh read, as on the web:
 * balances move together, so nothing is patched by hand.
 */
@Component({
  selector: 'app-group-dues',
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
    IonSegment,
    IonSegmentButton,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonBadge,
    IonSelect,
    IonSelectOption,
    IonSkeletonText,
    IonModal,
    NgTemplateOutlet,
    DuesEntryForm,
    DuesQuarterForm,
    DuesKurinRateForm,
  ],
  styles: `
    .subline {
      margin: 12px 20px 0;
      color: var(--lk-muted);
      font-size: 15px;
      line-height: 20px;
    }
    :host-context(.ios) .subline {
      margin-inline: 16px;
    }
    .note {
      margin: 24px 20px;
      color: var(--lk-muted);
      text-align: center;
    }
    .rates {
      padding: 12px 0;
      font-size: 15px;
      line-height: 22px;
      font-weight: 400;
      color: var(--lk-ink);
    }
    ion-label h3 {
      font-weight: 600;
      color: var(--lk-ink);
    }
    .money {
      font-size: 17px;
      font-weight: 700;
      color: var(--lk-ink);
      margin-inline-start: 8px;
      white-space: nowrap;
    }
    .debt {
      color: var(--lk-danger) !important;
    }
    .ok {
      color: var(--lk-primary) !important;
    }
    ion-badge {
      margin-inline-start: 6px;
      vertical-align: middle;
    }
    .lock {
      font-size: 14px;
      color: var(--lk-faint);
      margin-inline-start: 4px;
    }
    ion-segment {
      margin: 0 auto;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button [defaultHref]="'/tabs/kurin/group/' + groupKey" [text]="backText" /></ion-buttons>
        <ion-title>Вкладка</ion-title>
        @if (data()?.viewer?.canKeep) {
          <ion-buttons slot="end">
            <ion-button aria-label="Записати операцію" (click)="openEntry(null)" data-testid="add-entry">
              <ion-icon slot="icon-only" name="add" aria-hidden="true" />
            </ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
      <ion-toolbar>
        <ion-segment [value]="part()" (ionChange)="part.set($any($event).detail.value)">
          <ion-segment-button value="box"><ion-label>Каса</ion-label></ion-segment-button>
          <ion-segment-button value="people"><ion-label>Юнаки</ion-label></ion-segment-button>
          <ion-segment-button value="history"><ion-label>Історія</ion-label></ion-segment-button>
        </ion-segment>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @switch (loaded().state) {
        @case ('loading') {
          <ion-list [inset]="true">
            <ion-item-group>
              @for (row of [1, 2, 3, 4]; track row) {
                <ion-item>
                  <ion-label>
                    <ion-skeleton-text [animated]="true" style="width: 50%" />
                    <ion-skeleton-text [animated]="true" style="width: 30%" />
                  </ion-label>
                </ion-item>
              }
            </ion-item-group>
          </ion-list>
        }
        @case ('failed') {
          <p class="note">{{ denied() ? 'Немає доступу до вкладки цього гуртка.' : failedText }}</p>
        }
        @default {
          @if (data(); as d) {
            <p class="subline">{{ subline() }}</p>
            @switch (part()) {
              @case ('box') {
                <ion-list [inset]="true" data-testid="box">
                  <ion-list-header><ion-label>Каса</ion-label></ion-list-header>
                  <ion-item-group>
                    <ion-item>
                      <ion-label>
                        <h3>У касі</h3>
                        <p>готівка {{ money(d.box.cash) }} · картка {{ money(d.box.card) }}</p>
                      </ion-label>
                      <span slot="end" class="money">{{ money(d.box.total) }}</span>
                    </ion-item>
                    <ion-item>
                      <ion-label>
                        <h3>Гурткові</h3>
                        <p>можна витрачати</p>
                      </ion-label>
                      <span slot="end" class="money ok">{{ money(d.box.own) }}</span>
                    </ion-item>
                    <ion-item>
                      <ion-label class="ion-text-wrap">
                        <h3>До передачі курінному</h3>
                        <p>
                          {{ d.box.inTransit > 0 ? 'в дорозі ' + money(d.box.inTransit) : 'станиця й курінь з того, що зібрано' }}
                        </p>
                      </ion-label>
                      <span slot="end" class="money">{{ money(d.box.toForward) }}</span>
                    </ion-item>
                    <ion-item>
                      <ion-label>
                        <h3>Передано курінному</h3>
                        <p>підтверджено {{ money(d.handover.received) }}</p>
                      </ion-label>
                      <span slot="end" class="money">{{ money(d.handover.transferred) }}</span>
                    </ion-item>
                  </ion-item-group>
                </ion-list>

                <ion-list [inset]="true">
                  <ion-list-header><ion-label>Ставки</ion-label></ion-list-header>
                  <ion-item-group>
                    <ion-item>
                      <div class="rates" data-testid="rates">{{ ratesText() }}</div>
                    </ion-item>
                    @if (d.viewer.canKeep) {
                      <ion-item [button]="true" [detail]="true" (click)="openRate()" data-testid="group-rate">
                        <ion-label>Ставка гуртка</ion-label>
                        <ion-note slot="end">{{ groupRateNow() === null ? 'не задано' : money(groupRateNow()!) }}</ion-note>
                      </ion-item>
                    }
                    @if (d.viewer.canSetKurinRates) {
                      @if (standardArticles()) {
                        <ion-item [button]="true" [detail]="true" (click)="openKurinRate()" data-testid="kurin-rate">
                          <ion-label>Ставки куреня</ion-label>
                          @if (!kurinRateNow()) { <ion-note slot="end">не задано</ion-note> }
                        </ion-item>
                      } @else {
                        <ion-item data-testid="kurin-rate-web">
                          <ion-label class="ion-text-wrap">
                            <p>У куреня власні статті вкладки, їх змінюють у вебі.</p>
                          </ion-label>
                        </ion-item>
                      }
                    }
                  </ion-item-group>
                </ion-list>
              }

              @case ('people') {
                <ion-list [inset]="true">
                  <ion-item-group>
                    <ion-item>
                      <ion-select
                        label="Квартал"
                        interface="action-sheet"
                        cancelText="Скасувати"
                        [value]="quarterValue()"
                        (ionChange)="quarterChoice.set($any($event).detail.value)"
                        data-testid="quarter-picker"
                      >
                        @for (quarter of quarters(); track key(quarter)) {
                          <ion-select-option [value]="key(quarter)">{{ quarterName(quarter) }}</ion-select-option>
                        }
                      </ion-select>
                    </ion-item>
                  </ion-item-group>
                </ion-list>

                @if (currentPeople().length) {
                  <ion-list [inset]="true">
                    <ion-list-header><ion-label>У складі · {{ currentPeople().length }}</ion-label></ion-list-header>
                    <ion-item-group>
                      @for (row of currentPeople(); track row.account.membershipKey) {
                        <ng-container *ngTemplateOutlet="person; context: { $implicit: row }" />
                      }
                    </ion-item-group>
                  </ion-list>
                } @else {
                  <p class="note">Тут ще нікого немає. Юнаки зʼявляться, щойно курінь задасть ставки вкладки.</p>
                }
                @if (formerPeople().length) {
                  <ion-list [inset]="true">
                    <ion-list-header><ion-label>Переведені й вибулі з боргом</ion-label></ion-list-header>
                    <ion-item-group>
                      @for (row of formerPeople(); track row.account.membershipKey) {
                        <ng-container *ngTemplateOutlet="person; context: { $implicit: row }" />
                      }
                    </ion-item-group>
                  </ion-list>
                }
              }

              @case ('history') {
                <ion-list [inset]="true">
                  <ion-item-group>
                    @if (personName(); as name) {
                      <ion-item [button]="true" [detail]="false" (click)="personFilter.set(null)" data-testid="person-filter">
                        <ion-label>{{ name }}</ion-label>
                        <ion-note slot="end" color="primary">Усі люди</ion-note>
                      </ion-item>
                    }
                    <ion-item>
                      <ion-select
                        label="Квартал"
                        interface="action-sheet"
                        cancelText="Скасувати"
                        [value]="quarterFilter()"
                        (ionChange)="quarterFilter.set($any($event).detail.value)"
                        data-testid="history-quarter"
                      >
                        <ion-select-option value="all">Усі квартали</ion-select-option>
                        @for (quarter of quarters(); track key(quarter)) {
                          <ion-select-option [value]="key(quarter)">{{ quarterName(quarter) }}</ion-select-option>
                        }
                      </ion-select>
                    </ion-item>
                    <ion-item>
                      <ion-select
                        label="Вид"
                        interface="action-sheet"
                        cancelText="Скасувати"
                        [value]="kindFilter()"
                        (ionChange)="kindFilter.set($any($event).detail.value)"
                        data-testid="history-kind"
                      >
                        <ion-select-option value="all">Усі види</ion-select-option>
                        @for (kind of kinds; track kind) {
                          <ion-select-option [value]="kind">{{ kindLabels[kind] }}</ion-select-option>
                        }
                      </ion-select>
                    </ion-item>
                  </ion-item-group>
                </ion-list>

                @if (entries().length) {
                  <ion-list [inset]="true">
                    <ion-list-header><ion-label>Операції · {{ entries().length }}</ion-label></ion-list-header>
                    <ion-item-group>
                      @for (entry of entries(); track entry.duesEntryKey) {
                        <ion-item
                          [button]="hasActions(entry)"
                          [detail]="false"
                          [disabled]="busy() === entry.duesEntryKey"
                          (click)="hasActions(entry) && entryActions(entry)"
                          data-testid="entry"
                        >
                          <ion-label class="ion-text-wrap">
                            <h3>
                              {{ kindLabels[entry.kind] }}{{ entry.memberName ? ' · ' + entry.memberName : '' }}
                              @if (entry.isVerified) {
                                <ion-icon class="lock" name="lock-closed" aria-label="Перевірено" />
                              }
                            </h3>
                            <p>{{ entryLine(entry) }}</p>
                            @if (entry.note) { <p>{{ entry.note }}</p> }
                          </ion-label>
                          <span
                            slot="end"
                            class="money"
                            [class.ok]="signed(entry) !== null && signed(entry)! > 0"
                            [class.debt]="signed(entry) !== null && signed(entry)! < 0"
                          >
                            {{ amount(entry) }}
                          </span>
                        </ion-item>
                      }
                    </ion-item-group>
                  </ion-list>
                } @else {
                  <p class="note">
                    {{ d.entries.length ? 'Нічого не знайшлось. Спробуй інший квартал або вид операції.' : 'Каса ще порожня.' }}
                  </p>
                }
              }
            }
          }
        }
      }

      <ng-template #person let-row>
        <ion-item [button]="true" [detail]="true" (click)="personActions(row.account)" data-testid="person">
          <ion-label class="ion-text-wrap">
            <h3>
              {{ row.account.fullName }}
              @if (standing(row.account); as label) { <ion-badge class="lk-tag--secondary">{{ label }}</ion-badge> }
              @if (row.account.isConcessionNow) { <ion-badge class="lk-tag--info">пільгова</ion-badge> }
            </h3>
            <p>{{ cellLine(row.cell) }}</p>
            @if (row.account.balance !== 0) { <p>Загалом {{ balance(row.account.balance) }}</p> }
          </ion-label>
          @if (row.cell) {
            <span slot="end" class="money" [class.debt]="row.cell.balance < 0" [class.ok]="row.cell.balance >= 0">
              {{ balance(row.cell.balance) }}
            </span>
          }
        </ion-item>
      </ng-template>

      <ion-modal [isOpen]="entryOpen()" [canDismiss]="!saving()" (didDismiss)="entryOpen.set(false)">
        <ng-template>
          <app-dues-entry-form
            [currency]="currency()"
            [accounts]="data()?.accounts ?? []"
            [people]="data()?.people ?? []"
            [entry]="entryToEdit()"
            [saving]="saving()"
            [error]="formError()"
            (save)="saveEntry($event)"
            (cancel)="entryOpen.set(false)"
          />
        </ng-template>
      </ion-modal>

      <ion-modal
        [isOpen]="quarterForm() !== null"
        [canDismiss]="!saving()"
        [initialBreakpoint]="0.6"
        [breakpoints]="[0, 0.6, 1]"
        (didDismiss)="quarterForm.set(null)"
      >
        <ng-template>
          @if (quarterForm(); as form) {
            @if (data(); as d) {
              <app-dues-quarter-form
                [currency]="currency()"
                [mode]="form.mode"
                [quarters]="fromQuarters()"
                [current]="d.currentQuarter"
                [startShare]="groupRateNow()"
                [startOn]="form.account ? !form.account.isConcessionNow : true"
                [hint]="form.account
                  ? form.account.fullName + '. Пільга знижує лише станичну частину; курінь і гурток платяться повністю.'
                  : 'Гурткова частина квартальної вкладки. Станичну й курінну задає курінь.'"
                [saving]="saving()"
                [error]="formError()"
                (save)="saveQuarterForm(form, $event)"
                (cancel)="quarterForm.set(null)"
              />
            }
          }
        </ng-template>
      </ion-modal>

      <ion-modal
        [isOpen]="kurinRateOpen()"
        [canDismiss]="!saving()"
        [initialBreakpoint]="0.75"
        [breakpoints]="[0, 0.75, 1]"
        (didDismiss)="kurinRateOpen.set(false)"
      >
        <ng-template>
          @if (data(); as d) {
            <app-dues-kurin-rate-form
              [upstream]="d.upstream"
              [currency]="currency()"
              [rates]="d.kurinRates"
              [quarters]="fromQuarters()"
              [current]="d.currentQuarter"
              [saving]="saving()"
              [error]="formError()"
              (save)="saveKurinRate(d.kurinKey, $event)"
              (cancel)="kurinRateOpen.set(false)"
            />
          }
        </ng-template>
      </ion-modal>
    </ion-content>
  `,
})
export class GroupDuesPage implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  protected readonly groupKey = inject(ActivatedRoute).snapshot.paramMap.get('groupKey') ?? '';
  protected readonly failedText = FAILED_TEXT;
  /** iOS names where back leads; Material shows the arrow alone. */
  protected readonly backText = inject(Config).get('mode') === 'ios' ? 'Гурток' : undefined;
  protected readonly kinds = GROUP_KINDS;
  protected readonly kindLabels = KIND_LABELS;
  /** What this kurin counts its вкладка in, from its край's profile. */
  protected readonly currency = computed(() => this.data()?.currency ?? DEFAULT_CURRENCY);
  protected readonly numberLocale = computed(() => this.data()?.numberLocale ?? DEFAULT_NUMBER_LOCALE);
  protected readonly money = (amount: number): string => duesMoney(amount, this.currency(), this.numberLocale());
  protected readonly balance = (balance: number): string => balanceLabel(balance, this.currency(), this.numberLocale());
  protected readonly amount = (entry: DuesEntryDto): string => amountLabel(entry, this.currency(), this.numberLocale());
  protected readonly signed = signedAmount;
  protected readonly key = quarterKey;

  protected readonly loaded = signal<Loaded<GroupDuesDto>>({ state: 'loading' });
  protected readonly part = signal<Part>('box');
  protected readonly quarterChoice = signal<string | null>(null);
  protected readonly personFilter = signal<string | null>(null);
  protected readonly kindFilter = signal<DuesEntryKind | 'all'>('all');
  protected readonly quarterFilter = signal<string>('all');

  protected readonly denied = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly entryOpen = signal(false);
  protected readonly entryToEdit = signal<DuesEntryDto | null>(null);
  protected readonly kurinRateOpen = signal(false);
  protected readonly quarterForm = signal<{ mode: 'concession' | 'rate'; account: DuesAccountDto | null } | null>(null);

  protected readonly data = computed(() => valueOf(this.loaded()));
  protected readonly quarters = computed(() => allQuarters(this.data()?.years ?? []));
  protected readonly fromQuarters = computed(() => {
    const d = this.data();
    return d ? fromQuarterOptions(d.years, d.currentQuarter) : [];
  });
  /** The quarter the people list shows: the chosen one, else now. */
  private readonly quarter = computed<QuarterDto | null>(() => {
    const d = this.data();
    if (!d) return null;
    const choice = this.quarterChoice();
    return this.quarters().find((q) => quarterKey(q) === choice) ?? d.currentQuarter;
  });
  private readonly people = computed<PersonRow[]>(() => {
    const quarter = this.quarter();
    return (this.data()?.accounts ?? []).map((account) => ({
      account,
      cell: quarter ? (account.quarters.find((q) => sameQuarter(q.quarter, quarter)) ?? null) : null,
    }));
  });
  protected readonly quarterValue = computed(() => {
    const quarter = this.quarter();
    return quarter ? quarterKey(quarter) : null;
  });
  protected readonly currentPeople = computed(() => this.people().filter((r) => r.account.standing === 'Current'));
  protected readonly formerPeople = computed(() => this.people().filter((r) => r.account.standing !== 'Current'));

  protected readonly entries = computed(() => {
    const d = this.data();
    if (!d) return [];
    const person = this.personFilter();
    const kind = this.kindFilter();
    const quarter = this.quarterFilter();
    return d.entries.filter(
      (entry) =>
        (!person || entry.membershipKey === person) &&
        (kind === 'all' || entry.kind === kind) &&
        (quarter === 'all' || quarterKey(quarterOfDate(entry.occurredOn)) === quarter),
    );
  });
  protected readonly personName = computed(() => {
    const key = this.personFilter();
    return key ? (this.data()?.accounts.find((a) => a.membershipKey === key)?.fullName ?? null) : null;
  });

  protected readonly kurinRateNow = computed(() => {
    const d = this.data();
    return d ? rateAt(d.kurinRates, d.currentQuarter) : null;
  });
  protected readonly standardArticles = computed(() => articlesAreStandard(this.kurinRateNow()));
  protected readonly groupRateNow = computed(() => {
    const d = this.data();
    return d ? (rateAt(d.groupRates, d.currentQuarter)?.groupShare ?? null) : null;
  });
  protected readonly subline = computed(() => {
    const d = this.data();
    if (!d) return '';
    const debtors = d.accounts.filter((a) => a.balance < 0).length;
    const inGroup = d.accounts.filter((a) => a.standing === 'Current').length;
    return [d.groupName, `зараз ${quarterLabel(d.currentQuarter)}`, `${inGroup} у складі`, debtors ? `з боргом: ${debtors}` : '']
      .filter(Boolean)
      .join(' · ');
  });
  /** The web's sentence about the quarter's вкладка and its parts. */
  protected readonly ratesText = computed(() => {
    const d = this.data();
    if (!d) return '';
    const kurin = this.kurinRateNow();
    const group = this.groupRateNow();
    if (kurin) {
      const stanytsia = `${this.money(kurin.stanytsiaFull)}${withPilha(kurin.stanytsiaFull, kurin.stanytsiaReduced, this.currency(), this.numberLocale())}`;
      const own = `${this.money(kurin.kurinShare)}${withPilha(kurin.kurinShare, kurin.kurinReduced, this.currency(), this.numberLocale())}`;
      const krai = `${this.money(kurin.kraiFull)}${withPilha(kurin.kraiFull, kurin.kraiReduced, this.currency(), this.numberLocale())}`;
      // A край paid straight has no станиця part, unless an old rate still sets one.
      const parts = [
        kurin.stanytsiaFull > 0 || d.upstream !== 'krai' ? `станиця ${stanytsia}` : '',
        kurin.kraiFull > 0 ? `край ${krai}` : '',
        `курінь ${own}`,
      ].filter(Boolean);
      if (group === null) {
        const line = parts.join(' · ');
        return `${line.charAt(0).toUpperCase()}${line.slice(1)}. Гурткову частину ще не задано, поки вона нуль.`;
      }
      const total = kurin.stanytsiaFull + kurin.kraiFull + kurin.kurinShare + group;
      return `Квартальна вкладка ${this.money(total)} = ${parts.join(' · ')} · гурток ${this.money(group)}`;
    }
    const tail = group !== null ? `Гурток свою задав: ${this.money(group)} за квартал.` : 'Нарахувань поки немає.';
    return `Курінь ще не задав своїх статей вкладки. ${tail}`;
  });

  constructor() {
    addIcons({ add, lockClosed });
    afterRenderEffect(() => {
      this.part();
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

  protected quarterName(quarter: QuarterDto): string {
    const d = this.data();
    return d && sameQuarter(quarter, d.currentQuarter) ? `${quarterLabel(quarter)} (зараз)` : quarterLabel(quarter);
  }

  protected standing(account: DuesAccountDto): string | null {
    return STANDING_LABELS[account.standing];
  }

  protected cellLine(cell: DuesAccountQuarterDto | null): string {
    if (!cell) return 'У цьому кварталі не нараховано';
    return `Сплачено ${this.money(cell.paid.total)} з ${this.money(cell.charged.total)}${cell.isConcession ? ', пільгова' : ''}`;
  }

  protected entryLine(entry: DuesEntryDto): string {
    const parts = [shortDate(entry.occurredOn), methodLabel(entry)];
    if (entry.collectedByName) parts.push(`збирав ${entry.collectedByName}`);
    if (entry.isVerified && entry.verifiedByName) parts.push(`перевірив ${entry.verifiedByName}`);
    return parts.join(' · ');
  }

  /** As the web: the впорядник marks; the keeper edits what is not checked yet. */
  protected hasActions(entry: DuesEntryDto): boolean {
    const viewer = this.data()?.viewer;
    return !!viewer && (viewer.canVerify || (viewer.canKeep && !entry.isVerified));
  }

  protected async personActions(account: DuesAccountDto): Promise<void> {
    const viewer = this.data()?.viewer;
    if (!viewer?.canKeep || account.standing !== 'Current') {
      this.showHistoryOf(account);
      return;
    }
    const sheet = await this.sheets.create({
      header: account.fullName,
      buttons: [
        { text: 'Операції цієї людини', data: 'history' },
        { text: account.isConcessionNow ? 'Зняти пільгу…' : 'Пільгова вкладка…', data: 'concession' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (data === 'history') this.showHistoryOf(account);
    if (data === 'concession') this.openQuarterForm('concession', account);
  }

  protected async entryActions(entry: DuesEntryDto): Promise<void> {
    const viewer = this.data()?.viewer;
    if (!viewer) return;
    const buttons: ActionSheetButton[] = [];
    if (viewer.canVerify) {
      buttons.push(
        entry.isVerified
          ? { text: 'Зняти позначку «Перевірено»', data: 'unverify' }
          : { text: 'Позначити перевіреною', data: 'verify' },
      );
    }
    if (viewer.canKeep && !entry.isVerified) {
      buttons.push({ text: 'Змінити', data: 'edit' }, { text: 'Видалити', role: 'destructive', data: 'delete' });
    }
    buttons.push({ text: 'Скасувати', role: 'cancel' });
    const sheet = await this.sheets.create({
      header: `${KIND_LABELS[entry.kind]} · ${this.amount(entry)}`,
      subHeader: entry.memberName ?? undefined,
      buttons,
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (data === 'verify' || data === 'unverify') await this.setVerified(entry, data === 'verify');
    if (data === 'edit') this.openEntry(entry);
    if (data === 'delete') await this.confirmDelete(entry);
  }

  protected openEntry(entry: DuesEntryDto | null): void {
    this.entryToEdit.set(entry);
    this.formError.set(null);
    this.entryOpen.set(true);
  }

  protected openRate(): void {
    this.openQuarterForm('rate', null);
  }

  /** As the web, the kurin's rates can be set from a гурток's box too, by whoever may set them. */
  protected openKurinRate(): void {
    this.formError.set(null);
    this.kurinRateOpen.set(true);
  }

  protected async saveEntry(request: UpsertDuesEntryRequest): Promise<void> {
    const editing = this.entryToEdit();
    const done = await this.write(
      () =>
        editing
          ? this.api.put(`group/${this.groupKey}/dues/entries/${editing.duesEntryKey}`, request)
          : this.api.post(`group/${this.groupKey}/dues/entries`, request),
      'Не вдалося записати. Спробуй ще раз.',
    );
    if (!done) return;
    this.entryOpen.set(false);
    await this.toasts.show(editing ? 'Збережено' : 'Записано');
  }

  protected async saveQuarterForm(
    form: { mode: 'concession' | 'rate'; account: DuesAccountDto | null },
    value: { fromQuarter: QuarterDto; share: number; on: boolean },
  ): Promise<void> {
    const account = form.account;
    const done = await this.write(
      () =>
        form.mode === 'rate' || !account
          ? this.api.put(`group/${this.groupKey}/dues/rate`, { fromQuarter: value.fromQuarter, groupShare: value.share })
          : this.api.put(`group/${this.groupKey}/dues/members/${account.membershipKey}/concession`, {
              fromQuarter: value.fromQuarter,
              isConcession: value.on,
            }),
      'Не вдалося зберегти. Спробуй ще раз.',
    );
    if (!done) return;
    this.quarterForm.set(null);
    await this.toasts.show('Збережено');
  }

  protected async saveKurinRate(kurinKey: string, request: SetKurinDuesRateRequest): Promise<void> {
    const done = await this.write(
      () => this.api.put(`kurin/${kurinKey}/dues/rate`, request),
      'Не вдалося зберегти. Спробуй ще раз.',
    );
    if (!done) return;
    this.kurinRateOpen.set(false);
    await this.toasts.show('Збережено');
  }

  private showHistoryOf(account: DuesAccountDto): void {
    this.personFilter.set(account.membershipKey);
    this.part.set('history');
  }

  private openQuarterForm(mode: 'concession' | 'rate', account: DuesAccountDto | null): void {
    this.formError.set(null);
    this.quarterForm.set({ mode, account });
  }

  private async setVerified(entry: DuesEntryDto, isVerified: boolean): Promise<void> {
    this.busy.set(entry.duesEntryKey);
    try {
      await this.api.put(`group/${this.groupKey}/dues/entries/${entry.duesEntryKey}/verified`, { isVerified });
      await this.load();
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося позначити. Спробуй ще раз.', DUES_ERRORS), 'danger');
    } finally {
      this.busy.set(null);
    }
  }

  private async confirmDelete(entry: DuesEntryDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: 'Видалити операцію?',
      subHeader: `${KIND_LABELS[entry.kind]} на ${this.money(entry.amount)} зникне з каси. Запис про неї лишиться в історії змін.`,
      buttons: [
        { text: 'Видалити', role: 'destructive', data: 'delete' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (data !== 'delete') return;
    this.busy.set(entry.duesEntryKey);
    try {
      await this.api.delete(`group/${this.groupKey}/dues/entries/${entry.duesEntryKey}`);
      await this.load();
      await this.toasts.show('Видалено');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося видалити. Спробуй ще раз.', DUES_ERRORS), 'danger');
    } finally {
      this.busy.set(null);
    }
  }

  /** One write from a form: the form stays open with the reason if it fails. */
  private async write(call: () => Promise<unknown>, fallback: string): Promise<boolean> {
    this.saving.set(true);
    this.formError.set(null);
    try {
      await call();
      await this.load();
      return true;
    } catch (error) {
      this.formError.set(apiErrorText(error, fallback, DUES_ERRORS));
      return false;
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    const request = this.api.get<GroupDuesDto>(`group/${this.groupKey}/dues`);
    request.then(
      () => this.denied.set(false),
      (error: unknown) => this.denied.set(error instanceof HttpErrorResponse && error.status === 403),
    );
    await settle(request, this.loaded);
  }
}
