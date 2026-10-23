import { Component, ElementRef, inject, OnInit, ChangeDetectionStrategy, viewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TableModule } from '@openng/optimus-ui/table';
import { MemberService } from '../../services/member-service/member.service';
import { MemberDto } from '../../models/member.dto';
import { ButtonModule } from '@openng/optimus-ui/button';
import { GroupService } from '../../services/group-service/group.service';
import { GroupChevronComponent } from '../../components/group-chevron/group-chevron';
import { GroupDto } from '../../models/group.dto';
import { MemberListComponent } from '../../components/member-list/member-list';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';
import { EntityService } from '../../../authModule/services/entity-service/entity.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { LeadershipPanelComponent } from '../../components/leadership/leadership-panel/leadership-panel';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { ImageCropperComponent, ImageCroppedEvent } from 'ngx-image-cropper';
import { MenuModule } from '@openng/optimus-ui/menu';
import { MenuItem } from '@openng/optimus-ui/api';
import { MenuItemsCache } from '../../functions/menu-items-cache';
import { IMAGE_TOO_LARGE_DETAIL, isImageTooLarge } from '../../../../shared/functions/image-upload.function';

@Component({
  selector: 'app-group-panel',
  imports: [
    TableModule,
    ButtonModule,
    GroupChevronComponent,
    MemberListComponent,
    DialogModule,
    MultiSelectModule,
    FormsModule,
    ReactiveFormsModule,
    TextareaModule,
    LeadershipPanelComponent,
    ImageCropperComponent,
    MenuModule
  ],
  templateUrl: './group-panel.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './group-panel.css'
})
export class GroupPanelComponent implements OnInit {
  private readonly silhouetteInput = viewChild<ElementRef<HTMLInputElement>>('silhouetteInput');

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly router: Router = inject(Router);
  private readonly memberService = inject(MemberService);
  private readonly groupService = inject(GroupService);
  private readonly entityService = inject(EntityService);
  private readonly permissionService = inject(PermissionService);
  private readonly fb = inject(FormBuilder);

  groupKey = '';
  group: GroupDto | null = null;
  members: MemberDto[] = [];
  selectedMember: MemberDto | null = null;
  mentorDialogVisible = false;
  mentorCandidates: MemberLookupDto[] = [];
  assignedMentors: MemberLookupDto[] = [];
  selectedMentorUserKeys: string[] = [];
  initialMentorUserKeys: string[] = [];
  mentorSaveInProgress = false;
  canCreateMembers = false;
  canEditGroupProfile = false;
  // Both rights arrive together; until they do, the action menu is not offered at all.
  accessResolved = false;
  profileEditMode = false;
  profileSaving = false;
  descriptionExpanded = false;
  silhouetteSaving = false;
  silhouetteDialogVisible = false;
  silhouetteProcessing = false;
  silhouetteError: string | null = null;
  silhouetteImageFile?: File;
  silhouetteCroppedBlob: Blob | null = null;
  silhouetteProcessedBlob: Blob | null = null;
  silhouettePreviewUrl: string | null = null;
  silhouetteUseGrayscale = true;
  silhouetteUseOutline = true;
  silhouetteOutlineWidth = 8;

  readonly descriptionCollapseLimit = 220;
  readonly silhouetteMaxBytes = 5 * 1024 * 1024;
  private readonly allowedSilhouetteTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
  private readonly groupEditMenuCache = new MenuItemsCache();

  profileForm: FormGroup = this.fb.group({
    description: ['', Validators.maxLength(1000)]
  });

