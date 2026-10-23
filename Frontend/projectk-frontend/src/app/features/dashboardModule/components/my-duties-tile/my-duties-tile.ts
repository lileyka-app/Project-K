import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { dayLabel, timeLabel } from '../../functions/day-label.function';
import { MyDutyDto, MyDutyKind } from '../../models/me.dto';
import { kurinLabel } from '../../../../shared/functions/kurin-label.function';

export interface DutyRow {
  duty: MyDutyDto;
  icon: string;
  label: string;
  detail: string;
  /** Null when the duty lies in a kurin the token does not act in: the row is said, not opened. */
  link: unknown[] | null;
  /** An event's row in the kurin the token acts in: «Сходин не було» can be said from here. */
  canSayNotHeld: boolean;
}

const DUTY_META: Record<MyDutyKind, { icon: string; label: string }> = {
  BadgesToReview: { icon: 'pi pi-star', label: 'Вмілості на перевірку' },
  TransfersToConfirm: { icon: 'pi pi-arrow-down-left', label: 'Передачі від гуртків підтвердити' },
  EntriesToVerify: { icon: 'pi pi-wallet', label: 'Операції перевірити' },
  EventWithoutAttendance: { icon: 'pi pi-check-square', label: 'Відмітити присутність' }
};

/** One row of the tile, told apart from the others: what the page keys a pending action by. */
export function dutyRowKey(duty: MyDutyDto): string {
  return [duty.kind, duty.kurin.kurinKey, duty.groupKey ?? '', duty.agendaItemKey ?? '', duty.occurrenceStartUtc ?? ''].join('|');
}

/** Where each kind is done; every route is scoped by the kurin the token acts in. */
function linkOf(duty: MyDutyDto): unknown[] {
  const kurin = duty.kurin.kurinKey;
  switch (duty.kind) {
    case 'BadgesToReview':
      return ['/kurin', kurin, 'review', 'skills'];
    case 'TransfersToConfirm':
      return ['/kurin', kurin, 'dues'];
    case 'EntriesToVerify':
      return duty.groupKey ? ['/group', duty.groupKey, 'dues'] : ['/kurin', kurin, 'dues'];
    case 'EventWithoutAttendance':
      return ['/kurin', kurin, 'score', 'events', duty.agendaItemKey, duty.occurrenceStartUtc];
  }
}

/**
 * The провід's queue: what waits on the person across their kurins, each row a way straight to
 * where it is done. Only for someone who holds an office somewhere — the page decides that from
 * the reply itself.
 */
@Component({
  selector: 'app-my-duties-tile',
  imports: [NgTemplateOutlet, RouterLink, ButtonModule, SkeletonModule, EmptyStateComponent],
  templateUrl: './my-duties-tile.html',
  styleUrl: './my-duties-tile.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MyDutiesTileComponent {
  readonly duties = input<MyDutyDto[]>([]);
  readonly loading = input(false);
  readonly failed = input(false);
  readonly namesKurin = input(false);
  /** The row whose action is on its way to the server. */
  readonly savingKey = input<string | null>(null);

  /** «Сходин не було» on an event's row: the page tells the server and drops the row. */
  readonly notHeld = output<MyDutyDto>();

  readonly today = new Date();

  readonly rows = computed<DutyRow[]>(() => this.duties().map(duty => this.toRow(duty)));

  readonly total = computed(() => this.duties().reduce((sum, duty) => sum + duty.count, 0));

  rowKey(row: DutyRow): string {
    return dutyRowKey(row.duty);
  }

  private toRow(duty: MyDutyDto): DutyRow {
    const meta = DUTY_META[duty.kind];
    const parts: string[] = [];

    if (duty.kind === 'EventWithoutAttendance' && duty.occurrenceStartUtc) {
      const start = new Date(duty.occurrenceStartUtc);
      parts.push(dayLabel(start, this.today));
      const time = timeLabel(start, null, false);
      if (time) {
        parts.push(time);
      }
    } else if (duty.groupName) {
      parts.push(duty.groupName);
    } else if (duty.kind === 'EntriesToVerify') {
      parts.push('каса куреня');
    }
    if (this.namesKurin()) {
      parts.push(kurinLabel(duty.kurin.kurinNumber));
    }

    return {
      duty,
      icon: meta.icon,
      label: duty.kind === 'EventWithoutAttendance' && duty.title ? `${meta.label}: ${duty.title}` : meta.label,
      detail: parts.join(' · '),
      link: duty.kurin.isCurrent ? linkOf(duty) : null,
      canSayNotHeld: duty.kind === 'EventWithoutAttendance' && duty.kurin.isCurrent && !!duty.agendaItemKey && !!duty.occurrenceStartUtc
    };
  }
}
