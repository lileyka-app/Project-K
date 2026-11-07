import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { TagModule } from '@openng/optimus-ui/tag';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { AwardBlock, orderBlocks } from './award-blocks.function';

/**
 * Відзначення блоками в ряд, як вмілості: закріплені першими, далі останні здобуті. Те саме
 * відзначення, надане кілька разів, — один блок із лічильником; блок відкриває всі надання з датою й
 * тим, за що надано. «Переглянути всі» показує решту.
 */
@Component({
  selector: 'app-award-blocks',
  imports: [DatePipe, NgTemplateOutlet, ButtonModule, DialogModule, TagModule],
  templateUrl: './award-blocks.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './award-blocks.css'
})
export class AwardBlocksComponent {
  readonly blocks = input<AwardBlock[]>([]);
  /** The person's own card: they pin what they like best. */
  readonly ownCard = input(false);
  /** How many blocks the tile shows before «Переглянути всі». */
  readonly preview = input(4);
  readonly emptyText = input('Відзначень ще немає');

  readonly openGrant = output<MemberAwardDto>();
  readonly togglePin = output<AwardBlock>();

  readonly ordered = computed(() => orderBlocks(this.blocks()));
  readonly shown = computed(() => this.ordered().slice(0, this.preview()));

  detailsVisible = false;
  allVisible = false;
  selectedKey: string | null = null;

  /** The open block, read again from the input so a pin or a change shows at once. */
  get selected(): AwardBlock | null {
    return this.ordered().find(block => block.key === this.selectedKey) ?? null;
  }

  openDetails(block: AwardBlock): void {
    this.selectedKey = block.key;
    this.allVisible = false;
    this.detailsVisible = true;
  }

  open(award: MemberAwardDto): void {
    this.detailsVisible = false;
    this.openGrant.emit(award);
  }

  pin(block: AwardBlock): void {
    this.togglePin.emit(block);
  }
}
