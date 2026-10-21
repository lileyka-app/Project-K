import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { FormsModule } from '@angular/forms';
import { FloatLabel } from '@openng/optimus-ui/floatlabel';
import { PasswordModule } from '@openng/optimus-ui/password';
import { ButtonModule } from '@openng/optimus-ui/button';
import { AuthService } from '../../services/auth-service/auth.service';
import { LoginRequest } from '../../models/login-request.model';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InputOtpModule } from '@openng/optimus-ui/inputotp';

import { MessageService } from '@openng/optimus-ui/api';
import { authenticatedHomeRoute } from '../../functions/authenticated-home-route';
import { DemoSeat, DemoService } from '../../services/demo-service/demo.service';

const LOGIN_ERROR_TEXT: Record<string, string> = {
  InvalidCredentials: 'Неправильний email або пароль.',
  Unauthorized: 'Неправильний email або пароль.',
  TooManyAttempts: 'Забагато невдалих спроб для цієї пошти. Зачекай 15 хвилин або віднови пароль.'
};

@Component({
  selector: 'app-login-component',
  imports: [InputTextModule, FormsModule, FloatLabel, PasswordModule, ButtonModule, InputOtpModule, RouterLink],
  templateUrl: './login.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './login.css'
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly demo = inject(DemoService);
  private readonly route = inject(ActivatedRoute);

  /**
   * Signing in a second account from the account switcher: the account in use stays signed in and
   * one tap away, so the page offers a way back instead of the demo seats and the application link.
   */
  readonly addingAccount = this.route.snapshot.queryParamMap.get('add') === '1'
    && !!this.authService.getAuthStateValue()?.userKey;

  /** The demo stand offers three chairs instead of a password. */
  readonly isDemo = DemoService.isDemo();
  demoLoading: DemoSeat | null = null;

  email = '';
  password = '';
  showOtpInput = false;
  otpValue = '';
  loading = false;
  useRecoveryCode = false;
  /** What the password step handed back; goes with the code so the server knows the password was proved. */
  mfaToken: string | null = null;

  ngOnInit() {
    this.authService.getSetupStatus().subscribe({
      next: (status) => {
        if (!status.isInitialized) {
          this.router.navigate(['/setup']);
        }
      },
      error: () => {
        // If the endpoint is not available or fails, just continue
      }
    });
  }

  get canSubmit(): boolean {
    if (this.loading) {
      return false;
    }

    return this.showOtpInput
      ? this.otpValue.trim().length > 0
      : this.email.trim().length > 0 && this.password.length > 0;
  }

  onSubmit(): void {
    if (!this.canSubmit) {
      return;
    }

    if (this.showOtpInput) {
      this.verifyOtp();
      return;
    }

    const loginRequest: LoginRequest = {
      email: this.email,
      password: this.password
    };

    this.loading = true;
    this.authService.login(loginRequest, this.addingAccount).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.requiresMfa) {
          this.showOtpInput = true;
          this.otpValue = '';
          this.useRecoveryCode = false;
          this.mfaToken = response.mfaToken ?? null;
        } else {
          this.navigateToPanel();
        }
      },
      error: (error) => {
        this.loading = false;
        this.showError(this.errorText(error, 'Не вдалося увійти.'));
      }
    });
  }

  verifyOtp(): void {
    this.loading = true;
    this.authService.verifyMfaLogin(this.email, this.otpValue, this.mfaToken, this.addingAccount).subscribe({
      next: () => {
        this.loading = false;
        this.navigateToPanel();
      },
      error: (error) => {
        this.loading = false;
        this.showError(this.errorText(error, 'Неправильний код підтвердження.'));
      }
    });
  }

  onOtpChange(value: string | number | null): void {
    this.otpValue = String(value ?? '').replace(/\D/g, '').slice(0, 6);
  }

  onRecoveryCodeChange(value: string): void {
    this.otpValue = value;
  }

  backToPassword(): void {
    this.showOtpInput = false;
    this.otpValue = '';
    this.useRecoveryCode = false;
    this.mfaToken = null;
  }

  toggleRecoveryCode(): void {
    this.useRecoveryCode = !this.useRecoveryCode;
    this.otpValue = '';
  }

  enterDemo(seat: DemoSeat): void {
    if (this.demoLoading) {
      return;
    }

    this.demoLoading = seat;
    this.demo.enter(seat).subscribe({
      next: () => {
        this.demoLoading = null;
        this.navigateToPanel();
      },
      error: () => {
        this.demoLoading = null;
        this.messageService.add({ severity: 'error', summary: 'Демо зараз недоступне', detail: 'Спробуй за хвилину.' });
      }
    });
  }

  /** Back to the account that is still signed in. */
  cancelAddingAccount(): void {
    this.router.navigate(authenticatedHomeRoute(this.authService.getAuthStateValue()));
  }

  private navigateToPanel(): void {
    const home = authenticatedHomeRoute(this.authService.getAuthStateValue());
    if (this.addingAccount) {
      // A fresh page: services still hold what they read for the account that was in use.
      window.location.assign(this.router.serializeUrl(this.router.createUrlTree(home)));
      return;
    }
    this.router.navigate(home);
  }

  /**
   * The API answers every failure as `{ error, message }`, where `error` is a stable code and
   * `message` is English. The wording belongs to the UI, so map the code and fall back to our own
   * text — never to `HttpErrorResponse.message`, which Angular always fills with a transport string
   * like "Http failure response for ...", so it would render as the user-facing error.
   */
  private errorText(error: unknown, fallback: string): string {
    const code = (error as { error?: { error?: string } })?.error?.error;
    return (code && LOGIN_ERROR_TEXT[code]) || fallback;
  }

  private showError(detail: string): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Вхід',
      detail
    });
  }
}
