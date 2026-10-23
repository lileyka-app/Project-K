import { dayLabel, timeLabel } from '../../me/labels';
import { MyDutyDto, MyDutyKind } from '../../me/me.models';
import { kurinLabel } from '../../core/kurin-label';

/** One line of the «Справи» card: what to do, where, how many, and the screen it is done on. */
export interface DutyRow {
  duty: MyDutyDto;
  key: string;
  icon: string;
  /** The icon tile's colour on iPhone. */
  tile: string;
  label: string;
  detail: string;
  /** Shown for everything but an event, which is one thing to do. */
  count: number | null;
  /** Null when the phone has no screen for it or it lies in another kurin: the row is said, not opened. */
  link: string | null;
  queryParams: Record<string, string> | null;
  /** An event's row in the kurin the token acts in: «Сходин не було» can be said by a swipe. */
  canSayNotHeld: boolean;
}

const META: Record<MyDutyKind, { icon: string; tile: string; label: string }> = {
  BadgesToReview: { icon: 'star', tile: '#f4b400', label: 'Вмілості на перевірку' },
  TransfersToConfirm: { icon: 'arrow-down', tile: '#5b8def', label: 'Передачі від гуртків підтвердити' },
  EntriesToVerify: { icon: 'wallet', tile: '#34a853', label: 'Операції перевірити' },
  EventWithoutAttendance: { icon: 'checkbox', tile: '#e8710a', label: 'Відмітити присутність' },
};

/**
 * The web's my-duties-tile rows (dashboardModule/components/my-duties-tile), linked to the phone's
 * screens: a гурток's box, the kurin's box (its transfers open on the «Передачі» part), the queue.
 */
export function dutyRows(duties: MyDutyDto[], today: Date, namesKurin: boolean): DutyRow[] {
  return duties.map((duty) => {
    const meta = META[duty.kind];
    const parts: string[] = [];
    if (duty.kind === 'EventWithoutAttendance' && duty.occurrenceStartUtc) {
      const start = new Date(duty.occurrenceStartUtc);
      parts.push(dayLabel(start, today));
      const time = timeLabel(start, null, false);
      if (time) parts.push(time);
    } else if (duty.groupName) {
      parts.push(duty.groupName);
    } else if (duty.kind === 'EntriesToVerify') {
      parts.push('каса куреня');
    }
    if (namesKurin) parts.push(kurinLabel(duty.kurin.kurinNumber));

    const link = duty.kurin.isCurrent ? linkOf(duty) : null;
    return {
      duty,
      key: [duty.kind, duty.kurin.kurinKey, duty.groupKey ?? '', duty.agendaItemKey ?? '', duty.occurrenceStartUtc ?? ''].join('|'),
      icon: meta.icon,
      tile: meta.tile,
      label: duty.kind === 'EventWithoutAttendance' && duty.title ? `${meta.label}: ${duty.title}` : meta.label,
      detail: parts.join(' · '),
      count: duty.kind === 'EventWithoutAttendance' ? null : duty.count,
      link,
      queryParams: link ? queryOf(duty) : null,
      canSayNotHeld:
        duty.kind === 'EventWithoutAttendance' && duty.kurin.isCurrent && !!duty.agendaItemKey && !!duty.occurrenceStartUtc,
    };
  });
}

/** The web names the kurin on each row when the person is in more than one. */
export function dutiesSpanKurins(duties: MyDutyDto[]): boolean {
  return new Set(duties.map((duty) => duty.kurin.kurinKey)).size > 1 || duties.some((duty) => !duty.kurin.isCurrent);
}

function linkOf(duty: MyDutyDto): string | null {
  switch (duty.kind) {
    case 'BadgesToReview':
      return '/tabs/kurin/review/skills';
    case 'EntriesToVerify':
      return duty.groupKey ? `/tabs/kurin/group/${duty.groupKey}/dues` : '/tabs/kurin/dues';
    case 'EventWithoutAttendance':
      return duty.agendaItemKey ? `/tabs/calendar/attendance/${duty.agendaItemKey}` : null;
    case 'TransfersToConfirm':
      return '/tabs/kurin/dues';
  }
}

/** The occurrence an event row is about; on the kurin's box, the part the duty is done on. */
function queryOf(duty: MyDutyDto): Record<string, string> | null {
  if (duty.kind === 'TransfersToConfirm') return { part: 'transfers' };
  if (duty.kind === 'EntriesToVerify' && !duty.groupKey) return { part: 'history' };
  if (duty.kind === 'EventWithoutAttendance' && duty.occurrenceStartUtc) return { start: duty.occurrenceStartUtc };
  return null;
}
