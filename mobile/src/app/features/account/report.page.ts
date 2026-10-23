import { Component, computed, inject, signal } from '@angular/core';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSpinner,
  IonTitle,
  IonToolbar,
  NavController,
} from '@ionic/angular';
import { Router } from '@angular/router';
import { addIcons } from 'ionicons';
import { checkmarkCircle, closeCircle, image, warning } from 'ionicons/icons';
import { environment } from '../../../environments/environment';
import { apiErrorText } from '../../core/api';
import { ProblemReportReceipt } from './account.models';
import { AccountService } from './account.service';
import { prepareScreenshot } from './screenshot';

/** The server's limit (ReportProblemCommandHandler.MaxScreenshots) and the web's. */
export const MAX_SCREENSHOTS = 5;

const UPLOAD_ERRORS: Record<string, string> = {
  ImageTooLarge: 'Скриншот завеликий: до 5 МБ.',
};

/**
 * «Повідомити про проблему» (the web's report-problem-dialog): what happened, the steps and the
 * expectation, with screenshots from the photo library. Pictures go up first and come back as
 * addresses the report carries; the report lands in the project's public tracker, and the page
 * says so before the person types.
 */
@Component({
  selector: 'app-report',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonInput,
    IonIcon,
    IonSpinner,
  ],
  styles: `
    .wrap {
      max-width: 640px;
      margin: 0 auto;
      padding-bottom: 32px;
    }
    .hint {
      margin: 8px 20px 12px;
      color: var(--lk-muted);
      font-size: 15px;
      line-height: 21px;
    }
    .public {
      display: flex;
      gap: 10px;
      margin: 0 16px 8px;
      padding: 12px 14px;
      border-radius: 12px;
      background: var(--lk-accent-50);
      color: var(--lk-accent-700);
      font-size: 14px;
      line-height: 20px;
    }
    .public ion-icon {
      flex: none;
      font-size: 20px;
    }
    ion-item > .lk-field {
      width: 100%;
      padding: 12px 0;
    }
    .required {
      font-weight: 500;
      color: var(--lk-primary);
    }
    /* A plain textarea in the brand's field box (ion-textarea would put 55 kB into the first load). */
    .lk-input-box textarea {
      display: block;
      width: 100%;
      box-sizing: border-box;
      margin: 0;
      padding: 10px 12px;
      border: 0;
      outline: none;
      resize: none;
      field-sizing: content;
      min-height: calc(var(--rows, 3) * 24px + 20px);
      background: transparent;
      color: var(--lk-ink);
      font: inherit;
      font-size: 16px;
      line-height: 24px;
    }
    .lk-input-box textarea::placeholder {
      color: var(--lk-faint);
      opacity: 1;
    }
    .thumbs {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(84px, 1fr));
      gap: 8px;
      padding: 12px 0;
      width: 100%;
    }
    .thumb {
      position: relative;
      aspect-ratio: 1;
      border-radius: 10px;
      overflow: hidden;
      border: 1px solid var(--lk-line);
    }
    .thumb img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .thumb button {
      position: absolute;
      top: 2px;
      right: 2px;
      width: 44px;
      height: 44px;
      display: flex;
      align-items: flex-start;
      justify-content: flex-end;
      padding: 4px;
      background: transparent;
      border: 0;
      color: #fff;
      font-size: 22px;
      filter: drop-shadow(0 1px 2px rgb(0 0 0 / 50%));
    }
    .file {
      position: absolute;
      width: 1px;
      height: 1px;
      opacity: 0;
      pointer-events: none;
    }
    .error {
      margin: 0 20px 12px;
    }
    .done {
      text-align: center;
      padding: 48px 24px 24px;
    }
    .done ion-icon {
      font-size: 64px;
      color: var(--lk-primary);
    }
    .done h2 {
      margin: 12px 0 8px;
      font-size: 22px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .done p {
      margin: 0 0 8px;
      color: var(--lk-muted);
    }
    .done a {
      color: var(--lk-primary);
    }
    .actions {
      padding: 8px 16px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/more" text="Меню" /></ion-buttons>
        <ion-title>Проблема</ion-title>
        @if (!receipt()) {
          <ion-buttons slot="end">
            <ion-button [strong]="true" [disabled]="!canSend()" (click)="send()" data-testid="send">
              @if (sending()) { <ion-spinner name="crescent" /> } @else { Надіслати }
            </ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <div class="wrap">
        @if (receipt(); as done) {
          <div class="done" data-testid="report-done">
            <ion-icon name="checkmark-circle" aria-hidden="true" />
            <h2>Дякуємо, повідомлення пішло</h2>
            @if (done.issueUrl) {
              <p>
                Його вже видно як
                <a [href]="done.issueUrl" target="_blank" rel="noopener">issue #{{ done.issueNumber }}</a>
                на GitHub. Там же зʼявиться відповідь.
              </p>
            } @else {
              <p>Воно записане на сервері; той, хто його тримає, побачить його в журналі.</p>
            }
          </div>
          <div class="actions">
            <ion-button expand="block" (click)="close()">Готово</ion-button>
          </div>
        } @else {
          <p class="hint">
            Знайшов помилку чи щось працює не так, як мало б? Поясни своїми словами, що ти робив і що побачив.
          </p>
          <div class="public" role="note">
            <ion-icon name="warning" aria-hidden="true" />
            <span>
              Повідомлення потрапляє у <strong>публічний</strong> список задач проєкту на GitHub. Не пиши імен, телефонів і
              пошт, своїх чи чужих; на скриншоті закрий те, чого не має бачити хтось сторонній.
            </span>
          </div>

          <ion-list [inset]="true">
            <ion-item-group>
              <ion-item>
                <div class="lk-field">
                  <span class="lk-field__label">Коротко, в один рядок</span>
                  <div class="lk-input-box">
                    <ion-input
                      data-testid="report-title"
                      aria-label="Коротко, в один рядок"
                      [maxlength]="120"
                      placeholder="Наприклад: «Не зберігається дата народження»"
                      [value]="title()"
                      (ionInput)="title.set(text($event))"
                    />
                  </div>
                </div>
              </ion-item>
              <ion-item>
                <div class="lk-field">
                  <span class="lk-field__label">Що сталося <span class="required">обовʼязково</span></span>
                  <div class="lk-input-box">
                    <textarea
                      data-testid="report-description"
                      aria-label="Що сталося"
                      rows="4"
                      style="--rows: 4"
                      maxlength="8000"
                      placeholder="Що ти робив, що побачив, і що з цього не так"
                      [value]="description()"
                      (input)="description.set(area($event))"
                    ></textarea>
                  </div>
                </div>
              </ion-item>
              <ion-item>
                <div class="lk-field">
                  <span class="lk-field__label">Кроки, щоб відтворити</span>
                  <div class="lk-input-box">
                    <textarea
                      data-testid="report-steps"
                      aria-label="Кроки, щоб відтворити"
                      rows="3"
                      style="--rows: 3"
                      maxlength="4000"
                      placeholder="1. Відкрити картку учасника&#10;2. Натиснути «Редагувати»&#10;3. Змінити дату і зберегти"
                      [value]="steps()"
                      (input)="steps.set(area($event))"
                    ></textarea>
                  </div>
                </div>
              </ion-item>
              <ion-item>
                <div class="lk-field">
                  <span class="lk-field__label">Що мало статися</span>
                  <div class="lk-input-box">
                    <textarea
                      data-testid="report-expected"
                      aria-label="Що мало статися"
                      rows="2"
                      style="--rows: 2"
                      maxlength="4000"
                      placeholder="Наприклад: дата зберігається і видна в картці"
                      [value]="expected()"
                      (input)="expected.set(area($event))"
                    ></textarea>
                  </div>
                </div>
              </ion-item>
            </ion-item-group>
          </ion-list>

          <ion-list [inset]="true">
            <ion-list-header><ion-label>Скриншоти</ion-label></ion-list-header>
            <ion-item-group>
              @if (screenshots().length) {
                <ion-item>
                  <div class="thumbs" data-testid="thumbs">
                    @for (url of screenshots(); track url) {
                      <div class="thumb">
                        <img [src]="url" alt="Скриншот до повідомлення" />
                        <button type="button" (click)="remove(url)" aria-label="Прибрати скриншот">
                          <ion-icon name="close-circle" aria-hidden="true" />
                        </button>
                      </div>
                    }
                  </div>
                </ion-item>
              }
              <ion-item
                [button]="true"
                [detail]="false"
                [disabled]="uploading() || screenshots().length >= max"
                (click)="picker.click()"
                data-testid="add-screenshot"
              >
                <ion-icon slot="start" name="image" aria-hidden="true" color="primary" />
                <ion-label color="primary">Додати скриншот</ion-label>
                @if (uploading()) {
                  <ion-spinner slot="end" name="crescent" />
                } @else {
                  <ion-note slot="end">{{ screenshots().length }} / {{ max }}</ion-note>
                }
              </ion-item>
            </ion-item-group>
          </ion-list>
          <input
            #picker
            class="file"
            type="file"
            accept="image/*"
            multiple
            tabindex="-1"
            aria-hidden="true"
            data-testid="screenshot-input"
            (change)="picked($event)"
          />

          @if (error(); as message) {
            <p class="lk-field__error error" role="alert">{{ message }}</p>
          }
        }
      </div>
    </ion-content>
  `,
})
export class ReportPage {
  private readonly service = inject(AccountService);
  private readonly router = inject(Router);
  private readonly nav = inject(NavController);
  protected readonly max = MAX_SCREENSHOTS;

