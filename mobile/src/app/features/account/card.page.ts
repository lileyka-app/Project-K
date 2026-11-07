import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import {
  AlertController,
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSegment,
  IonSegmentButton,
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import { HttpErrorResponse } from '@angular/common/http';
import { Api, apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';

/** What a shared card may show besides the name — flags, as the API sends them (web CARD_FIELD). */
export const CARD_FIELDS: { bit: number; label: string }[] = [
  { bit: 1, label: 'Фото' },
  { bit: 2, label: 'Курінь і гурток' },
  { bit: 4, label: 'Ступінь' },
  { bit: 8, label: 'Відзначення' },
  { bit: 16, label: 'Вмілості' },
  { bit: 32, label: 'Проби' },
];

export interface MyCardDto {
  allowance: { mayShare: boolean; isAdult: boolean; maxFields: number; mayLink: boolean };
  fields: number;
  linkUrl: string | null;
  linkIssuedAtUtc: string | null;
}

export interface CardCodeDto {
  url: string;
  qrCode: string;
  expiresAtUtc: string | null;
}

/** A fresh code a little before the old one stops working (web card-check-dialog). */
const REFRESH_BEFORE_MS = 20_000;

type Part = 'check' | 'transfer' | 'settings';

/**
 * «Моя картка» on a phone (web card-check-dialog, transfer-code-dialog and the card section of the
 * account settings in one place): a QR to be checked by, replaced on its own every few minutes; the
 * transfer code as a QR the Звʼязковий of the new kurin scans; and what the card shows.
 */
@Component({
  selector: 'app-card',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonSegment,
    IonSegmentButton,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonToggle,
    IonButton,
    IonSpinner,
  ],
  styles: `
    .qr {
      display: grid;
      gap: 12px;
      justify-items: center;
      padding: 24px 16px 8px;
      text-align: center;
    }
    .qr img {
      aspect-ratio: 1;
      background: #fff;
      border-radius: 16px;
      max-width: 260px;
      padding: 8px;
      width: 80%;
    }
    .qr .code {
      font-size: 1.5rem;
      font-variant-numeric: tabular-nums;
      font-weight: 800;
      letter-spacing: 0.08em;
      user-select: all;
    }
    .qr p {
      color: var(--ion-color-medium);
      margin: 0;
      max-width: 22rem;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/more" text="" /></ion-buttons>
        <ion-title>Моя картка</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <ion-segment [value]="part()" (ionChange)="setPart($any($event).detail.value)">
        <ion-segment-button value="check"><ion-label>Перевірка</ion-label></ion-segment-button>
        <ion-segment-button value="transfer"><ion-label>Перехід</ion-label></ion-segment-button>
        <ion-segment-button value="settings"><ion-label>Що видно</ion-label></ion-segment-button>
      </ion-segment>

      @switch (part()) {
        @case ('check') {
          <div class="qr" data-testid="check">
            @if (notAllowed()) {
              <p>Курінь ще не дозволив показувати картку.</p>
            } @else if (check(); as code) {
              <img [src]="code.qrCode" alt="QR-код картки" data-testid="card-qr" />
              <ion-note>Новий QR за {{ countdown() }}</ion-note>
            } @else {
              <ion-spinner name="crescent" />
            }
          </div>
        }
        @case ('transfer') {
          <div class="qr" data-testid="transfer">
            @if (transfer(); as code) {
              <img [src]="code.qrCode" alt="QR-код для переходу" data-testid="transfer-qr" />
              <span class="code" data-testid="transfer-code-value">{{ transferCode() }}</span>
              <ion-button fill="outline" (click)="copy(transferCode())">Скопіювати</ion-button>
            } @else {
              <ion-spinner name="crescent" />
            }
          </div>
        }
        @case ('settings') {
          @if (card(); as c) {
            @if (!c.allowance.mayShare) {
              <ion-list [inset]="true">
                <ion-item><ion-label class="ion-text-wrap"><p>Курінь ще не дозволив показувати картку.</p></ion-label></ion-item>
              </ion-list>
            } @else {
              <ion-list [inset]="true" data-testid="fields">
                <ion-list-header><ion-label>Що видно на картці</ion-label></ion-list-header>
                <ion-item-group>
                  @for (field of fields; track field.bit) {
                    <ion-item>
                      <ion-toggle
                        [checked]="shown(field.bit)"
                        [disabled]="!allowed(field.bit)"
                        (ionChange)="toggle(field.bit, $any($event).detail.checked)"
                        [attr.data-testid]="'field-' + field.bit"
                        >{{ field.label }}</ion-toggle
                      >
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>

              @if (c.allowance.mayLink) {
              <ion-list [inset]="true" data-testid="link">
                <ion-list-header><ion-label>Постійне посилання</ion-label></ion-list-header>
                <ion-item-group>
                  @if (c.linkUrl; as url) {
                    <ion-item [button]="true" [detail]="false" (click)="share(url)">
                      <ion-label color="primary">Поділитися посиланням</ion-label>
                    </ion-item>
                    <ion-item [button]="true" [detail]="false" (click)="disableLink()">
                      <ion-label color="danger">Вимкнути</ion-label>
                    </ion-item>
                  } @else {
                    <ion-item [button]="true" [detail]="false" (click)="issueLink()" data-testid="issue-link">
                      <ion-label color="primary">Створити посилання</ion-label>
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
              }
            }
          }
        }
      }
    </ion-content>
  `,
})
export class CardPage implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  private readonly toasts = inject(Toasts);
  private readonly alerts = inject(AlertController);
  private readonly route = inject(ActivatedRoute);

  protected readonly fields = CARD_FIELDS;
  protected readonly part = signal<Part>('check');
  protected readonly check = signal<CardCodeDto | null>(null);
  protected readonly notAllowed = signal(false);
  protected readonly transfer = signal<CardCodeDto | null>(null);
  protected readonly card = signal<MyCardDto | null>(null);
  private readonly now = signal(Date.now());
  protected readonly countdown = computed(() => {
    const expires = this.check()?.expiresAtUtc;
    const left = expires ? Math.max(0, Math.round((Date.parse(expires) - REFRESH_BEFORE_MS - this.now()) / 1000)) : 0;
    return `${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')}`;
  });
  protected readonly transferCode = computed(() => {
    const url = this.transfer()?.url;
    return url ? (new URL(url).searchParams.get('code') ?? '') : '';
  });

  private timer: ReturnType<typeof setInterval> | null = null;
  private loadingCheck = false;

  ngOnInit(): void {
    const part = this.route.snapshot.queryParamMap.get('part');
    this.setPart(part === 'transfer' || part === 'settings' ? part : 'check');
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      const expires = this.check()?.expiresAtUtc;
      if (this.part() === 'check' && expires && Date.parse(expires) - Date.now() < REFRESH_BEFORE_MS) void this.loadCheck();
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  protected setPart(part: Part): void {
    this.part.set(part);
    if (part === 'check' && !this.check()) void this.loadCheck();
    if (part === 'transfer' && !this.transfer()) void this.loadTransfer();
    if (part === 'settings' && !this.card()) void this.loadCard();
  }

  protected allowed(bit: number): boolean {
    return ((this.card()?.allowance.maxFields ?? 0) & bit) !== 0;
  }

  protected shown(bit: number): boolean {
    return this.allowed(bit) && ((this.card()?.fields ?? 0) & bit) !== 0;
  }

  protected async toggle(bit: number, on: boolean): Promise<void> {
    const card = this.card();
    if (!card) return;
    const fields = on ? card.fields | bit : card.fields & ~bit;
    try {
      this.card.set(await this.api.put<MyCardDto>('me/card/fields', { fields }));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.'), 'danger');
    }
  }

  protected async issueLink(): Promise<void> {
    try {
      this.card.set(await this.api.post<MyCardDto>('me/card/link'));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося створити посилання.'), 'danger');
    }
  }

  protected async disableLink(): Promise<void> {
    const alert = await this.alerts.create({
      header: 'Вимкнути посилання?',
      message: 'Хто має посилання на картку, більше її не побачить.',
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        { text: 'Вимкнути', role: 'destructive', handler: () => void this.doDisable() },
      ],
    });
    await alert.present();
  }

  protected async share(url: string): Promise<void> {
    if (navigator.share) {
      try {
        await navigator.share({ title: 'Моя картка в Лілейці', url });
        return;
      } catch {
        return;
      }
    }
    await this.copy(url);
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      await this.toasts.show('Скопійовано.');
    } catch {
      await this.toasts.show(`Не вдалося скопіювати: ${text}`, 'danger');
    }
  }

  private async doDisable(): Promise<void> {
    try {
      this.card.set(await this.api.delete<MyCardDto>('me/card/link'));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося вимкнути.'), 'danger');
    }
  }

  private async loadCheck(): Promise<void> {
    if (this.loadingCheck) return;
    this.loadingCheck = true;
    try {
      this.check.set(await this.api.get<CardCodeDto>('me/card/check-code'));
      this.notAllowed.set(false);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) this.notAllowed.set(true);
      else await this.toasts.show(apiErrorText(error, 'QR не завантажився. Спробуй ще раз.'), 'danger');
    } finally {
      this.loadingCheck = false;
    }
  }

  private async loadTransfer(): Promise<void> {
    try {
      this.transfer.set(await this.api.get<CardCodeDto>('me/transfer-code'));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Код не завантажився. Спробуй ще раз.'), 'danger');
    }
  }

  private async loadCard(): Promise<void> {
    try {
      this.card.set(await this.api.get<MyCardDto>('me/card'));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося завантажити.'), 'danger');
    }
  }
}
