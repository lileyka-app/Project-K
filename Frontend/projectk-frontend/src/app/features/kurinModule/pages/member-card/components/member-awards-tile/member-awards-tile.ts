import { Component, inject, ChangeDetectionStrategy, output, input } from '@angular/core';

import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { MemberAwardLevel } from '../../../../models/enums/member-award-level.enum';
import { BadgeProgressStatus } from '../../../../models/enums/badge-progress-status.enum';
import { MemberAwardsDialogComponent } from '../member-awards-dialog/member-awards-dialog';
import { UpsertMemberAwardRequest } from '../../../../services/member-award-service/member-award.service';
import { BadgeImageBlobService } from '../../../../services/probes-and-badges/badge-image-blob.service';
import { environment } from '../../../../../../../environments/environment';
import { dateOnlyTime } from '../../../../functions/to-date-only-string.function';

interface AwardGroup {
  level: MemberAwardLevel;
  count: number;
  latest: MemberAwardDto;
}

@Component({
  selector: 'app-member-awards-tile',
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    MemberAwardsDialogComponent
],
  providers: [ConfirmationService],
  templateUrl: './member-awards-tile.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-awards-tile.css'
})
export class MemberAwardsTileComponent {
  readonly awards = input<MemberAwardDto[]>([]);
  /** Add an award and change one: whoever may change the card. */
  readonly canEdit = input(false);
  /**
   * Курінь, у якому відзначення здобуте, за його ключем. Порожня мапа, поки людина в одному курені:
   * штамп потрібен тільки там, де курінь неочевидний.
   */
  readonly kurinStamps = input<Record<string, string>>({});
  /** The levels (1–4) the kurin lets the viewer confirm. */
  readonly confirmLevels = input<number[]>([]);
  /** Remove or change a confirmed award, or remove someone else's submission. */
  readonly canRemove = input(false);
  /** The viewer's account, to tell their own submissions, which they may always withdraw. */
  readonly viewerUserKey = input<string | null>(null);
  /** The viewer is the member: they may withdraw what waits on their own card. */
  readonly ownCard = input(false);

  readonly saveAward = output<UpsertMemberAwardRequest>();
  readonly deleteAward = output<string>();
  readonly reviewAward = output<{
    awardKey: string;
    isApproved: boolean;
}>();

  dialogVisible = false;
  selectedAward: MemberAwardDto | null = null;

  private readonly confirmationService = inject(ConfirmationService);
  private readonly badgeImageBlobService = inject(BadgeImageBlobService);

  awardStamp(award: MemberAwardDto): string | null {
    return this.kurinStamps()[award.kurinKey] ?? null;
  }

  getAwardTitle(level: MemberAwardLevel): string {
    switch (level) {
      case MemberAwardLevel.First: return 'Перше відзначення';
      case MemberAwardLevel.Second: return 'Друге відзначення';
      case MemberAwardLevel.Third: return 'Третє відзначення';
      case MemberAwardLevel.Fourth: return 'Четверте відзначення';
      default: return 'Невідоме відзначення';
    }
  }

  get groupedAwards() {
    const map = new Map<string, AwardGroup>();
    for (const award of this.awards().filter(item => this.isVisibleAward(item))) {
      const existing = map.get(award.level);
      if (existing) {
        existing.count++;
        if (dateOnlyTime(award.dateAcquired) > dateOnlyTime(existing.latest.dateAcquired)) {
          existing.latest = award;
        }
      } else {
        map.set(award.level, { level: award.level, count: 1, latest: award });
      }
    }
    return Array.from(map.values()).sort((a, b) => this.getAwardLevelWeight(a.level) - this.getAwardLevelWeight(b.level));
  }

  trackAwardGroup(_: number, group: AwardGroup): MemberAwardLevel {
    return group.level;
  }

  getAwardImageUrl(award: MemberAwardDto): string | null {
    const status = this.normalizeStatus(award.status);
    const level = this.getAwardLevelWeight(award.level);
    const colored = status === BadgeProgressStatus.Confirmed;
    const sourceUrl = award.imageUrl || `${environment.apiUrl}/awards/images/${level}?colored=${colored}`;
    return this.badgeImageBlobService.resolveBadgeImageForDisplay(sourceUrl);
  }

  isPendingConfirmation(award: MemberAwardDto): boolean {
    return this.normalizeStatus(award.status) === BadgeProgressStatus.Submitted;
  }

  private getAwardLevelWeight(level: MemberAwardLevel): number {
    switch (level) {
      case MemberAwardLevel.Fourth: return 4;
      case MemberAwardLevel.Third: return 3;
      case MemberAwardLevel.Second: return 2;
      case MemberAwardLevel.First: return 1;
      default: return 0;
    }
  }

  /** Confirming is the kurin's to give level by level; it does not need the right to change the card. */
  canConfirm(award: MemberAwardDto | null): boolean {
    return !!award
      && this.normalizeStatus(award.status) === BadgeProgressStatus.Submitted
      && this.confirmLevels().includes(this.getAwardLevelWeight(award.level));
  }

  canEditAward(award: MemberAwardDto | null): boolean {
    if (!this.canEdit()) {
      return false;
    }
    return !award || this.normalizeStatus(award.status) !== BadgeProgressStatus.Confirmed || this.canRemove();
  }

  /** The server's rule: removal rights, or withdrawing an unconfirmed one you submitted or that is yours. */
  canDeleteAward(award: MemberAwardDto | null): boolean {
    if (!award) {
      return false;
    }
    if (this.canRemove()) {
      return true;
    }
    if (this.normalizeStatus(award.status) === BadgeProgressStatus.Confirmed) {
      return false;
    }
    const viewer = this.viewerUserKey()?.toLowerCase();
    return this.ownCard() || (!!viewer && award.submittedByUserKey?.toLowerCase() === viewer);
  }

  canOpen(award: MemberAwardDto | null): boolean {
    return this.canEditAward(award) || this.canConfirm(award) || this.canDeleteAward(award);
  }

  openDialog(award?: MemberAwardDto): void {
    if (!this.canOpen(award ?? null)) return;
    this.selectedAward = award || null;
    this.dialogVisible = true;
  }

  onSave(request: UpsertMemberAwardRequest): void {
    this.saveAward.emit(request);
  }

  confirmDelete(award: MemberAwardDto): void {
    this.confirmationService.confirm({
      message: 'Ви впевнені, що хочете видалити це відзначення?',
      header: 'Підтвердження видалення',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleteAward.emit(award.memberAwardKey);
      }
    });
  }

  approveAward(awardKey: string): void {
    this.reviewAward.emit({ awardKey, isApproved: true });
  }

  private isVisibleAward(award: MemberAwardDto): boolean {
    const status = this.normalizeStatus(award.status);
    return status === BadgeProgressStatus.Submitted || status === BadgeProgressStatus.Confirmed;
  }

  private normalizeStatus(status: BadgeProgressStatus | string | number): BadgeProgressStatus {
    if (typeof status === 'number' && BadgeProgressStatus[status] !== undefined) {
      return status as BadgeProgressStatus;
    }

    if (typeof status === 'string') {
      const enumValue = BadgeProgressStatus[status as keyof typeof BadgeProgressStatus];
      if (typeof enumValue === 'number') {
        return enumValue;
      }
    }

    return BadgeProgressStatus.Draft;
  }
}
