
import { Component, OnInit, inject, ChangeDetectionStrategy, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DividerModule } from '@openng/optimus-ui/divider';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { PasswordModule } from '@openng/optimus-ui/password';
import { TagModule } from '@openng/optimus-ui/tag';
import { CalendarFeedSectionComponent } from '../../components/calendar-feed-section/calendar-feed-section';
import { MfaSetupDialogComponent } from '../../components/mfa-setup-dialog/mfa-setup-dialog';
import { AccountSettings, AccountSettingsService } from '../../services/account-settings-service/account-settings.service';
import { AuthService } from '../../services/auth-service/auth.service';
import { MfaEnforcerService } from '../../services/mfa-enforcer-service/mfa-enforcer.service';
import { PermissionService } from '../../services/permission-service/permission.service';
import { PasswordRulesComponent } from '../../../../shared/password-rules/password-rules';
import { PhoneInputComponent } from '../../../../shared/phone-input/phone-input';
import { passwordMeetsRules } from '../../../../shared/functions/password-rules.function';
import { AccountRoleTag, signedInRoleTag } from '../../../kurinModule/functions/account-role-tag.function';

@Component({
  selector: 'app-account-settings',
  imports: [
    FormsModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    DividerModule,
    TagModule,
    MfaSetupDialogComponent,
    CalendarFeedSectionComponent,
    PasswordRulesComponent,
    PhoneInputComponent
  ],
  templateUrl: './account-settings.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './account-settings.css'
})
export class AccountSettingsComponent implements OnInit {
  private readonly accountService = inject(AccountSettingsService);
  private readonly authService = inject(AuthService);
  private readonly mfaEnforcer = inject(MfaEnforcerService);
  private readonly messageService = inject(MessageService);
  private readonly permissionService = inject(PermissionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly mfaSetupDialog = viewChild<MfaSetupDialogComponent>('mfaSetupDialog');

  settings: AccountSettings | null = null;
  loading = false;
  savingProfile = false;
  changingPassword = false;
  resettingMfa = false;
  confirmingEmail = false;
  pendingEmail: string | null = null;

  profileForm = {
    email: '',
    phoneNumber: '',
    currentPassword: ''
  };

  passwordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };

  mfaResetPassword = '';
  recoveryCodesPassword = '';
  recoveryCodes: string[] = [];
  rotatingRecoveryCodes = false;

  /** The same tag the sidebar footer shows, not the raw role name the settings endpoint answers with. */
  get roleTag(): AccountRoleTag {
    return signedInRoleTag(this.permissionService, this.authService.getAuthStateValue()?.roles ?? []);
  }

  get canSaveProfile(): boolean {
    return this.profileForm.email.trim().length > 0
      && (!this.isEmailChanged || this.profileForm.currentPassword.length > 0)
      && !this.savingProfile
      && !this.confirmingEmail;
  }

  get canChangePassword(): boolean {
    return this.passwordForm.currentPassword.length > 0
      && passwordMeetsRules(this.passwordForm.newPassword)
      && this.passwordForm.newPassword === this.passwordForm.confirmPassword
      && !this.changingPassword;
  }

  get isPrivileged(): boolean {
    return this.permissionService.isAdmin() || this.permissionService.canManageWholeKurin();
  }

  get isEmailChanged(): boolean {
    return !!this.settings
      && this.profileForm.email.trim().toLowerCase() !== this.settings.email.toLowerCase();
  }

  get canResetMfa(): boolean {
    return !!this.settings?.twoFactorEnabled
      && this.isPrivileged
      && this.mfaResetPassword.length > 0
      && !this.resettingMfa;
  }

  get canDisableMfa(): boolean {
    return !!this.settings?.twoFactorEnabled
      && !this.isPrivileged
      && this.mfaResetPassword.length > 0
      && !this.resettingMfa;
  }

  get canRotateRecoveryCodes(): boolean {
    return !!this.settings?.twoFactorEnabled
      && this.recoveryCodesPassword.length > 0
      && !this.rotatingRecoveryCodes;
  }

  ngOnInit(): void {
    if (!this.confirmEmailFromRoute()) {
      this.load();
    }
  }

  load(): void {
    this.loading = true;
    this.accountService.getSettings().subscribe({
      next: settings => {
        this.applySettings(settings);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.showError('Не вдалося завантажити налаштування акаунта.');
      }
    });
  }

  saveProfile(): void {
    if (!this.canSaveProfile) {
      return;
    }

    this.savingProfile = true;
    this.accountService.updateProfile({
      email: this.profileForm.email.trim(),
      phoneNumber: this.profileForm.phoneNumber.trim() || null,
      currentPassword: this.isEmailChanged ? this.profileForm.currentPassword : null
    }).subscribe({
      next: settings => {
        this.applySettings(settings);
        this.profileForm.currentPassword = '';
        this.savingProfile = false;

        if (settings.pendingEmail) {
          this.messageService.add({
            severity: 'info',
            summary: 'Акаунт',
            detail: `Ми надіслали лист підтвердження на ${settings.pendingEmail}. Поточний email зміниться після підтвердження.`
          });
          return;
        }

        this.authService.updateEmail(settings.email);
        this.messageService.add({ severity: 'success', summary: 'Акаунт', detail: 'Контактні дані оновлено.' });
      },
      error: () => {
        this.savingProfile = false;
        this.showError('Не вдалося оновити контактні дані. Перевірте email.');
      }
    });
  }

