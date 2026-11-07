import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActionSheetController, IonBadge, IonButton, IonIcon } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { chevronDown, notificationsOutline } from 'ionicons/icons';
import { apiErrorText } from '../core/api';
import { Toasts } from '../core/toast';
import { kurinLabel, kurinShortLabel } from '../features/account/account.labels';
import { NotificationsService } from '../features/leader/notifications.service';
import { KurinScopes } from './kurin-scopes.service';
import { kurinName } from '../core/kurin-label';

/**
 * The right side of the web's toolbar, on every tab's first screen: the kurin switcher (only with
 * more than one kurin, as on the web) and the bell. Goes inside `<ion-buttons slot="end">`.
 */
@Component({
  selector: 'app-header-actions',
  imports: [IonButton, IonIcon, IonBadge, RouterLink],
  host: { style: 'display: contents' },
  styles: `
    .kurin {
      --padding-start: 8px;
      --padding-end: 6px;
      font-size: 15px;
      font-weight: 600;
    }
    .kurin ion-icon {
      font-size: 14px;
      margin-inline-start: 2px;
    }
    .bell {
      position: relative;
      overflow: visible;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    /* The badge hangs off the bell's top corner, not the button's: on Android the button is wider
       than the glyph, and a badge pinned to its edge sat on top of the bell. */
    .bell__glyph {
      position: relative;
      display: inline-flex;
    }
    .bell__glyph ion-icon {
      font-size: 24px;
    }
    .bell ion-badge {
      position: absolute;
      top: -7px;
      inset-inline-start: 13px;
      min-width: 17px;
      height: 17px;
      padding: 2px 4px;
      border-radius: 9px;
      font-size: 10px;
      line-height: 13px;
      pointer-events: none;
    }
  `,
  template: `
    @if (scopes.options().length > 1 && scopes.current(); as current) {
      <ion-button
        class="kurin"
        [disabled]="switching()"
        (click)="chooseKurin()"
        data-testid="kurin-switcher"
        [attr.aria-label]="'Курінь: ' + label(current) + '. Змінити'"
      >
        {{ label(current) }}
        <ion-icon name="chevron-down" aria-hidden="true" />
      </ion-button>
    }
    <ion-button class="bell" routerLink="/tabs/home/notifications" data-testid="bell">
      <span class="bell__glyph" slot="icon-only">
        <ion-icon name="notifications-outline" aria-hidden="true" />
        @if (unread() > 0) {
          <ion-badge color="danger" aria-hidden="true">{{ unread() > 99 ? '99+' : unread() }}</ion-badge>
        }
      </span>
      <span class="sr-only">{{ bellLabel() }}</span>
    </ion-button>
  `,
})
export class HeaderActions implements OnInit {
  protected readonly scopes = inject(KurinScopes);
  private readonly notifications = inject(NotificationsService);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  protected readonly label = kurinShortLabel;
  protected readonly switching = signal(false);
  protected readonly unread = this.notifications.unread;
  protected readonly bellLabel = computed(() =>
    this.unread() > 0 ? `Сповіщення, непрочитаних: ${this.unread()}` : 'Сповіщення',
  );

  constructor() {
    addIcons({ chevronDown, notificationsOutline });
  }

  ngOnInit(): void {
    void this.scopes.load();
    void this.notifications.refreshUnread();
    this.notifications.watch();
  }

  /** As the web's switcher: every kurin, the current one marked; another one starts the app again there. */
  protected async chooseKurin(): Promise<void> {
    const current = this.scopes.current();
    const sheet = await this.sheets.create({
      header: 'Курінь',
      buttons: [
        ...this.scopes.options().map((option) => ({
          text: option === current ? `${kurinLabel(option)} ✓` : kurinLabel(option),
          data: option,
        })),
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (!data || data === current) return;
    this.switching.set(true);
    try {
      await this.scopes.switchTo(data.kurinKey, 'tabs/home');
    } catch (error) {
      this.switching.set(false);
      await this.toasts.show(apiErrorText(error, `${kurinName(data.kurinNumber, data.branch)} лишився недосяжним. Спробуй ще раз.`), 'danger');
    }
  }
}