  protected readonly title = signal('');
  protected readonly description = signal('');
  protected readonly steps = signal('');
  protected readonly expected = signal('');
  protected readonly screenshots = signal<string[]>([]);
  protected readonly uploading = signal(false);
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly receipt = signal<ProblemReportReceipt | null>(null);
  protected readonly canSend = computed(
    () => this.description().trim().length > 0 && !this.uploading() && !this.sending(),
  );

  constructor() {
    addIcons({ checkmarkCircle, closeCircle, image, warning });
  }

  protected text(event: Event): string {
    return String((event as CustomEvent<{ value?: string | null }>).detail.value ?? '');
  }

  protected area(event: Event): string {
    return (event.target as HTMLTextAreaElement).value;
  }

  /** One at a time, in the order picked, until the limit. */
  protected async picked(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.error.set(null);
    for (const file of files) {
      if (this.screenshots().length >= MAX_SCREENSHOTS) {
        this.error.set(`Не більше ${MAX_SCREENSHOTS} скриншотів на одне повідомлення.`);
        return;
      }
      if (!(await this.attach(file))) return;
    }
  }

  protected remove(url: string): void {
    this.screenshots.update((list) => list.filter((item) => item !== url));
  }

  protected async send(): Promise<void> {
    if (!this.canSend()) return;
    this.sending.set(true);
    this.error.set(null);
    try {
      this.receipt.set(
        await this.service.reportProblem({
          title: this.title().trim() || null,
          // Plain text is Markdown already; the server appends the screenshots under it.
          description: this.description().trim(),
          steps: this.steps().trim() || null,
          expected: this.expected().trim() || null,
          route: this.router.url,
          appVersion: `${environment.version} (PWA)`,
          screenshotUrls: this.screenshots(),
        }),
      );
    } catch (error) {
      this.error.set(apiErrorText(error, 'Не вдалося надіслати. Спробуй ще раз за хвилину.'));
    } finally {
      this.sending.set(false);
    }
  }

  protected async close(): Promise<void> {
    await this.nav.navigateBack('/tabs/more');
  }

  private async attach(file: File): Promise<boolean> {
    this.uploading.set(true);
    try {
      const { blob, name } = await prepareScreenshot(file);
      const { url } = await this.service.uploadScreenshot(blob, name);
      this.screenshots.update((list) => [...list, url]);
      return true;
    } catch (error) {
      this.error.set(
        apiErrorText(error, 'Не вдалося додати скриншот. PNG, JPEG або WebP до 5 МБ.', UPLOAD_ERRORS),
      );
      return false;
    } finally {
      this.uploading.set(false);
    }
  }
}
