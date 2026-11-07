import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { SelectModule } from '@openng/optimus-ui/select';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { HonoursService } from '../../../../services/honours-service/honours.service';
import { UpsertMemberAwardRequest } from '../../../../services/member-award-service/member-award.service';
import { parseDateOnlyString, toDateOnlyString } from '../../../../functions/to-date-only-string.function';
import { seniorAwardBlocks, seniorAwardOptions } from './senior-awards.function';
import { AwardBlocksComponent } from '../award-blocks/award-blocks';
import { AwardBlock } from '../award-blocks/award-blocks.function';

/**
 * Відзначення старшого пластуна: грамоти, ордени, хрести з каталогу. Вносить людина сама або провід
 * куреня, і вони одразу стоять — підтверджувати нікому. Змінити чи прибрати — сама людина або той,
 * кому курінь дав прибирати відзначення.
 */
@Component({
  selector: 'app-member-senior-awards-tile',
  imports: [AwardBlocksComponent, FormsModule, ButtonModule, DatePickerModule, DialogModule, SelectModule, TextareaModule],
  templateUrl: './member-senior-awards-tile.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-senior-awards-tile.css'
})
export class MemberSeniorAwardsTileComponent {
  private readonly honours = toSignal(inject(HonoursService).all(), { initialValue: [] });

  readonly awards = input<MemberAwardDto[]>([]);
  /** The viewer may add one: the person on their own card, or the провід. */
  readonly canAdd = input(false);
  /** The viewer is the person the card is about. */
  readonly ownCard = input(false);
  /** The kurin lets the viewer remove awards. */
  readonly canRemove = input(false);
  readonly saveAward = output<UpsertMemberAwardRequest>();
  readonly deleteAward = output<string>();
  /** The person pins or unpins a відзначення on their own card. */
  readonly pinAward = output<AwardBlock>();

  readonly blocks = computed(() => seniorAwardBlocks(this.awards(), this.honours(), this.canChange));
  readonly options = computed(() => seniorAwardOptions(this.honours()));
  readonly today = new Date();

  dialogVisible = false;
  editing: MemberAwardDto | null = null;
  catalogId: string | null = null;
  dateAcquired: Date | null = null;
  note = '';

  /** Changing or removing one is the person's own, or the right to remove awards. */
  get canChange(): boolean {
    return this.ownCard() || this.canRemove();
  }

  open(award: MemberAwardDto | null): void {
    if (award ? !this.canChange : !this.canAdd()) {
      return;
    }

    this.editing = award;
    this.catalogId = award?.catalogId ?? null;
    // Today at midnight: «now» would be past the picker's maxDate and show nothing.
    this.dateAcquired = parseDateOnlyString(award?.dateAcquired ?? null) ?? (award ? null : new Date(new Date().setHours(0, 0, 0, 0)));
    this.note = award?.note ?? '';
    this.dialogVisible = true;
  }

  save(): void {
    const date = toDateOnlyString(this.dateAcquired);
    if (!this.catalogId || !date) {
      return;
    }

    this.saveAward.emit({
      memberAwardKey: this.editing?.memberAwardKey,
      catalogId: this.catalogId,
      dateAcquired: date,
      note: this.note.trim() || undefined
    });
    this.dialogVisible = false;
  }

  remove(): void {
    if (this.editing) {
      this.deleteAward.emit(this.editing.memberAwardKey);
    }
    this.dialogVisible = false;
  }
}
