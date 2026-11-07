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
import { honourSection, toDateOnly } from './kurin.labels';
import { CatalogHonour, MemberAwardDto } from './kurin.models';
import { KurinApi } from './kurin.service';

/**
 * A senior відзначення (web member-senior-awards-tile): which one from the catalogue, the date and a
 * note. It stands as soon as it is saved; nobody confirms it. Dismisses with `true` after any change.
 */
@Component({
  selector: 'app-senior-award-sheet',
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
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="!valid() || busy()" (click)="save()" data-testid="save-senior-award">Зберегти</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item>
            <ion-select
              label="Відзначення"
              interface="alert"
              cancelText="Скасувати"
              okText="Обрати"
              placeholder="Обери"
              [value]="catalogId()"
              (ionChange)="catalogId.set($any($event).detail.value)"
              data-testid="senior-award-item"
            >
              @for (item of options(); track item.id) {
                <ion-select-option [value]="item.id">{{ item.title }}{{ section(item) ? ' · ' + section(item) : '' }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Дата здобуття</span>
              <div class="lk-input-box">
                <ion-input type="date" aria-label="Дата здобуття" [max]="today" [value]="date()" (ionInput)="date.set($any($event).detail.value ?? '')" />
              </div>
            </div>
          </ion-item>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">За що</span>
              <div class="lk-input-box">
                <ion-textarea aria-label="За що" [autoGrow]="true" [rows]="2" [value]="note()" (ionInput)="note.set($any($event).detail.value ?? '')" />
              </div>
            </div>
          </ion-item>
        </ion-item-group>
      </ion-list>

      @if (award) {
        <div class="actions">
          <ion-button expand="block" fill="clear" color="danger" class="lk-danger" [disabled]="busy()" (click)="remove()">
            @if (busy()) { <ion-spinner name="crescent" /> } @else { Видалити відзначення }
          </ion-button>
        </div>
      }
    </ion-content>
  `,
})
export class SeniorAwardSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);

  // Set by ModalController's componentProps.
  memberKey = '';
  award: MemberAwardDto | null = null;
  honours: CatalogHonour[] = [];

  protected readonly today = toDateOnly(new Date());
  protected readonly section = honourSection;
  protected readonly catalogId = signal<string | null>(null);
  protected readonly date = signal('');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  private readonly items = signal<CatalogHonour[]>([]);
  protected readonly options = computed(() => this.items().filter((item) => item.kind === 'Award'));
  protected readonly valid = computed(() => !!this.catalogId() && /^\d{4}-\d{2}-\d{2}$/.test(this.date()));

  ngOnInit(): void {
    this.items.set(this.honours);
    this.catalogId.set(this.award?.catalogId ?? null);
    this.date.set(this.award?.dateAcquired.slice(0, 10) ?? this.today);
    this.note.set(this.award?.note ?? '');
  }

  protected close(changed: boolean): void {
    void this.modals.dismiss(changed);
  }

  protected async save(): Promise<void> {
    const catalogId = this.catalogId();
    if (!catalogId || !this.valid() || this.busy()) return;
    await this.run(
      () =>
        this.data.saveAward(this.memberKey, {
          memberAwardKey: this.award?.memberAwardKey,
          catalogId,
          dateAcquired: this.date(),
          note: this.note().trim() || undefined,
        }),
      'Відзначення збережено.',
      'Не вдалося зберегти відзначення.',
    );
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
