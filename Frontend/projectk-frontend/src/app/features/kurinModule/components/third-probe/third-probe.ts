import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { EditorModule } from '@openng/optimus-ui/editor';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TagModule } from '@openng/optimus-ui/tag';
import { formatUtcDateTime } from '../../../../shared/functions/utc-date-time.function';
import { DeltaOp, quillDeltaToMarkdown } from '../../../systemModule/functions/quill-delta-to-markdown.function';
import { leadershipRoleDisplayName } from '../../functions/leadership-role-display.function';
import { resolveBadgeImageUrl } from '../../functions/member-skills-view-mapper.function';
import { hasContent, markdownBlocksToDelta, parseProjectMarkdown } from '../../functions/project-markdown.function';
import {
  CAMP_KINDS,
  THIRD_PROBE_STAGES,
  campKindLabel,
  dayLabel,
  fromIsoDate,
  megabytesLabel,
  thirdProbeChecklist,
  toIsoDate
} from '../../functions/third-probe.function';
import { MemberDto } from '../../models/member.dto';
import {
  SaveThirdProbeFileRequestDto,
  SetThirdProbeStagesRequestDto,
  ThirdProbeBadgeDto,
  ThirdProbeCampKind,
  ThirdProbeDto
} from '../../models/probes-and-badges/probe-project.dto';
import { BadgeImageBlobService } from '../../services/probes-and-badges/badge-image-blob.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { ProbeProjectsComponent, refusal } from '../probe-projects/probe-projects';
import { MarkdownViewComponent } from './markdown-view';
import { ThirdProbeCountdownComponent } from './third-probe-countdown';

interface QuillLike {
  getContents(): { ops: DeltaOp[] };
  setContents(delta: unknown, source?: string): unknown;
}

type StageKey = keyof SetThirdProbeStagesRequestDto;

const CERTIFICATE_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'application/pdf'];

const FILE_REFUSALS: Record<string, string> = {
  FieldTooLong: 'Одне з полів задовге.',
  DescriptionTooLong: 'Резюме задовге. Скороти його.',
  ForeignImage: 'У резюме лише текст, без зображень.',
  EndsBeforeStart: 'Табір не може закінчитися раніше, ніж почався.',
  BadgeNotHeld: 'Ця вмілість ще не підтверджена.',
  NothingToConfirm: 'Спершу запиши дату рефлексії, тоді можна підтвердити.',
  CertificateTooLarge: 'Посвідка завелика: до 2 МБ.',
  InvalidCertificate: 'Посвідка — це фото (PNG, JPEG, WebP) або PDF.',
  StorageFull: 'Файли третьої проби вже займають майже все місце. Прибери зайві фото.',
  StagesOutOfOrder: 'Етапи не по черзі: завершити можна після відкриття, іменувати — після завершення.',
  NotProjectProbe: 'Це не третя проба.'
};

/**
 * The «Третя проба» tab as the УПЮ third-prober file has it: how long is left to close it, what is
 * done and what is not, the stages the referent tracks, the mentors and the reflection, the camp
 * with its посвідка, the вмілості, five projects and a spare, and the пластове резюме. The person
 * and whoever may sign their probe write it; leaders confirm the reflection and each project, and
 * set the stages.
 */
