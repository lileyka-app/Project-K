import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';

import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { OnboardingService, InvitationValidationResponse } from '../../../services/onboarding-service/onboarding.service';
import { AuthService } from '../../../services/auth-service/auth.service';
import { authenticatedHomeRoute } from '../../../functions/authenticated-home-route';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { PasswordModule } from '@openng/optimus-ui/password';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { MessageService } from '@openng/optimus-ui/api';
import { failureDetail } from '../../../../../shared/functions/failure-detail.function';
import { PasswordRulesComponent } from '../../../../../shared/password-rules/password-rules';
import { passwordRulesValidator } from '../../../../../shared/functions/password-rules.function';

/**
 * Where the invitation letter lands. The password chosen here signs the person in on the spot:
 * the answer is a session, and the page takes them to their home rather than to a sign-in form
 * they have never used.
 */
@Component({
  selector: 'app-account-activation',
  imports: [ReactiveFormsModule, InputTextModule, PasswordModule, ButtonModule, CardModule, MessageModule, PasswordRulesComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <main class="flex justify-center items-center min-h-screen p-4">
      <p-card header="Активація акаунта" [style]="{ width: 'min(100%, 420px)' }">
        @if (loading && !validationData) {
          <div class="text-center p-4">
            <i class="pi pi-spin pi-spinner text-4xl"></i>
            <p>Перевіряємо запрошення…</p>
          </div>
        }

        @if (!loading && !validationData?.isValid) {
          <div class="flex flex-col gap-4">
            <p-message severity="warn" styleClass="w-full" text="Посилання недійсне або прострочене." />
            <p class="text-muted-color m-0">
              Ми надішлемо нове запрошення на ту саму адресу — це та сама форма, що й для забутого пароля.
            </p>
            <div class="flex flex-col gap-2">
              <p-button label="Надіслати нове запрошення" (onClick)="goToRecovery()" />
              <p-button label="До входу" [text]="true" (onClick)="goToLogin()" />
            </div>
          </div>
        }

        @if (validationData?.isValid && !activated) {
          <div class="flex flex-col gap-4">
            <p-message severity="info" styleClass="w-full"
              [text]="'Вітаємо, ' + validationData.firstName + '! Оберіть пароль для акаунта ' + validationData.email + '.'" />

            <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
              <div class="flex flex-col gap-2">
                <label for="password">Новий пароль</label>
                <p-password inputId="password" formControlName="password" [feedback]="false" [toggleMask]="true" autocomplete="new-password" styleClass="w-full" inputStyleClass="w-full" />
                <app-password-rules [password]="form.value.password" />
              </div>
              <div class="flex flex-col gap-2">
                <label for="confirmPassword">Повторіть пароль</label>
                <p-password inputId="confirmPassword" formControlName="confirmPassword" [feedback]="false" [toggleMask]="true" autocomplete="new-password" styleClass="w-full" inputStyleClass="w-full" />
                @if (form.errors?.['mismatch'] && form.get('confirmPassword')?.touched) {
                  <p-message severity="error" text="Паролі не збігаються." />
                }
              </div>

              <p-button label="Активувати і увійти" type="submit" [disabled]="form.invalid || submitting" [loading]="submitting" styleClass="w-full" />
            </form>
          </div>
        }

        @if (activated) {
          <p-message severity="success" styleClass="w-full" text="Акаунт активовано. Заходимо…" />
        }
      </p-card>
    </main>
  `
})
export class AccountActivationComponent implements OnInit {
  token: string | null = null;
  loading = true;
  submitting = false;
  activated = false;
  validationData: InvitationValidationResponse | null = null;
  form: FormGroup;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly onboardingService = inject(OnboardingService);
  private readonly authService = inject(AuthService);
  private readonly messageService = inject(MessageService);

  constructor() {
    this.form = this.fb.group({
      password: ['', [Validators.required, passwordRulesValidator]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit() {
    this.token = this.route.snapshot.paramMap.get('token');
    if (this.token) {
      this.validateToken();
    } else {
      this.loading = false;
    }
  }

  validateToken() {
    this.onboardingService.validateInvitation(this.token!).subscribe({
      next: (data) => {
        this.validationData = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  passwordMatchValidator(g: FormGroup) {
    return g.get('password')?.value === g.get('confirmPassword')?.value ? null : { mismatch: true };
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.submitting = true;
    const payload = {
      token: this.token,
      password: this.form.value.password
    };

    this.onboardingService.activateAccount(payload).subscribe({
      next: (session) => {
        this.activated = true;
        this.submitting = false;
        this.authService.applyLoginResponse(session);
        this.messageService.add({ severity: 'success', summary: 'Активовано', detail: 'Вітаємо в Лілейці!' });
        this.router.navigate(authenticatedHomeRoute(this.authService.getAuthStateValue()));
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Не вдалося активувати', detail: failureDetail(err, 'Спробуйте ще раз або замовте нове запрошення.') });
        this.submitting = false;
      }
    });
  }

  goToLogin() {
    this.router.navigate(['/login']);
  }

  goToRecovery() {
    this.router.navigate(['/forgot-password']);
  }
}
