import { Component, NgZone, computed, effect, inject, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import {
  ActionSheetController,
  AlertController,
  IonButton,
  IonCard,
  IonCardContent,
  IonSpinner,
  ModalController,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { shortDate, utcDate } from './kurin.labels';
import { ProbeProjectDto, ThirdProbeDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { ProbeProjectSheet } from './probe-project.sheet';
import { MarkdownBlock, hasContent, parseProjectMarkdown } from './project-markdown';
import { THIRD_PROBE_STAGES, areaOf, countdownParts, dayWord, reachLabels, thirdProbeChecklist, youthEndsOn } from './third-probe';
import { ThirdProbeFileSheet, ThirdProbeStagesSheet } from './third-probe-file.sheet';

const IMAGE_SCHEME = 'project-image:';
const CERTIFICATE_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'application/pdf'];

const TEXTS: readonly { field: 'description' | 'goal' | 'plan' | 'results'; label: string }[] = [
  { field: 'description', label: 'Опис і моя роль' },
  { field: 'goal', label: 'Ціль' },
  { field: 'plan', label: 'Підготовка' },
  { field: 'results', label: 'Підсумки' },
];

const REFUSALS = {
  CertificateTooLarge: 'Посвідка завелика: до 2 МБ.',
  InvalidCertificate: 'Посвідка — це фото (PNG, JPEG, WebP) або PDF.',
  StorageFull: 'Файли третьої проби вже займають майже все місце (до 10 МБ).',
  TooManyShared: 'Спільних може бути лише два з пʼяти основних.',
  NothingToConfirm: 'Спершу запиши дату рефлексії.',
};

interface ProjectView {
  project: ProbeProjectDto;
  area: { letter: string; label: string } | null;
  facts: { label: string; value: string }[];
  texts: { label: string; blocks: MarkdownBlock[] }[];
}

/**
 * The «Третя проба» tab on the phone (web third-probe): the countdown to closing it, what is done,
 * the referent's stages, mentors and reflection, the camp with its посвідка, the вмілості, five
 * projects and the spare, and the résumé — read and written here as on the web. Every write answers
 * with the whole tab, passed up through `changed`.
 */
@Component({
  selector: 'app-third-probe',
  imports: [NgTemplateOutlet, IonCard, IonCardContent, IonButton, IonSpinner],
  styles: `
    h2 { margin: 0 0 4px; font-size: 18px; font-weight: 700; color: var(--lk-ink); }
    h3 { margin: 0; font-size: 16px; font-weight: 700; color: var(--lk-ink); overflow-wrap: anywhere; }
    h4 { margin: 10px 0 2px; font-size: 12px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase; color: var(--lk-muted); }
    .meta { margin: 4px 0 0; font-size: 13px; color: var(--lk-muted); }
    .meta strong, .line { color: var(--lk-ink); }
    .line { margin: 4px 0 0; font-size: 14px; }
    .digits { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 6px; margin-top: 8px; }
    .unit { display: flex; flex-direction: column; align-items: center; padding: 8px 2px; border: 1px solid var(--lk-line); border-radius: 12px; }
    .value { font-size: 26px; font-weight: 800; line-height: 1.1; font-variant-numeric: tabular-nums; color: var(--lk-primary); }
    .soon .value { color: var(--lk-accent-500, var(--lk-primary)); }
    .name { font-size: 11px; color: var(--lk-muted); }
    .checks { display: flex; flex-direction: column; gap: 6px; margin-top: 6px; }
    .check { display: flex; gap: 8px; align-items: baseline; font-size: 14px; color: var(--lk-ink); }
    .check .mark { width: 16px; color: var(--lk-faint); }
    .check.done .mark { color: var(--lk-primary); }
    .head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .badge { border: 1px solid var(--lk-line); border-radius: 999px; padding: 1px 8px; font-size: 11px; font-weight: 600; color: var(--lk-muted); }
    .badge.ok { border-color: var(--lk-primary); color: var(--lk-primary); }
    .letter { border: 1px solid var(--lk-primary); color: var(--lk-primary); border-radius: 8px; width: 22px; height: 22px;
      display: inline-flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700; flex: none; }
    .facts { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 2px 10px; margin: 8px 0 0; font-size: 13px; }
    .facts dt { color: var(--lk-muted); }
    .facts dd { margin: 0; color: var(--lk-ink); overflow-wrap: anywhere; }
    .mentors { margin: 6px 0 0; padding-left: 18px; font-size: 14px; color: var(--lk-ink); }
    .spare { border: 1px dashed var(--lk-line); }
    .vml { display: flex; gap: 10px; align-items: center; margin-top: 8px; }
    .vml img { width: 36px; height: 36px; object-fit: contain; flex: none; }
    .vml > div { flex: 1; min-width: 0; }
    .certificate { display: block; max-width: 100%; max-height: 220px; margin-top: 8px; border: 1px solid var(--lk-line); border-radius: 8px; }
    .actions { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 8px; }
    .upload { display: none; }
    .text { color: var(--lk-ink); font-size: 15px; line-height: 1.5; overflow-wrap: anywhere; }
    .text p { margin: 0; min-height: 0.75em; }
    .text .h { font-weight: 700; margin-top: 6px; }
    .text .li { position: relative; }
    .text .mk { display: inline-block; width: 20px; margin-left: -20px; color: var(--lk-muted); }
    .text blockquote { margin: 4px 0; padding-left: 10px; border-left: 1px solid var(--lk-line); color: var(--lk-muted); }
    .text pre, .text code { font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 0.875em; border: 1px solid var(--lk-line); border-radius: 8px; }
    .text code { padding: 0 4px; }
    .text pre { padding: 8px 10px; white-space: pre-wrap; }
    .text a { color: var(--lk-primary); }
    .text img { display: block; max-width: 100%; max-height: 320px; margin: 8px 0; border: 1px solid var(--lk-line); border-radius: 8px; }
    .b { font-weight: 600; }
    .i { font-style: italic; }
    .s { text-decoration: line-through; }
  `,
  template: `
    @if (opened() && deadline()) {
      <ion-card data-testid="third-probe-countdown" [class.soon]="parts().days < 90">
        <ion-card-content>
          @if (!parts().isOver) {
            <h2>До закриття третьої проби</h2>
            <p class="meta">останній день — {{ endLabel() }}, {{ data().limits.youthEndsAtAge }}-річчя</p>
            <div class="digits" role="timer">
              <div class="unit"><span class="value">{{ parts().days }}</span><span class="name">{{ days() }}</span></div>
              <div class="unit"><span class="value">{{ pad(parts().hours) }}</span><span class="name">год</span></div>
              <div class="unit"><span class="value">{{ pad(parts().minutes) }}</span><span class="name">хв</span></div>
              <div class="unit"><span class="value">{{ pad(parts().seconds) }}</span><span class="name">с</span></div>
            </div>
          } @else {
            <h2>Строк третьої проби минув</h2>
            <p class="meta">{{ endLabel() }}</p>
          }
        </ion-card-content>
      </ion-card>
    }

    @if (opened()) {
    <ion-card data-testid="third-probe-checklist">
      <ion-card-content>
        <h2>Що вже є</h2>
        <div class="checks">
          @for (item of checklist(); track item.label) {
            <div class="check" [class.done]="item.done">
              <span class="mark">{{ item.done ? '✓' : '–' }}</span>
              <span><strong>{{ item.label }}</strong><span class="meta">&nbsp;· {{ item.detail }}</span></span>
            </div>
          }
        </div>
      </ion-card-content>
    </ion-card>
    }

    <ion-card data-testid="third-probe-stages">
      <ion-card-content>
        <h2>{{ opened() ? 'Закриття проби' : 'Відкриття проби' }}</h2>
        @if (opened()) {
          <p class="line" data-testid="third-probe-opened">✓ Пробу відкрито {{ day(data().file?.stages?.openedOn ?? null) }}</p>
        }
        <div class="checks">
          @for (stage of stages(); track stage.key) {
            <div class="check" [class.done]="stage.day">
              <span class="mark">{{ stage.day ? '✓' : '–' }}</span>
              <span><strong>{{ stage.label }}</strong><span class="meta">&nbsp;· {{ stage.day ?? '—' }}</span></span>
            </div>
          }
        </div>
        @if (canSign()) {
          <div class="actions"><ion-button size="small" fill="outline" (click)="editStages()">Змінити етапи</ion-button></div>
        }
      </ion-card-content>
    </ion-card>

    <ion-card data-testid="third-probe-file">
      <ion-card-content>
        <h2>{{ opened() ? 'Ментори й рефлексія' : 'Ментори' }}</h2>
        <ul class="mentors">
          <li><span class="meta">Звʼязковий:</span> {{ data().file?.mentorZvyazkovyi || '—' }}</li>
          <li><span class="meta">Впорядник:</span> {{ data().file?.mentorVporiadnyk || '—' }}</li>
          <li><span class="meta">Референт ІІІ проби:</span> {{ data().file?.mentorReferent || '—' }}</li>
        </ul>
        @if (opened()) {
        <p class="line"><strong>Рефлексія:</strong> {{ day(data().file?.reflectionOn ?? null) ?? 'ще не відбулася' }}@if (data().file?.reflectionConfirmedAtUtc) { ✓ }</p>
        <p class="line"><strong>Табір:</strong> {{ data().file?.campName ?? 'ще не вказано' }}</p>
        @if (data().file?.campCertificate; as cert) {
          @if (cert.contentType === 'application/pdf') {
            <p class="line"><a [href]="cert.url" target="_blank" rel="noopener noreferrer">Посвідка (PDF)</a></p>
          } @else {
            <a [href]="cert.url" target="_blank" rel="noopener noreferrer"><img class="certificate" [src]="cert.url" alt="Посвідка табору" /></a>
          }
        }
        <p class="meta">Зараховує табір край; Лілейка лише зберігає, що це був за табір.</p>
        }
        <div class="actions">
          @if (canEdit()) {
            <ion-button size="small" fill="outline" (click)="editFile()" data-testid="edit-third-probe">Редагувати</ion-button>
          }
          @if (canEdit() && opened()) {
            <ion-button size="small" fill="clear" (click)="certificateInput.click()">
              {{ data().file?.campCertificate ? 'Замінити посвідку' : 'Прикріпити посвідку' }}
            </ion-button>
            <input #certificateInput class="upload" type="file" [accept]="certificateAccept" (change)="onCertificate($event)" />
          }
          @if (canSign() && data().file?.reflectionOn && !data().file?.reflectionConfirmedAtUtc) {
            <ion-button size="small" fill="clear" (click)="confirmReflection(true)">Підтвердити рефлексію</ion-button>
          } @else if (canUnsign() && data().file?.reflectionConfirmedAtUtc) {
            <ion-button size="small" fill="clear" color="medium" (click)="confirmReflection(false)">Зняти підтвердження</ion-button>
          }
          @if (busy()) { <ion-spinner name="crescent" /> }
        </div>
      </ion-card-content>
    </ion-card>

    @if (opened()) {
    <ion-card data-testid="third-probe-badges">
      <ion-card-content>
        <h2>Вмілості</h2>
        <p class="meta">під час проби {{ during().length }} з {{ data().limits.requiredBadges }}, одна — з тіловиховання</p>
        @for (badge of during(); track badge.badgeId) {
          <div class="vml">
            @if (badge.imagePath) { <img [src]="badge.imagePath" alt="" loading="lazy" /> }
            <div>
              <p class="line"><strong>{{ badge.title }}</strong></p>
              @if (badge.badgeId === data().file?.physicalBadgeId) { <span class="badge ok">Тіловиховання</span> }
            </div>
            @if (canEdit() && badge.badgeId !== data().file?.physicalBadgeId) {
              <ion-button size="small" fill="clear" (click)="choosePhysical(badge.badgeId)">Тіловиховання</ion-button>
            }
          </div>
        }
        @if (earlier().length) {
          <p class="meta">До проби: {{ earlierTitles() }}</p>
        }
      </ion-card-content>
    </ion-card>

    @if (canAdd()) {
      <div class="actions" style="padding: 0 16px">
        <ion-button expand="block" (click)="editProject(null)" data-testid="add-project">
          {{ nextIsSpare() ? 'Додати запасний проєкт' : 'Додати проєкт' }}
        </ion-button>
      </div>
    }

    @for (item of projects(); track item.project.projectKey) {
      <ion-card data-testid="probe-project" [class.spare]="item.project.isSpare">
        <ion-card-content>
          <div class="head">
            @if (item.area) { <span class="letter">{{ item.area.letter }}</span> }
            <h3>{{ item.project.title }}</h3>
            @if (item.project.isSpare) { <span class="badge">Запасний</span> }
            @if (item.project.isShared) { <span class="badge">Спільний</span> }
            @if (item.project.completedAtUtc) { <span class="badge ok">Виконано</span> } @else if (item.project.approvedAtUtc) { <span class="badge ok">Дозвіл є</span> }
          </div>
          @if (item.facts.length) {
            <dl class="facts">
              @for (fact of item.facts; track fact.label) { <dt>{{ fact.label }}</dt><dd>{{ fact.value }}</dd> }
            </dl>
          }
          @for (text of item.texts; track text.label) {
            <h4>{{ text.label }}</h4>
            <div class="text"><ng-container *ngTemplateOutlet="blocksTpl; context: { $implicit: text.blocks }" /></div>
          }
          @if (item.project.coordinatorComment) {
            <p class="meta">«{{ item.project.coordinatorComment }}» — координатор</p>
          }
          @if (canEdit() || canSign()) {
            <div class="actions">
              @if (canEdit()) { <ion-button size="small" fill="outline" (click)="editProject(item.project)">Редагувати</ion-button> }
              <ion-button size="small" fill="clear" (click)="projectActions(item.project)">Ще…</ion-button>
            </div>
          }
        </ion-card-content>
      </ion-card>
    }

    <ion-card>
      <ion-card-content>
        <h2>Пластове резюме</h2>
        @if (resume().length) {
          <div class="text"><ng-container *ngTemplateOutlet="blocksTpl; context: { $implicit: resume() }" /></div>
        } @else {
          <p class="meta">Ще не написано.</p>
        }
        @if (canEdit()) {
          <div class="actions"><ion-button size="small" fill="outline" (click)="editFile()">{{ resume().length ? 'Редагувати' : 'Написати' }}</ion-button></div>
        }
      </ion-card-content>
    </ion-card>
    }

    <ng-template #blocksTpl let-blocks>
      @for (block of blocks; track $index) {
        @switch (block.kind) {
          @case ('heading') { <p class="h"><ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" /></p> }
          @case ('item') {
            <p class="li" [style.padding-left.px]="20 + block.indent * 20">
              <span class="mk" aria-hidden="true">{{ marker(block) }}</span>
              <ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" />
            </p>
          }
          @case ('quote') { <blockquote><ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" /></blockquote> }
          @case ('code') { <pre>{{ block.text }}</pre> }
          @default { <p><ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" /></p> }
        }
      }
    </ng-template>

    <ng-template #runsTpl let-runs>
      @for (run of runs; track $index) {
        @if (run.image) {
          <img [src]="run.image" [alt]="run.text || 'Зображення проєкту'" loading="lazy" />
        } @else if (run.link) {
          <a [href]="run.link" target="_blank" rel="noopener noreferrer" [class.b]="run.bold" [class.i]="run.italic" [class.s]="run.strike">{{ run.text }}</a>
        } @else if (run.code) {
          <code>{{ run.text }}</code>
        } @else {
          <span [class.b]="run.bold" [class.i]="run.italic" [class.s]="run.strike">{{ run.text }}</span>
        }
      }
    </ng-template>
  `,
})
export class ThirdProbeCard {
  private readonly api = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly alerts = inject(AlertController);
  private readonly toasts = inject(Toasts);

  readonly memberKey = input.required<string>();
  readonly probeId = input.required<string>();
  readonly data = input.required<ThirdProbeDto>();
  /** Null when the viewer may not see the birth date: then there is no clock. */
  readonly dateOfBirth = input<string | null | undefined>(null);
  /** A senior's probe is their archive: shown, never written. */
  readonly readOnly = input(false);
  readonly changed = output<ThirdProbeDto>();

  private readonly now = signal(new Date());
  protected readonly busy = signal(false);
  protected readonly certificateAccept = CERTIFICATE_TYPES.join(',');

  protected readonly canEdit = computed(() => !this.readOnly() && this.data().canEdit);
  protected readonly canSign = computed(() => !this.readOnly() && this.data().canSign);
  protected readonly canUnsign = computed(() => !this.readOnly() && this.data().canUnsign);
  protected readonly mains = computed(() => this.data().projects.filter((p) => !p.isSpare));
  protected readonly nextIsSpare = computed(() => this.mains().length >= this.data().limits.maxMainProjects);
  protected readonly canAdd = computed(
    () => this.canEdit() && this.data().projects.length < this.data().limits.maxMainProjects + this.data().limits.maxSpareProjects,
  );

  protected readonly deadline = computed(() => youthEndsOn(this.dateOfBirth(), this.data().limits.youthEndsAtAge));
  protected readonly parts = computed(() => countdownParts(this.deadline() ?? this.now(), this.now()));
  protected readonly days = computed(() => dayWord(this.parts().days));
  protected readonly endLabel = computed(() => this.deadline()?.toLocaleDateString('uk-UA', { day: 'numeric', month: 'long', year: 'numeric' }) ?? '');
  protected readonly checklist = computed(() => thirdProbeChecklist(this.data()));
  /** Open once the referent sets «Пробу відкрито» (web third-probe isOpened); until then only the way there shows. */
  protected readonly opened = computed(() => !!this.data().file?.stages?.openedOn);
  /** The stages of the phase now — opening, then closing — those done and the next one (web stageParts). */
  protected readonly stages = computed(() => {
    const phase = THIRD_PROBE_STAGES.filter((stage) => stage.closing === this.opened())
      .map((stage) => ({ ...stage, day: this.day(this.data().file?.stages?.[stage.key] ?? null) }));
    const next = phase.findIndex((stage) => !stage.day);
    return phase.filter((stage, index) => !!stage.day || index === next);
  });
  protected readonly during = computed(() => this.data().badges.filter((badge) => badge.duringProbe));
  protected readonly earlier = computed(() => this.data().badges.filter((badge) => !badge.duringProbe));
  protected readonly earlierTitles = computed(() => this.earlier().map((badge) => badge.title).join(', '));
  protected readonly resume = computed(() => {
    const blocks = parseProjectMarkdown(this.data().file?.resume ?? '', () => null);
    return hasContent(blocks) ? blocks : [];
  });

  protected readonly projects = computed<ProjectView[]>(() =>
    [...this.data().projects]
      .sort((a, b) => Number(a.isSpare) - Number(b.isSpare))
      .map((project) => ({
        project,
        area: areaOf(project.area),
        facts: [
          ['Ділянка', areaOf(project.area)?.label],
          ['Для кого', reachLabels(project.reach).join(', ')],
          ['Коли', [this.day(project.startsOn), this.day(project.endsOn)].filter(Boolean).join(' — ')],
          ['Де', project.location],
          ['Координатор', [project.coordinatorName, project.coordinatorContact].filter(Boolean).join(' · ')],
        ]
          .filter(([, value]) => !!value)
          .map(([label, value]) => ({ label: label!, value: value! })),
        texts: TEXTS.map((text) => ({
          label: text.label,
          blocks: parseProjectMarkdown(project[text.field] ?? '', (target) => imageUrl(project, target)),
        })).filter((text) => hasContent(text.blocks)),
      })),
  );

  constructor() {
    const zone = inject(NgZone);
    effect((onCleanup) => {
      if (!this.deadline() || this.parts().isOver) return;
      const timer = zone.runOutsideAngular(() => setInterval(() => this.now.set(new Date()), 1000));
      onCleanup(() => clearInterval(timer));
    });
  }

  protected pad(value: number): string {
    return String(value).padStart(2, '0');
  }

  protected day(value: string | null): string | null {
    if (!value) return null;
    return shortDate(utcDate(`${value}T12:00:00Z`));
  }

  protected marker(block: MarkdownBlock): string {
    if (block.kind !== 'item') return '';
    switch (block.list) {
      case 'ordered':
        return `${block.ordinal}.`;
      case 'checked':
        return '☑';
      case 'unchecked':
        return '☐';
      default:
        return '•';
    }
  }

  protected async editFile(): Promise<void> {
    await this.openSheet(ThirdProbeFileSheet, {});
  }

  protected async editStages(): Promise<void> {
    await this.openSheet(ThirdProbeStagesSheet, {});
  }

  protected async editProject(project: ProbeProjectDto | null): Promise<void> {
    await this.openSheet(ProbeProjectSheet, { project });
  }

  protected async confirmReflection(sign: boolean): Promise<void> {
    await this.run(() => this.api.confirmReflection(this.memberKey(), this.probeId(), sign));
  }

  protected async choosePhysical(badgeId: string): Promise<void> {
    const file = this.data().file;
    await this.run(() =>
      this.api.saveThirdProbeFile(this.memberKey(), this.probeId(), {
        mentorZvyazkovyi: file?.mentorZvyazkovyi ?? null,
        mentorVporiadnyk: file?.mentorVporiadnyk ?? null,
        mentorReferent: file?.mentorReferent ?? null,
        reflectionOn: file?.reflectionOn ?? null,
        reflectionWith: file?.reflectionWith ?? null,
        campKind: file?.campKind ?? null,
        campName: file?.campName ?? null,
        campFrom: file?.campFrom ?? null,
        campTo: file?.campTo ?? null,
        physicalBadgeId: badgeId,
        resume: file?.resume ?? '',
      }),
    );
  }

  protected async onCertificate(event: Event): Promise<void> {
    const picker = event.target as HTMLInputElement;
    const file = picker.files?.[0];
    picker.value = '';
    if (!file) return;
    if (file.size > this.data().limits.maxCertificateBytes) {
      await this.toasts.show(REFUSALS.CertificateTooLarge, 'danger');
      return;
    }
    if (!CERTIFICATE_TYPES.includes(file.type)) {
      await this.toasts.show(REFUSALS.InvalidCertificate, 'danger');
      return;
    }
    await this.run(() => this.api.uploadCampCertificate(this.memberKey(), this.probeId(), file));
  }

  /** Everything else about a project, in one action sheet: confirmations, the spare swap, removing. */
  protected async projectActions(project: ProbeProjectDto): Promise<void> {
    const buttons: { text: string; role?: string; data: string }[] = [];
    if (this.canSign() && !project.approvedAtUtc) buttons.push({ text: 'Дати дозвіл', data: 'approve' });
    if (this.canUnsign() && project.approvedAtUtc && !project.completedAtUtc) buttons.push({ text: 'Зняти дозвіл', data: 'unapprove' });
    if (this.canSign() && !project.completedAtUtc) buttons.push({ text: 'Позначити виконаним', data: 'complete' });
    if (this.canUnsign() && project.completedAtUtc) buttons.push({ text: 'Зняти «виконано»', data: 'uncomplete' });
    if (this.canEdit() && project.isSpare && this.mains().length) buttons.push({ text: 'Поставити замість основного…', data: 'swap' });
    if (this.canEdit()) buttons.push({ text: 'Видалити проєкт', role: 'destructive', data: 'delete' });
    const sheet = await this.sheets.create({ header: project.title, buttons: [...buttons, { text: 'Скасувати', role: 'cancel', data: '' }] });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    const member = this.memberKey();
    const probe = this.probeId();
    switch (data) {
      case 'approve':
      case 'unapprove':
        await this.run(() => this.api.signProbeProject(member, probe, project.projectKey, 'approval', data === 'approve'));
        break;
      case 'uncomplete':
        await this.run(() => this.api.signProbeProject(member, probe, project.projectKey, 'completion', false));
        break;
      case 'complete':
        await this.complete(project);
        break;
      case 'swap':
        await this.swap(project);
        break;
      case 'delete':
        await this.run(() => this.api.deleteProbeProject(member, probe, project.projectKey));
        break;
    }
  }

  private async complete(project: ProbeProjectDto): Promise<void> {
    const alert = await this.alerts.create({
      header: 'Проєкт виконано',
      message: 'Перепиши коментар координатора, якщо він його дав.',
      inputs: [{ name: 'comment', type: 'textarea', placeholder: 'Коментар координатора' }],
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        { text: 'Позначити', role: 'confirm' },
      ],
    });
    await alert.present();
    const { role, data } = await alert.onWillDismiss<{ values: { comment?: string } }>();
    if (role !== 'confirm') return;
    const comment = data?.values.comment?.trim() || null;
    await this.run(() => this.api.signProbeProject(this.memberKey(), this.probeId(), project.projectKey, 'completion', true, comment));
  }

  private async swap(spare: ProbeProjectDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: `«${spare.title}» стане основним замість…`,
      buttons: [
        ...this.mains().map((main) => ({ text: `${areaOf(main.area)?.letter ?? '·'}. ${main.title}`, data: main.projectKey })),
        { text: 'Скасувати', role: 'cancel', data: '' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (!data) return;
    await this.run(() => this.api.swapSpareProject(this.memberKey(), this.probeId(), spare.projectKey, data));
  }

  private async openSheet(component: unknown, props: Record<string, unknown>): Promise<void> {
    const modal = await this.modals.create({
      component: component as never,
      componentProps: { memberKey: this.memberKey(), probeId: this.probeId(), tab: this.data(), ...props },
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<ThirdProbeDto | null>();
    if (data) this.changed.emit(data);
  }

  private async run(action: () => Promise<ThirdProbeDto>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      this.changed.emit(await action());
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти. Спробуй ще раз.', REFUSALS), 'danger');
    } finally {
      this.busy.set(false);
    }
  }
}

function imageUrl(project: ProbeProjectDto, target: string): string | null {
  if (!target.startsWith(IMAGE_SCHEME)) return null;
  const key = target.slice(IMAGE_SCHEME.length);
  return project.images.find((image) => image.imageKey === key)?.url ?? null;
}
