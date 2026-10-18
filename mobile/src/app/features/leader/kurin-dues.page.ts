import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE } from '../../me/labels';
import { Component, ElementRef, OnDestroy, OnInit, afterRenderEffect, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
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
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { add, lockClosed } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { Api, apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { GlassEffects } from '../../ui/glass';
import {
  KIND_LABELS,
  KURIN_KINDS,
  kurinKinds,
  paysKrai,
  paysStanytsia,
  allQuarters,
  amountLabel,
  articlesAreStandard,
  duesMoney,
  fromQuarterOptions,
  kurinRatesText,
  methodLabel,
  quarterKey,
  quarterLabel,
  quarterOfDate,
  rateAt,
  sameQuarter,
  shortDate,
  signedAmount,
} from './dues-format';
import { DuesEntryForm, DuesKurinRateForm } from './dues-forms';
import {
  DuesEntryDto,
  DuesEntryKind,
  DuesTransferDto,
  KurinDuesDto,
  KurinDuesQuarterGroupDto,
  KurinGroupHandoverDto,
  QuarterDto,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest,
} from './leader.models';

type Part = 'box' | 'groups' | 'transfers' | 'history';

const PARTS: readonly Part[] = ['box', 'groups', 'transfers', 'history'];

/** The API's kurin dues error codes, in the web's words. */
const DUES_ERRORS: Record<string, string> = {
  EntryVerified: 'Операцію вже перевірено, її не змінити.',
  EntryNotFound: 'Цієї операції вже немає.',
  TransferNotFound: 'Цієї передачі вже немає.',
  CollectorNotInKurin: 'Той, хто збирав, має бути з цього куреня.',
  DuesArticlesInUse: 'У куреня власні статті вкладки, їх змінюють у вебі.',
};

/**
 * The kurin's box on a phone (the web's kurin-dues): what the box holds and the rates, how each
 * гурток stands and its youth by quarter, the transfers from гуртки awaiting «Отримано», and the
 * kurin's own operations. The курінний скарбник and the Звʼязковий keep it (`canKeep`), the
 * Звʼязковий marks operations checked (`canVerify`) and sets the rates (`canSetRates`). As on the
 * web, every write is followed by a fresh read.
 */
@Component({
  selector: 'app-kurin-dues',
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
    IonToggle,
    IonModal,
    DuesEntryForm,
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
    .footnote {
      margin: -8px 32px 16px;
      color: var(--lk-muted);
      font-size: 13px;
      line-height: 18px;
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
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/kurin" [text]="backText" /></ion-buttons>
        <ion-title>Вкладка куреня</ion-title>
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
          <ion-segment-button value="groups"><ion-label>Гуртки</ion-label></ion-segment-button>
          <ion-segment-button value="transfers"><ion-label>Передачі</ion-label></ion-segment-button>
          <ion-segment-button value="history"><ion-label>Історія</ion-label></ion-segment-button>
        </ion-segment>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @if (!kurinKey) {
        <p class="note">Ти ще не в курені.</p>
      } @else {
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
            <p class="note">{{ denied() ? 'Немає доступу до вкладки куреня.' : failedText }}</p>
          }
          @default {
            @if (data(); as d) {
              <p class="subline" data-testid="subline">{{ subline() }}</p>
              @switch (part()) {
                @case ('box') {
                  <ion-list [inset]="true" data-testid="box">
                    <ion-list-header><ion-label>Каса куреня</ion-label></ion-list-header>
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
                          <h3>Курінні</h3>
                          <p>можна витрачати</p>
                        </ion-label>
                        <span slot="end" class="money ok">{{ money(d.box.own) }}</span>
                      </ion-item>
                      @if (hasStanytsia()) {
                        <ion-item data-testid="to-stanytsia">
                          <ion-label class="ion-text-wrap">
                            <h3>До передачі в станицю</h3>
                            <p>передано {{ money(d.sentToStanytsia) }}</p>
                          </ion-label>
                          <span slot="end" class="money">{{ money(d.toStanytsia) }}</span>
                        </ion-item>
                      }
                      @if (hasKrai()) {
                        <ion-item data-testid="to-krai">
                          <ion-label class="ion-text-wrap">
                            <h3>До передачі в край</h3>
                            <p>передано {{ money(d.sentToKrai) }}</p>
                          </ion-label>
                          <span slot="end" class="money">{{ money(d.toKrai) }}</span>
                        </ion-item>
                      }
                      <ion-item>
                        <ion-label class="ion-text-wrap">
                          <h3>В дорозі від гуртків</h3>
                          <p>{{ outstanding() > 0 ? 'ще в гуртках ' + money(outstanding()) : 'гуртки все передали' }}</p>
                        </ion-label>
                        <span slot="end" class="money">{{ money(d.box.inTransit) }}</span>
                      </ion-item>
                    </ion-item-group>
                  </ion-list>

                  @if (pending().length) {
                    <ion-list [inset]="true">
                      <ion-item-group>
                        <ion-item [button]="true" [detail]="true" (click)="showPending()" data-testid="pending">
                          <ion-label class="ion-text-wrap">Передачі чекають підтвердження</ion-label>
                          <ion-badge slot="end" class="lk-tag--warn">{{ pending().length }}</ion-badge>
                        </ion-item>
                      </ion-item-group>
                    </ion-list>
                  }

                  <ion-list [inset]="true">
                    <ion-list-header><ion-label>Ставки</ion-label></ion-list-header>
                    <ion-item-group>
                      <ion-item>
                        <div class="rates" data-testid="rates">{{ ratesText() }}</div>
                      </ion-item>
                      @if (d.viewer.canSetRates) {
                        @if (standardArticles()) {
                          <ion-item [button]="true" [detail]="true" (click)="openRate()" data-testid="kurin-rate">
                            <ion-label>Ставки куреня</ion-label>
                            @if (!rateNow()) { <ion-note slot="end">не задано</ion-note> }
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

                @case ('groups') {
                  @if (d.groups.length) {
                    <ion-list [inset]="true" data-testid="groups">
                      <ion-list-header><ion-label>Гуртки · {{ d.groups.length }}</ion-label></ion-list-header>
                      <ion-item-group>
                        @for (group of d.groups; track group.groupKey) {
                          <ion-item [button]="true" [detail]="true" (click)="groupActions(group)" data-testid="group">
                            <ion-label class="ion-text-wrap">
                              <h3>{{ group.groupName }}</h3>
                              <p>Зібрано вгору {{ money(group.owedUp) }} · передано {{ money(group.transferred) }}</p>
                              <p>{{ handoverLine(group) }}</p>
                            </ion-label>
                            <span slot="end" class="money" [class.debt]="group.outstanding > 0" [class.ok]="group.outstanding <= 0">
                              {{ group.outstanding > 0 ? money(group.outstanding) : 'усе передано' }}
                            </span>
                          </ion-item>
                        }
                      </ion-item-group>
                    </ion-list>
                    <p class="footnote">Праворуч — що гурток зібрав для станиці й куреня, але ще не передав.</p>
                  } @else {
                    <p class="note">Гуртків поки немає.</p>
                  }

                  @if (quarters().length) {
                    <ion-list [inset]="true">
                      <ion-list-header><ion-label>По кварталах</ion-label></ion-list-header>
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
                            @for (row of quarters(); track key(row.quarter)) {
                              <ion-select-option [value]="key(row.quarter)">{{ quarterName(row.quarter) }}</ion-select-option>
                            }
                          </ion-select>
                        </ion-item>
                      </ion-item-group>
                    </ion-list>
                    @if (quarter(); as row) {
                      <ion-list [inset]="true" data-testid="quarter">
                        <ion-item-group>
                          @for (group of row.groups; track group.groupKey) {
                            <ion-item [button]="true" [detail]="true" (click)="openGroup(group.groupKey)" data-testid="quarter-group">
                              <ion-label class="ion-text-wrap">
                                <h3>{{ group.groupName }}</h3>
                                <p>{{ quarterLine(group) }}</p>
                                @if (hasStanytsia()) { <p>До станиці {{ money(group.stanytsiaCollected) }} з {{ money(group.stanytsiaExpected) }}</p> }
                                @if (hasKrai()) { <p>До краю {{ money(group.kraiCollected) }} з {{ money(group.kraiExpected) }}</p> }
                              </ion-label>
                              <span slot="end" class="money" [class.debt]="group.debtUp > 0" [class.ok]="group.debtUp <= 0">
                                {{ group.debtUp > 0 ? money(group.debtUp) : 'сплачено' }}
                              </span>
                            </ion-item>
                          }
                          <ion-item data-testid="quarter-total">
                            <ion-label class="ion-text-wrap">
                              <h3>Разом</h3>
                              <p>{{ quarterLine(row.total) }}</p>
                              @if (hasStanytsia()) { <p>До станиці {{ money(row.total.stanytsiaCollected) }} з {{ money(row.total.stanytsiaExpected) }}</p> }
                              @if (hasKrai()) { <p>До краю {{ money(row.total.kraiCollected) }} з {{ money(row.total.kraiExpected) }}</p> }
                            </ion-label>
                            <span slot="end" class="money" [class.debt]="row.total.debtUp > 0" [class.ok]="row.total.debtUp <= 0">
                              {{ row.total.debtUp > 0 ? money(row.total.debtUp) : 'сплачено' }}
                            </span>
                          </ion-item>
                        </ion-item-group>
                      </ion-list>
                      <p class="footnote">Що юнаки кожного гуртка мають віддати на станицю й курінь, і скільки вже здали.</p>
                    }
                  } @else {
                    <p class="note">Нарахувань ще немає. Квартал зʼявиться тут, щойно курінь задасть ставки.</p>
                  }
                }

                @case ('transfers') {
                  <ion-list [inset]="true">
                    <ion-item-group>
                      @if (groupFilterName(); as name) {
                        <ion-item [button]="true" [detail]="false" (click)="groupFilter.set(null)" data-testid="group-filter">
                          <ion-label>{{ name }}</ion-label>
                          <ion-note slot="end" color="primary">Усі гуртки</ion-note>
                        </ion-item>
                      }
                      <ion-item>
                        <ion-toggle
                          justify="space-between"
                          [checked]="pendingOnly()"
                          (ionChange)="pendingOnly.set($any($event).detail.checked)"
                          data-testid="pending-only"
                        >
                          Лише в дорозі
                        </ion-toggle>
                      </ion-item>
                    </ion-item-group>
                  </ion-list>

                  @if (transfers().length) {
                    <ion-list [inset]="true" data-testid="transfers">
                      <ion-list-header><ion-label>Передачі від гуртків · {{ transfers().length }}</ion-label></ion-list-header>
                      <ion-item-group>
                        @for (transfer of transfers(); track transfer.duesEntryKey) {
                          <ion-item
                            [button]="d.viewer.canKeep"
                            [detail]="false"
                            [disabled]="busy() === transfer.duesEntryKey"
                            (click)="d.viewer.canKeep && transferActions(transfer)"
                            data-testid="transfer"
                          >
                            <ion-label class="ion-text-wrap">
                              <h3>
                                {{ transfer.groupName }}
                                @if (transfer.isReceived) {
                                  <ion-badge class="lk-tag--info">отримано</ion-badge>
                                } @else {
                                  <ion-badge class="lk-tag--secondary">в дорозі</ion-badge>
                                }
                              </h3>
                              <p>{{ transferLine(transfer) }}</p>
                              @if (transfer.note) { <p>{{ transfer.note }}</p> }
                            </ion-label>
                            <span slot="end" class="money ok">+{{ money(transfer.amount) }}</span>
                          </ion-item>
                        }
                      </ion-item-group>
                    </ion-list>
                  } @else {
                    <p class="note">
                      {{
                        d.transfers.length
                          ? 'Нічого не знайшлось. Усе отримано — або спробуй інший гурток.'
                          : 'Передач ще не було. Коли гурток передасть гроші курінному, це зʼявиться тут.'
                      }}
                    </p>
                  }
                }

                @case ('history') {
                  <ion-list [inset]="true">
                    <ion-item-group>
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
                          @for (quarter of allQuarters(); track key(quarter)) {
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
                          @for (kind of allKinds; track kind) {
                            <ion-select-option [value]="kind">{{ kindLabels[kind] }}</ion-select-option>
                          }
                        </ion-select>
                      </ion-item>
                    </ion-item-group>
                  </ion-list>

                  @if (entries().length) {
                    <ion-list [inset]="true">
                      <ion-list-header><ion-label>Операції куреня · {{ entries().length }}</ion-label></ion-list-header>
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
                                {{ kindLabels[entry.kind] }}
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
                      {{
                        d.entries.length
                          ? 'Нічого не знайшлось. Спробуй інший квартал або вид операції.'
                          : 'Курінна каса ще порожня. Перша записана операція — і історія почнеться.'
                      }}
                    </p>
                  }
                }
              }
            }
          }
        }
      }

      <ion-modal [isOpen]="entryOpen()" [canDismiss]="!saving()" (didDismiss)="entryOpen.set(false)">
        <ng-template>
          <app-dues-entry-form
            [currency]="currency()"
            [kinds]="kinds()"
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
        [isOpen]="rateOpen()"
        [canDismiss]="!saving()"
        [initialBreakpoint]="0.75"
        [breakpoints]="[0, 0.75, 1]"
        (didDismiss)="rateOpen.set(false)"
      >
        <ng-template>
          @if (data(); as d) {
            <app-dues-kurin-rate-form
              [upstream]="d.upstream"
              [currency]="currency()"
              [rates]="d.rates"
              [quarters]="fromQuarters()"
              [current]="d.currentQuarter"
              [saving]="saving()"
              [error]="formError()"
              (save)="saveRate($event)"
              (cancel)="rateOpen.set(false)"
            />
          }
        </ng-template>
      </ion-modal>
    </ion-content>
  `,
})
export class KurinDuesPage implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly glass = new GlassEffects(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);

  protected readonly kurinKey = inject(AuthService).user()?.kurinKey ?? null;
  protected readonly failedText = FAILED_TEXT;
  /** iOS names where back leads; Material shows the arrow alone. */
  protected readonly backText = inject(Config).get('mode') === 'ios' ? 'Курінь' : undefined;
  protected readonly upstream = computed(() => this.data()?.upstream ?? 'stanytsia');
  protected readonly kinds = computed(() => kurinKinds(this.upstream()));
  /** The history filter keeps every kind: old operations stay whatever the край takes now. */
  protected readonly allKinds = KURIN_KINDS;
  protected readonly kindLabels = KIND_LABELS;
  /** What this kurin counts its вкладка in, from its край's profile. */
  protected readonly currency = computed(() => this.data()?.currency ?? DEFAULT_CURRENCY);
  protected readonly numberLocale = computed(() => this.data()?.numberLocale ?? DEFAULT_NUMBER_LOCALE);
  protected readonly money = (amount: number): string => duesMoney(amount, this.currency(), this.numberLocale());
  protected readonly amount = (entry: DuesEntryDto): string => amountLabel(entry, this.currency(), this.numberLocale());
  protected readonly signed = signedAmount;
  protected readonly key = quarterKey;

  protected readonly loaded = signal<Loaded<KurinDuesDto>>({ state: 'loading' });
  /** «Справи» opens straight on the transfers (`?part=transfers`). */
  protected readonly part = signal<Part>('box');
  protected readonly quarterChoice = signal<string | null>(null);
  protected readonly groupFilter = signal<string | null>(null);
  protected readonly pendingOnly = signal(false);
  protected readonly kindFilter = signal<DuesEntryKind | 'all'>('all');
  protected readonly quarterFilter = signal<string>('all');

  protected readonly denied = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly entryOpen = signal(false);
  protected readonly entryToEdit = signal<DuesEntryDto | null>(null);
  protected readonly rateOpen = signal(false);

  protected readonly data = computed(() => valueOf(this.loaded()));
  protected readonly allQuarters = computed(() => allQuarters(this.data()?.years ?? []));
  protected readonly fromQuarters = computed(() => {
    const d = this.data();
    return d ? fromQuarterOptions(d.years, d.currentQuarter) : [];
  });
  protected readonly rateNow = computed(() => {
    const d = this.data();
    return d ? rateAt(d.rates, d.currentQuarter) : null;
  });
  protected readonly ratesText = computed(() => kurinRatesText(this.rateNow(), this.currency(), this.numberLocale()));
  protected readonly standardArticles = computed(() => articlesAreStandard(this.rateNow()));
  /** The web's: the край takes money straight, or some of it is still here or was sent before. */
  protected readonly hasKrai = computed(() => {
    const d = this.data();
    return !!d && (paysKrai(this.upstream()) || d.toKrai !== 0 || d.sentToKrai !== 0 || d.rates.some((r) => r.kraiFull > 0));
  });
  /** The same for the станиця, which a край paid straight does not have. */
  protected readonly hasStanytsia = computed(() => {
    const d = this.data();
    return !!d && (paysStanytsia(this.upstream()) || d.toStanytsia !== 0 || d.sentToStanytsia !== 0 || d.rates.some((r) => r.stanytsiaFull > 0));
  });
  protected readonly pending = computed(() => (this.data()?.transfers ?? []).filter((t) => !t.isReceived));
  protected readonly outstanding = computed(() => (this.data()?.groups ?? []).reduce((sum, g) => sum + g.outstanding, 0));

  /** Quarters somebody was charged in, newest first: the web shows no row for the others. */
  protected readonly quarters = computed(() => [...(this.data()?.quarters ?? [])].reverse());
  /** The quarter the гуртки list shows: the chosen one, else now, else the latest. */
  protected readonly quarter = computed(() => {
    const d = this.data();
    const quarters = this.quarters();
    if (!d) return null;
    const choice = this.quarterChoice();
    return (
      quarters.find((q) => quarterKey(q.quarter) === choice) ??
      quarters.find((q) => sameQuarter(q.quarter, d.currentQuarter)) ??
      quarters[0] ??
      null
    );
  });
  protected readonly quarterValue = computed(() => {
    const row = this.quarter();
    return row ? quarterKey(row.quarter) : null;
  });

  protected readonly transfers = computed(() => {
    const group = this.groupFilter();
    const pendingOnly = this.pendingOnly();
    return (this.data()?.transfers ?? []).filter((t) => (!group || t.groupKey === group) && (!pendingOnly || !t.isReceived));
  });
  protected readonly groupFilterName = computed(() => {
    const key = this.groupFilter();
    return key ? (this.data()?.groups.find((g) => g.groupKey === key)?.groupName ?? null) : null;
  });

  protected readonly entries = computed(() => {
    const kind = this.kindFilter();
    const quarter = this.quarterFilter();
    return (this.data()?.entries ?? []).filter(
      (entry) =>
        (kind === 'all' || entry.kind === kind) && (quarter === 'all' || quarterKey(quarterOfDate(entry.occurredOn)) === quarter),
    );
  });

  protected readonly subline = computed(() => {
    const d = this.data();
    if (!d) return '';
    const pending = this.pending().length;
    return [`зараз ${quarterLabel(d.currentQuarter)}`, `гуртків: ${d.groups.length}`, pending ? `чекають підтвердження: ${pending}` : '']
      .filter(Boolean)
      .join(' · ');
  });

  constructor() {
    addIcons({ add, lockClosed });
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const part = params.get('part') as Part | null;
      if (part && PARTS.includes(part)) this.part.set(part);
    });
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

  protected handoverLine(group: KurinGroupHandoverDto): string {
    const confirmed = `підтверджено ${this.money(group.received)}`;
    return group.inTransit > 0 ? `${confirmed} · в дорозі ${this.money(group.inTransit)}` : confirmed;
  }

  protected quarterLine(row: KurinDuesQuarterGroupDto): string {
    return `Юнаків ${row.youthCount} · здали ${this.money(row.collectedUp)} з ${this.money(row.expectedUp)}`;
  }

  protected transferLine(transfer: DuesTransferDto): string {
    const parts = [shortDate(transfer.occurredOn), methodLabel({ method: transfer.method, counterMethod: null })];
    if (transfer.collectedByName) parts.push(`передав ${transfer.collectedByName}`);
    if (transfer.isReceived && transfer.receivedByName) parts.push(`підтвердив ${transfer.receivedByName}`);
    return parts.join(' · ');
  }

  protected entryLine(entry: DuesEntryDto): string {
    const parts = [shortDate(entry.occurredOn), methodLabel(entry)];
    if (entry.collectedByName) parts.push(entry.collectedByName);
    if (entry.isVerified && entry.verifiedByName) parts.push(`перевірив ${entry.verifiedByName}`);
    return parts.join(' · ');
  }

  /** As the web: the Звʼязковий marks; the keeper edits what is not checked yet. */
  protected hasActions(entry: DuesEntryDto): boolean {
    const viewer = this.data()?.viewer;
    return !!viewer && (viewer.canVerify || (viewer.canKeep && !entry.isVerified));
  }

  protected showPending(): void {
    this.groupFilter.set(null);
    this.pendingOnly.set(true);
    this.part.set('transfers');
  }

  protected openGroup(groupKey: string): void {
    void this.router.navigate(['/tabs/kurin/group', groupKey, 'dues']);
  }

  /** The web's row narrows the transfers to the гурток, its name opens the гурток's box: both here. */
  protected async groupActions(group: KurinGroupHandoverDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: group.groupName,
      buttons: [
        { text: 'Передачі цього гуртка', data: 'transfers' },
        { text: 'Вкладка гуртка', data: 'open' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (data === 'transfers') {
      this.groupFilter.set(group.groupKey);
      this.pendingOnly.set(false);
      this.part.set('transfers');
    }
    if (data === 'open') this.openGroup(group.groupKey);
  }

  protected async transferActions(transfer: DuesTransferDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: `${transfer.groupName} · +${this.money(transfer.amount)}`,
      subHeader: transfer.isReceived && transfer.receivedByName ? `Підтвердив ${transfer.receivedByName}` : undefined,
      buttons: [
        transfer.isReceived ? { text: 'Зняти підтвердження', data: 'undo' } : { text: 'Отримано', data: 'received' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (data === 'received' || data === 'undo') await this.setReceived(transfer, data === 'received');
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
    const sheet = await this.sheets.create({ header: `${KIND_LABELS[entry.kind]} · ${this.amount(entry)}`, buttons });
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
    this.formError.set(null);
    this.rateOpen.set(true);
  }

  protected async saveEntry(request: UpsertDuesEntryRequest): Promise<void> {
    const editing = this.entryToEdit();
    const done = await this.write(
      () =>
        editing
          ? this.api.put(`${this.base()}/entries/${editing.duesEntryKey}`, request)
          : this.api.post(`${this.base()}/entries`, request),
      'Не вдалося записати. Спробуй ще раз.',
    );
    if (!done) return;
    this.entryOpen.set(false);
    await this.toasts.show(editing ? 'Збережено' : 'Записано');
  }

  protected async saveRate(request: SetKurinDuesRateRequest): Promise<void> {
    const done = await this.write(() => this.api.put(`${this.base()}/rate`, request), 'Не вдалося зберегти. Спробуй ще раз.');
    if (!done) return;
    this.rateOpen.set(false);
    await this.toasts.show('Збережено');
  }

  private base(): string {
    return `kurin/${this.kurinKey}/dues`;
  }

  private async setReceived(transfer: DuesTransferDto, isReceived: boolean): Promise<void> {
    await this.mark(
      transfer.duesEntryKey,
      () => this.api.put(`${this.base()}/transfers/${transfer.duesEntryKey}/received`, { isReceived }),
      'Не вдалося позначити. Спробуй ще раз.',
    );
  }

  private async setVerified(entry: DuesEntryDto, isVerified: boolean): Promise<void> {
    await this.mark(
      entry.duesEntryKey,
      () => this.api.put(`${this.base()}/entries/${entry.duesEntryKey}/verified`, { isVerified }),
      'Не вдалося позначити. Спробуй ще раз.',
    );
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
    const done = await this.mark(
      entry.duesEntryKey,
      () => this.api.delete(`${this.base()}/entries/${entry.duesEntryKey}`),
      'Не вдалося видалити. Спробуй ще раз.',
    );
    if (done) await this.toasts.show('Видалено');
  }

  /** One write from a row: the row waits, a failure is a toast. */
  private async mark(key: string, call: () => Promise<unknown>, fallback: string): Promise<boolean> {
    this.busy.set(key);
    try {
      await call();
      await this.load();
      return true;
    } catch (error) {
      await this.toasts.show(apiErrorText(error, fallback, DUES_ERRORS), 'danger');
      return false;
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
    if (!this.kurinKey) return;
    const request = this.api.get<KurinDuesDto>(this.base());
    request.then(
      () => this.denied.set(false),
      (error: unknown) => this.denied.set(error instanceof HttpErrorResponse && error.status === 403),
    );
    await settle(request, this.loaded);
  }
}
