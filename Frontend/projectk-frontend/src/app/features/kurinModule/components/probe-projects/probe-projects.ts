import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { map, Observable, shareReplay, tap } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { EditorModule } from '@openng/optimus-ui/editor';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { RadioButtonModule } from '@openng/optimus-ui/radiobutton';
import { SelectModule } from '@openng/optimus-ui/select';
import { TagModule } from '@openng/optimus-ui/tag';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { formatUtcDateTime } from '../../../../shared/functions/utc-date-time.function';
import { DeltaOp, quillDeltaToMarkdown } from '../../../systemModule/functions/quill-delta-to-markdown.function';
import { hasContent, markdownBlocksToDelta, MarkdownBlock, parseProjectMarkdown } from '../../functions/project-markdown.function';
import {
  PROJECT_AREAS,
  PROJECT_REACHES,
  areaOf,
  dayLabel,
  fromIsoDate,
  reachLabels,
  toIsoDate
} from '../../functions/third-probe.function';
import {
  PROJECT_IMAGE_SCHEME,
  ProbeProjectArea,
  ProbeProjectDto,
  ProbeProjectReach,
  ProbeProjectSavedDto,
  SaveProbeProjectRequestDto,
  ThirdProbeDto
} from '../../models/probes-and-badges/probe-project.dto';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { MarkdownViewComponent } from '../third-probe/markdown-view';

/** The slice of Quill the editors touch; each instance arrives from its own `onInit`. */
interface QuillLike {
  getModule(name: 'toolbar'): { addHandler(name: string, fn: () => void): void } | undefined;
  getModule(name: 'uploader'): { options: { mimetypes?: string[]; handler?: (range: unknown, files: File[]) => void } } | undefined;
  getContents(): { ops: DeltaOp[] };
  getSelection(focus?: boolean): { index: number; length: number } | null;
  getLength(): number;
  insertEmbed(index: number, type: string, value: string, source?: string): unknown;
  insertText(index: number, text: string, source?: string): unknown;
  setSelection(index: number, length: number, source?: string): unknown;
  setContents(delta: unknown, source?: string): unknown;
}

/** What the project is and the person's part in it, then the template's three: what, how, what came of it. */
export type ProjectText = 'description' | 'goal' | 'plan' | 'results';

export const PROJECT_TEXTS: readonly { field: ProjectText; label: string; hint: string }[] = [
  { field: 'description', label: 'Опис і моя роль', hint: 'Що це за проєкт, навіщо його створено, яка твоя роль у ньому — скільки треба абзаців' },
  { field: 'goal', label: 'Ціль', hint: 'Що? Чому? Для кого?' },
  { field: 'plan', label: 'Підготовка', hint: 'План, як я знатиму, що осягнув ціль, з ким співпрацюю, реченець, які є перешкоди' },
  { field: 'results', label: 'Підсумки виконання', hint: 'Що осягнув? Чого навчився? Що би змінив? Фото, афіші — сюди' }
];

/** One project as the page shows it. */
interface ProjectView {
  project: ProbeProjectDto;
  area: { letter: string; label: string } | null;
  reach: string[];
  dates: string | null;
  texts: { label: string; blocks: MarkdownBlock[] }[];
  updatedLabel: string;
}

export const PROJECT_IMAGE_MIME_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

const REFUSALS: Record<string, string> = {
  ImageTooLarge: 'Зображення завелике.',
  UnsupportedImageType: 'Підходять лише PNG, JPEG або WebP.',
  InvalidImageContent: 'Це не зображення, або файл пошкоджений.',
  TooManyImages: 'У проєкті вже повний набір зображень. Прибери зайве й збережи, тоді можна додати нове.',
  TooManyProjects: 'Вже є пʼять проєктів і запасний.',
  TooManyShared: 'Спільних з іншими третьопробниками може бути лише два з пʼяти основних.',
  TitleRequired: 'Дай проєкту назву.',
  FieldTooLong: 'Одне з полів задовге.',
  DescriptionTooLong: 'Текст задовгий. Скороти його.',
  EndsBeforeStart: 'Проєкт не може закінчитися раніше, ніж почався.',
  ForeignImage: 'Зображення можна лише завантажити в проєкт, а не вставити посиланням.',
  StorageFull: 'Файли третьої проби вже займають майже все місце (до 10 МБ). Прибери зайві фото.',
  NotProjectProbe: 'Проєкти є лише в третій пробі.'
};

