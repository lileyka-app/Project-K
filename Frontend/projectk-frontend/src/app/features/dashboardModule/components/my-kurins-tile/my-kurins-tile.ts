import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TagModule } from '@openng/optimus-ui/tag';
import { KurinMarkComponent } from '../../../kurinModule/components/kurin-mark/kurin-mark';
import { membershipKindLabel } from '../../../kurinModule/models/enums/membership-kind.enum';
import { KurinScopeOption } from '../../../kurinModule/models/kurin-scope-option.model';
import { MyGroupDto } from '../../models/me.dto';
import { compareKurinNumbers, kurinHeadline, kurinLabel, kurinSubline } from '../../../../shared/functions/kurin-label.function';
import { hasGroups } from '../../../kurinModule/models/enums/kurin-branch.enum';

export interface KurinRow {
  option: KurinScopeOption;
  isCurrent: boolean;
}

/**
 * Where the person stands now, kurin by kurin and гурток by гурток, and a way into each. The offices
 * are shown for the kurin acted in — the only one whose offices the card has read.
 */
@Component({
  selector: 'app-my-kurins-tile',
  imports: [SkeletonModule, TagModule, KurinMarkComponent],
  templateUrl: './my-kurins-tile.html',
  styleUrl: './my-kurins-tile.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MyKurinsTileComponent {
  protected readonly numberLabel = kurinLabel;

  readonly kurins = input<KurinScopeOption[]>([]);
  readonly groups = input<MyGroupDto[]>([]);

  /** Гуртки згадуються в назві лише там, де вони є: у старших куренях їх немає. */
  readonly hasAnyGroups = computed(() => this.groups().length > 0 || this.kurins().some(k => hasGroups(k.branch)));
  readonly currentKurinKey = input<string | null>(null);
  readonly currentOffices = input<string[]>([]);
  /** The kurin or гурток being opened; every row waits while the token switches. */
  readonly openingKey = input<string | null>(null);
  readonly loading = input(false);
  readonly open = output<KurinScopeOption>();
  readonly openGroup = output<MyGroupDto>();

  readonly kindLabel = membershipKindLabel;

  readonly rows = computed<KurinRow[]>(() => {
    const current = this.currentKurinKey();
    return this.kurins()
      .map(option => ({ option, isCurrent: option.kurinKey === current }))
      .sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent) || compareKurinNumbers(a.option.kurinNumber, b.option.kurinNumber));
  });

  /** A гурток names its kurin only when there is more than one to tell apart. */
  readonly namesKurin = computed(() => this.kurins().length > 1);

  readonly sortedGroups = computed(() =>
    [...this.groups()].sort((a, b) => Number(b.kurin.isCurrent) - Number(a.kurin.isCurrent) || compareKurinNumbers(a.kurin.kurinNumber, b.kurin.kurinNumber))
  );

  headline(option: KurinScopeOption): string {
    return kurinHeadline({ number: option.kurinNumber, branch: option.branch, branches: option.branches, namedAfter: option.namedAfter });
  }

  subline(option: KurinScopeOption): string {
    return kurinSubline({ number: option.kurinNumber, branch: option.branch, branches: option.branches, namedAfter: option.namedAfter });
  }

  /** Leading one's own гурток is sitting in its провід; leading another's is being its впорядник. */
  groupRole(group: MyGroupDto): string {
    if (group.isOwn) {
      return group.isLed ? 'мій гурток · провід' : 'мій гурток';
    }
    return 'впорядник';
  }
}
