import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { CARD_FIELD, CARD_FIELDS, CardFieldName, CardService, MyCardDto, hasCardField } from '../../services/card-service/card.service';
import { SharedCardComponent } from '../shared-card/shared-card';
import { CardCheckDialogComponent } from '../card-check-dialog/card-check-dialog';

/**
 * «Моя картка» in the account settings: what the shared card shows, the permanent link, and a preview
 * of the card as others see it. Under 18 the kurin decides whether sharing is open at all and how
 * much of it; the parts it does not allow are shown switched off and cannot be turned on.
 */
@Component({
  selector: 'app-card-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, ButtonModule, ConfirmDialogModule, InputTextModule, ToggleSwitchModule,
    SharedCardComponent, CardCheckDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './card-section.html',
  styleUrl: './card-section.css'
})
export class CardSectionComponent implements OnInit {
  private readonly cards = inject(CardService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);

  protected readonly fieldList = CARD_FIELDS;
  protected readonly card = signal<MyCardDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly checkVisible = signal(false);
  protected readonly allowance = computed(() => this.card()?.allowance ?? null);

  ngOnInit(): void {
    this.cards.mine().subscribe(card => this.card.set(card));
  }

  protected allowed(name: CardFieldName): boolean {
    const allowed = hasCardField(this.allowance()?.maxFields ?? 0, name);
    // The whole way is a way of showing відзначення: off while they are hidden.
    return name === 'AwardsHistory' ? allowed && hasCardField(this.card()?.fields ?? 0, 'Awards') : allowed;
  }

  /** On only when chosen and allowed: a part the kurin does not allow reads as off, whatever was chosen. */
  protected shown(name: CardFieldName): boolean {
    const card = this.card();
    return !!card && this.allowed(name) && hasCardField(card.fields, name);
  }

  protected toggle(name: CardFieldName, on: boolean): void {
    const card = this.card();
    if (!card) {
      return;
    }

    const fields = on ? card.fields | CARD_FIELD[name] : card.fields & ~CARD_FIELD[name];
    this.cards.setFields(fields).subscribe(updated => this.card.set(updated));
  }

  protected issueLink(): void {
    const replacing = !!this.card()?.linkUrl;
    if (!replacing) {
      this.doIssue();
      return;
    }

    this.confirmation.confirm({
      header: 'Створити нове посилання?',
      message: 'Старе посилання перестане працювати.',
      icon: 'pi pi-refresh',
      acceptButtonProps: { label: 'Створити нове', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => this.doIssue()
    });
  }

  protected disableLink(): void {
    this.confirmation.confirm({
      header: 'Вимкнути посилання?',
      message: 'Посилання перестане працювати.',
      icon: 'pi pi-ban',
      acceptButtonProps: { label: 'Вимкнути', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.busy.set(true);
        this.cards.disableLink().subscribe({
          next: card => { this.busy.set(false); this.card.set(card); },
          error: () => this.busy.set(false)
        });
      }
    });
  }

  protected async copy(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.messages.add({ severity: 'success', summary: 'Посилання скопійовано' });
    } catch {
      this.messages.add({ severity: 'warn', summary: 'Не вдалося скопіювати', detail: url });
    }
  }

  private doIssue(): void {
    this.busy.set(true);
    this.cards.issueLink().subscribe({
      next: card => { this.busy.set(false); this.card.set(card); },
      error: () => this.busy.set(false)
    });
  }
}
