import { ChangeDetectionStrategy, Component, effect, inject, model, signal } from '@angular/core';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { MessageService } from '@openng/optimus-ui/api';
import { environment } from '../../../../../environments/environment';
import { trimTrailingSlashes } from '../../../../shared/functions/trim-trailing-slashes.function';
import { PhoneAppInstallCode, PhoneAppService } from '../../services/phone-app-service/phone-app.service';

/**
 * «Встановити на телефон» on a computer. The phone app lives under /m/ and is meant for phones, so
 * sending a desktop there read as «install it on this computer». Instead the computer shows a code
 * the phone's camera opens; /m/install on the phone then shows the steps for that phone.
 */
@Component({
  selector: 'app-install-on-phone-dialog',
  imports: [DialogModule, ButtonModule],
  templateUrl: './install-on-phone-dialog.html',
  styleUrl: './install-on-phone-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InstallOnPhoneDialogComponent {
  private readonly messages = inject(MessageService);
  private readonly phoneApp = inject(PhoneAppService);

  readonly visible = model(false);
  readonly code = signal<PhoneAppInstallCode | null>(null);
  readonly failed = signal(false);
  readonly helpUrl = `${trimTrailingSlashes(environment.docsUrl)}/user/howto/install-app/`;

  constructor() {
    // Asked when the dialog first opens: nobody who never opens it pays for the picture.
    effect(() => {
      if (this.visible() && !this.code()) {
        this.load();
      }
    });
  }

  async copyLink(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.messages.add({ severity: 'success', summary: 'Посилання скопійовано', detail: 'Надішли його собі на телефон.' });
    } catch {
      this.messages.add({ severity: 'warn', summary: 'Не вдалося скопіювати', detail: url });
    }
  }

  private load(): void {
    this.failed.set(false);
    this.phoneApp.installCode$.subscribe({
      next: code => this.code.set(code),
      error: () => this.failed.set(true)
    });
  }
}
