import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { PlastLevelHistoryDto } from '../../../../models/plast-level-history.dto';
import { HonoursService } from '../../../../services/honours-service/honours.service';
import { currentDegree, DEGREE_LADDER, degreeRows } from './member-degrees.function';

/**
 * Поточний ступінь людини — найвищий на всій драбині, у якому б курені не відкрили картку, — великим
 * планом, з відзнакою з каталогу. Усі ступені показує пластовий життєпис. Вносять їх у формі картки: сама людина або провід.
 * Старший ступінь, який людина внесла собі сама, не рахується, поки його не підтвердить Звʼязковий: він
 * стоїть окремо з позначкою, а Звʼязковий бачить біля нього «Підтвердити» й «Відхилити».
 */
@Component({
  selector: 'app-member-degrees-tile',
  imports: [ButtonModule, TagModule, DatePipe],
  templateUrl: './member-degrees-tile.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-degrees-tile.css'
})
export class MemberDegreesTileComponent {
  private readonly honours = toSignal(inject(HonoursService).all(), { initialValue: [] });

  readonly histories = input<PlastLevelHistoryDto[]>([]);
  readonly canEdit = input(false);
  /** Звʼязковий куреня, де людина стоїть, і не про себе: відповідає на ступені, що чекають. */
  readonly canConfirm = input(false);
  /** Ступінь, відповідь на який уже їде на сервер. */
  readonly busyKey = input<string | null>(null);
  readonly edit = output<void>();
  readonly confirm = output<string>();
  readonly reject = output<string>();

  /** Only the current ступінь that counts: the whole way is the пластовий життєпис's to show. */
  readonly highest = computed(() => currentDegree(this.histories().filter(history => !history.awaitsConfirmation), this.honours()));

  /** Ступені, що чекають на Звʼязкового, — кожен зі своїм ключем, на який відповідають. */
  readonly waiting = computed(() => degreeRows(this.histories().filter(history => history.awaitsConfirmation), DEGREE_LADDER, this.honours()));
}
