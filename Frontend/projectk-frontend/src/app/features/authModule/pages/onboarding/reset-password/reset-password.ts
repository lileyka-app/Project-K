import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { PasswordModule } from '@openng/optimus-ui/password';
import { MessageService } from '@openng/optimus-ui/api';
import { MessageModule } from '@openng/optimus-ui/message';
import { OnboardingService } from '../../../services/onboarding-service/onboarding.service';
import { PasswordRulesComponent } from '../../../../../shared/password-rules/password-rules';
import { passwordRulesValidator } from '../../../../../shared/functions/password-rules.function';

/**
 * Where the link in the reset email lands: `/reset-password?token=…&email=…`, matching the URL
 * `ResendEmailService` builds.
 */
@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, PasswordModule, ButtonModule, CardModule, RouterLink, MessageModule, PasswordRulesComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <main class="flex justify-center items-center min-h-screen p-4">
      <p-card header="Новий пароль" [style]="{ width: 'min(100%, 420px)' }">
        @if (!token || !email) {
          <p class="mb-4">
            Посилання неповне або застаріле. Спробуй надіслати запит ще раз.
          </p>
          <a routerLink="/forgot-password">Надіслати новий лист</a>
        } @else {
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
            <div class="flex flex-col gap-2">
              <label for="newPassword">Новий пароль</label>
              <p-password
                inputId="newPassword"
                formControlName="newPassword"
                [toggleMask]="true"
                [feedback]="false"
                autocomplete="new-password"
              />
              <app-password-rules [password]="form.value.newPassword" />
            </div>

            <div class="flex flex-col gap-2">
              <label for="confirmPassword">Повтори пароль</label>
              <p-password
                inputId="confirmPassword"
                formControlName="confirmPassword"
                [toggleMask]="true"
                [feedback]="false"
                autocomplete="new-password"
              />
            </div>

            @if (form.hasError('passwordsDiffer') && form.get('confirmPassword')?.touched) {
              <p-message severity="error" text="Паролі не збігаються." />
            }

            <p-button
              type="submit"
              label="Встановити пароль"
              [disabled]="form.invalid || loading"
              [loading]="loading"
            />
          </form>
        }
      </p-card>
    </main>
  `
})
export class ResetPasswordComponent implements OnInit {
  private readonly onboardingService = inject(OnboardingService);
  private readonly messageService = inject(MessageService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  loading = false;
  token = '';
  email = '';

  form = this.formBuilder.group(
    {
      newPassword: ['', [Validators.required, passwordRulesValidator]],
      confirmPassword: ['', Validators.required]
    },
    { validators: passwordsMatch }
  );

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }

    this.loading = true;
    this.onboardingService
      .resetPassword({ email: this.email, token: this.token, newPassword: this.form.value.newPassword! })
      .subscribe({
        next: () => {
          this.loading = false;
          this.messageService.add({
            severity: 'success',
            summary: 'Готово',
            detail: 'Пароль оновлено. Тепер увійди із ним.'
          });
          this.router.navigate(['/login']);
        },
        error: (error) => {
          this.loading = false;
          // The API answers { error, message }; its wording is English, so the page keeps its own.
          const known = error?.error?.error === 'InvalidInvitationToken';
          this.messageService.add({
            severity: 'error',
            summary: 'Помилка',
            detail: known
              ? 'Посилання застаріле. Надішли запит ще раз.'
              : 'Не вдалося встановити пароль.'
          });
        }
      });
  }
}

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value;
  const confirmation = control.get('confirmPassword')?.value;
  return password && confirmation && password !== confirmation ? { passwordsDiffer: true } : null;
}
