import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { TableModule } from '@openng/optimus-ui/table';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageModule } from '@openng/optimus-ui/message';
import { TagModule } from '@openng/optimus-ui/tag';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { resolveBadgeImageUrl } from '../../functions/member-skills-view-mapper.function';
import { MemberDto } from '../../models/member.dto';
import { BadgeCatalogItemDto } from '../../models/probes-and-badges/badge-catalog-item.dto';
import { BadgeProgressDto } from '../../models/probes-and-badges/badge-progress.dto';
import { MemberService } from '../../services/member-service/member.service';
import { BadgesCatalogService } from '../../services/probes-and-badges/badges-catalog.service';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { BadgeImageBlobService } from '../../services/probes-and-badges/badge-image-blob.service';
import { formatUtcDateTime, parseUtcDateTime } from '../../../../shared/functions/utc-date-time.function';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { BadgeDetailsDialogComponent } from '../../components/badge-details-dialog/badge-details-dialog';

interface SkillsReviewItemView {
  reviewKey: string;
  memberKey: string;
  memberDisplayName: string;
  memberPhotoUrl: string | null;
  memberInitials: string;
  badgeId: string;
  badgeTitle: string;
  badgeImageSourceUrl: string | null;
  submittedAtUtc: string | null;
}

