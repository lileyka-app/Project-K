
import { Component, inject, ChangeDetectionStrategy, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputOtpModule } from '@openng/optimus-ui/inputotp';
import { AuthService, MfaSetupResponse } from '../../services/auth-service/auth.service';

@Component({
  selector: 'app-mfa-setup-dialog',
  imports: [FormsModule, DialogModule, ButtonModule, InputOtpModule],
  templateUrl: './mfa-setup-dialog.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './mfa-setup-dialog.css'
})
export class MfaSetupDialogComponent {
  private readonly authService = inject(AuthService);
  private readonly messageService = inject(MessageService);

  readonly enabled = output<void>();

  visible = false;
  mandatory = false;
  loadingSetup = false;
  saving = false;
  setup: MfaSetupResponse | null = null;
  verificationCode = '';
  recoveryCodes: string[] = [];

  get canEnable(): boolean {
    return this.verificationCode.length === 6 && !this.saving && !this.loadingSetup;
  }

  show(mandatory = false): void {
    this.mandatory = mandatory;
    this.visible = true;
    this.loadingSetup = true;
    this.saving = false;
    this.setup = null;
    this.verificationCode = '';
    this.recoveryCodes = [];

    this.authService.getMfaSetup().subscribe({
      next: setup => {
        this.setup = setup;
        this.loadingSetup = false;
      },
      error: () => {
        this.loadingSetup = false;
        this.messageService.add({
          severity: 'error',
          summary: 'MFA',
          detail: 'Не вдалося завантажити дані для налаштування.'
        });
      }
    });
  }

  onCodeChange(value: string | number | null): void {
    this.verificationCode = String(value ?? '').replace(/\D/g, '').slice(0, 6);
  }

  enable(): void {
    if (!this.canEnable) {
      return;
    }

    this.saving = true;
    this.authService.enableMfa(this.verificationCode).subscribe({
      next: response => {
        this.saving = false;
        this.recoveryCodes = response.recoveryCodes ?? [];
        this.enabled.emit();
        this.messageService.add({
          severity: 'success',
          summary: 'MFA',
          detail: 'Двофакторну автентифікацію увімкнено.'
        });
      },
      error: () => {
        this.saving = false;
        this.messageService.add({
          severity: 'error',
          summary: 'MFA',
          detail: 'Код не підійшов. Перевір застосунок і спробуй ще раз.'
        });
      }
    });
  }

  closeAfterSavingCodes(): void {
    this.visible = false;
    this.recoveryCodes = [];
  }
}
