import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, model, output, signal } from '@angular/core';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ImageCropperComponent, ImageCroppedEvent } from 'ngx-image-cropper';

/**
 * Обрізати картинку перед завантаженням: сильветка гуртка, емблема старшого куреня. Сам лише обрізає;
 * обробку (чорно-біле, обводку) сторінка кладе збоку власними полями і тоді дає свій `previewUrl`.
 * Без нього попередній перегляд — сама обрізана картинка.
 */
@Component({
  selector: 'app-image-crop-dialog',
  imports: [DialogModule, ButtonModule, ImageCropperComponent],
  templateUrl: './image-crop-dialog.html',
  styleUrl: './image-crop-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImageCropDialogComponent {
  readonly visible = model(false);
  readonly header = input.required<string>();
  readonly file = input<File | undefined>();
  /** Ширина до висоти; null — рамку тягнуть як завгодно (емблема буває щитом, а не квадратом). */
  readonly aspectRatio = input<number | null>(1);
  readonly saving = input(false);
  /** Сторінка ще обробляє обрізане — «Завантажити» чекає. */
  readonly processing = input(false);
  readonly previewUrl = input<string | null>(null);
  readonly previewAlt = input('Попередній перегляд');

  readonly cropped = output<Blob>();
  /** Обрізана картинка, PNG; null — рамку ще не вибрано. */
  readonly save = output<Blob | null>();
  readonly cancelled = output<void>();

  private readonly croppedBlob = signal<Blob | null>(null);
  private readonly ownPreview = signal<string | null>(null);
  readonly shownPreview = computed(() => this.previewUrl() ?? this.ownPreview());

  constructor() {
    inject(DestroyRef).onDestroy(() => this.setOwnPreview(null));
  }

  onCropped(event: ImageCroppedEvent): void {
    if (!event.blob) {
      return;
    }

    this.croppedBlob.set(event.blob);
    this.setOwnPreview(URL.createObjectURL(event.blob));
    this.cropped.emit(event.blob);
  }

  onSave(): void {
    this.save.emit(this.croppedBlob());
  }

  onCancel(): void {
    if (this.saving()) {
      return;
    }

    this.cancelled.emit();
  }

  /** Діалог закрили хрестиком чи Esc — так само, як «Скасувати»; обрізане з минулого разу не лишається. */
  onHide(): void {
    this.croppedBlob.set(null);
    this.setOwnPreview(null);
    this.cancelled.emit();
  }

  private setOwnPreview(url: string | null): void {
    const previous = this.ownPreview();
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    this.ownPreview.set(url);
  }
}
