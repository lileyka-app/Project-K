import { ChangeDetectionStrategy, Component, effect, inject, model, signal } from '@angular/core';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { MessageService } from '@openng/optimus-ui/api';
import { AuthService } from '../../services/auth-service/auth.service';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import { CardService } from '../../services/card-service/card.service';

/**
 * «Мій код для переходу»: the code a person hands to the Звʼязковий of the kurin that takes them in.
 * Only its owner reads it — the server leaves it off the card for anyone else, and since 2026-10-30
 * it is drawn at random, so nobody who sees the person's card can work it out either.
 */
@Component({
  selector: 'app-transfer-code-dialog',
  imports: [DialogModule, ButtonModule],
  templateUrl: './transfer-code-dialog.html',
  styleUrl: './transfer-code-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TransferCodeDialogComponent {
  private readonly auth = inject(AuthService);
  private readonly members = inject(MemberService);
  private readonly messages = inject(MessageService);
  private readonly cards = inject(CardService);

  readonly visible = model(false);
  readonly code = signal<string | null>(null);
  /** The code as a QR that opens «Переходи» with it typed in; none if it did not load — the code still works. */
  readonly qr = signal<string | null>(null);
  readonly failed = signal(false);

  constructor() {
    // Asked when the dialog opens, not with the menu: the code is not something to keep in memory.
    effect(() => {
      if (this.visible() && !this.code()) {
        this.load();
      }
    });
  }

  async copy(code: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(code);
      this.messages.add({ severity: 'success', summary: 'Код скопійовано', detail: 'Передай його Звʼязковому, який тебе приймає.' });
    } catch {
      this.messages.add({ severity: 'warn', summary: 'Не вдалося скопіювати', detail: code });
    }
  }

  private load(): void {
    const memberKey = this.auth.getAuthStateValue()?.memberKey;
    if (!memberKey) {
      this.failed.set(true);
      return;
    }

    this.failed.set(false);
    this.members.getByKey(memberKey).subscribe({
      next: member => member.publicId ? this.code.set(member.publicId) : this.failed.set(true),
      error: () => this.failed.set(true)
    });
    this.cards.transferCode().subscribe({ next: code => this.qr.set(code.qrCode), error: () => this.qr.set(null) });
  }
}
