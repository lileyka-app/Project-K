import { Component, OnInit, computed, signal } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonNote,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { inject } from '@angular/core';
import { addIcons } from 'ionicons';
import { medal, pin, pinOutline } from 'ionicons/icons';
import { AwardBlockRow, dateOnly, shortDate } from './kurin.labels';
import { MemberAwardDto } from './kurin.models';

/** What the sheet hands back: a grant to open, or a block to pin or unpin. */
export interface AwardBlocksResult {
  open?: MemberAwardDto;
  pin?: AwardBlockRow;
}

/**
 * Відзначення блоками (web award-blocks): «Переглянути всі» shows every block; a block shows each of
 * its grants with the date and what it was given for. The person pins a block on their own card.
 */
@Component({
  selector: 'app-award-blocks-sheet',
  imports: [IonHeader, IonToolbar, IonTitle, IonButtons, IonButton, IonContent, IonList, IonItem, IonItemGroup, IonLabel, IonNote, IonIcon],
  styles: `
    .blocks {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;
      padding: 16px;
    }
    .block {
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      padding: 12px 6px 10px;
      border: 1px solid var(--lk-border, rgba(127, 127, 127, 0.25));
      border-radius: 14px;
      background: transparent;
      color: inherit;
      font: inherit;
      text-align: center;
    }
    .block img,
    .block ion-icon {
      width: 56px;
      height: 56px;
      object-fit: contain;
    }
    .block span {
      font-size: 13px;
      font-weight: 600;
      line-height: 1.2;
    }
    .count {
      position: absolute;
      top: 6px;
      right: 6px;
      padding: 0 6px;
      border-radius: 999px;
      background: var(--ion-color-primary);
      color: var(--ion-color-primary-contrast);
      font-size: 11px;
    }
    .head {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px 20px 0;
    }
    .head img,
    .head ion-icon {
      width: 88px;
      height: 88px;
      object-fit: contain;
      flex: none;
    }
    .head p {
      margin: 4px 0;
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          @if (selected() && all) {
            <ion-button (click)="selectedKey.set(null)">Усі</ion-button>
          } @else {
            <ion-button (click)="close({})">Закрити</ion-button>
          }
        </ion-buttons>
        <ion-title>{{ selected()?.title ?? 'Усі відзначення' }}</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      @if (selected(); as block) {
        <div class="head">
          @if (block.imageUrl) { <img [src]="block.imageUrl" alt="" /> } @else { <ion-icon name="medal" aria-hidden="true" /> }
          <div>
            @if (block.section) { <p>{{ block.section }}</p> }
            <p>{{ block.grants.length > 1 ? 'Надано ' + block.grants.length + ' рази' : 'Надано один раз' }}</p>
            @if (ownCard) {
              <ion-button size="small" [fill]="block.pinned ? 'solid' : 'outline'" (click)="close({ pin: block })" data-testid="award-pin">
                <ion-icon slot="start" [name]="block.pinned ? 'pin' : 'pin-outline'" aria-hidden="true" />
                {{ block.pinned ? 'Відкріпити' : 'Закріпити' }}
              </ion-button>
            }
          </div>
        </div>
        <ion-list [inset]="true">
          <ion-item-group>
            @for (grant of block.grants; track grant.award.memberAwardKey) {
              <ion-item [button]="grant.canOpen" [detail]="grant.canOpen" (click)="openGrant(grant.canOpen, grant.award)" data-testid="award-grant">
                <ion-label class="ion-text-wrap">
                  <h3>{{ day(grant.award.dateAcquired) }}</h3>
                  <p>За що: {{ grant.award.note || '—' }}</p>
                </ion-label>
                @if (grant.pending) { <ion-note slot="end">очікує</ion-note> }
              </ion-item>
            }
          </ion-item-group>
        </ion-list>
      } @else {
        <div class="blocks">
          @for (block of blocks; track block.key) {
            <button type="button" class="block" (click)="selectedKey.set(block.key)" data-testid="award-block">
              @if (block.grants.length > 1) { <em class="count">×{{ block.grants.length }}</em> }
              @if (block.imageUrl) { <img [src]="block.imageUrl" alt="" /> } @else { <ion-icon name="medal" aria-hidden="true" /> }
              <span>{{ block.title }}</span>
            </button>
          }
        </div>
      }
    </ion-content>
  `,
})
export class AwardBlocksSheet implements OnInit {
  private readonly modals = inject(ModalController);

  // Set by ModalController's componentProps.
  blocks: AwardBlockRow[] = [];
  /** Opened from «Переглянути всі»: the grid first, a block on tap. */
  all = false;
  startKey: string | null = null;
  ownCard = false;

  protected readonly selectedKey = signal<string | null>(null);
  protected readonly selected = computed(() => this.blocks.find((block) => block.key === this.selectedKey()) ?? null);

  constructor() {
    addIcons({ medal, pin, pinOutline });
  }

  ngOnInit(): void {
    this.selectedKey.set(this.startKey);
  }

  protected day(value: string): string {
    return shortDate(dateOnly(value));
  }

  protected openGrant(canOpen: boolean, award: MemberAwardDto): void {
    if (canOpen) this.close({ open: award });
  }

  protected close(result: AwardBlocksResult): void {
    void this.modals.dismiss(result);
  }
}
