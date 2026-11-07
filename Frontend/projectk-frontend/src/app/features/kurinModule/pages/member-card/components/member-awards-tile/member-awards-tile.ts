import { Component, inject, ChangeDetectionStrategy, output, input } from '@angular/core';

import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { MemberAwardDto, YouthAwardDto, isYouthAward } from '../../../../models/member-award.dto';
import { AwardBlocksComponent } from '../award-blocks/award-blocks';
import { AwardBlock, groupGrants } from '../award-blocks/award-blocks.function';
import { MemberAwardLevel } from '../../../../models/enums/member-award-level.enum';
import { BadgeProgressStatus } from '../../../../models/enums/badge-progress-status.enum';
import { MemberAwardsDialogComponent } from '../member-awards-dialog/member-awards-dialog';
import { UpsertMemberAwardRequest } from '../../../../services/member-award-service/member-award.service';
import { BadgeImageBlobService } from '../../../../services/probes-and-badges/badge-image-blob.service';
import { environment } from '../../../../../../../environments/environment';

@Component({
  selector: 'app-member-awards-tile',
  imports: [
    AwardBlocksComponent,
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
  readonly awards = input<YouthAwardDto[]>([]);
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
  /** The person pins or unpins a відзначення on their own card. */
  readonly pinAward = output<AwardBlock>();
  readonly deleteAward = output<string>();
  readonly reviewAward = output<{
    awardKey: string;
    isApproved: boolean;
}>();

  dialogVisible = false;
  selectedAward: YouthAwardDto | null = null;

  private readonly confirmationService = inject(ConfirmationService);
  private readonly badgeImageBlobService = inject(BadgeImageBlobService);

  awardStamp(award: YouthAwardDto): string | null {
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

  /** One block per level, every grant of it inside (award-blocks). */
  get blocks(): AwardBlock[] {
    const groups = groupGrants(this.awards().filter(item => this.isVisibleAward(item)), award => award.level);
    return [...groups.values()].map(grants => ({
      key: grants[0].level,
      title: this.getAwardTitle(grants[0].level),
      imageUrl: this.getAwardImageUrl(grants[0]),
      section: '',
      grants: grants.map(award => ({
        award,
        pending: this.isPendingConfirmation(award),
        stamp: this.awardStamp(award),
        canOpen: this.canOpen(award)
      })),
      pinned: grants.some(award => award.isPinned),
      pending: grants.some(award => this.isPendingConfirmation(award)),
      pin: { level: grants[0].level }
    }));
  }

  /** The block hands back the grant as a plain award; this card only holds УПЮ ones. */
  openGrant(award: MemberAwardDto): void {
    if (isYouthAward(award)) {
      this.openDialog(award);
    }
  }

  getAwardImageUrl(award: YouthAwardDto): string | null {
    const status = this.normalizeStatus(award.status);
    const level = this.getAwardLevelWeight(award.level);
    const colored = status === BadgeProgressStatus.Confirmed;
    const sourceUrl = award.imageUrl || `${environment.apiUrl}/awards/images/${level}?colored=${colored}`;
    return this.badgeImageBlobService.resolveBadgeImageForDisplay(sourceUrl);
  }

  isPendingConfirmation(award: YouthAwardDto): boolean {
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
  canConfirm(award: YouthAwardDto | null): boolean {
    return !!award
      && this.normalizeStatus(award.status) === BadgeProgressStatus.Submitted
      && this.confirmLevels().includes(this.getAwardLevelWeight(award.level));
  }

  canEditAward(award: YouthAwardDto | null): boolean {
    if (!this.canEdit()) {
      return false;
    }
    return !award || this.normalizeStatus(award.status) !== BadgeProgressStatus.Confirmed || this.canRemove();
  }

  /** The server's rule: removal rights, or withdrawing an unconfirmed one you submitted or that is yours. */
  canDeleteAward(award: YouthAwardDto | null): boolean {
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

  canOpen(award: YouthAwardDto | null): boolean {
    return this.canEditAward(award) || this.canConfirm(award) || this.canDeleteAward(award);
  }

  openDialog(award?: YouthAwardDto): void {
    if (!this.canOpen(award ?? null)) return;
    this.selectedAward = award || null;
    this.dialogVisible = true;
  }

  onSave(request: UpsertMemberAwardRequest): void {
    this.saveAward.emit(request);
  }

  confirmDelete(award: YouthAwardDto): void {
    this.confirmationService.confirm({
      message: 'Відзначення зникне з картки людини.',
      header: 'Видалити відзначення?',
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.deleteAward.emit(award.memberAwardKey);
      }
    });
  }

  approveAward(awardKey: string): void {
    this.reviewAward.emit({ awardKey, isApproved: true });
  }

  private isVisibleAward(award: YouthAwardDto): boolean {
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
