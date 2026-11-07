import { Component, OnInit, inject, signal, viewChild } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonList,
  IonListHeader,
  IonLabel,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { ThirdProbeDto, ThirdProbeFileRequest, ThirdProbeStagesRequest } from './kurin.models';
import { KurinApi } from './kurin.service';
import { MarkdownBlock, parseProjectMarkdown } from './project-markdown';
import { RichEditor } from './rich-editor';
import { THIRD_PROBE_STAGES } from './third-probe';

const REFUSALS = {
  FieldTooLong: 'Одне з полів задовге.',
  DescriptionTooLong: 'Резюме задовге.',
  EndsBeforeStart: 'Табір не може закінчитися раніше, ніж почався.',
  StagesOutOfOrder: 'Етапи не по черзі: завершити можна після відкриття, іменувати — після завершення.',
};

type FileField = 'mentorZvyazkovyi' | 'mentorVporiadnyk' | 'mentorReferent' | 'reflectionOn' | 'reflectionWith' | 'campName' | 'campFrom' | 'campTo';

/**
 * The third-probe file on the phone (web third-probe dialogs): mentors by role, the reflection, the
 * camp and the résumé. Sent whole, what the sheet does not show goes back as the tab has it.
 * Dismisses with the whole tab after a save.
 */
@Component({
  selector: 'app-third-probe-file-sheet',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonButton, IonContent, IonList, IonListHeader, IonLabel, IonItem, IonInput,
    IonSelect, IonSelectOption, IonSpinner, RichEditor,
  ],
  styles: `
    ion-item > div { width: 100%; padding: 10px 0; }
    .resume { padding: 12px 16px 24px; }
    .resume h3 { margin: 0 0 8px; font-size: 15px; font-weight: 700; color: var(--lk-ink); }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-button (click)="close(null)">Скасувати</ion-button></ion-buttons>
        <ion-title>Третя проба</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="busy()" (click)="save()" data-testid="save-third-probe">
            @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      @for (group of groups; track group.title) {
        <ion-list [inset]="true">
          <ion-list-header><ion-label>{{ group.title }}</ion-label></ion-list-header>
          @for (field of group.fields; track field.key) {
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">{{ field.label }}</span>
                <div class="lk-input-box">
                  <ion-input [type]="field.date ? 'date' : 'text'" [attr.aria-label]="field.label" maxlength="200" [placeholder]="field.placeholder ?? ''"
                    [value]="values()[field.key]" (ionInput)="set(field.key, $any($event).detail.value ?? '')" />
                </div>
              </div>
            </ion-item>
          }
          @if (group.title === 'Табір') {
            <ion-item>
              <ion-select label="Який табір" interface="action-sheet" cancelText="Скасувати" placeholder="КВТ чи інший"
                [value]="campKind()" (ionChange)="campKind.set($any($event).detail.value)">
                <ion-select-option value="Kvt">КВТ</ion-select-option>
                <ion-select-option value="Alternative">Альтернативний табір</ion-select-option>
              </ion-select>
            </ion-item>
          }
        </ion-list>
      }
      <div class="resume">
        <h3>Пластове резюме</h3>
        <app-rich-editor [initial]="resume" [withImages]="false" placeholder="Коли вступив, які проби, табори, вмілості й ролі мав, чим живеш поза Пластом" />
      </div>
    </ion-content>
  `,
})
export class ThirdProbeFileSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly toasts = inject(Toasts);
  private readonly resumeEditor = viewChild.required(RichEditor);

  // Set by ModalController's componentProps.
  memberKey = '';
  probeId = '';
  tab!: ThirdProbeDto;

  protected readonly groups: readonly { title: string; fields: readonly { key: FileField; label: string; date?: boolean; placeholder?: string }[] }[] = [
    {
      title: 'Ментори',
      fields: [
        { key: 'mentorZvyazkovyi', label: 'Звʼязковий' },
        { key: 'mentorVporiadnyk', label: 'Впорядник' },
        { key: 'mentorReferent', label: 'Референт ІІІ проби' },
      ],
    },
    {
      title: 'Рефлексія зі звʼязковим',
      fields: [
        { key: 'reflectionOn', label: 'Коли відбулася', date: true },
        { key: 'reflectionWith', label: 'З ким', placeholder: 'Звʼязковий чи референт' },
      ],
    },
    {
      title: 'Табір',
      fields: [
        { key: 'campName', label: 'Назва табору' },
        { key: 'campFrom', label: 'Від', date: true },
        { key: 'campTo', label: 'До', date: true },
      ],
    },
  ];

  protected resume: MarkdownBlock[] = [];
  protected readonly values = signal<Record<FileField, string>>({
    mentorZvyazkovyi: '', mentorVporiadnyk: '', mentorReferent: '', reflectionOn: '', reflectionWith: '', campName: '', campFrom: '', campTo: '',
  });
  protected readonly campKind = signal<'Kvt' | 'Alternative' | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    const file = this.tab.file;
    this.values.set({
      mentorZvyazkovyi: file?.mentorZvyazkovyi ?? '',
      mentorVporiadnyk: file?.mentorVporiadnyk ?? '',
      mentorReferent: file?.mentorReferent ?? '',
      reflectionOn: file?.reflectionOn ?? '',
      reflectionWith: file?.reflectionWith ?? '',
      campName: file?.campName ?? '',
      campFrom: file?.campFrom ?? '',
      campTo: file?.campTo ?? '',
    });
    this.campKind.set(file?.campKind ?? null);
    this.resume = parseProjectMarkdown(file?.resume ?? '', () => null);
  }

  protected set(key: FileField, value: string): void {
    this.values.update((all) => ({ ...all, [key]: value }));
  }

  protected close(tab: ThirdProbeDto | null): void {
    void this.modals.dismiss(tab);
  }

  protected async save(): Promise<void> {
    if (this.busy()) return;
    const v = this.values();
    const text = (value: string) => value.trim() || null;
    const body: ThirdProbeFileRequest = {
      mentorZvyazkovyi: text(v.mentorZvyazkovyi),
      mentorVporiadnyk: text(v.mentorVporiadnyk),
      mentorReferent: text(v.mentorReferent),
      reflectionOn: text(v.reflectionOn),
      reflectionWith: text(v.reflectionWith),
      campKind: this.campKind(),
      campName: text(v.campName),
      campFrom: text(v.campFrom),
      campTo: text(v.campTo),
      physicalBadgeId: this.tab.file?.physicalBadgeId ?? null,
      resume: this.resumeEditor().markdown(),
    };
    this.busy.set(true);
    try {
      const tab = await this.data.saveThirdProbeFile(this.memberKey, this.probeId, body);
      await this.toasts.show('Збережено.');
      this.close(tab);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', REFUSALS), 'danger');
    } finally {
      this.busy.set(false);
    }
  }
}

