import { Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import {
  ActionSheetController,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonTextarea,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { IMAGE_TOO_LARGE, IMAGE_TYPES, MAX_IMAGE_BYTES } from './image-crop';
import { CroppedImage, ImageCropSheet } from './image-crop.sheet';
import { GroupDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { GroupSilhouette } from './group-silhouette';

/** The API's limit on a гурток's description (UpdateGroupRequest.Description). */
const DESCRIPTION_MAX = 1000;

const IMAGE_ERRORS = {
  ImageTooLarge: IMAGE_TOO_LARGE,
  UnsupportedImageType: 'Підтримуються лише PNG, JPEG або WebP.',
  InvalidImage: 'Це не схоже на зображення. Обери інше.',
};

export type GroupEditResult = 'saved' | 'deleted' | null;

/**
 * A new гурток, or the profile of one (web kurin-panel's manage-panel and group-panel's
 * «Редагувати»): its name, description and silhouette, cropped and processed as on the web. The
 * name and «Розформувати гурток» are the Звʼязковий's (web canManageGroups); the description and
 * silhouette belong to whoever the server lets change the гурток. A new silhouette or its removal
 * goes up on «Зберегти», after the гурток itself.
 */
@Component({
  selector: 'app-group-edit-sheet',
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonInput,
    IonTextarea,
    GroupSilhouette,
  ],
  styles: `
    ion-item > div {
      width: 100%;
      padding: 10px 0;
    }
    .silhouette {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 16px 16px 0;
    }
    .silhouette input {
      display: none;
    }
    .silhouette-actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
    }
    .lk-input-box ion-textarea {
      --padding-start: 12px;
      --padding-end: 12px;
      font-size: 16px;
    }
    .hint {
      margin: 0 20px;
      font-size: 13px;
      color: var(--lk-muted);
    }
    .actions {
      padding: 4px 16px 24px;
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button [disabled]="busy()" (click)="close(null)">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ group ? 'Гурток' : 'Новий гурток' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="!valid() || busy()" (click)="save()" data-testid="save-group">
            {{ group ? 'Зберегти' : 'Створити' }}
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <div class="silhouette" data-testid="group-edit-silhouette">
        <app-group-silhouette [size]="96" [url]="shownSilhouette()" />
        <div class="silhouette-actions">
          <ion-button fill="clear" size="small" [disabled]="busy()" (click)="pick()" data-testid="pick-silhouette">
            {{ shownSilhouette() ? 'Замінити сильветку' : 'Завантажити сильветку' }}
          </ion-button>
          @if (shownSilhouette()) {
            <ion-button fill="clear" size="small" color="danger" [disabled]="busy()" (click)="clearSilhouette()" data-testid="clear-silhouette">
              Видалити сильветку
            </ion-button>
          }
        </div>
        <input #fileInput type="file" accept="image/png,image/jpeg,image/webp" (change)="picked($event)" data-testid="silhouette-input" />
      </div>

      <ion-list [inset]="true">
        <ion-list-header><ion-label>Гурток</ion-label></ion-list-header>
        <ion-item-group>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Назва гуртка</span>
              <div class="lk-input-box" [class.lk-invalid]="touched() && !name().trim()">
                <ion-input
                  aria-label="Назва гуртка"
                  placeholder="Наприклад: Сіроманці"
                  autocapitalize="sentences"
                  [disabled]="!canRename"
                  [value]="name()"
                  (ionInput)="setName($any($event).detail.value)"
                  data-testid="group-name"
                />
              </div>
            </div>
          </ion-item>
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">Опис</span>
              <div class="lk-input-box" [class.lk-invalid]="description().length > descriptionMax">
                <ion-textarea
                  aria-label="Опис"
                  [autoGrow]="true"
                  [rows]="3"
                  [counter]="true"
                  [maxlength]="descriptionMax"
                  [value]="description()"
                  (ionInput)="description.set($any($event).detail.value ?? '')"
                  data-testid="group-description"
                />
              </div>
            </div>
          </ion-item>
        </ion-item-group>
      </ion-list>
      @if (!canRename) {
        <p class="hint">Назву гуртка змінює Звʼязковий.</p>
      }

      @if (group && canDelete) {
        <div class="actions">
          <ion-button expand="block" fill="clear" color="danger" class="lk-danger" [disabled]="busy()" (click)="remove()" data-testid="delete-group">
            Розформувати гурток
          </ion-button>
        </div>
      }
    </ion-content>
  `,
})
export class GroupEditSheet implements OnInit, OnDestroy {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');

  // Set by ModalController's componentProps.
  /** The гурток to change; null for a new one. */
  group: GroupDto | null = null;
  kurinKey = '';
  /** Web canManageGroups (Звʼязковий, admin): naming and deleting. A new гурток is always named. */
  canRename = true;
  canDelete = false;

  protected readonly descriptionMax = DESCRIPTION_MAX;
  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly touched = signal(false);
  protected readonly busy = signal(false);
  private readonly picture = signal<CroppedImage | null>(null);
  private readonly preview = signal<string | null>(null);
  private readonly removed = signal(false);

  protected readonly shownSilhouette = computed(() => this.preview() ?? (this.removed() ? null : (this.group?.silhouetteUrl ?? null)));
  protected readonly valid = computed(() => !!this.name().trim() && this.description().length <= DESCRIPTION_MAX);

  ngOnInit(): void {
    this.name.set(this.group?.name ?? '');
    this.description.set(this.group?.description ?? '');
    if (!this.group) this.canRename = true;
  }

  ngOnDestroy(): void {
    const url = this.preview();
    if (url) URL.revokeObjectURL(url);
  }

  protected setName(value: string | null | undefined): void {
    this.touched.set(true);
    this.name.set(value ?? '');
  }

  protected close(result: GroupEditResult): void {
    void this.modals.dismiss(result);
  }

  protected pick(): void {
    this.fileInput()?.nativeElement.click();
  }

  /** Type and size are checked on picking, as the web's onSilhouetteSelected does. */
  protected async picked(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    if (!file) return;
    if (!IMAGE_TYPES.includes(file.type)) {
      await this.toasts.show('Підтримуються лише PNG, JPEG або WebP.', 'danger');
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      await this.toasts.show('Файл має бути не більший за 5 МБ.', 'danger');
      return;
    }
    const modal = await this.modals.create({ component: ImageCropSheet, componentProps: { file, silhouette: true } });
    await modal.present();
    const { data } = await modal.onWillDismiss<CroppedImage | null>();
    if (!data) return;
    this.setPreview(URL.createObjectURL(data.blob));
    this.picture.set(data);
    this.removed.set(false);
  }

  protected clearSilhouette(): void {
    this.setPreview(null);
    this.picture.set(null);
    this.removed.set(true);
  }

  protected async save(): Promise<void> {
    if (!this.valid() || this.busy()) return;
    this.busy.set(true);
    const request = { name: this.name().trim(), description: this.description().trim() || null };
    let group = this.group;
    try {
      if (!group) group = await this.data.createGroup(this.kurinKey, request);
      else if (request.name !== group.name || request.description !== (group.description ?? null)) {
        group = await this.data.updateGroup(group.groupKey, this.canRename ? request : { ...request, name: group.name });
      }
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти гурток. Спробуй ще раз.'), 'danger');
      this.busy.set(false);
      return;
    }
    const created = !this.group;
    // The гурток is saved; a silhouette that does not go up is said, and the sheet still closes.
    let note = created ? 'Гурток створено.' : 'Гурток збережено.';
    let failed = false;
    try {
      const picture = this.picture();
      if (picture) await this.data.uploadSilhouette(group.groupKey, picture.blob, picture.name);
      else if (this.removed() && this.group?.silhouetteUrl) await this.data.deleteSilhouette(group.groupKey);
    } catch (error) {
      failed = true;
      note += ` ${apiErrorText(error, this.picture() ? 'Не вдалося завантажити сильветку.' : 'Не вдалося видалити сильветку.', IMAGE_ERRORS)}`;
    }
    await this.toasts.show(note, failed ? 'danger' : undefined);
    this.busy.set(false);
    this.close('saved');
  }

  protected async remove(): Promise<void> {
    const group = this.group;
    if (!group || this.busy()) return;
    const sheet = await this.sheets.create({
      header: `Розформувати гурток «${group.name}»? Він лишиться в архіві куреня.`,
      buttons: [
        { text: 'Розформувати', role: 'destructive', data: 'remove' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data !== 'remove') return;
    this.busy.set(true);
    try {
      await this.data.deleteGroup(group.groupKey);
      await this.toasts.show('Гурток розформовано.');
      this.close('deleted');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося розформувати гурток. Спробуй ще раз.'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  private setPreview(url: string | null): void {
    const before = this.preview();
    if (before) URL.revokeObjectURL(before);
    this.preview.set(url);
  }
}