/**
 * The third probe's projects as the УПЮ template lays them out: area, whom it serves, dates,
 * place, who runs and who coordinates it, then the goal, the plan and the results — each a text
 * with formatting and a few small pictures. Five count, one more waits as the spare. A leader gives
 * «дозвіл» before and marks «успішно виконав» after, with the coordinator's words.
 *
 * Texts leave the editors as Markdown, the same path as «Повідомити про проблему», pictures as keys
 * since their links expire. Every write answers with the whole tab, passed up through `tabChange`.
 */
@Component({
  selector: 'app-probe-projects',
  imports: [
    NgTemplateOutlet, FormsModule, ButtonModule, CheckboxModule, DatePickerModule, DialogModule, EditorModule, InputTextModule,
    RadioButtonModule, SelectModule, TagModule, TextareaModule, EmptyStateComponent, MarkdownViewComponent
  ],
  templateUrl: './probe-projects.html',
  styleUrl: './probe-projects.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProbeProjectsComponent {
  private readonly progress = inject(MemberProgressService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly editors = new Map<ProjectText, QuillLike>();
  /** Picture links in the editors and the keys they stand for. */
  private readonly imageKeys = new Map<string, string>();
  /** The quiet first save of a new project; every picture dropped meanwhile waits for this one. */
  private creating: Observable<string> | null = null;

  readonly memberKey = input.required<string>();
  readonly probeId = input.required<string>();
  readonly tab = input.required<ThirdProbeDto>();
  /** A senior's probe is their archive: shown, never written. */
  readonly readOnly = input(false);
  readonly tabChange = output<ThirdProbeDto>();

  readonly areas = [...PROJECT_AREAS];
  readonly reaches = [...PROJECT_REACHES];
  readonly texts = PROJECT_TEXTS;

  readonly dialogVisible = signal(false);
  readonly editingKey = signal<string | null>(null);
  readonly title = signal('');
  readonly area = signal<ProbeProjectArea | null>(null);
  readonly reach = signal<ProbeProjectReach[]>([]);
  readonly isShared = signal(false);
  readonly sharedWith = signal('');
  readonly startsOn = signal<Date | null>(null);
  readonly endsOn = signal<Date | null>(null);
  readonly location = signal('');
  readonly eventLeads = signal('');
  readonly coordinatorName = signal('');
  readonly coordinatorContact = signal('');
  readonly imageCount = signal(0);
  readonly isSaving = signal(false);
  private readonly uploads = signal(0);
  readonly isUploading = computed(() => this.uploads() > 0);
  readonly errorMessage = signal<string | null>(null);

  readonly completing = signal<ProbeProjectDto | null>(null);
  readonly coordinatorComment = signal('');
  readonly swapping = signal<ProbeProjectDto | null>(null);
  readonly swapWith = signal<string | null>(null);

  readonly editorFormats = ['bold', 'italic', 'strike', 'code', 'list', 'link', 'image', 'header', 'blockquote', 'indent'];
  readonly imageAccept = PROJECT_IMAGE_MIME_TYPES.join(',');

  readonly canEdit = computed(() => !this.readOnly() && this.tab().canEdit);
  readonly canSign = computed(() => !this.readOnly() && this.tab().canSign);
  readonly canUnsign = computed(() => !this.readOnly() && this.tab().canUnsign);
  readonly limits = computed(() => this.tab().limits);
  readonly maxImageLabel = computed(() => megabytes(this.limits().maxImageBytes));
  readonly storedLimitLabel = computed(() => `${megabytes(this.limits().maxStoredBytes)} МБ`);
  readonly views = computed<ProjectView[]>(() => this.tab().projects.map(project => this.view(project)));
  readonly mains = computed(() => this.views().filter(view => !view.project.isSpare));
  readonly spares = computed(() => this.views().filter(view => view.project.isSpare));
  readonly canAdd = computed(() =>
    this.canEdit() && this.tab().projects.length < this.limits().maxMainProjects + this.limits().maxSpareProjects);
  readonly nextIsSpare = computed(() => this.mains().length >= this.limits().maxMainProjects);
  readonly sharedCount = computed(() => this.mains().filter(view => view.project.isShared).length);
  readonly coveredAreas = computed(() => new Set(this.mains().map(view => view.project.area).filter(Boolean)));

  openNew(): void {
    this.openEditor(null);
  }

  edit(project: ProbeProjectDto): void {
    this.openEditor(project);
  }

  remove(project: ProbeProjectDto): void {
    const spareNote = !project.isSpare && this.spares().length ? ' Його місце займе запасний.' : '';
    this.confirmation.confirm({
      header: 'Видалити проєкт',
      message: `Проєкт «${project.title}» зникне разом з текстами й зображеннями.${spareNote}`,
      icon: 'pi pi-exclamation-triangle',
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      accept: () => this.apply(this.progress.deleteProbeProject(this.memberKey(), this.probeId(), project.projectKey))
    });
  }

  approve(project: ProbeProjectDto, sign: boolean): void {
    this.apply(this.progress.signProbeProject(this.memberKey(), this.probeId(), project.projectKey, 'approval', sign));
  }

  openCompletion(project: ProbeProjectDto): void {
    this.coordinatorComment.set('');
    this.completing.set(project);
  }

  complete(): void {
    const project = this.completing();
    if (!project) {
      return;
    }
    const comment = this.coordinatorComment().trim() || null;
    this.completing.set(null);
    this.apply(this.progress.signProbeProject(this.memberKey(), this.probeId(), project.projectKey, 'completion', true, comment));
  }

  uncomplete(project: ProbeProjectDto): void {
    this.apply(this.progress.signProbeProject(this.memberKey(), this.probeId(), project.projectKey, 'completion', false));
  }

  openSwap(spare: ProbeProjectDto): void {
    this.swapWith.set(null);
    this.swapping.set(spare);
  }

  swap(): void {
    const spare = this.swapping();
    const mainKey = this.swapWith();
    if (!spare || !mainKey) {
      return;
    }
    this.swapping.set(null);
    this.apply(this.progress.swapSpareProject(this.memberKey(), this.probeId(), spare.projectKey, mainKey));
  }

  toggleReach(value: ProbeProjectReach, on: boolean): void {
    this.reach.update(list => on ? [...new Set([...list, value])] : list.filter(item => item !== value));
  }

  onEditorInit(field: ProjectText, event: { editor: QuillLike }): void {
    const quill = event.editor;
    this.editors.set(field, quill);
    quill.getModule('toolbar')?.addHandler('image', () => this.pickImage(field));
    const uploader = quill.getModule('uploader');
    if (uploader) {
      uploader.options.mimetypes = PROJECT_IMAGE_MIME_TYPES;
      uploader.options.handler = (_range, files) => files.forEach(file => this.attach(file, field));
    }
    this.fillEditor(field);
  }

  onTextChange(): void {
    let count = 0;
    for (const quill of this.editors.values()) {
      count += quill.getContents().ops.filter(op => typeof op.insert !== 'string' && op.insert.image).length;
    }
    this.imageCount.set(count);
  }

  pickImage(field: ProjectText): void {
    const picker = document.createElement('input');
    picker.type = 'file';
    picker.accept = this.imageAccept;
    picker.addEventListener('change', () => {
      const file = picker.files?.[0];
      if (file) {
        this.attach(file, field);
      }
    });
    picker.click();
  }

  save(): void {
    const request = this.request();
    if (!request) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    const key = this.editingKey();
    const call = key
      ? this.progress.updateProbeProject(this.memberKey(), this.probeId(), key, request)
      : this.progress.addProbeProject(this.memberKey(), this.probeId(), request);

    call.subscribe({
      next: saved => {
        this.isSaving.set(false);
        this.tabChange.emit(saved.tab);
        this.dialogVisible.set(false);
      },
      error: (error: unknown) => {
        this.isSaving.set(false);
        this.errorMessage.set(refusal(error, 'Не вдалося зберегти. Спробуй ще раз.'));
      }
    });
  }

  close(): void {
    this.dialogVisible.set(false);
  }

  private apply(call: Observable<ThirdProbeDto>): void {
    call.subscribe({ next: tab => this.tabChange.emit(tab) });
  }

  private view(project: ProbeProjectDto): ProjectView {
    const when = formatUtcDateTime(project.updatedAtUtc);
    const from = dayLabel(project.startsOn);
    const to = dayLabel(project.endsOn);
    return {
      project,
      area: areaOf(project.area),
      reach: reachLabels(project.reach),
      dates: from && to ? `${from} — ${to}` : from ?? to,
      texts: PROJECT_TEXTS
        .map(text => ({ label: text.label, blocks: parseProjectMarkdown(project[text.field], target => imageUrl(project, target)) }))
        .filter(text => hasContent(text.blocks)),
      updatedLabel: [when ? `Оновлено ${when}` : null, project.updatedByName].filter(Boolean).join(' · ')
    };
  }

  private openEditor(project: ProbeProjectDto | null): void {
    this.creating = null;
    this.editingKey.set(project?.projectKey ?? null);
    this.title.set(project?.title ?? '');
    this.area.set(project?.area ?? this.firstUncoveredArea());
    this.reach.set(project?.reach ?? []);
    this.isShared.set(project?.isShared ?? false);
    this.sharedWith.set(project?.sharedWith ?? '');
    this.startsOn.set(fromIsoDate(project?.startsOn ?? null));
    this.endsOn.set(fromIsoDate(project?.endsOn ?? null));
    this.location.set(project?.location ?? '');
    this.eventLeads.set(project?.eventLeads ?? '');
    this.coordinatorName.set(project?.coordinatorName ?? '');
    this.coordinatorContact.set(project?.coordinatorContact ?? '');
    this.errorMessage.set(null);
    this.imageKeys.clear();
    project?.images.forEach(image => this.imageKeys.set(image.url, image.imageKey));
    this.dialogVisible.set(true);
    PROJECT_TEXTS.forEach(text => this.fillEditor(text.field));
  }

  private firstUncoveredArea(): ProbeProjectArea | null {
    return PROJECT_AREAS.find(area => !this.coveredAreas().has(area.value))?.value ?? null;
  }

  /** An editor exists only while the dialog is open, so it is filled from whichever comes last. */
  private fillEditor(field: ProjectText): void {
    const quill = this.editors.get(field);
    if (!quill || !this.dialogVisible()) {
      return;
    }

    const project = this.tab().projects.find(p => p.projectKey === this.editingKey());
    const blocks = project ? parseProjectMarkdown(project[field], target => imageUrl(project, target)) : [];
    quill.setContents(markdownBlocksToDelta(blocks), 'api');
    this.onTextChange();
  }

  private markdown(field: ProjectText): string {
    const quill = this.editors.get(field);
    if (!quill) {
      return '';
    }

    return quillDeltaToMarkdown(quill.getContents().ops, {
      imageAlt: 'зображення',
      imageTarget: src => {
        const key = this.imageKeys.get(src);
        return key ? `${PROJECT_IMAGE_SCHEME}${key}` : null;
      }
    });
  }

  private request(): SaveProbeProjectRequestDto | null {
    const title = this.title().trim();
    if (!title) {
      this.errorMessage.set(REFUSALS['TitleRequired']);
      return null;
    }

    return {
      title,
      area: this.area(),
      reach: this.reach(),
      isShared: this.isShared(),
      sharedWith: this.isShared() ? this.sharedWith().trim() || null : null,
      startsOn: toIsoDate(this.startsOn()),
      endsOn: toIsoDate(this.endsOn()),
      location: this.location().trim() || null,
      eventLeads: this.eventLeads().trim() || null,
      coordinatorName: this.coordinatorName().trim() || null,
      coordinatorContact: this.coordinatorContact().trim() || null,
      description: this.markdown('description'),
      goal: this.markdown('goal'),
      plan: this.markdown('plan'),
      results: this.markdown('results')
    };
  }

  /**
   * A picture goes into a project that exists. A new one is saved first, quietly, so the person
   * does not have to know about it; that needs a title.
   */
  private attach(file: File, field: ProjectText): void {
    if (file.size > this.limits().maxImageBytes) {
      this.errorMessage.set(`Зображення завелике: до ${this.maxImageLabel()} МБ.`);
      return;
    }
    if (!PROJECT_IMAGE_MIME_TYPES.includes(file.type)) {
      this.errorMessage.set(REFUSALS['UnsupportedImageType']);
      return;
    }

    const key = this.editingKey();
    if (key) {
      this.upload(key, file, field);
      return;
    }

    const request = this.request();
    if (!request) {
      this.errorMessage.set('Спершу дай проєкту назву — тоді можна додати зображення.');
      return;
    }

    this.uploads.update(count => count + 1);
    this.creating ??= this.progress
      .addProbeProject(this.memberKey(), this.probeId(), { ...request, description: '', goal: '', plan: '', results: '' })
      .pipe(
        tap((saved: ProbeProjectSavedDto) => {
          this.editingKey.set(saved.projectKey);
          this.tabChange.emit(saved.tab);
        }),
        map(saved => saved.projectKey),
        shareReplay(1)
      );
    const creating = this.creating;
    creating.subscribe({
      next: projectKey => {
        this.uploads.update(count => count - 1);
        this.upload(projectKey, file, field);
      },
      error: (error: unknown) => {
        if (this.creating === creating) {
          this.creating = null;
        }
        this.uploads.update(count => count - 1);
        this.errorMessage.set(refusal(error, 'Не вдалося зберегти проєкт. Спробуй ще раз.'));
      }
    });
  }

  private upload(projectKey: string, file: File, field: ProjectText): void {
    this.uploads.update(count => count + 1);
    this.errorMessage.set(null);
    this.progress.uploadProbeProjectImage(this.memberKey(), this.probeId(), projectKey, file).subscribe({
      next: image => {
        this.uploads.update(count => count - 1);
        this.imageKeys.set(image.url, image.imageKey);
        this.insertImage(field, image.url);
      },
      error: (error: unknown) => {
        this.uploads.update(count => count - 1);
        const limit = `PNG, JPEG або WebP до ${this.maxImageLabel()} МБ.`;
        this.errorMessage.set(error instanceof HttpErrorResponse && error.status === 413
          ? `Зображення завелике: ${limit}`
          : refusal(error, `Не вдалося завантажити зображення. ${limit}`));
      }
    });
  }

  private insertImage(field: ProjectText, url: string): void {
    const quill = this.editors.get(field);
    if (!quill) {
      return;
    }

    let at = quill.getSelection(true)?.index ?? quill.getLength() - 1;
    if (at > 0) {
      quill.insertText(at, '\n', 'user');
      at += 1;
    }
    quill.insertEmbed(at, 'image', url, 'user');
    quill.insertText(at + 1, '\n', 'user');
    quill.setSelection(at + 2, 0, 'api');
    this.onTextChange();
  }
}

function imageUrl(project: ProbeProjectDto, target: string): string | null {
  if (!target.startsWith(PROJECT_IMAGE_SCHEME)) {
    return null;
  }
  const key = target.slice(PROJECT_IMAGE_SCHEME.length);
  return project.images.find(image => image.imageKey === key)?.url ?? null;
}

function megabytes(bytes: number): string {
  return (bytes / (1024 * 1024)).toLocaleString('uk-UA', { maximumFractionDigits: 1 });
}

export function refusal(error: unknown, fallback: string, refusals: Record<string, string> = REFUSALS): string {
  if (error instanceof HttpErrorResponse && error.status === 400) {
    const code = (error.error as { error?: string } | null)?.error;
    if (code && refusals[code]) {
      return refusals[code];
    }
  }
  return failureDetail(error, fallback);
}
