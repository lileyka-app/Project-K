import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CardService, PublicCardDto } from '../../services/card-service/card.service';
import { SharedCardComponent } from '../../components/shared-card/shared-card';

/**
 * A shared card opened from a QR or a link, by anyone, signed in or not. `check` is the three-minute
 * «перевірити» QR and shows when Лілейка checked it; `link` is the person's permanent card. Neither
 * is for search engines.
 */
@Component({
  selector: 'app-public-card',
  imports: [SharedCardComponent, RouterLink],
  templateUrl: './public-card.html',
  styleUrl: './public-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PublicCardComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly cards = inject(CardService);
  private readonly meta = inject(Meta);

  readonly mode: 'check' | 'link' = this.route.snapshot.data['mode'] === 'link' ? 'link' : 'check';
  readonly card = signal<PublicCardDto | null>(null);
  readonly missing = signal(false);

  ngOnInit(): void {
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    const token = this.route.snapshot.paramMap.get('token') ?? '';
    (this.mode === 'link' ? this.cards.linked(token) : this.cards.checked(token)).subscribe({
      next: card => this.card.set(card),
      error: () => this.missing.set(true)
    });
  }

  ngOnDestroy(): void {
    this.meta.removeTag('name="robots"');
  }
}