  changePassword(): void {
    if (!this.canChangePassword) {
      return;
    }

    this.changingPassword = true;
    this.accountService.changePassword({
      currentPassword: this.passwordForm.currentPassword,
      newPassword: this.passwordForm.newPassword
    }).subscribe({
      next: () => {
        this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
        this.changingPassword = false;
        this.messageService.add({ severity: 'success', summary: 'Акаунт', detail: 'Пароль змінено.' });
      },
      error: () => {
        this.changingPassword = false;
        this.showError('Не вдалося змінити пароль. Перевірте поточний пароль і вимоги до нового.');
      }
    });
  }

  resetMfa(): void {
    if (!this.canResetMfa) {
      return;
    }

    this.resettingMfa = true;
    this.accountService.resetMfa({ currentPassword: this.mfaResetPassword }).subscribe({
      next: () => {
        if (this.settings) {
          this.settings = { ...this.settings, twoFactorEnabled: false };
        }

        this.mfaResetPassword = '';
        this.resettingMfa = false;
        this.messageService.add({
          severity: 'success',
          summary: 'MFA',
          detail: 'MFA скинуто. Налаштуйте застосунок-автентифікатор заново.'
        });

        if (this.isPrivileged) {
          this.setupMfa(true);
        }
      },
      error: () => {
        this.resettingMfa = false;
        this.showError('Не вдалося скинути MFA.');
      }
    });
  }

  disableMfa(): void {
    if (!this.canDisableMfa) {
      return;
    }

    this.resettingMfa = true;
    this.accountService.disableMfa({ currentPassword: this.mfaResetPassword }).subscribe({
      next: () => {
        if (this.settings) {
          this.settings = { ...this.settings, twoFactorEnabled: false };
        }

        this.mfaResetPassword = '';
        this.resettingMfa = false;
        this.messageService.add({
          severity: 'success',
          summary: 'MFA',
          detail: 'MFA вимкнено.'
        });
      },
      error: () => {
        this.resettingMfa = false;
        this.showError('Не вдалося вимкнути MFA. Перевірте поточний пароль.');
      }
    });
  }

  setupMfa(mandatory = false): void {
    this.mfaSetupDialog()?.show(mandatory);
  }

  onMfaEnabled(): void {
    if (this.settings) {
      this.settings = { ...this.settings, twoFactorEnabled: true };
    }
    this.mfaEnforcer.markMfaEnabledForCurrentSession();
  }

  rotateRecoveryCodes(): void {
    if (!this.canRotateRecoveryCodes) {
      return;
    }

    this.rotatingRecoveryCodes = true;
    this.authService.rotateMfaRecoveryCodes(this.recoveryCodesPassword).subscribe({
      next: response => {
        this.recoveryCodes = response.recoveryCodes ?? [];
        this.recoveryCodesPassword = '';
        this.rotatingRecoveryCodes = false;
        this.messageService.add({ severity: 'success', summary: 'MFA', detail: 'Резервні коди оновлено.' });
      },
      error: () => {
        this.rotatingRecoveryCodes = false;
        this.showError('Не вдалося оновити recovery codes. Перевірте поточний пароль.');
      }
    });
  }

  private confirmEmailFromRoute(): boolean {
    const query = this.route.snapshot.queryParamMap;
    if (query.get('confirmEmail') !== 'true') {
      return false;
    }

    const email = query.get('email');
    const token = query.get('token');
    if (!email || !token) {
      this.showError('Посилання підтвердження email неповне.');
      this.clearEmailConfirmationQuery();
      return false;
    }

    this.confirmingEmail = true;
    this.accountService.confirmEmailChange({ email, token }).subscribe({
      next: settings => {
        this.applySettings(settings);
        this.authService.updateEmail(settings.email);
        this.confirmingEmail = false;
        this.clearEmailConfirmationQuery();
        this.messageService.add({ severity: 'success', summary: 'Акаунт', detail: 'Email підтверджено та оновлено.' });
      },
      error: () => {
        this.confirmingEmail = false;
        this.clearEmailConfirmationQuery();
        this.showError('Не вдалося підтвердити email. Посилання могло застаріти або вже бути використаним.');
      }
    });

    return true;
  }

  private clearEmailConfirmationQuery(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {},
      replaceUrl: true
    });
  }

  private applySettings(settings: AccountSettings): void {
    this.settings = settings;
    this.pendingEmail = settings.pendingEmail;
    this.profileForm = {
      email: settings.email,
      phoneNumber: settings.phoneNumber ?? '',
      currentPassword: this.profileForm.currentPassword
    };
  }

  private showError(detail: string): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Акаунт',
      detail
    });
  }
}
