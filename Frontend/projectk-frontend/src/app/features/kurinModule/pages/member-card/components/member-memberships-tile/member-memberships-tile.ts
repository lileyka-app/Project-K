import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TagModule } from '@openng/optimus-ui/tag';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { MembershipDto } from '../../../../models/membership.dto';
import { KURIN_BRANCH_LABELS, hasGroups, isSeniorBranch } from '../../../../models/enums/kurin-branch.enum';
import { membershipKindLabel } from '../../../../models/enums/membership-kind.enum';
import { ButtonModule } from '@openng/optimus-ui/button';
import { RouterLink } from '@angular/router';
import { kurinLabel } from '../../../../../../shared/functions/kurin-label.function';

/**
 * Тека «Членства» з коробки людини: де вона є зараз і де була раніше.
 *
 * Курені, які людина покинула, звідси не зникають — вихід із куреня не стирає того, що там було
 * прожито. Тому теперішнє й минуле показані окремо, а не одним списком із датами.
 */
@Component({
  selector: 'app-member-memberships-tile',
  imports: [TagModule, SkeletonModule, ButtonModule, RouterLink],
  templateUrl: './member-memberships-tile.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-memberships-tile.css'
})
export class MemberMembershipsTileComponent {
  readonly memberships = input<MembershipDto[]>([]);
  readonly isLoading = input(false);
  readonly loadFailed = input(false);
  /**
   * Курінь, у якому ми зараз дієш. Дії пропонуються лише тут: членством у чужому курені порядкує
   * його власний провід.
   */
  readonly scopedKurinKey = input<string | null>(null);
  /**
   * Чи може той, хто дивиться, розставляти людей — переводити в гурток і виводити з куреня. Це
   * справа Звʼязкового: впорядник не переводить ні себе, ні своїх юнаків.
   */
  readonly canManage = input(false);
  /**
   * Інші курені, у яких стоїть той, хто дивиться: туди можна перейти, перемкнувши курінь. До
   * куреня, де його немає, назва лишається просто назвою.
   */
  readonly reachableKurinKeys = input<string[]>([]);
  readonly switchingTo = input<string | null>(null);

  readonly moveToGroup = output<MembershipDto>();
  readonly leaveKurin = output<MembershipDto>();
  readonly openKurin = output<MembershipDto>();

  readonly branchLabels = KURIN_BRANCH_LABELS;
  readonly kindLabel = membershipKindLabel;

  readonly current = computed(() => this.memberships().filter(m => m.isCurrent));
  readonly past = computed(() => this.memberships().filter(m => !m.isCurrent));

  isScoped(membership: MembershipDto): boolean {
    return membership.kurinKey === this.scopedKurinKey();
  }

  isReachable(membership: MembershipDto): boolean {
    return !this.isScoped(membership) && this.reachableKurinKeys().includes(membership.kurinKey);
  }

  canAct(membership: MembershipDto): boolean {
    return this.canManage() && membership.isCurrent && membership.kurinKey === this.scopedKurinKey();
  }

  /** «к. ч. 47»; a senior kurin by its own name, else «5-й курінь УСП». */
  kurinTitle(membership: MembershipDto): string {
    return isSeniorBranch(membership.branch) && membership.kurinNamedAfter
      ? membership.kurinNamedAfter
      : kurinLabel(membership.kurinNumber, membership.branch);
  }

  /** A senior kurin has no гуртки, so there is nowhere to move anyone. */
  hasGroups(membership: MembershipDto): boolean {
    return hasGroups(membership.branch);
  }

  /** The name line under the title: a УПЮ kurin's patron; a senior kurin's name is already the title. */
  namedLine(membership: MembershipDto): string | null {
    return isSeniorBranch(membership.branch) ? null : membership.kurinNamedAfter ?? null;
  }

  period(membership: MembershipDto): string {
    const from = this.year(membership.joinedAtUtc);
    if (membership.isCurrent) {
      return `з ${from}`;
    }

    const to = this.year(membership.leftAtUtc);
    return from === to ? from : `${from} — ${to}`;
  }

  private year(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }

    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? '—' : String(parsed.getUTCFullYear());
  }
}