@Component({
  selector: 'app-third-probe',
  imports: [
    FormsModule, ButtonModule, DatePickerModule, DialogModule, EditorModule, InputTextModule, SelectModule,
    SkeletonModule, TagModule, ProbeProjectsComponent, MarkdownViewComponent, ThirdProbeCountdownComponent
  ],
  templateUrl: './third-probe.html',
  styleUrl: './third-probe.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ThirdProbeComponent {
  private readonly progress = inject(MemberProgressService);
  private readonly badgeImages = inject(BadgeImageBlobService);
  private resumeEditor: QuillLike | null = null;

  readonly memberKey = input.required<string>();
  readonly probeId = input.required<string>();
  /** For the countdown (birth date, when the viewer may see it) and the offices held during the probe. */
  readonly member = input<MemberDto | null>(null);
  /** A senior's probe is their archive: shown, never written. */
  readonly readOnly = input(false);

  readonly tab = signal<ThirdProbeDto | null>(null);
  readonly isLoading = signal(false);
  readonly loadFailed = signal(false);
  readonly busy = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly fileDialogVisible = signal(false);
  readonly mentorZvyazkovyi = signal('');
  readonly mentorVporiadnyk = signal('');
  readonly mentorReferent = signal('');
  readonly reflectionOn = signal<Date | null>(null);
  readonly reflectionWith = signal('');
  readonly campKind = signal<ThirdProbeCampKind | null>(null);
  readonly campName = signal('');
  readonly campFrom = signal<Date | null>(null);
  readonly campTo = signal<Date | null>(null);
  readonly dialogError = signal<string | null>(null);

  readonly stagesDialogVisible = signal(false);
  readonly stageDates = signal<Record<StageKey, Date | null>>(emptyStages());

  readonly resumeDialogVisible = signal(false);
  readonly showAllBadges = signal(false);

  readonly campKinds = [...CAMP_KINDS];
  readonly stageList = THIRD_PROBE_STAGES;
  readonly resumeFormats = ['bold', 'italic', 'list', 'link', 'header'];
  readonly certificateAccept = CERTIFICATE_TYPES.join(',');

  readonly file = computed(() => this.tab()?.file ?? null);
  readonly canEdit = computed(() => !this.readOnly() && !!this.tab()?.canEdit);
  readonly canSign = computed(() => !this.readOnly() && !!this.tab()?.canSign);
  readonly canUnsign = computed(() => !this.readOnly() && !!this.tab()?.canUnsign);
  readonly checklist = computed(() => this.tab() ? thirdProbeChecklist(this.tab()!) : []);
  readonly doneCount = computed(() => this.checklist().filter(item => item.done).length);
  readonly reflectionLabel = computed(() => dayLabel(this.file()?.reflectionOn ?? null));
  readonly campDates = computed(() => {
    const from = dayLabel(this.file()?.campFrom ?? null);
    const to = dayLabel(this.file()?.campTo ?? null);
    return from && to ? `${from} — ${to}` : from ?? to;
  });
  readonly campKindLabel = computed(() => campKindLabel(this.file()?.campKind ?? null));
  readonly isPdf = computed(() => this.file()?.campCertificate?.contentType === 'application/pdf');
  readonly duringBadges = computed(() => this.tab()?.badges.filter(badge => badge.duringProbe) ?? []);
  readonly earlierBadges = computed(() => this.tab()?.badges.filter(badge => !badge.duringProbe) ?? []);
  readonly resumeBlocks = computed(() => parseProjectMarkdown(this.file()?.resume ?? '', () => null));
  readonly hasResume = computed(() => hasContent(this.resumeBlocks()));
  readonly storage = computed(() => {
    const tab = this.tab();
    return tab?.file ? `${megabytesLabel(tab.file.storedBytes)} з ${megabytesLabel(tab.limits.maxStoredBytes)}` : null;
  });

  readonly mentors = computed(() => {
    const file = this.file();
    return [
      { role: 'Звʼязковий', name: file?.mentorZvyazkovyi },
      { role: 'Впорядник', name: file?.mentorVporiadnyk },
      { role: 'Референт ІІІ проби', name: file?.mentorReferent }
    ];
  });

  /** Each stage with its day, the opening ones first; the derived opening day says where it came from. */
  readonly stages = computed(() => {
    const stages = this.file()?.stages;
    return THIRD_PROBE_STAGES.map(stage => ({
      ...stage,
      day: dayLabel(stages?.[stage.key] ?? null)
    }));
  });

  readonly stageParts = computed(() => [
    { title: 'Відкриття', stages: this.stages().filter(stage => !stage.closing) },
    { title: 'Закриття', stages: this.stages().filter(stage => stage.closing) }
  ]);

  /** Offices held since the probe opened: «Які діловодства ти виконував протягом проби». */
  readonly offices = computed(() => {
    const opened = fromIsoDate(this.file()?.openedOn ?? null);
    return (this.member()?.leadershipHistories ?? [])
      .filter(office => !opened || !office.endDate || new Date(office.endDate) >= opened)
      .map(office => ({
        role: leadershipRoleDisplayName(office.role),
        where: office.groupName ?? null,
        dates: [formatUtcDateTime(office.startDate, { month: 'short', year: 'numeric' }),
                office.endDate ? formatUtcDateTime(office.endDate, { month: 'short', year: 'numeric' }) : 'досі'].join(' — ')
      }));
  });

  constructor() {
    effect(() => this.load(this.memberKey(), this.probeId()));
  }

  badgeImage(badge: ThirdProbeBadgeDto): string | null {
    return badge.imagePath ? this.badgeImages.resolveBadgeImageForDisplay(resolveBadgeImageUrl(badge.imagePath)) : null;
  }

  badgeDate(badge: ThirdProbeBadgeDto): string | null {
    return formatUtcDateTime(badge.confirmedAtUtc, { day: 'numeric', month: 'short', year: 'numeric' });
  }

  onTabChange(tab: ThirdProbeDto): void {
    this.tab.set(tab);
  }

  openFileDialog(): void {
    const file = this.file();
    this.mentorZvyazkovyi.set(file?.mentorZvyazkovyi ?? '');
    this.mentorVporiadnyk.set(file?.mentorVporiadnyk ?? '');
    this.mentorReferent.set(file?.mentorReferent ?? '');
    this.reflectionOn.set(fromIsoDate(file?.reflectionOn ?? null));
    this.reflectionWith.set(file?.reflectionWith ?? '');
    this.campKind.set(file?.campKind ?? null);
    this.campName.set(file?.campName ?? '');
    this.campFrom.set(fromIsoDate(file?.campFrom ?? null));
    this.campTo.set(fromIsoDate(file?.campTo ?? null));
    this.dialogError.set(null);
    this.fileDialogVisible.set(true);
  }

  saveFileDialog(): void {
    this.saveFile({
      mentorZvyazkovyi: this.mentorZvyazkovyi().trim() || null,
      mentorVporiadnyk: this.mentorVporiadnyk().trim() || null,
      mentorReferent: this.mentorReferent().trim() || null,
      reflectionOn: toIsoDate(this.reflectionOn()),
      reflectionWith: this.reflectionWith().trim() || null,
      campKind: this.campKind(),
      campName: this.campName().trim() || null,
      campFrom: toIsoDate(this.campFrom()),
      campTo: toIsoDate(this.campTo())
    }, () => this.fileDialogVisible.set(false));
  }

  openStagesDialog(): void {
    const stages = this.file()?.stages;
    const dates = emptyStages();
    for (const stage of THIRD_PROBE_STAGES) {
      dates[stage.key] = fromIsoDate(stages?.[stage.key] ?? null);
    }
    // A derived opening day is not a stage anyone set; the dialog starts it empty.
    if (this.file()?.openedOnIsDerived) {
      dates.openedOn = null;
    }
    this.stageDates.set(dates);
    this.dialogError.set(null);
    this.stagesDialogVisible.set(true);
  }

  setStage(key: StageKey, value: Date | null): void {
    this.stageDates.update(dates => ({ ...dates, [key]: value }));
  }

  saveStages(): void {
    const dates = this.stageDates();
    const request = Object.fromEntries(THIRD_PROBE_STAGES.map(stage => [stage.key, toIsoDate(dates[stage.key])])) as SetThirdProbeStagesRequestDto;
    this.dialogError.set(null);
    this.run(this.progress.setThirdProbeStages(this.memberKey(), this.probeId(), request), () => this.stagesDialogVisible.set(false), this.dialogError);
  }

  choosePhysical(badgeId: string): void {
    this.saveFile({ physicalBadgeId: badgeId });
  }

  openResume(): void {
    this.dialogError.set(null);
    this.resumeDialogVisible.set(true);
    this.fillResume();
  }

  onResumeInit(event: { editor: QuillLike }): void {
    this.resumeEditor = event.editor;
    this.fillResume();
  }

  saveResume(): void {
    const resume = this.resumeEditor ? quillDeltaToMarkdown(this.resumeEditor.getContents().ops, { imageTarget: () => null }) : '';
    this.saveFile({ resume }, () => this.resumeDialogVisible.set(false));
  }

  confirmReflection(sign: boolean): void {
    this.run(this.progress.confirmReflection(this.memberKey(), this.probeId(), sign));
  }

  onCertificatePicked(event: Event): void {
    const picker = event.target as HTMLInputElement;
    const file = picker.files?.[0];
    picker.value = '';
    if (!file) {
      return;
    }
    const limit = this.tab()?.limits.maxCertificateBytes ?? 0;
    if (file.size > limit) {
      this.errorMessage.set(FILE_REFUSALS['CertificateTooLarge']);
      return;
    }
    if (!CERTIFICATE_TYPES.includes(file.type)) {
      this.errorMessage.set(FILE_REFUSALS['InvalidCertificate']);
      return;
    }
    this.run(this.progress.uploadCampCertificate(this.memberKey(), this.probeId(), file));
  }

  removeCertificate(): void {
    this.run(this.progress.deleteCampCertificate(this.memberKey(), this.probeId()));
  }

  private fillResume(): void {
    if (this.resumeEditor && this.resumeDialogVisible()) {
      this.resumeEditor.setContents(markdownBlocksToDelta(this.resumeBlocks()), 'api');
    }
  }

  /** The file is written whole: what is not changed here goes back as the tab has it. */
  private saveFile(changes: Partial<SaveThirdProbeFileRequestDto>, done?: () => void): void {
    const file = this.file();
    const request: SaveThirdProbeFileRequestDto = {
      mentorZvyazkovyi: file?.mentorZvyazkovyi ?? null,
      mentorVporiadnyk: file?.mentorVporiadnyk ?? null,
      mentorReferent: file?.mentorReferent ?? null,
      reflectionOn: file?.reflectionOn ?? null,
      reflectionWith: file?.reflectionWith ?? null,
      campKind: file?.campKind ?? null,
      campName: file?.campName ?? null,
      campFrom: file?.campFrom ?? null,
      campTo: file?.campTo ?? null,
      physicalBadgeId: file?.physicalBadgeId ?? null,
      resume: file?.resume ?? '',
      ...changes
    };
    this.dialogError.set(null);
    this.run(this.progress.saveThirdProbeFile(this.memberKey(), this.probeId(), request), done, this.dialogError);
  }

  private run(call: Observable<ThirdProbeDto>, done?: () => void, errorTarget = this.errorMessage): void {
    this.busy.set(true);
    this.errorMessage.set(null);
    call.subscribe({
      next: tab => {
        this.busy.set(false);
        this.tab.set(tab);
        done?.();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        errorTarget.set(refusal(error, 'Не вдалося зберегти. Спробуй ще раз.', FILE_REFUSALS));
      }
    });
  }

  private load(memberKey: string, probeId: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.progress.getThirdProbe(memberKey, probeId).subscribe({
      next: tab => {
        this.tab.set(tab);
        this.isLoading.set(false);
      },
      error: () => {
        this.tab.set(null);
        this.loadFailed.set(true);
        this.isLoading.set(false);
      }
    });
  }
}

function emptyStages(): Record<StageKey, Date | null> {
  return Object.fromEntries(THIRD_PROBE_STAGES.map(stage => [stage.key, null])) as Record<StageKey, Date | null>;
}
