import { Component, OnInit, computed, inject, signal, viewChildren } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonCheckbox,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonList,
  IonNote,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { ProbeProjectArea, ProbeProjectDto, ProbeProjectReach, ProbeProjectRequest, ThirdProbeDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { MarkdownBlock, parseProjectMarkdown } from './project-markdown';
import { IMAGE_MIME_TYPES, RichEditor } from './rich-editor';
import { PROJECT_AREAS, sharedWhilePending } from './third-probe';

const IMAGE_SCHEME = 'project-image:';

type TextField = 'description' | 'goal' | 'plan' | 'results';

const TEXTS: readonly { field: TextField; label: string; hint: string }[] = [
  { field: 'description', label: 'Опис і моя роль', hint: 'Що це за проєкт, навіщо, яка твоя роль — скільки треба абзаців' },
  { field: 'goal', label: 'Ціль', hint: 'Що? Чому? Для кого?' },
  { field: 'plan', label: 'Підготовка', hint: 'План, як знатиму, що осягнув ціль, з ким, реченець, перешкоди' },
  { field: 'results', label: 'Підсумки виконання', hint: 'Що осягнув, чого навчився, що б змінив; фото й афіші' },
];

const REACHES: readonly { value: ProbeProjectReach; label: string }[] = [
  { value: 'GroupOrKurin', label: 'гурток або курінь' },
  { value: 'Stanytsia', label: 'станиця' },
  { value: 'UkrainianCommunity', label: 'українська громада' },
  { value: 'WiderSociety', label: 'ширша громада' },
];

const REFUSALS = {
  ImageTooLarge: 'Зображення завелике: до 1 МБ.',
  UnsupportedImageType: 'Підходять лише PNG, JPEG або WebP.',
  InvalidImageContent: 'Це не зображення, або файл пошкоджений.',
  TooManyImages: 'У проєкті вже пʼять зображень. Прибери зайве й збережи.',
  StorageFull: 'Файли третьої проби вже займають майже все місце (до 10 МБ).',
  TooManyProjects: 'Вже є пʼять проєктів і запасний.',
  TooManyShared: 'Спільних може бути лише два з пʼяти основних.',
  TitleRequired: 'Дай проєкту назву.',
  EndsBeforeStart: 'Проєкт не може закінчитися раніше, ніж почався.',
  DescriptionTooLong: 'Текст задовгий.',
};

/**
 * A third-probe project on the phone (web probe-projects dialog): the template's fields and the
 * four texts, with pictures from the camera roll. A new project is saved quietly before its first
 * picture, since pictures go into a project that exists. Dismisses with the whole tab after a save.
 */
@Component({
  selector: 'app-probe-project-sheet',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonButton, IonContent, IonList, IonItem, IonInput, IonSelect,
    IonSelectOption, IonCheckbox, IonNote, IonSpinner, RichEditor,
  ],
  styles: `
    ion-item > div { width: 100%; padding: 10px 0; }
    .text { padding: 12px 16px 4px; }
    .text h3 { margin: 0; font-size: 15px; font-weight: 700; color: var(--lk-ink); }
    .text p { margin: 2px 0 8px; font-size: 13px; color: var(--lk-muted); }
    .limits { display: block; padding: 4px 16px 24px; font-size: 13px; color: var(--lk-muted); }
    .reach { display: flex; flex-direction: column; gap: 8px; padding: 6px 0; }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-button (click)="close(null)">Скасувати</ion-button></ion-buttons>
        <ion-title>{{ project ? 'Проєкт' : isSpare() ? 'Запасний проєкт' : 'Новий проєкт' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="busy() || uploading()" (click)="save()" data-testid="save-project">
            @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item>
          <div class="lk-field">
            <span class="lk-field__label">Назва проєкту</span>
            <div class="lk-input-box">
              <ion-input aria-label="Назва проєкту" maxlength="200" [value]="title()" (ionInput)="title.set($any($event).detail.value ?? '')" data-testid="project-title" />
            </div>
          </div>
        </ion-item>
        <ion-item>
          <ion-select label="Тематика (ділянка)" interface="action-sheet" cancelText="Скасувати" placeholder="Обери"
            [value]="area()" (ionChange)="area.set($any($event).detail.value)">
            @for (item of areas; track item.value) {
              <ion-select-option [value]="item.value">{{ item.letter }}. {{ item.label }}</ion-select-option>
            }
          </ion-select>
        </ion-item>
        <ion-item>
          <div>
            <span class="lk-field__label">Корисний для</span>
            <div class="reach">
              @for (item of reaches; track item.value) {
                <ion-checkbox labelPlacement="end" justify="start" [checked]="reach().includes(item.value)"
                  (ionChange)="toggleReach(item.value, $any($event).detail.checked)">{{ item.label }}</ion-checkbox>
              }
            </div>
          </div>
        </ion-item>
        <ion-item>
          <div class="lk-field">
            <span class="lk-field__label">Дата початку</span>
            <div class="lk-input-box"><ion-input type="date" aria-label="Дата початку" [value]="startsOn()" (ionInput)="startsOn.set($any($event).detail.value ?? '')" /></div>
          </div>
        </ion-item>
        <ion-item>
          <div class="lk-field">
            <span class="lk-field__label">Дата закінчення</span>
            <div class="lk-input-box"><ion-input type="date" aria-label="Дата закінчення" [value]="endsOn()" (ionInput)="endsOn.set($any($event).detail.value ?? '')" /></div>
          </div>
        </ion-item>
        @for (field of shortFields; track field.key) {
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">{{ field.label }}</span>
              <div class="lk-input-box">
                <ion-input [attr.aria-label]="field.label" maxlength="200" [placeholder]="field.placeholder ?? ''"
                  [value]="short()[field.key]" (ionInput)="setShort(field.key, $any($event).detail.value ?? '')" />
              </div>
            </div>
          </ion-item>
        }
        <ion-item>
          <ion-checkbox labelPlacement="end" justify="start" [checked]="isShared()" (ionChange)="isShared.set($any($event).detail.checked)">
            Спільний з іншими третьопробниками
          </ion-checkbox>
        </ion-item>
        @if (isShared()) {
          <ion-item>
            <div class="lk-field">
              <span class="lk-field__label">З ким</span>
              <div class="lk-input-box"><ion-input aria-label="З ким" maxlength="200" [value]="sharedWith()" (ionInput)="sharedWith.set($any($event).detail.value ?? '')" /></div>
            </div>
          </ion-item>
        }
      </ion-list>

      @for (text of texts; track text.field) {
        <div class="text">
          <h3>{{ text.label }}</h3>
          <p>{{ text.hint }}</p>
          <app-rich-editor [initial]="blocks[text.field]" (imagePicked)="attach($event, text.field)" />
        </div>
      }
      <ion-note class="limits">
        @if (uploading()) { Завантажую… · }
        До {{ tab.limits.maxImagesPerProject }} зображень на проєкт, кожне до 1 МБ (PNG, JPEG, WebP); усі файли проби — до 10 МБ.
      </ion-note>
    </ion-content>
  `,
})
export class ProbeProjectSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly toasts = inject(Toasts);
  private readonly editors = viewChildren(RichEditor);
  /** Picture links in the editors and the keys they stand for. */
  private readonly imageKeys = new Map<string, string>();
  private latest: ThirdProbeDto | null = null;

  // Set by ModalController's componentProps.
  memberKey = '';
  probeId = '';
  tab!: ThirdProbeDto;
  project: ProbeProjectDto | null = null;

  protected readonly areas = PROJECT_AREAS;
  protected readonly reaches = REACHES;
  protected readonly texts = TEXTS;
  protected readonly shortFields: readonly { key: 'location' | 'eventLeads' | 'coordinatorName' | 'coordinatorContact'; label: string; placeholder?: string }[] = [
    { key: 'location', label: 'Локація', placeholder: 'Країна, область, місце' },
    { key: 'eventLeads', label: 'Провід заходу' },
    { key: 'coordinatorName', label: 'Координатор / комендант', placeholder: 'Хто курує й дає рекомендацію' },
    { key: 'coordinatorContact', label: 'Контакт координатора', placeholder: 'Телефон чи пошта' },
  ];

  protected blocks: Record<TextField, MarkdownBlock[]> = { description: [], goal: [], plan: [], results: [] };
  protected readonly title = signal('');
  protected readonly area = signal<ProbeProjectArea | null>(null);
  protected readonly reach = signal<ProbeProjectReach[]>([]);
  protected readonly startsOn = signal('');
  protected readonly endsOn = signal('');
  protected readonly short = signal({ location: '', eventLeads: '', coordinatorName: '', coordinatorContact: '' });
  protected readonly isShared = signal(false);
  protected readonly sharedWith = signal('');
  protected readonly busy = signal(false);
  private readonly uploads = signal(0);
  protected readonly uploading = computed(() => this.uploads() > 0);
  private readonly key = signal<string | null>(null);
  protected readonly isSpare = computed(() => !this.project && this.tab.projects.filter((p) => !p.isSpare).length >= this.tab.limits.maxMainProjects);

  ngOnInit(): void {
    const p = this.project;
    this.key.set(p?.projectKey ?? null);
    this.title.set(p?.title ?? '');
    this.area.set(p?.area ?? PROJECT_AREAS.find((a) => !this.tab.projects.some((x) => !x.isSpare && x.area === a.value))?.value ?? null);
    this.reach.set(p?.reach ?? []);
    this.startsOn.set(p?.startsOn ?? '');
    this.endsOn.set(p?.endsOn ?? '');
    this.short.set({
      location: p?.location ?? '',
      eventLeads: p?.eventLeads ?? '',
      coordinatorName: p?.coordinatorName ?? '',
      coordinatorContact: p?.coordinatorContact ?? '',
    });
    this.isShared.set(p?.isShared ?? false);
    this.sharedWith.set(p?.sharedWith ?? '');
    p?.images.forEach((image) => this.imageKeys.set(image.url, image.imageKey));
    if (p) {
      const resolve = (target: string) => imageUrl(p, target);
      this.blocks = {
        description: parseProjectMarkdown(p.description, resolve),
        goal: parseProjectMarkdown(p.goal, resolve),
        plan: parseProjectMarkdown(p.plan, resolve),
        results: parseProjectMarkdown(p.results, resolve),
      };
    }
  }

  protected setShort(key: 'location' | 'eventLeads' | 'coordinatorName' | 'coordinatorContact', value: string): void {
    this.short.update((all) => ({ ...all, [key]: value }));
  }

  protected toggleReach(value: ProbeProjectReach, on: boolean): void {
    this.reach.update((list) => (on ? [...new Set([...list, value])] : list.filter((item) => item !== value)));
  }

  protected close(tab: ThirdProbeDto | null): void {
    void this.modals.dismiss(tab ?? this.latest);
  }

  protected async save(): Promise<void> {
    const body = this.request();
    if (!body || this.busy()) return;
    this.busy.set(true);
    try {
      const saved = await this.data.saveProbeProject(this.memberKey, this.probeId, this.key(), body);
      await this.toasts.show('Проєкт збережено.');
      this.close(saved.tab);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', REFUSALS), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  /** A picture goes into a project that exists; a new one is saved first, which needs a title. */
  protected async attach(file: File, field: TextField): Promise<void> {
    if (file.size > this.tab.limits.maxImageBytes) {
      await this.toasts.show(REFUSALS.ImageTooLarge, 'danger');
      return;
    }
    if (!IMAGE_MIME_TYPES.includes(file.type)) {
      await this.toasts.show(REFUSALS.UnsupportedImageType, 'danger');
      return;
    }
    this.uploads.update((count) => count + 1);
    try {
      const key = await this.projectKey();
      if (!key) return;
      const image = await this.data.uploadProbeProjectImage(this.memberKey, this.probeId, key, file);
      this.imageKeys.set(image.url, image.imageKey);
      this.editor(field)?.insertImage(image.url);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося завантажити зображення. PNG, JPEG або WebP до 1 МБ.', REFUSALS), 'danger');
    } finally {
      this.uploads.update((count) => count - 1);
    }
  }

  /** The project to put pictures into; a new one is saved first, which needs a title. */
  private async projectKey(): Promise<string | null> {
    return this.key() ?? (this.request() ? await this.create() : null);
  }

  private readonly create = sharedWhilePending(async () => {
    const body = this.request()!;
    const saved = await this.data.saveProbeProject(this.memberKey, this.probeId, null, { ...body, description: '', goal: '', plan: '', results: '' });
    this.key.set(saved.projectKey);
    this.latest = saved.tab;
    return saved.projectKey;
  });

  private editor(field: TextField): RichEditor | undefined {
    return this.editors()[TEXTS.findIndex((text) => text.field === field)];
  }

  private markdown(field: TextField): string {
    return this.editor(field)?.markdown((src) => (this.imageKeys.has(src) ? `${IMAGE_SCHEME}${this.imageKeys.get(src)}` : null)) ?? '';
  }

  private request(): ProbeProjectRequest | null {
    const title = this.title().trim();
    if (!title) {
      void this.toasts.show(REFUSALS.TitleRequired, 'danger');
      return null;
    }
    const short = this.short();
    return {
      title,
      area: this.area(),
      reach: this.reach(),
      isShared: this.isShared(),
      sharedWith: this.isShared() ? this.sharedWith().trim() || null : null,
      startsOn: this.startsOn() || null,
      endsOn: this.endsOn() || null,
      location: short.location.trim() || null,
      eventLeads: short.eventLeads.trim() || null,
      coordinatorName: short.coordinatorName.trim() || null,
      coordinatorContact: short.coordinatorContact.trim() || null,
      description: this.markdown('description'),
      goal: this.markdown('goal'),
      plan: this.markdown('plan'),
      results: this.markdown('results'),
    };
  }
}

function imageUrl(project: ProbeProjectDto, target: string): string | null {
  if (!target.startsWith(IMAGE_SCHEME)) return null;
  const key = target.slice(IMAGE_SCHEME.length);
  return project.images.find((image) => image.imageKey === key)?.url ?? null;
}