/**
 * The referent's stages on the phone (web «Етапи проби» dialog): the day each happened, empty for
 * not yet. Sent whole. Dismisses with the whole tab after a save.
 */
@Component({
  selector: 'app-third-probe-stages-sheet',
  imports: [IonHeader, IonToolbar, IonTitle, IonButtons, IonButton, IonContent, IonList, IonListHeader, IonLabel, IonItem, IonInput, IonSpinner],
  styles: `ion-item > div { width: 100%; padding: 10px 0; }`,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-button (click)="close(null)">Скасувати</ion-button></ion-buttons>
        <ion-title>Етапи проби</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="busy()" (click)="save()" data-testid="save-stages">
            @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      @for (part of parts; track part.title) {
        <ion-list [inset]="true">
          <ion-list-header><ion-label>{{ part.title }}</ion-label></ion-list-header>
          @for (stage of part.stages; track stage.key) {
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">{{ stage.label }}@if (stage.hint) { · {{ stage.hint }} }</span>
                <div class="lk-input-box">
                  <ion-input type="date" [attr.aria-label]="stage.label" [clearInput]="true" [value]="dates()[stage.key]"
                    (ionInput)="set(stage.key, $any($event).detail.value ?? '')" />
                </div>
              </div>
            </ion-item>
          }
        </ion-list>
      }
    </ion-content>
  `,
})
export class ThirdProbeStagesSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly toasts = inject(Toasts);

  // Set by ModalController's componentProps.
  memberKey = '';
  probeId = '';
  tab!: ThirdProbeDto;

  protected readonly parts = [
    { title: 'Відкриття', stages: THIRD_PROBE_STAGES.filter((stage) => !stage.closing) },
    { title: 'Закриття', stages: THIRD_PROBE_STAGES.filter((stage) => stage.closing) },
  ];
  protected readonly dates = signal({} as Record<keyof ThirdProbeStagesRequest, string>);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    const stages = this.tab.file?.stages;
    const dates = {} as Record<keyof ThirdProbeStagesRequest, string>;
    for (const stage of THIRD_PROBE_STAGES) dates[stage.key] = stages?.[stage.key] ?? '';
    this.dates.set(dates);
  }

  protected set(key: keyof ThirdProbeStagesRequest, value: string): void {
    this.dates.update((all) => ({ ...all, [key]: value }));
  }

  protected close(tab: ThirdProbeDto | null): void {
    void this.modals.dismiss(tab);
  }

  protected async save(): Promise<void> {
    if (this.busy()) return;
    const dates = this.dates();
    const body = Object.fromEntries(THIRD_PROBE_STAGES.map((stage) => [stage.key, dates[stage.key] || null])) as ThirdProbeStagesRequest;
    this.busy.set(true);
    try {
      const tab = await this.data.setThirdProbeStages(this.memberKey, this.probeId, body);
      await this.toasts.show('Етапи збережено.');
      this.close(tab);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', REFUSALS), 'danger');
    } finally {
      this.busy.set(false);
    }
  }
}
