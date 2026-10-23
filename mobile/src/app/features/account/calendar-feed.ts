import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AlertController, IonItem, IonItemGroup, IonLabel, IonList, IonListHeader, IonNote, IonToggle } from '@ionic/angular';
import { Api, apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { openExternal } from './web-links';

const FAILED = 'Не вдалося зберегти. Спробуй ще раз.';

export type CalendarFeedState = 'None' | 'Active' | 'Revoked';
export type CalendarFeedRevokeReason = 'Reissued' | 'Disabled' | 'SecurityChange' | 'Suspended';

export interface CalendarFeedOptions {
  includeSchedules: boolean;
  includeTasks: boolean;
}

export interface CalendarFeedStatus extends CalendarFeedOptions {
  state: CalendarFeedState;
  createdUtc: string | null;
  lastFetchedUtc: string | null;
  revokedUtc: string | null;
  revokedReason: CalendarFeedRevokeReason | null;
}

interface IssuedCalendarFeed {
  feedUrl: string;
  status: CalendarFeedStatus;
}

/** `webcal://` hands the link to the phone's calendar, which asks to subscribe. */
export function webcalUrlOf(feedUrl: string): string {
  return feedUrl.replace(/^https?:\/\//, 'webcal://');
}

export function googleCalendarUrlOf(feedUrl: string): string {
  return `https://calendar.google.com/calendar/r?cid=${encodeURIComponent(webcalUrlOf(feedUrl))}`;
}

/** Whether the link went off on its own, so the person has to be told; one they switched off is not news. */
export function revokedBySecurity(status: CalendarFeedStatus | null): boolean {
  return status?.state === 'Revoked' && (status.revokedReason === 'SecurityChange' || status.revokedReason === 'Suspended');
}

/**
 * «Календар» on the account page: the web's «Календар у Google, Apple, Outlook» (account settings,
 * calendar-feed-section) as an inset list. The link is shown only right after it is issued — the
 * server keeps its hash alone — so a lost one is reissued.
 */
@Component({
  selector: 'app-calendar-feed',
  imports: [IonList, IonListHeader, IonItemGroup, IonItem, IonLabel, IonNote, IonToggle],
  styles: `
    .footnote {
      margin: -8px 32px 16px;
      font-size: 13px;
      line-height: 18px;
      color: var(--lk-muted);
    }
    :host-context(.md) .footnote {
      margin: 0 20px 16px;
    }
    .link {
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 13px;
      word-break: break-all;
    }
  `,
  template: `
    @if (status(); as current) {
      <ion-list [inset]="true" id="calendar">
        <ion-list-header><ion-label>Календар</ion-label></ion-list-header>
        <ion-item-group>
          @if (revokedBySecurity() && !feedUrl()) {
            <ion-item>
              <ion-label class="ion-text-wrap" color="warning" data-testid="feed-revoked">
                Посилання вимкнено після зміни пароля, пошти чи двофакторного входу. Календар на ньому більше не
                оновлюється: створи нове й додай його знову, а стару підписку видали.
              </ion-label>
            </ion-item>
          }
          <ion-item>
            <ion-label>Статус</ion-label>
            <ion-note slot="end" data-testid="feed-status">{{ isActive() ? 'Підключено' : 'Вимкнено' }}</ion-note>
          </ion-item>
          <ion-item>
            <ion-toggle [checked]="options().includeSchedules" (ionChange)="setOption('includeSchedules', $event.detail.checked)">
              Графіки гуртків
            </ion-toggle>
          </ion-item>
          <ion-item>
            <ion-toggle [checked]="options().includeTasks" (ionChange)="setOption('includeTasks', $event.detail.checked)">
              Задачі з датою
            </ion-toggle>
          </ion-item>
          @if (feedUrl(); as url) {
            <ion-item>
              <ion-label class="ion-text-wrap"><p class="link" data-testid="feed-url">{{ url }}</p></ion-label>
            </ion-item>
            <ion-item [button]="true" [detail]="false" (click)="copy(url)">
              <ion-label color="primary">Скопіювати посилання</ion-label>
            </ion-item>
            <ion-item [button]="true" [detail]="true" (click)="openWebcal(url)">
              <ion-label>Додати в календар телефону</ion-label>
            </ion-item>
            <ion-item [button]="true" [detail]="true" (click)="openGoogle(url)">
              <ion-label>Додати в Google Calendar</ion-label>
            </ion-item>
          }
          <ion-item [button]="true" [detail]="false" [disabled]="busy()" (click)="issue()" data-testid="feed-issue">
            <ion-label color="primary">{{ isActive() ? 'Створити нове посилання' : 'Створити посилання' }}</ion-label>
          </ion-item>
          @if (isActive()) {
            <ion-item [button]="true" [detail]="false" [disabled]="busy()" (click)="disable()">
              <ion-label color="danger">Вимкнути посилання</ion-label>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>
      <p class="footnote">
        @if (feedUrl()) {
          Посилання показується лише зараз. Google оновлює підписки раз на кілька годин; Notion Calendar бере календар
          із Google.
        } @else {
          Твої події з Лілейки у Google, Apple чи Outlook. Посилання одне на всі курені й оновлюється само.
        }
      </p>
    }
  `,
})
export class CalendarFeedComponent implements OnInit {
  private readonly api = inject(Api);
  private readonly alerts = inject(AlertController);
  private readonly toasts = inject(Toasts);

  protected readonly status = signal<CalendarFeedStatus | null>(null);
  protected readonly feedUrl = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly options = signal<CalendarFeedOptions>({ includeSchedules: false, includeTasks: false });
  protected readonly isActive = computed(() => this.status()?.state === 'Active');
  protected readonly revokedBySecurity = computed(() => revokedBySecurity(this.status()));

  async ngOnInit(): Promise<void> {
    try {
      this.show(await this.api.get<CalendarFeedStatus>('me/calendar-feed'));
    } catch {
      // The rest of the account page still works; the section just stays away.
    }
  }

  protected async setOption(name: keyof CalendarFeedOptions, value: boolean): Promise<void> {
    this.options.update((current) => ({ ...current, [name]: value }));
    if (!this.isActive()) return;
    try {
      this.show(await this.api.patch<CalendarFeedStatus>('me/calendar-feed', this.options()));
      await this.toasts.show('Збережено. Календар підхопить зміни під час наступного оновлення.');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, FAILED), 'danger');
    }
  }

  protected async issue(): Promise<void> {
    if (this.isActive() && !(await this.confirm('Створити нове посилання?', 'Старе перестане оновлюватися. Його доведеться видалити з календаря й додати нове.', 'Створити'))) {
      return;
    }
    this.busy.set(true);
    try {
      const issued = await this.api.post<IssuedCalendarFeed>('me/calendar-feed', this.options());
      this.feedUrl.set(issued.feedUrl);
      this.show(issued.status);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, FAILED), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  protected async disable(): Promise<void> {
    if (!(await this.confirm('Вимкнути посилання?', 'Календар, підписаний на нього, перестане отримувати події з Лілейки.', 'Вимкнути'))) {
      return;
    }
    this.busy.set(true);
    try {
      this.feedUrl.set(null);
      this.show(await this.api.delete<CalendarFeedStatus>('me/calendar-feed'));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, FAILED), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  protected async copy(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      await this.toasts.show('Посилання скопійовано');
    } catch {
      await this.toasts.show('Не вдалося скопіювати. Виділи посилання й скопіюй вручну.', 'danger');
    }
  }

  protected openWebcal(url: string): void {
    globalThis.location.href = webcalUrlOf(url);
  }

  protected openGoogle(url: string): void {
    openExternal(googleCalendarUrlOf(url));
  }

  private async confirm(header: string, message: string, accept: string): Promise<boolean> {
    const alert = await this.alerts.create({
      header,
      message,
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        { text: accept, role: 'confirm' },
      ],
    });
    await alert.present();
    return (await alert.onDidDismiss()).role === 'confirm';
  }

  private show(status: CalendarFeedStatus): void {
    this.status.set(status);
    if (status.state === 'Active') {
      this.options.set({ includeSchedules: status.includeSchedules, includeTasks: status.includeTasks });
    }
  }
}
