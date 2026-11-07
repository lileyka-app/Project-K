import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { ButtonModule } from '@openng/optimus-ui/button';
import { PlastLevelHistoryDto } from '../../../../models/plast-level-history.dto';
import { HonoursService } from '../../../../services/honours-service/honours.service';
import { currentDegree } from './member-degrees.function';

/**
 * Поточний ступінь людини — найвищий на всій драбині, у якому б курені не відкрили картку, — великим
 * планом, з відзнакою з каталогу. Усі ступені показує пластовий життєпис. Вносять їх у формі картки: сама людина або провід.
 */
@Component({
  selector: 'app-member-degrees-tile',
  imports: [ButtonModule, DatePipe],
  templateUrl: './member-degrees-tile.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-degrees-tile.css'
})
export class MemberDegreesTileComponent {
  private readonly honours = toSignal(inject(HonoursService).all(), { initialValue: [] });

  readonly histories = input<PlastLevelHistoryDto[]>([]);
  readonly canEdit = input(false);
  readonly edit = output<void>();

  /** Only the current ступінь: the whole way is the пластовий життєпис's to show. */
  readonly highest = computed(() => currentDegree(this.histories(), this.honours()));
}