@Component({
  selector: 'app-skills-review-page',
  imports: [
    TableModule,
    ButtonModule,
    MessageModule,
    TagModule,
    SkeletonModule,
    DialogModule,
    TextareaModule,
    TooltipModule,
    FormsModule,
    EmptyStateComponent,
    BadgeDetailsDialogComponent
  ],
  templateUrl: './skills-review-page.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './skills-review-page.css'
})
export class SkillsReviewPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly permissionService = inject(PermissionService);
  private readonly memberService = inject(MemberService);
  private readonly badgesCatalogService = inject(BadgesCatalogService);
  private readonly memberProgressService = inject(MemberProgressService);
  private readonly badgeImageBlobService = inject(BadgeImageBlobService);

  kurinKey: string | null = null;
  reviewItems: SkillsReviewItemView[] = [];

  isLoading = false;
  loadFailed = false;
  roleRestricted = false;
  activeReviewKey: string | null = null;
  isReviewDialogVisible = false;
  pendingReviewItem: SkillsReviewItemView | null = null;
  pendingReviewIsApproved = true;
  pendingReviewNote = '';
  isBadgeDetailsVisible = false;
  badgeDetailsId: string | null = null;

  feedbackMessage: string | null = null;
  feedbackSeverity: 'success' | 'info' | 'warn' | 'error' = 'info';

  get queueCountLabel(): string {
    const count = this.reviewItems.length;
    if (count === 0) {
      return 'нічого на розгляді';
    }

    const rest = count % 10;
    const teens = count % 100 >= 11 && count % 100 <= 14;
    let noun = 'заявок';
    if (!teens && rest === 1) {
      noun = 'заявка';
    } else if (!teens && rest >= 2 && rest <= 4) {
      noun = 'заявки';
    }
    return `${count} ${noun} на розгляді`;
  }

  get canReviewSkills(): boolean {
    return this.permissionService.canReviewSkills();
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const routeKurinKey = params.get('kurinKey');
      this.kurinKey = routeKurinKey || this.authService.getAuthStateValue()?.kurinKey || null;
      this.feedbackMessage = null;
      this.loadReviewQueue();
    });
  }

  refreshQueue(): void {
    this.feedbackMessage = null;
    this.loadReviewQueue();
  }

  isReviewing(item: SkillsReviewItemView): boolean {
    return this.activeReviewKey === item.reviewKey;
  }

  onReview(item: SkillsReviewItemView, isApproved: boolean): void {
    if (!this.canReviewSkills || this.activeReviewKey !== null) {
      return;
    }

    this.openReviewDialog(item, isApproved);
  }

  cancelReviewDialog(): void {
    this.isReviewDialogVisible = false;
    this.pendingReviewItem = null;
    this.pendingReviewIsApproved = true;
    this.pendingReviewNote = '';
  }

  confirmReviewDialog(): void {
    const item = this.pendingReviewItem;
    if (!item || !this.canReviewSkills || this.activeReviewKey !== null) {
      return;
    }

    const isApproved = this.pendingReviewIsApproved;
    const note = this.pendingReviewNote.trim().length > 0 ? this.pendingReviewNote.trim() : null;

    this.cancelReviewDialog();

    this.activeReviewKey = item.reviewKey;
    this.feedbackMessage = null;

    this.memberProgressService
      .reviewBadgeProgress(item.memberKey, item.badgeId, { isApproved, note })
      .pipe(finalize(() => {
        this.activeReviewKey = null;
      }))
      .subscribe({
        next: () => {
          this.reviewItems = this.reviewItems.filter(current => current.reviewKey !== item.reviewKey);
          this.feedbackSeverity = 'success';
          this.feedbackMessage = isApproved
            ? `Вмілість "${item.badgeTitle}" підтверджено.`
            : `Вмілість "${item.badgeTitle}" відхилено.`;
        },
        error: (error) => {
          if (error?.status === 409) {
            this.reviewItems = this.reviewItems.filter(current => current.reviewKey !== item.reviewKey);
            this.feedbackSeverity = 'warn';
            this.feedbackMessage = 'Заявка вже опрацьована в іншому запиті. Список синхронізовано.';
            return;
          }

          if (error?.status === 403) {
            this.feedbackSeverity = 'error';
            this.feedbackMessage = failureDetail(error, 'Немає доступу до модерації цієї заявки.');
            return;
          }

          this.feedbackSeverity = 'error';
          this.feedbackMessage = failureDetail(error, 'Не вдалося виконати дію. Спробуй ще раз.');
        }
      });
  }

  getReviewDialogHeader(): string {
    return this.pendingReviewIsApproved ? 'Підтвердження вмілості' : 'Відхилення вмілості';
  }

  getReviewDialogActionLabel(): string {
    return this.pendingReviewIsApproved ? 'Підтвердити' : 'Відхилити';
  }

  formatSubmittedDate(value: string | null): string {
    if (!value) {
      return '—';
    }

    return formatUtcDateTime(value, { year: 'numeric', month: '2-digit', day: '2-digit' }) ?? value;
  }

  openBadgeDetails(badgeId: string): void {
    this.badgeDetailsId = badgeId;
    this.isBadgeDetailsVisible = true;
  }

  getBadgeImageUrl(imageUrl: string | null): string | null {
    return this.badgeImageBlobService.resolveBadgeImageForDisplay(imageUrl);
  }

  private loadReviewQueue(): void {
    this.roleRestricted = !this.canReviewSkills;
    this.loadFailed = false;

    if (this.roleRestricted || !this.kurinKey) {
      this.reviewItems = [];
      return;
    }

    this.isLoading = true;

    forkJoin({
      progresses: this.memberProgressService.getBadgeReviewQueue(this.kurinKey).pipe(
        catchError((error) => {
          console.error('Error loading badge review queue:', error);
          return of([] as BadgeProgressDto[]);
        })
      ),
      badges: this.badgesCatalogService.getAll(500).pipe(
        catchError((error) => {
          console.error('Error loading badges catalog for skills review:', error);
          return of([] as BadgeCatalogItemDto[]);
        })
      )
    })
      .pipe(
        finalize(() => {
          this.isLoading = false;
        })
      )
      .subscribe({
        next: ({ progresses, badges }) => {
          this.reviewItems = this.buildReviewItemsFromQueue(progresses, badges);
        },
        error: (error) => {
          console.error('Error building skills review queue:', error);
          this.loadFailed = true;
          this.reviewItems = [];
        }
      });
  }

  private openReviewDialog(item: SkillsReviewItemView, isApproved: boolean): void {
    this.pendingReviewItem = item;
    this.pendingReviewIsApproved = isApproved;
    this.pendingReviewNote = '';
    this.isReviewDialogVisible = true;
  }

  private buildReviewItemsFromQueue(
    progresses: BadgeProgressDto[],
    badges: BadgeCatalogItemDto[]
  ): SkillsReviewItemView[] {
    const badgesById = new Map<string, BadgeCatalogItemDto>(badges.map(badge => [badge.id, badge]));

    const queue: SkillsReviewItemView[] = progresses.map(progress => {
      const badge = badgesById.get(progress.badgeId);
      const memberDisplayName = [progress.memberFirstName?.trim(), progress.memberLastName?.trim()].filter(Boolean).join(' ') || progress.memberKey;
      
      return {
        reviewKey: `${progress.memberKey}:${progress.badgeId}`,
        memberKey: progress.memberKey,
        memberDisplayName,
        memberPhotoUrl: this.badgeImageBlobService.resolveBadgeImageForDisplay(progress.memberPhotoUrl || null),
        memberInitials: this.resolveInitials(memberDisplayName),
        badgeId: progress.badgeId,
        badgeTitle: badge?.title ?? progress.badgeId,
        badgeImageSourceUrl: resolveBadgeImageUrl(badge?.imagePath ?? null),
        submittedAtUtc: progress.submittedAtUtc
      };
    });

    return queue.sort((left, right) => this.toUnixTime(right.submittedAtUtc) - this.toUnixTime(left.submittedAtUtc));
  }

  private formatMemberDisplayName(member: MemberDto): string {
    const firstLast = [member.firstName?.trim(), member.lastName?.trim()].filter(Boolean).join(' ');
    return firstLast || member.memberKey;
  }

  private resolveInitials(displayName: string): string {
    const parts = displayName
      .split(' ')
      .map(part => part.trim())
      .filter(Boolean);

    if (!parts.length) {
      return '??';
    }

    return parts.slice(0, 2).map(part => part[0]?.toUpperCase() ?? '').join('');
  }

  private toUnixTime(value: string | null): number {
    if (!value) {
      return 0;
    }

    return parseUtcDateTime(value)?.getTime() ?? 0;
  }
}