  tableHeaders: string[] = [
    'MemberKey',
    'FirstName',
    'LastName'
  ];

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.groupKey = params.get('groupKey')!;
    });
    this.refreshData();
  }

  get canManageMentors(): boolean {
    return this.permissionService.canManageMentors();
  }

  get canManageMembers(): boolean {
    return this.canCreateMembers;
  }

  get descriptionText(): string {
    return this.group?.description?.trim() ?? '';
  }

  get isDescriptionLong(): boolean {
    return this.descriptionText.length > this.descriptionCollapseLimit;
  }

  get hasGroupEditActions(): boolean {
    return this.canEditGroupProfile || this.canManageMembers || this.canManageMentors;
  }

  get groupEditMenuItems(): MenuItem[] {
    return this.groupEditMenuCache.get(
      [
        this.canEditGroupProfile,
        this.canManageMembers,
        this.canManageMentors,
        !!this.group?.silhouetteUrl,
        this.silhouetteSaving
      ],
      () => this.buildGroupEditMenuItems());
  }

  private buildGroupEditMenuItems(): MenuItem[] {
    const items: MenuItem[] = [];

    if (this.canEditGroupProfile) {
      items.push({
        label: 'Редагувати профіль',
        icon: 'pi pi-pencil',
        command: () => this.startProfileEdit()
      });
    }

    if (this.canManageMembers) {
      items.push({
        label: 'Додати учасника',
        icon: 'pi pi-plus',
        command: () => this.onMemberCreate()
      });
    }

    if (this.canManageMentors) {
      items.push({
        label: 'Впорядники',
        icon: 'pi pi-users',
        command: () => this.openMentorDialog()
      });
    }

    if (this.canEditGroupProfile) {
      items.push({
        label: this.group?.silhouetteUrl ? 'Замінити сильветку' : 'Завантажити сильветку',
        icon: 'pi pi-upload',
        disabled: this.silhouetteSaving,
        command: () => this.openSilhouettePicker()
      });
    }

    if (this.canEditGroupProfile && this.group?.silhouetteUrl) {
      items.push({
        label: 'Видалити сильветку',
        icon: 'pi pi-trash',
        styleClass: 'group-edit-menu__danger',
        disabled: this.silhouetteSaving,
        command: () => this.deleteSilhouette()
      });
    }

    return items;
  }

  get mentorOptions(): { label: string; value: string; }[] {
    return this.mentorCandidates
      .filter(candidate => !!candidate.userKey)
      .map(candidate => {
        const middleName = candidate.middleName ? ` ${candidate.middleName}` : '';
        return {
          label: `${candidate.lastName} ${candidate.firstName}${middleName}`,
          value: candidate.userKey as string
        };
      });
  }

  refreshData(): void {
    this.groupService.exists(this.groupKey).subscribe({
      next: (exists) => {
        if (!exists) {
          this.router.navigate(['/panel'], { replaceUrl: true });
        }
      }
    });
    this.groupService.getByKey(this.groupKey).subscribe({
      next: (group) => {
        this.group = group;
        this.descriptionExpanded = false;
        this.patchProfileForm(group);
        if (this.canManageMentors) {
          this.loadMentorManagementData();
        }
      }
    });
    this.updateGroupAccess();
    this.memberService.getAll(this.groupKey).subscribe({
      next: (members) => {
        this.members = members;
      },
      error: (err) => {
        console.error('Error fetching members:', err);
      }
    });
  }

  onMemberSelect(): void {
    this.router.navigate(['/member', this.selectedMember?.memberKey]);
  }

  onMemberCreate(): void {
    this.router.navigate(['/group', this.groupKey, 'member', 'upsert']);
  }

  openMentorDialog(): void {
    if (!this.canManageMentors) {
      return;
    }

    this.loadMentorManagementData();
    this.mentorDialogVisible = true;
  }

  startProfileEdit(): void {
    if (!this.group || !this.canEditGroupProfile) {
      return;
    }

    this.profileEditMode = true;
    this.patchProfileForm(this.group);
  }

  cancelProfileEdit(): void {
    this.profileEditMode = false;
    if (this.group) {
      this.patchProfileForm(this.group);
    }
  }

  toggleDescription(): void {
    this.descriptionExpanded = !this.descriptionExpanded;
  }

  onSilhouetteSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';

    if (!file || !this.canEditGroupProfile) {
      return;
    }

    if (!this.allowedSilhouetteTypes.has(file.type)) {
      this.silhouetteError = 'Підтримуються лише PNG, JPEG або WebP.';
      return;
    }

    if (file.size > this.silhouetteMaxBytes) {
      this.silhouetteError = 'Файл має бути не більший за 5 МБ.';
      return;
    }

    this.resetSilhouetteEditor();
    this.silhouetteImageFile = file;
    this.silhouetteError = null;
    this.silhouetteDialogVisible = true;
  }

  onSilhouetteCropped(event: ImageCroppedEvent): void {
    if (!event.blob) {
      return;
    }

    this.silhouetteCroppedBlob = event.blob;
    this.renderSilhouettePreview(event.blob);
  }

  onSilhouetteOptionsChange(): void {
    if (this.silhouetteCroppedBlob) {
      this.renderSilhouettePreview(this.silhouetteCroppedBlob);
    }
  }

  uploadProcessedSilhouette(): void {
    const blob = this.silhouetteProcessedBlob ?? this.silhouetteCroppedBlob;
    if (!blob) {
      this.silhouetteError = 'Оберіть область зображення перед завантаженням.';
      return;
    }

    const fileName = this.buildSilhouetteFileName(this.silhouetteImageFile?.name ?? 'silhouette.png');
    const file = new File([blob], fileName, { type: 'image/png' });
    this.uploadSilhouetteFile(file);
  }

  uploadOriginalSilhouette(): void {
    if (!this.silhouetteImageFile) {
      return;
    }

    this.uploadSilhouetteFile(this.silhouetteImageFile);
  }

  cancelSilhouetteEditor(): void {
    if (this.silhouetteSaving) {
      return;
    }

    this.silhouetteDialogVisible = false;
    this.resetSilhouetteEditor();
  }

  openSilhouettePicker(): void {
    if (this.silhouetteSaving) {
      return;
    }

    this.silhouetteInput()?.nativeElement.click();
  }

  deleteSilhouette(): void {
    if (!this.group?.silhouetteUrl || !this.canEditGroupProfile || this.silhouetteSaving) {
      return;
    }

    this.silhouetteSaving = true;
    this.silhouetteError = null;
    this.groupService.deleteSilhouette(this.groupKey).subscribe({
      next: (updated) => {
        this.group = updated;
        this.patchProfileForm(updated);
        this.silhouetteSaving = false;
      },
      error: (err) => {
        console.error('Error deleting group silhouette:', err);
        this.silhouetteError = 'Не вдалося видалити сильветку.';
        this.silhouetteSaving = false;
      }
    });
  }

  saveProfile(): void {
    if (!this.group || this.profileForm.invalid) {
      return;
    }

    const raw = this.profileForm.value;
    const request = {
      name: this.group.name,
      description: this.normalizeText(raw.description)
    };

    this.profileSaving = true;
    this.groupService.update(this.groupKey, request).subscribe({
      next: (updated) => {
        this.group = updated;
        this.descriptionExpanded = false;
        this.patchProfileForm(updated);
        this.profileEditMode = false;
        this.profileSaving = false;
      },
      error: (err) => {
        console.error('Error updating group profile:', err);
        this.profileSaving = false;
      }
    });
  }

  private uploadSilhouetteFile(file: File | Blob): void {
    if (isImageTooLarge(file)) {
      this.silhouetteError = IMAGE_TOO_LARGE_DETAIL;
      return;
    }

    this.silhouetteSaving = true;
    this.silhouetteError = null;
    this.groupService.uploadSilhouette(this.groupKey, file).subscribe({
      next: (updated) => {
        this.group = updated;
        this.patchProfileForm(updated);
        this.silhouetteSaving = false;
        this.silhouetteDialogVisible = false;
        this.resetSilhouetteEditor();
      },
      error: (err) => {
        console.error('Error uploading group silhouette:', err);
        this.silhouetteError = 'Не вдалося завантажити сильветку.';
        this.silhouetteSaving = false;
      }
    });
  }

  private loadMentorManagementData(): void {
    if (!this.group?.kurinKey) {
      return;
    }

    forkJoin({
      mentorCandidates: this.memberService.getMentorCandidates(this.group.kurinKey),
      assigned: this.groupService.getMentors(this.groupKey)
    }).subscribe({
      next: ({ mentorCandidates, assigned }) => {
        this.mentorCandidates = mentorCandidates;
        this.assignedMentors = assigned;
        const assignedUserKeys = assigned
          .map(m => m.userKey)
          .filter((key): key is string => !!key);

        this.initialMentorUserKeys = [...assignedUserKeys];
        this.selectedMentorUserKeys = [...assignedUserKeys];
      },
      error: (err) => {
        console.error('Error loading mentor management data:', err);
      }
    });
  }

  saveMentorAssignments(): void {
    if (this.mentorSaveInProgress) {
      return;
    }

    const initial = new Set(this.initialMentorUserKeys);
    const selected = new Set(this.selectedMentorUserKeys);

    const toAssign = [...selected].filter(key => !initial.has(key));
    const toRevoke = [...initial].filter(key => !selected.has(key));

    if (toAssign.length === 0 && toRevoke.length === 0) {
      this.mentorDialogVisible = false;
      return;
    }

    const assignRequests = toAssign.map(userKey => this.groupService.assignMentor(this.groupKey, userKey));
    const revokeRequests = toRevoke.map(userKey => this.groupService.revokeMentor(this.groupKey, userKey));
    const requests = [...assignRequests, ...revokeRequests];

    this.mentorSaveInProgress = true;
    (requests.length ? forkJoin(requests) : of([])).subscribe({
      next: () => {
        this.mentorSaveInProgress = false;
        this.mentorDialogVisible = false;
        this.loadMentorManagementData();
      },
      error: (err) => {
        console.error('Error saving mentor assignments:', err);
        this.mentorSaveInProgress = false;
      }
    });
  }

  private updateGroupAccess(): void {
    // Rights from the previous load are not carried over while the new answer is in flight.
    this.accessResolved = false;
    this.canCreateMembers = false;
    this.canEditGroupProfile = false;
    if (!this.groupKey) {
      return;
    }

    // One answer for both rights. Taken one at a time, the menu was built on whichever came
    // first, and whoever clicked at once saw it without «Редагувати профіль» — once in six full
    // e2e runs, which is exactly often enough to be a flake and never a bug report.
    forkJoin({
      canCreate: this.entityService.checkEntityAccess('group', this.groupKey, 'Create').pipe(catchError(() => of(false))),
      canUpdate: this.entityService.checkEntityAccess('group', this.groupKey, 'Update').pipe(catchError(() => of(false)))
    }).subscribe(({ canCreate, canUpdate }) => {
      this.canCreateMembers = canCreate;
      this.canEditGroupProfile = canUpdate;
      this.accessResolved = true;
    });
  }

  private patchProfileForm(group: GroupDto): void {
    this.profileForm.patchValue({
      description: group.description ?? ''
    }, { emitEvent: false });
    this.profileForm.markAsPristine();
  }

  private normalizeText(value: string | null | undefined): string | null {
    const text = String(value ?? '').trim();
    return text.length > 0 ? text : null;
  }

  private renderSilhouettePreview(blob: Blob): void {
    this.silhouetteProcessing = true;
    this.buildProcessedSilhouette(blob)
      .then((processed) => {
        this.silhouetteProcessedBlob = processed;
        this.setSilhouettePreviewUrl(URL.createObjectURL(processed));
      })
      .catch((err) => {
        console.error('Error processing group silhouette:', err);
        this.silhouetteProcessedBlob = null;
        this.silhouetteError = 'Не вдалося обробити зображення.';
      })
      .finally(() => {
        this.silhouetteProcessing = false;
      });
  }

  private buildProcessedSilhouette(source: Blob): Promise<Blob> {
    const sourceUrl = URL.createObjectURL(source);
    const image = new Image();

    return new Promise((resolve, reject) => {
      image.onload = () => {
        URL.revokeObjectURL(sourceUrl);

        const imageSize = 512;
        const outlineWidth = this.silhouetteUseOutline ? Math.max(0, Math.min(32, this.silhouetteOutlineWidth)) : 0;
        const canvasSize = imageSize + outlineWidth * 2;
        const canvas = document.createElement('canvas');
        canvas.width = canvasSize;
        canvas.height = canvasSize;

        const ctx = canvas.getContext('2d');
        if (!ctx) {
          reject(new Error('Canvas context is unavailable.'));
          return;
        }

        const center = canvasSize / 2;
        ctx.clearRect(0, 0, canvasSize, canvasSize);
        ctx.save();
        ctx.beginPath();
        ctx.arc(center, center, imageSize / 2, 0, Math.PI * 2);
        ctx.clip();
        ctx.filter = this.silhouetteUseGrayscale ? 'grayscale(1)' : 'none';
        ctx.drawImage(image, outlineWidth, outlineWidth, imageSize, imageSize);
        ctx.restore();

        if (outlineWidth > 0) {
          ctx.beginPath();
          ctx.arc(center, center, imageSize / 2 + outlineWidth / 2, 0, Math.PI * 2);
          ctx.lineWidth = outlineWidth;
          // Canvas cannot read a CSS variable by itself; the outline takes the brandbook's ink token.
          ctx.strokeStyle = getComputedStyle(document.documentElement).getPropertyValue('--lil-ink').trim() || 'currentColor';
          ctx.stroke();
        }

        canvas.toBlob((result) => {
          if (result) {
            resolve(result);
          } else {
            reject(new Error('Canvas export failed.'));
          }
        }, 'image/png');
      };

      image.onerror = () => {
        URL.revokeObjectURL(sourceUrl);
        reject(new Error('Image load failed.'));
      };

      image.src = sourceUrl;
    });
  }

  private resetSilhouetteEditor(): void {
    this.silhouetteImageFile = undefined;
    this.silhouetteCroppedBlob = null;
    this.silhouetteProcessedBlob = null;
    this.silhouetteUseGrayscale = true;
    this.silhouetteUseOutline = true;
    this.silhouetteOutlineWidth = 8;
    this.setSilhouettePreviewUrl(null);
  }

  private setSilhouettePreviewUrl(url: string | null): void {
    if (this.silhouettePreviewUrl) {
      URL.revokeObjectURL(this.silhouettePreviewUrl);
    }

    this.silhouettePreviewUrl = url;
  }

  private buildSilhouetteFileName(fileName: string): string {
    const baseName = fileName.replace(/\.[^.]+$/, '') || 'silhouette';
    return `${baseName}.png`;
  }
}
