import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, model, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { CardCodeDto, CardService } from '../../services/card-service/card.service';

/** A fresh code a little before the old one stops working, so the one on screen always opens. */
const REFRESH_BEFORE_MS = 20_000;

/**
 * «Показати картку»: a QR that whoever checks the person scans with any camera. It works for three
 * minutes and is replaced on its own while the window is open, so a screenshot of it is soon useless —
 * the page behind it is read live from the server, like a document shown from «Дія».
 */
@Component({
  selector: 'app-card-check-dialog',
  imports: [DialogModule, ButtonModule, RouterLink],
  templateUrl: './card-check-dialog.html',
  styleUrl: './card-check-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardCheckDialogComponent {
  private readonly cards = inject(CardService);

  readonly visible = model(false);
  readonly code = signal<CardCodeDto | null>(null);
  readonly notAllowed = signal(false);
  readonly failed = signal(false);
  private readonly now = signal(Date.now());
  readonly secondsLeft = computed(() => {
    const expires = this.code()?.expiresAtUtc;
    return expires ? Math.max(0, Math.round((Date.parse(expires) - this.now()) / 1000)) : 0;
  });
  /** Time to the next code, not to expiry: the code is replaced a little before it stops working. */
  readonly countdown = computed(() => {
    const left = Math.max(0, this.secondsLeft() - REFRESH_BEFORE_MS / 1000);
    return `${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')}`;
  });

  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    effect(() => (this.visible() ? this.start() : this.stop()));
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  private start(): void {
    if (this.timer) {
      return;
    }

    this.load();
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      const expires = this.code()?.expiresAtUtc;
      if (expires && Date.parse(expires) - Date.now() < REFRESH_BEFORE_MS) {
        this.load();
      }
    }, 1000);
  }

  private stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.code.set(null);
  }

  private loading = false;

  private load(): void {
    if (this.loading) {
      return;
    }

    this.loading = true;
    this.failed.set(false);
    this.cards.checkCode().subscribe({
      next: code => {
        this.loading = false;
        this.notAllowed.set(false);
        this.code.set(code);
        this.now.set(Date.now());
      },
      error: (error: unknown) => {
        this.loading = false;
        if (error instanceof HttpErrorResponse && error.status === 403) {
          this.notAllowed.set(true);
          this.stop();
        } else {
          this.failed.set(true);
        }
      }
    });
  }
}
