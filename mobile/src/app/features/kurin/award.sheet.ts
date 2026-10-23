import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  ActionSheetController,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonItemGroup,
  IonList,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonTextarea,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { AWARD_LEVELS, progressStatus, toDateOnly } from './kurin.labels';
import { AwardLevel, MemberAwardDto } from './kurin.models';
import { KurinApi } from './kurin.service';

const STATUS_LABELS: Record<string, string> = {
  Submitted: 'Очікує підтвердження',
  Confirmed: 'Підтверджено',
  Rejected: 'Відхилено',
};

/**
 * Adding or changing one УПЮ відзначення (web member-awards-dialog): its ступінь (fixed once
 * added), the date and a note. What the viewer may do with it — change, confirm, remove — the card
 * works out from the kurin's policy (abilities.ts awardRights). Dismisses with `true` after any change.
 */
@Component({
  selector: 'app-award-sheet',
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonList,
    IonItem,
    IonItemGroup,
    IonSelect,
    IonSelectOption,
    IonInput,
    IonTextarea,
    IonSpinner,
  ],
  styles: `
    ion-item > div {
      width: 100%;
      padding: 10px 0;
    }
    .status {
      margin: 8px 20px 0;
      color: var(--lk-muted);
    }
    .actions {
      padding: 4px 16px 24px;
      display: grid;
      gap: 8px;
    }
    .lk-input-box ion-textarea {
      --padding-start: 12px;
      --padding-end: 12px;
      font-size: 16px;
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="close(false)">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ award ? 'Відзначення' : 'Нове відзначення' }}</ion-title>
        @if (canEdit) {
          <ion-buttons slot="end">
            <ion-button [strong]="true" [disabled]="!valid() || busy()" (click)="save()" data-testid="save-award">Зберегти</ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>
    <ion-content>
      @if (statusLabel(); as status) { <p class="status">{{ status }}</p> }
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-select
              label="Ступінь відзначення"
              interface="action-sheet"
              cancelText="Скасувати"
              placeholder="Обери"
              [value]="level()"
              [disabled]="!!award || !canEdit"
              (ionChange)="level.set($any($event).detail.value)"
              data-testid="award-level"
            >
              @for (option of levels; track option.value) {
                <ion-select-option [value]="option.value">{{ option.label }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Дата здобуття</span>
              <div class="lk-input-box">
                <ion-input
                  type="date"
                  aria-label="Дата здобуття"
                  [max]="today"
                  [value]="date()"
                  [disabled]="!canEdit"
                  (ionInput)="date.set($any($event).detail.value ?? '')"
                  data-testid="award-date"
                />
              </div>
            </div>
          </ion-item>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Нотатка</span>
              <div class="lk-input-box">
                <ion-textarea
                  aria-label="Нотатка"
                  [autoGrow]="true"
                  [rows]="3"
                  [value]="note()"
                  [disabled]="!canEdit"
                  (ionInput)="note.set($any($event).detail.value ?? '')"
                />
              </div>
            </div>
          </ion-item>
        </ion-item-group>
      </ion-list>

      @if (canApprove() || canDelete()) {
        <div class="actions">
          @if (canApprove()) {
            <ion-button expand="block" [disabled]="busy()" (click)="approve()" data-testid="approve-award">
              @if (busy()) { <ion-spinner name="crescent" /> } @else { Підтвердити }
            </ion-button>
          }
          @if (canDelete()) {
            <ion-button expand="block" fill="clear" color="danger" class="lk-danger" [disabled]="busy()" (click)="remove()">
              Видалити відзначення
            </ion-button>
          }
        </div>
      }
    </ion-content>
  `,
})
export class AwardSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);

  // Set by ModalController's componentProps.
  memberKey = '';
  award: MemberAwardDto | null = null;
  canEdit = false;
  canReview = false;
  canRemove = false;

  protected readonly levels = AWARD_LEVELS;
  protected readonly today = toDateOnly(new Date());
  protected readonly level = signal<AwardLevel | null>(null);
  protected readonly date = signal('');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly valid = computed(() => !!this.level() && /^\d{4}-\d{2}-\d{2}$/.test(this.date()));
  protected readonly statusLabel = computed(() => (this.award ? (STATUS_LABELS[progressStatus(this.award.status)] ?? null) : null));
  protected readonly canApprove = computed(() => this.canReview && !!this.award && progressStatus(this.award.status) === 'Submitted');
  protected readonly canDelete = computed(() => this.canRemove && !!this.award);

  ngOnInit(): void {
    if (this.award) {
      this.level.set(this.award.level);
      this.date.set(this.award.dateAcquired.slice(0, 10));
      this.note.set(this.award.note ?? '');
    }
  }

  protected close(changed: boolean): void {
    void this.modals.dismiss(changed);
  }

  protected async save(): Promise<void> {
    const level = this.level();
    if (!level || !this.valid() || this.busy()) return;
    await this.run(
      () =>
        this.data.saveAward(this.memberKey, {
          memberAwardKey: this.award?.memberAwardKey,
          level,
          dateAcquired: this.date(),
          note: this.note(),
        }),
      'Відзначення збережено.',
      'Не вдалося зберегти відзначення.',
    );
  }

  protected async approve(): Promise<void> {
    const award = this.award;
    if (!award) return;
    await this.run(() => this.data.reviewAward(this.memberKey, award.memberAwardKey, true), 'Відзначення підтверджено.', 'Не вдалося підтвердити.');
  }

  protected async remove(): Promise<void> {
    const award = this.award;
    if (!award) return;
    const sheet = await this.sheets.create({
      header: 'Видалити це відзначення?',
      buttons: [
        { text: 'Видалити', role: 'destructive', data: 'remove' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data !== 'remove') return;
    await this.run(() => this.data.deleteAward(this.memberKey, award.memberAwardKey), 'Відзначення видалено.', 'Не вдалося видалити.');
  }

  private async run(action: () => Promise<unknown>, done: string, failed: string): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      await this.toasts.show(done);
      this.close(true);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, `${failed} Спробуй ще раз.`), 'danger');
    } finally {
      this.busy.set(false);
    }
  }
}
