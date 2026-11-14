import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageService } from '@openng/optimus-ui/api';
import { catchError, forkJoin, of } from 'rxjs';
import { OnboardingService } from '../../../services/onboarding-service/onboarding.service';

/**
 * Asks for the address to send a reset link to.
 *
 * The answer is deliberately the same whether or not the address is registered — telling an
 * anonymous caller that an email exists would turn this page into an account-enumeration tool.
 */
@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, InputTextModule, ButtonModule, CardModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <main class="flex justify-center items-center min-h-screen p-4">
      <p-card
        header="Відновлення пароля"
        subheader="Надішлемо посилання для встановлення нового пароля"
        [style]="{ width: 'min(100%, 420px)' }"
      >
        @if (sent) {
          <p class="mb-4">
            Якщо такий обліковий запис існує, лист із посиланням уже в дорозі. Перевір пошту,
            зокрема теку зі спамом. Якщо акаунт ще не активований, ми також надіслали нове
            запрошення для активації.
          </p>
          <a routerLink="/login">Повернутися до входу</a>
        } @else {
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
            <div class="flex flex-col gap-2">
              <label for="email">Email</label>
              <input pInputText id="email" type="email" formControlName="email" autocomplete="email" />
            </div>

            <p-button
              type="submit"
              label="Надіслати посилання"
              [disabled]="form.invalid || loading"
              [loading]="loading"
            />
          </form>

          <p class="mt-4">
            Згадали пароль?
            <a routerLink="/login">Увійти</a>
          </p>
        }
      </p-card>
    </main>
  `
})
export class ForgotPasswordComponent {
  private readonly onboardingService = inject(OnboardingService);
  private readonly messageService = inject(MessageService);
  private readonly formBuilder = inject(FormBuilder);

  loading = false;
  sent = false;

  form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]]
  });

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }

    this.loading = true;
    const email = this.form.value.email!;

    // Two independent asks: reset a password, and re-send an invitation to someone who never
    // activated. Neither may cancel the other — forkJoin aborts its siblings on the first error,
    // and an aborted request can leave a token sent by email that was never written down.
    forkJoin({
      reset: this.onboardingService.requestPasswordReset(email).pipe(catchError(() => of(null))),
      invitation: this.onboardingService.resendInvitationByEmail(email).pipe(catchError(() => of(null)))
    }).subscribe({
      next: () => {
        this.loading = false;
        this.sent = true;
      },
      error: () => {
        // Same outcome either way: a failure here must not reveal whether the address is known.
        this.loading = false;
        this.sent = true;
      }
    });
  }
}
