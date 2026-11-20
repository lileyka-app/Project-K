import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { map, Observable, of } from 'rxjs';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import {
  CARD_FIELD, CARD_FIELDS, CardFieldName, CardService, KurinCardSharingDto, hasCardField
} from '../../../authModule/services/card-service/card.service';
import { countChanges } from '../../../../shared/functions/count-changes.function';

/**
 * «Картки до 18 років» in the kurin settings: whether the kurin's people under 18 may show their card
 * at all, which parts they may choose from, and whether they may also make a permanent link. Saved
 * with the rest of the page. Those 18 or older decide for themselves and are not bound by it.
 */
@Component({
  selector: 'app-kurin-card-sharing',
  imports: [FormsModule, CheckboxModule, ToggleSwitchModule],
  templateUrl: './kurin-card-sharing.html',
  styleUrl: './kurin-card-sharing.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinCardSharingComponent {
  private readonly cards = inject(CardService);

  readonly kurinKey = input.required<string>();

  protected readonly fieldList = CARD_FIELDS;
  protected readonly value = signal<KurinCardSharingDto | null>(null);
  private readonly saved = signal<KurinCardSharingDto | null>(null);
  readonly dirty = computed(() => JSON.stringify(this.value()) !== JSON.stringify(this.saved()));
  /** How many of the rule's parts differ from what the server has; each field counts on its own. */
  readonly changeCount = computed(() => {
    const value = this.value();
    const saved = this.saved();
    if (!value || !saved) {
      return 0;
    }
    const flags = Object.values(CARD_FIELD).filter(bit => (value.minorFields & bit) !== (saved.minorFields & bit)).length;
    return countChanges({ share: saved.minorsMayShare, link: saved.minorsMayLink }, { share: value.minorsMayShare, link: value.minorsMayLink }) + flags;
  });

  constructor() {
    effect(() => {
      const key = this.kurinKey();
      if (key) {
        this.cards.kurinSharing(key).subscribe(sharing => this.apply(sharing));
      }
    });
  }

  protected has(name: CardFieldName): boolean {
    return hasCardField(this.value()?.minorFields ?? 0, name);
  }

  protected setShare(on: boolean): void {
    this.value.update(v => v && { ...v, minorsMayShare: on, minorsMayLink: on && v.minorsMayLink });
  }

  protected setLink(on: boolean): void {
    this.value.update(v => v && { ...v, minorsMayLink: on });
  }

  protected setField(name: CardFieldName, on: boolean): void {
    this.value.update(v => v && {
      ...v,
      minorFields: on ? v.minorFields | CARD_FIELD[name] : v.minorFields & ~CARD_FIELD[name]
    });
  }

  save(): Observable<void> {
    const value = this.value();
    if (!value || !this.dirty()) {
      return of(undefined);
    }
    return this.cards.setKurinSharing(this.kurinKey(), value).pipe(map(saved => this.apply(saved)));
  }

  private apply(sharing: KurinCardSharingDto): void {
    this.saved.set(sharing);
    this.value.set({ ...sharing });
  }
}
