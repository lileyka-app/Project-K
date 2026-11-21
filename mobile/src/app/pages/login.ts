import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IonButton, IonContent, IonIcon, IonInput, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { downloadOutline } from 'ionicons/icons';
import { AuthService, loginErrorText } from '../auth/auth.service';
import { webPage } from '../features/account/web-links';
import { InstallService } from '../pwa/install.service';

type Step = 'password' | 'code';

@Component({
  selector: 'app-login',
  imports: [IonContent, IonInput, IonButton, IonSpinner, IonIcon, RouterLink],
  styles: `
    .wrap {
      max-width: 420px;
      margin: 0 auto;
      padding: calc(env(safe-area-inset-top) + 48px) 0 32px;
    }
    .brand {
      text-align: center;
      margin-bottom: 24px;
    }
    .brand img {
      width: 88px;
      height: 88px;
    }
    .brand h1 {
      margin: 12px 0 4px;
      font-size: 28px;
      font-weight: 700;
    }
    .brand p {
      margin: 0;
      color: var(--lk-muted);
    }
    .fields {
      display: grid;
      gap: 16px;
      padding: 0 16px 16px;
    }
    .actions {
      padding: 8px 16px 0;
    }
    .error {
      margin: -8px 16px 8px;
    }
    .links {
      display: flex;
      justify-content: space-between;
      padding: 16px 20px 0;
      font-size: 15px;
    }
    /* The way onto the home screen, quieter than signing in. */
    .install {
      display: flex;
      justify-content: center;
      padding: 28px 16px 0;
    }
    .install a {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 15px;
    }
  `,
  template: `
    <ion-content>
      <div class="wrap">
        <div class="brand">
          <img src="icons/icon.svg" alt="" />
          <h1>Лілейка</h1>
          <p>
            @if (step() === 'code') {
              Підтвердження входу
            } @else if (addingAccount) {
              Увійди в інший акаунт. Поточний лишиться в меню, щоб перемикатися між ними
            } @else {
              Увійди у свій акаунт
            }
          </p>
        </div>

        <form (submit)="$event.preventDefault(); submit()">
          @if (step() === 'password') {
            <div class="fields">
              <div class="lk-field">
                <span class="lk-field__label">Email</span>
                <div class="lk-input-box" [class.lk-invalid]="!!error()">
                  <ion-input
                    data-testid="email"
                    aria-label="Email"
                    type="email"
                    inputmode="email"
                    autocomplete="username"
                    autocapitalize="off"
                    [value]="email()"
                    (ionInput)="email.set(text($event))"
                  />
                </div>
              </div>
              <div class="lk-field">
                <span class="lk-field__label">Пароль</span>
                <div class="lk-input-box" [class.lk-invalid]="!!error()">
                  <ion-input
                    data-testid="password"
                    aria-label="Пароль"
                    type="password"
                    autocomplete="current-password"
                    [value]="password()"
                    (ionInput)="password.set(text($event))"
                  />
                </div>
              </div>
            </div>
          } @else {
            <div class="fields">
              <div class="lk-field">
                <span class="lk-field__label">{{ codeLabel() }}</span>
                <div class="lk-input-box" [class.lk-invalid]="!!error()">
                  <ion-input
                    data-testid="code"
                    [attr.aria-label]="codeLabel()"
                    [type]="useRecoveryCode() ? 'text' : 'tel'"
                    [inputmode]="useRecoveryCode() ? 'text' : 'numeric'"
                    autocomplete="one-time-code"
                    [maxlength]="useRecoveryCode() ? 32 : 6"
                    [value]="code()"
                    (ionInput)="code.set(text($event))"
                  />
                </div>
              </div>
            </div>
          }

          @if (error(); as message) {
            <p class="lk-field__error error" role="alert">{{ message }}</p>
          }

          <div class="actions">
            <ion-button type="submit" expand="block" [disabled]="!canSubmit()">
              @if (busy()) {
                <ion-spinner name="crescent" />
              } @else {
                {{ step() === 'password' ? 'Увійти' : 'Підтвердити' }}
              }
            </ion-button>
          </div>
        </form>

        <div class="links">
          @if (step() === 'password' && addingAccount) {
            <a href="" (click)="$event.preventDefault(); cancel()" data-testid="cancel-add">Скасувати</a>
            <a [href]="forgotUrl" [attr.target]="outTarget" rel="noopener" data-testid="forgot">Забув пароль?</a>
          } @else if (step() === 'password') {
            <a [href]="forgotUrl" [attr.target]="outTarget" rel="noopener" data-testid="forgot">Забув пароль?</a>
            <a [href]="joinUrl" [attr.target]="outTarget" rel="noopener" data-testid="join">Подати заявку</a>
          } @else {
            <a href="" (click)="$event.preventDefault(); back()">Назад</a>
            <a href="" (click)="$event.preventDefault(); toggleRecovery()">
              {{ useRecoveryCode() ? 'Код з застосунку' : 'Код відновлення' }}
            </a>
          }
        </div>

        @if (addingAccount) {
          <!-- Already in the app: no install offer while adding an account. -->
        } @else if (install.canPrompt()) {
          <div class="install">
            <ion-button fill="outline" size="small" (click)="install.prompt()" data-testid="login-install">
              <ion-icon slot="start" name="download-outline" aria-hidden="true" />
              Встановити застосунок
            </ion-button>
          </div>
        } @else if (!install.standalone()) {
          <div class="install">
            <a routerLink="/install" data-testid="login-install-help">
              <ion-icon name="download-outline" aria-hidden="true" />
              Як встановити на телефон
            </a>
          </div>
        }
      </div>
    </ion-content>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /**
   * Opened from the menu's «Додати акаунт»: the account in use stays signed in and goes to the
   * switcher once this one signs in.
   */
  protected readonly addingAccount =
    inject(ActivatedRoute).snapshot.queryParamMap.get('add') === '1' && this.auth.signedIn();

  /**
   * Password reset, activation and the join form are the web's pages on this same origin. From the
   * home screen they open in the system's browser over the app, which stays on this screen for when
   * the person comes back with a new password; in a browser tab they open in place.
   */
  protected readonly forgotUrl = webPage('/forgot-password');
  protected readonly joinUrl = webPage('/join');
  protected readonly install = inject(InstallService);
  protected readonly outTarget = this.install.standalone() ? '_blank' : null;

  protected readonly step = signal<Step>('password');
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly code = signal('');
  protected readonly useRecoveryCode = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private mfaToken: string | null = null;

  constructor() {
    addIcons({ downloadOutline });
  }

  protected readonly codeLabel = computed(() =>
    this.useRecoveryCode() ? 'Код відновлення' : 'Код з застосунку-автентифікатора',
  );

  protected readonly canSubmit = computed(() => {
    if (this.busy()) return false;
    return this.step() === 'password'
      ? this.email().trim().length > 0 && this.password().length > 0
      : this.code().trim().length > 0;
  });

  protected text(event: Event): string {
    return String((event as CustomEvent<{ value?: string | null }>).detail.value ?? '');
  }

  protected async submit(): Promise<void> {
    if (!this.canSubmit()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      if (this.step() === 'password') {
        const outcome = await this.auth.login(this.email().trim(), this.password(), this.addingAccount);
        if (outcome.kind === 'mfa') {
          this.mfaToken = outcome.mfaToken;
          this.step.set('code');
          return;
        }
      } else {
        await this.auth.verifyMfa(this.email().trim(), this.code().trim(), this.mfaToken, this.addingAccount);
      }
      if (this.addingAccount) {
        // A fresh start: services still hold what they read for the account that was in use.
        globalThis.location.assign(new URL('tabs/home', document.baseURI).href);
        return;
      }
      await this.router.navigateByUrl('/tabs/home', { replaceUrl: true });
    } catch (error) {
      const fallback = this.step() === 'password' ? 'Не вдалося увійти.' : 'Неправильний код підтвердження.';
      this.error.set(loginErrorText(error, fallback));
    } finally {
      this.busy.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigateByUrl('/tabs/more', { replaceUrl: true });
  }

  protected back(): void {
    this.step.set('password');
    this.code.set('');
    this.useRecoveryCode.set(false);
    this.error.set(null);
    this.mfaToken = null;
  }

  protected toggleRecovery(): void {
    this.useRecoveryCode.update((v) => !v);
    this.code.set('');
  }
}
