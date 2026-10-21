import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { FloatLabel } from '@openng/optimus-ui/floatlabel';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { PasswordModule } from '@openng/optimus-ui/password';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { AuthService, InitializeSetupRequest } from '../../services/auth-service/auth.service';
import { authenticatedHomeRoute } from '../../functions/authenticated-home-route';
import { PasswordRulesComponent } from '../../../../shared/password-rules/password-rules';
import { passwordRulesValidator } from '../../../../shared/functions/password-rules.function';

@Component({
  selector: 'app-setup',
  imports: [
    ReactiveFormsModule,
    PasswordRulesComponent,
    ButtonModule,
    FloatLabel,
    InputTextModule,
    MessageModule,
    PasswordModule,
    ToggleSwitchModule
  ],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './setup.html'
})
export class SetupComponent {
  setupForm: FormGroup;
  isSubmitting = false;
  error: string | null = null;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  constructor() {
    this.setupForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      password: ['', [Validators.required, passwordRulesValidator]],
      enforcePrivilegedMfa: [false],
      seedDemoData: [false]
    });
  }

  onSubmit(): void {
    if (this.setupForm.invalid || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.error = null;

    const request: InitializeSetupRequest = this.setupForm.getRawValue();
    this.authService.initializeSetup(request).subscribe({
      next: () => {
        const authState = this.authService.getAuthStateValue();
        this.router.navigate(authState ? authenticatedHomeRoute(authState) : ['/']);
      },
      error: (err) => {
        let errorMessage = 'Не вдалося ініціалізувати систему. Спробуйте ще раз.';
        if (err.error && typeof err.error === 'object' && err.error.message) {
          errorMessage = err.error.message;
        } else if (err.error && typeof err.error === 'string') {
          errorMessage = err.error;
        } else if (err.message) {
          errorMessage = err.message;
        }

        this.error = errorMessage;
        this.isSubmitting = false;
      }
    });
  }
}
