import { Component, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AlertController,
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonModal,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { CountryCode } from 'libphonenumber-js';
import { addIcons } from 'ionicons';
import { checkmarkCircle, ellipseOutline } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { DEFAULT_PHONE_COUNTRY, formatPhone, toStoredPhone } from '../../core/phone';
import { Toasts } from '../../core/toast';
import { PASSWORD_RULES, isPrivileged, passwordMeetsRules } from './account.labels';
import { AccountSettings } from './account.models';
import { AccountService } from './account.service';
import { CalendarFeedComponent } from './calendar-feed';
import { KurinApi } from '../kurin/kurin.service';

const CONTACT_ERRORS: Record<string, string> = {
  InvalidEmail: 'Перевір email.',
  EmailAlreadyInUse: 'Цей email уже має інший акаунт.',
  InvalidCredentials: 'Неправильний поточний пароль.',
};
const PASSWORD_ERRORS: Record<string, string> = {
  PasswordChangeFailed: 'Не вдалося змінити пароль. Перевір поточний пароль і вимоги до нового.',
};
const MFA_ERRORS: Record<string, string> = {
  InvalidCredentials: 'Неправильний поточний пароль.',
  MfaRequired: 'Для твоєї ролі двофакторний вхід обовʼязковий: його можна лише скинути.',
  MfaNotEnabled: 'Двофакторний вхід уже вимкнено.',
};

type Sheet = 'contacts' | 'password' | 'codes' | null;

/**
 * The web's «Налаштування акаунта» as an iOS Settings page: contacts, password and the second
 * factor. Each edit opens a sheet with «Скасувати» and «Зберегти»; actions that need the password
 * (codes, switching the second factor off) ask for it in an alert, as iOS does.
 */
@Component({
  selector: 'app-account',
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
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonInput,
    IonIcon,
    IonModal,
    IonSkeletonText,
    IonSpinner,
    CalendarFeedComponent,
  ],
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
    .failed {
      padding: 16px 20px;
      color: var(--lk-muted);
    }
    .fields {
      display: grid;
      gap: 16px;
      padding: 16px;
    }
    .hint {
      margin: 0;
      font-size: 13px;
      color: var(--lk-muted);
    }
    .rules {
      display: grid;
      gap: 4px;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 13px;
      color: var(--lk-muted);
    }
    .rules li {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .rules .met {
      color: var(--lk-primary);
    }
    .codes {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
      padding: 8px 20px 16px;
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 16px;
      color: var(--lk-ink);
    }
    .actions {
      display: grid;
      gap: 8px;
      padding: 4px 16px 24px;
    }
    .center {
      text-align: center;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/more" text="Меню" /></ion-buttons>
        <ion-title>Акаунт</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @switch (settings().state) {
        @case ('loading') {
          <ion-list [inset]="true">
            <ion-item-group>
              @for (row of [1, 2, 3]; track row) {
                <ion-item><ion-skeleton-text [animated]="true" style="width: 70%" /></ion-item>
              }
            </ion-item-group>
          </ion-list>
        }
        @case ('failed') {
          <p class="failed">{{ failedText }}</p>
        }
        @default {
          @if (account(); as me) {
            <ion-list [inset]="true">
              <ion-list-header><ion-label>Контакти</ion-label></ion-list-header>
              <ion-item-group>
                <ion-item [button]="true" [detail]="true" (click)="openContacts()" data-testid="email-row">
                  <ion-label>Email</ion-label>
                  <ion-note slot="end">{{ me.email }}</ion-note>
                </ion-item>
                @if (me.pendingEmail) {
                  <ion-item>
                    <ion-label class="ion-text-wrap">
                      <p>Очікує підтвердження: <strong>{{ me.pendingEmail }}</strong></p>
                    </ion-label>
                  </ion-item>
                }
                <ion-item [button]="true" [detail]="true" (click)="openContacts()">
                  <ion-label>Телефон</ion-label>
                  <ion-note slot="end">{{ formatPhone(me.phoneNumber) || 'не вказано' }}</ion-note>
                </ion-item>
              </ion-item-group>
            </ion-list>
            <p class="footnote">
              @if (me.memberKey) {
                Телефон оновлюється одразу і в твоїй картці учасника. Email зміниться тільки після підтвердження з нової адреси.
              } @else {
                Ці дані належать тільки акаунту, бо картки учасника в нього немає.
              }
            </p>

            <ion-list [inset]="true">
              <ion-list-header><ion-label>Пароль</ion-label></ion-list-header>
              <ion-item-group>
                <ion-item [button]="true" [detail]="true" (click)="openPassword()">
                  <ion-label>Змінити пароль</ion-label>
                </ion-item>
              </ion-item-group>
            </ion-list>

            <ion-list [inset]="true">
              <ion-list-header><ion-label>Двофакторний вхід</ion-label></ion-list-header>
              <ion-item-group>
                <ion-item>
                  <ion-label>Статус</ion-label>
                  <ion-note slot="end" data-testid="mfa-status">{{ me.twoFactorEnabled ? 'Увімкнено' : 'Вимкнено' }}</ion-note>
                </ion-item>
                @if (me.twoFactorEnabled) {
                  <ion-item [button]="true" [detail]="false" [disabled]="busy()" (click)="rotateCodes()">
                    <ion-label color="primary">Оновити резервні коди</ion-label>
                  </ion-item>
                  <ion-item [button]="true" [detail]="false" [disabled]="busy()" (click)="turnOff()">
                    <ion-label color="danger">
                      {{ privileged() ? 'Скинути двофакторний вхід' : 'Вимкнути двофакторний вхід' }}
                    </ion-label>
                  </ion-item>
                } @else {
                  <ion-item [button]="true" [detail]="true" (click)="turnOn()">
                    <ion-label>Увімкнути</ion-label>
                  </ion-item>
                }
              </ion-item-group>
            </ion-list>
            <p class="footnote">
              @if (me.twoFactorEnabled && privileged()) {
                Для твоєї ролі двофакторний вхід обовʼязковий. Скинути можна, щоб налаштувати його на іншому пристрої.
              } @else if (me.twoFactorEnabled) {
                Під час входу Лілейка питає код із застосунку-автентифікатора.
              } @else if (privileged()) {
                Для твоєї ролі двофакторний вхід обовʼязковий. Налаштуй застосунок-автентифікатор, щоб акаунт лишався захищеним.
              } @else {
                Можеш увімкнути його для додаткового захисту акаунта.
              }
            </p>

            <app-calendar-feed />
          }
        }
      }

      <!-- Contacts -->
      <ion-modal [isOpen]="sheet() === 'contacts'" [presentingElement]="presenting()" (didDismiss)="closeSheet()">
        <ng-template>
          <ion-header>
            <ion-toolbar>
              <ion-buttons slot="start"><ion-button (click)="closeSheet()">Скасувати</ion-button></ion-buttons>
              <ion-title>Контакти</ion-title>
              <ion-buttons slot="end">
                <ion-button [strong]="true" [disabled]="!canSaveContacts()" (click)="saveContacts()" data-testid="save-contacts">
                  @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
                </ion-button>
              </ion-buttons>
            </ion-toolbar>
          </ion-header>
          <ion-content>
            <form class="fields" (submit)="$event.preventDefault(); saveContacts()">
              <div class="lk-field">
                <span class="lk-field__label">Email</span>
                <div class="lk-input-box">
                  <ion-input
                    data-testid="contact-email"
                    aria-label="Email"
                    type="email"
                    inputmode="email"
                    autocomplete="email"
                    autocapitalize="off"
                    [value]="email()"
                    (ionInput)="email.set(text($event))"
                  />
                </div>
              </div>
              <div class="lk-field">
                <span class="lk-field__label">Телефон</span>
                <div class="lk-input-box">
                  <ion-input
                    data-testid="contact-phone"
                    aria-label="Телефон"
                    type="tel"
                    inputmode="tel"
                    autocomplete="tel"
                    [value]="phone()"
                    (ionInput)="phone.set(text($event))"
                  />
                </div>
              </div>
              @if (emailChanged()) {
                <div class="lk-field">
                  <span class="lk-field__label">Поточний пароль</span>
                  <div class="lk-input-box">
                    <ion-input
                      data-testid="contact-password"
                      aria-label="Поточний пароль"
                      type="password"
                      autocomplete="current-password"
                      [value]="contactPassword()"
                      (ionInput)="contactPassword.set(text($event))"
                    />
                  </div>
                  <p class="hint">На нову адресу прийде лист: email зміниться, коли ти перейдеш за посиланням у ньому.</p>
                </div>
              }
              @if (error(); as message) {
                <span class="lk-field__error" role="alert">{{ message }}</span>
              }
              <button type="submit" hidden></button>
            </form>
          </ion-content>
        </ng-template>
      </ion-modal>

      <!-- Password -->
      <ion-modal [isOpen]="sheet() === 'password'" [presentingElement]="presenting()" (didDismiss)="closeSheet()">
        <ng-template>
          <ion-header>
            <ion-toolbar>
              <ion-buttons slot="start"><ion-button (click)="closeSheet()">Скасувати</ion-button></ion-buttons>
              <ion-title>Пароль</ion-title>
              <ion-buttons slot="end">
                <ion-button [strong]="true" [disabled]="!canChangePassword()" (click)="changePassword()" data-testid="save-password">
                  @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
                </ion-button>
              </ion-buttons>
            </ion-toolbar>
          </ion-header>
          <ion-content>
            <form class="fields" (submit)="$event.preventDefault(); changePassword()">
              <div class="lk-field">
                <span class="lk-field__label">Поточний пароль</span>
                <div class="lk-input-box">
                  <ion-input
                    data-testid="password-current"
                    aria-label="Поточний пароль"
                    type="password"
                    autocomplete="current-password"
                    [value]="currentPassword()"
                    (ionInput)="currentPassword.set(text($event))"
                  />
                </div>
              </div>
              <div class="lk-field">
                <span class="lk-field__label">Новий пароль</span>
                <div class="lk-input-box">
                  <ion-input
                    data-testid="password-new"
                    aria-label="Новий пароль"
                    type="password"
                    autocomplete="new-password"
                    [value]="newPassword()"
                    (ionInput)="newPassword.set(text($event))"
                  />
                </div>
                <ul class="rules" aria-live="polite">
                  @for (rule of rules(); track rule.id) {
                    <li [class.met]="rule.met">
                      <ion-icon [name]="rule.met ? 'checkmark-circle' : 'ellipse-outline'" aria-hidden="true" />
                      <span>{{ rule.label }}</span>
                    </li>
                  }
                </ul>
              </div>
              <div class="lk-field">
                <span class="lk-field__label">Повтори новий пароль</span>
                <div class="lk-input-box" [class.lk-invalid]="mismatch()">
                  <ion-input
                    data-testid="password-confirm"
                    aria-label="Повтори новий пароль"
                    type="password"
                    autocomplete="new-password"
                    [value]="confirmPassword()"
                    (ionInput)="confirmPassword.set(text($event))"
                  />
                </div>
                @if (mismatch()) {
                  <span class="lk-field__error">Паролі не збігаються.</span>
                }
              </div>
              @if (error(); as message) {
                <span class="lk-field__error" role="alert">{{ message }}</span>
              }
              <button type="submit" hidden></button>
            </form>
          </ion-content>
        </ng-template>
      </ion-modal>

      <!-- New recovery codes, shown once like mfa-setup does -->
      <ion-modal [isOpen]="sheet() === 'codes'" [presentingElement]="presenting()" (didDismiss)="closeSheet()">
        <ng-template>
          <ion-header>
            <ion-toolbar>
              <ion-title>Резервні коди</ion-title>
              <ion-buttons slot="end"><ion-button [strong]="true" (click)="closeSheet()">Готово</ion-button></ion-buttons>
            </ion-toolbar>
          </ion-header>
          <ion-content>
            <div class="fields">
              <p class="hint">
                Старі коди більше не діють. Кожен новий спрацьовує один раз, якщо застосунок-автентифікатор
                буде недоступний. Збережи їх у надійному місці.
              </p>
            </div>
            <div class="codes" data-testid="recovery-codes">
              @for (item of recoveryCodes(); track item) { <span>{{ item }}</span> }
            </div>
            <div class="actions">
              <ion-button expand="block" fill="outline" (click)="copyCodes()">Скопіювати коди</ion-button>
            </div>
          </ion-content>
        </ng-template>
      </ion-modal>
    </ion-content>
  `,
})
export class AccountPage implements OnInit {
  private readonly service = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly alerts = inject(AlertController);
  private readonly toasts = inject(Toasts);
  private readonly kurins = inject(KurinApi);
  /** Where a number typed without «+» is read: the signed-in kurin's край. */
  private phoneCountry: CountryCode = DEFAULT_PHONE_COUNTRY;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  protected readonly failedText = FAILED_TEXT;

  protected readonly settings = signal<Loaded<AccountSettings>>({ state: 'loading' });
  protected readonly account = computed(() => valueOf(this.settings()));
  protected readonly formatPhone = formatPhone;
  protected readonly privileged = computed(() => isPrivileged(this.auth.user()));
  protected readonly sheet = signal<Sheet>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly email = signal('');
  protected readonly phone = signal('');
  protected readonly contactPassword = signal('');
  protected readonly emailChanged = computed(() => {
    const current = this.account()?.email ?? '';
    return this.email().trim().toLowerCase() !== current.toLowerCase();
  });
  protected readonly canSaveContacts = computed(
    () =>
      !this.busy() &&
      this.email().trim().length > 0 &&
      (!this.emailChanged() || this.contactPassword().length > 0),
  );

  protected readonly currentPassword = signal('');
  protected readonly newPassword = signal('');
  protected readonly confirmPassword = signal('');
  protected readonly rules = computed(() =>
    PASSWORD_RULES.map((rule) => ({ id: rule.id, label: rule.label, met: rule.test(this.newPassword()) })),
  );
  protected readonly mismatch = computed(
    () => this.confirmPassword().length > 0 && this.confirmPassword() !== this.newPassword(),
  );
  protected readonly canChangePassword = computed(
    () =>
      !this.busy() &&
      this.currentPassword().length > 0 &&
      passwordMeetsRules(this.newPassword()) &&
      this.newPassword() === this.confirmPassword(),
  );

  protected readonly recoveryCodes = signal<string[]>([]);

  constructor() {
    addIcons({ checkmarkCircle, ellipseOutline });
  }

  ngOnInit(): void {
    void this.load();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected text(event: Event): string {
    return String((event as CustomEvent<{ value?: string | null }>).detail.value ?? '');
  }

  protected openContacts(): void {
    const me = this.account();
    if (!me) return;
    this.email.set(me.email);
    this.phone.set(formatPhone(me.phoneNumber));
    const kurinKey = this.auth.user()?.kurinKey;
    if (kurinKey) void this.kurins.phoneCountry(kurinKey).then((country) => (this.phoneCountry = country), () => undefined);
    this.contactPassword.set('');
    this.error.set(null);
    this.sheet.set('contacts');
  }

  protected openPassword(): void {
    this.currentPassword.set('');
    this.newPassword.set('');
    this.confirmPassword.set('');
    this.error.set(null);
    this.sheet.set('password');
  }

  /** iOS shows the sheets as cards over the whole tabbed screen, tab bar included. */
  protected presenting(): HTMLElement {
    return this.host.closest<HTMLElement>('app-tabs') ?? this.host;
  }

  protected closeSheet(): void {
    this.sheet.set(null);
  }

  /** The phone as it is sent: +420777123456, or as it was when left untouched. */
  private storedPhone(): string {
    const before = this.account()?.phoneNumber ?? '';
    return this.phone().trim() === formatPhone(before) ? before : toStoredPhone(this.phone(), this.phoneCountry);
  }

  protected async saveContacts(): Promise<void> {
    if (!this.canSaveContacts()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const settings = await this.service.updateProfile({
        email: this.email().trim(),
        phoneNumber: this.storedPhone() || null,
        currentPassword: this.emailChanged() ? this.contactPassword() : null,
      });
      this.settings.set({ state: 'ready', value: settings });
      this.sheet.set(null);
      if (settings.pendingEmail) {
        await this.toasts.show(`Ми надіслали лист на ${settings.pendingEmail}. Email зміниться після підтвердження.`);
      } else {
        this.auth.updateEmail(settings.email);
        await this.toasts.show('Контакти оновлено.');
      }
    } catch (error) {
      this.error.set(apiErrorText(error, 'Не вдалося оновити контакти. Перевір email.', CONTACT_ERRORS));
    } finally {
      this.busy.set(false);
    }
  }

  protected async changePassword(): Promise<void> {
    if (!this.canChangePassword()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.service.changePassword(this.currentPassword(), this.newPassword());
      this.sheet.set(null);
      await this.toasts.show('Пароль змінено.');
    } catch (error) {
      this.error.set(
        apiErrorText(error, 'Не вдалося змінити пароль. Перевір поточний пароль і вимоги до нового.', PASSWORD_ERRORS),
      );
    } finally {
      this.busy.set(false);
    }
  }

  /** The existing setup flow; it comes back to Головна when done. */
  protected async turnOn(): Promise<void> {
    await this.router.navigateByUrl('/mfa');
  }

  protected async rotateCodes(): Promise<void> {
    const password = await this.askPassword(
      'Нові резервні коди',
      'Старі коди перестануть діяти. Введи поточний пароль.',
      'Оновити',
    );
    if (password === null) return;
    this.busy.set(true);
    try {
      this.recoveryCodes.set(await this.auth.rotateRecoveryCodes(password));
      this.sheet.set('codes');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося оновити резервні коди.', MFA_ERRORS), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Провід «Скинути» (and set it up again straight away, the server demands it); everyone else
   * «Вимкнути». The alert is the confirmation: it names the action in red and needs the password.
   */
  protected async turnOff(): Promise<void> {
    const reset = this.privileged();
    const password = await this.askPassword(
      reset ? 'Скинути двофакторний вхід?' : 'Вимкнути двофакторний вхід?',
      reset
        ? 'Поточний ключ автентифікатора перестане діяти, і одразу треба буде налаштувати новий.'
        : 'Під час входу Лілейка більше не питатиме код. Введи поточний пароль.',
      reset ? 'Скинути' : 'Вимкнути',
      true,
    );
    if (password === null) return;
    this.busy.set(true);
    try {
      if (reset) await this.service.resetMfa(password);
      else await this.service.disableMfa(password);
      this.auth.mfaTurnedOff();
      const me = this.account();
      if (me) this.settings.set({ state: 'ready', value: { ...me, twoFactorEnabled: false } });
      if (reset) {
        await this.toasts.show('Двофакторний вхід скинуто. Налаштуй його заново.');
        await this.router.navigateByUrl('/mfa');
      } else {
        await this.toasts.show('Двофакторний вхід вимкнено.');
      }
    } catch (error) {
      const fallback = reset ? 'Не вдалося скинути двофакторний вхід.' : 'Не вдалося вимкнути двофакторний вхід.';
      await this.toasts.show(apiErrorText(error, fallback, MFA_ERRORS), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  protected async copyCodes(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.recoveryCodes().join('\n'));
      await this.toasts.show('Коди скопійовано');
    } catch {
      await this.toasts.show('Не вдалося скопіювати. Виділи текст вручну.');
    }
  }

  private async load(): Promise<void> {
    await settle(this.service.settings(), this.settings);
    const me = this.account();
    if (me) this.auth.mfaEnabled.set(me.twoFactorEnabled);
  }

  /** The password in an alert with a secure field; null when the person backed out. */
  private async askPassword(header: string, message: string, action: string, destructive = false): Promise<string | null> {
    const alert = await this.alerts.create({
      header,
      message,
      inputs: [
        {
          name: 'password',
          type: 'password',
          placeholder: 'Поточний пароль',
          attributes: { autocomplete: 'current-password', 'aria-label': 'Поточний пароль' },
        },
      ],
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        { text: action, role: destructive ? 'destructive' : 'confirm' },
      ],
    });
    await alert.present();
    const { data, role } = await alert.onWillDismiss<{ values: { password?: string } }>();
    const password = data?.values?.password ?? '';
    return role === 'cancel' || role === 'backdrop' || !password ? null : password;
  }
}
