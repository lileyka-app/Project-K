import { ChangeDetectionStrategy, Component, inject, OnInit, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import {
  CalendarOptions, DateSelectInfo, DatesSetInfo, DayCellInfo, DayHeaderInfo, EventApi, EventClickInfo, EventDisplayInfo, EventDropInfo,
  EventInput, EventResizeDoneInfo, formatDate, FormatDateOptions, FullCalendarComponent, FullCalendarModule, joinClassNames, MountInfo
} from '@fullcalendar/angular';
import dayGridPlugin from '@fullcalendar/angular/daygrid';
import interactionPlugin from '@fullcalendar/angular/interaction';
import classicTheme from '@fullcalendar/angular/themes/classic';
import timeGridPlugin from '@fullcalendar/angular/timegrid';
// @fullcalendar/angular maps ./locales/* to files it does not ship (7.1.3), so the locale comes from the core package.
import ukLocale from 'fullcalendar/locales/uk';
import { MessageService } from '@openng/optimus-ui/api';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { AgendaItemDialogComponent } from '../../components/agenda-item-dialog/agenda-item-dialog';
import { AgendaItemDto, UpdateAgendaItemRequest } from '../../models/agenda';

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Month + hourly week/day calendar built on FullCalendar 7 (MIT only: daygrid, timegrid, interaction and
 * the classic theme, whose palette is mapped onto the brand tokens in the stylesheet). Server-expanded
 * recurrence instances render as normal events. FullCalendar's own header is disabled — navigation is
 * driven by native Optimus buttons wired to its API — and the grid is bounded so the timegrid scrolls
 * inside the card instead of stretching the page. Drag/resize writes the new dates back through
 * UpdateAgendaItem.
 */
@Component({
  selector: 'app-agenda-calendar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, SelectButtonModule, ToggleSwitchModule, FullCalendarModule, AgendaItemDialogComponent],
  templateUrl: './agenda-calendar.html',
  styleUrl: './agenda-calendar.css'
})
export class AgendaCalendarComponent implements OnInit {
  private readonly agendaService = inject(AgendaService);
  private readonly permissionService = inject(PermissionService);
  private readonly messages = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly calendar = viewChild(FullCalendarComponent);

  protected readonly kurinKey = signal('');
  protected readonly items = signal<AgendaItemDto[]>([]);
  protected readonly dialogVisible = signal(false);
  protected readonly editing = signal<AgendaItemDto | null>(null);
  protected readonly presetStart = signal<Date | null>(null);
  protected readonly presetEnd = signal<Date | null>(null);
  protected readonly presetAllDay = signal(true);

  /** Title + active view mirrored from FullCalendar so the native toolbar stays in sync. */
  protected readonly viewTitle = signal('');
  protected readonly currentView = signal('dayGridMonth');
  protected readonly viewOptions = [
    { label: 'Місяць', value: 'dayGridMonth' },
    { label: 'Тиждень', value: 'timeGridWeek' },
    { label: 'День', value: 'timeGridDay' }
  ];

  protected readonly calendarOptions = signal<CalendarOptions>({
    plugins: [classicTheme, dayGridPlugin, timeGridPlugin, interactionPlugin],
    locale: ukLocale,
    initialView: 'dayGridMonth',
    firstDay: 1,
    headerToolbar: false,
    // 'auto' lets FullCalendar render each view at its natural height (a fixed/percentage height collapses
    // the view-harness to 0 in this layout). The .agenda-calendar-host wrapper then caps the height and
    // scrolls, so the timegrid scrolls inside the card instead of stretching the page.
    height: 'auto',
    // Under 'auto' v7 still gives every month row a basis of width / aspectRatio / 6 (137px on a desktop
    // card). A huge ratio drops that to nothing, so rows take their content height, as they did in v6
    // (the v6 minimum is rebuilt by .agenda-cal__day-events and .agenda-cal__day-foot).
    aspectRatio: 1000,
    views: {
      dayGridMonth: { dayCellInnerClass: 'agenda-cal__day-events', dayCellBottomClass: 'agenda-cal__day-foot' },
      // v7's week title is «Жовтень 2026 р.»; v6 named the days.
      timeGridWeek: { titleFormat: { year: 'numeric', month: 'short', day: 'numeric' } }
    },
    nowIndicator: true,
    slotMinTime: '06:00:00',
    slotMaxTime: '23:00:00',
    allDaySlot: true,
    editable: true,
    selectable: true,
    selectMirror: true,
    // No dayMaxEvents: v6 ignored `true` under height 'auto' and showed every chip, while v7 would fold
    // them into «+ще» links sized to the row basis.
    // Render every event as a solid block (not the default month-view dot) so a category's colour fills
    // the whole event bar in all views. Month view drags by day; the time views drag by time and day.
    eventDisplay: 'block',
    events: [],
    // v7 renders no semantic fc-* classes (the theme's are hashed), so the stylesheet hooks onto these.
    eventInnerClass: 'agenda-ev__main',
    columnEventClass: 'agenda-ev--column',
    // Narrow (phone) cells get 1px chip margins in the theme; v6's 2px leaves the title no sliver.
    rowEventClass: (arg: EventDisplayInfo) => arg.isNarrow && joinClassNames(arg.isStart && 'agenda-ev--narrow-start', arg.isEnd && 'agenda-ev--narrow-end'),
    dayHeaderInnerClass: 'agenda-cal__day-head',
    dayHeaderContent: (arg: DayHeaderInfo) => this.dayHeaderText(arg),
    slotHeaderInnerClass: 'agenda-cal__axis',
    allDayHeaderInnerClass: 'agenda-cal__axis',
    dayCellTopInnerClass: (arg: DayCellInfo) => arg.isOther ? 'agenda-cal__day-num agenda-cal__day-num--other' : 'agenda-cal__day-num',
    eventContent: (arg: EventDisplayInfo) => this.renderEvent(arg),
    eventDidMount: (arg: MountInfo<EventDisplayInfo>) => this.paintCategory(arg),
    datesSet: (arg: DatesSetInfo) => this.onDatesSet(arg),
    eventClick: (arg: EventClickInfo) => this.onEventClick(arg),
    select: (arg: DateSelectInfo) => this.onSelect(arg),
    eventDrop: (arg: EventDropInfo) => this.applyDateChange(arg.event, arg.revert),
    eventResize: (arg: EventResizeDoneInfo) => this.applyDateChange(arg.event, arg.revert)
  });

  canManage(): boolean {
    return this.permissionService.canManageAgenda();
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.kurinKey.set(params.get('kurinKey') ?? '');
    });
  }

  // ---- Native toolbar → FullCalendar API ----
  private api() {
    return this.calendar()?.getApi();
  }

  prev(): void {
    this.api()?.prev();
  }

  next(): void {
    this.api()?.next();
  }

  today(): void {
    this.api()?.today();
  }

  changeView(view: string): void {
    this.api()?.changeView(view);
  }

  openCreate(): void {
    this.editing.set(null);
    this.presetStart.set(null);
    this.presetEnd.set(null);
    this.presetAllDay.set(true);
    this.dialogVisible.set(true);
  }

  onSaved(): void {
    this.reload();
  }

  private currentRange: { from: string; to: string } | null = null;

  private onDatesSet(arg: DatesSetInfo): void {
    this.currentRange = { from: arg.start.toISOString(), to: arg.end.toISOString() };
    // «жовтень 2026 р.»: only the first letter goes up; text-transform: capitalize made it «Р.» too.
    // v7 joins a range as «19–25»; the spaced dash is v6's, and v7 has no separator option left.
    const title = arg.view.title.split('–').map(part => part.trim()).join(' – ');
    this.viewTitle.set(title.charAt(0).toUpperCase() + title.slice(1));
    this.currentView.set(arg.view.type);
    this.reload();
  }

  /**
   * «Графіки гуртків»: every гурток's сходини (any group marked «графік куреня») on top of one's own.
   * On by default; remembered per viewer in this browser, which is a convenience, not data.
   */
  protected showSchedules = AgendaCalendarComponent.readShowSchedules();

  private static readonly showSchedulesKey = 'lil.agenda.showSchedules';

  private static readShowSchedules(): boolean {
    try {
      return localStorage.getItem(AgendaCalendarComponent.showSchedulesKey) !== 'off';
    } catch {
      return true;
    }
  }

  /** The link for Google, Apple and Outlook lives with the account, not with a kurin. */
  protected openSubscription(): void {
    void this.router.navigate(['/settings/account'], { fragment: 'calendar' });
  }

  onShowSchedulesChange(): void {
    try {
      localStorage.setItem(AgendaCalendarComponent.showSchedulesKey, this.showSchedules ? 'on' : 'off');
    } catch {
      // Private window or blocked storage: the switch still works for this visit.
    }
    this.reload();
  }

  // Flipping the switch (or paging) while a fetch is in flight raced: the older answer could land
  // last and bring back the сходини just hidden. Only the latest request may paint.
  private reloadSeq = 0;

  private reload(): void {
    if (!this.kurinKey() || !this.currentRange) {
      return;
    }
    const seq = ++this.reloadSeq;
    this.agendaService.getCalendar(this.kurinKey(), this.currentRange.from, this.currentRange.to, this.showSchedules)
      .subscribe(items => {
        if (seq !== this.reloadSeq) {
          return;
        }
        this.items.set(items);
        this.calendarOptions.update(opts => ({ ...opts, events: items.map(item => this.toEvent(item)) }));
      });
  }

  private toEvent(item: AgendaItemDto): EventInput {
    // All-day events are placed by date only (no time-of-day, no timezone shift); FullCalendar's all-day
    // range is end-exclusive, so the inclusive stored end is pushed one day out. Timed events keep their
    // real UTC instants and render in the viewer's local time.
    const allDay = item.isAllDay;
    const start = allDay ? item.startUtc!.slice(0, 10) : item.startUtc ?? undefined;
    const end = allDay ? this.allDayEnd(item.endUtc) : (item.endUtc ?? undefined);

    return {
      id: `${item.agendaItemKey}|${item.startUtc}`,
      title: this.eventTitle(item),
      start,
      end,
      allDay,
      editable: item.canEdit,
      className: this.eventClasses(item).join(' '),
      extendedProps: { item }
    };
  }

  /**
   * A schedule event names its гурток: six «Сходини» on one day say nothing. The first group target
   * goes first, «+N» for the rest; an event aimed at the whole kurin keeps its title as it is.
   */
  private eventTitle(item: AgendaItemDto): string {
    if (!item.isKurinSchedule) {
      return item.title;
    }
    const groups = item.assignments.filter(a => a.targetType === 'Group' && a.label);
    if (!groups.length || item.title.includes(groups[0].label!)) {
      return item.title;
    }
    const more = groups.length > 1 ? ` +${groups.length - 1}` : '';
    return `${groups[0].label}${more} · ${item.title}`;
  }

  /**
   * v6's day heads: «пн» over the month, «пн 19.10» over a week, «четвер» over a day. v7 shortens them
   * to one letter once a column is under 60px, and on a phone П and С would each name two days.
   */
  private dayHeaderText(arg: DayHeaderInfo): string | true {
    if (arg.inPopover) {
      return true;
    }
    const formats: Record<string, FormatDateOptions> = {
      timeGridWeek: { weekday: 'short', month: 'numeric', day: 'numeric', omitCommas: true },
      timeGridDay: { weekday: 'long' }
    };
    return formatDate(arg.date, { ...(formats[arg.view.type] ?? { weekday: 'short' }), locale: 'uk' });
  }

  /**
   * Dot, text, glyph: the dot carries the kind or status colour so a chip reads at a glance
   * even when a category paints the whole bar; the glyph sits faint at the far end.
   */
  private renderEvent(arg: EventDisplayInfo): { domNodes: Node[] } {
    const item = arg.event.extendedProps['item'] as AgendaItemDto;
    const dot = document.createElement('span');
    dot.className = 'agenda-ev__dot';
    const title = document.createElement('span');
    title.className = 'agenda-ev__title';
    title.textContent = arg.timeText ? `${arg.timeText} ${arg.event.title}` : arg.event.title;
    const icon = document.createElement('i');
    icon.className = `agenda-ev__icon pi ${item.kind === 'Task' ? 'pi-check-square' : 'pi-calendar'}`;
    icon.setAttribute('aria-hidden', 'true');
    return { domNodes: [dot, title, icon] };
  }

  private eventClasses(item: AgendaItemDto): string[] {
    const classes = ['agenda-ev', item.kind === 'Task' ? `agenda-ev--task agenda-ev--${item.viewerStatus.toLowerCase()}` : 'agenda-ev--event'];
    if (item.isRecurrenceInstance) {
      classes.push('agenda-ev--series');
    }
    if (item.audience === 'Schedule') {
      classes.push('agenda-ev--schedule');
    } else if (!item.addressedToViewer) {
      classes.push('agenda-ev--foreign');
    }
    // A category tints the plate from its colour (see paintCategory); only a plain item takes
    // the kind/status plate.
    classes.push(item.categoryColorHex ? 'agenda-ev--category' : 'agenda-ev--plain');
    return classes;
  }

  /**
   * Hands the category colour to the stylesheet as a custom property rather than as FullCalendar's
   * inline background: an inline fill is the same in both themes, while the stylesheet mixes the
   * colour into the paper by a per-theme amount (BRANDBOOK §2, `--lil-agenda-cat-*-mix`).
   */
  private paintCategory(arg: MountInfo<EventDisplayInfo>): void {
    const item = arg.event.extendedProps['item'] as AgendaItemDto | undefined;
    if (item?.categoryColorHex) {
      arg.el.style.setProperty('--agenda-cat', item.categoryColorHex);
    }
  }

  private onEventClick(arg: EventClickInfo): void {
    // Open for everyone: editors get the form, viewers get read-only details + RSVP.
    const item = arg.event.extendedProps['item'] as AgendaItemDto;
    this.editing.set(item);
    this.presetStart.set(null);
    this.dialogVisible.set(true);
  }

  private onSelect(arg: DateSelectInfo): void {
    if (!this.canManage()) {
      return;
    }
    this.editing.set(null);
    this.presetAllDay.set(arg.allDay);
    this.presetStart.set(arg.start);
    // An all-day selection's end is exclusive; step back a day for our inclusive model.
    this.presetEnd.set(arg.allDay ? new Date(arg.end.getTime() - DAY_MS) : arg.end);
    this.dialogVisible.set(true);
  }

  /**
   * Common path for drag and resize. A one-off saves the event's new dates directly; a recurring occurrence
   * shifts the whole series (v1) by the same delta the dragged occurrence moved — in whole days for all-day
   * items, in milliseconds for timed ones.
   */
  private applyDateChange(event: EventApi, revert: () => void): void {
    const item = event.extendedProps['item'] as AgendaItemDto;
    const allDay = event.allDay;
    let startUtc: string;
    let endUtc: string | null;

    if (item.isRecurrenceInstance) {
      ({ startUtc, endUtc } = this.shiftedSeriesDates(item, event, allDay));
    } else if (allDay) {
      // startStr/endStr are calendar-date strings; persist them as UTC midnight (end is exclusive → −1 day).
      startUtc = this.dateToUtcMidnight(event.startStr.slice(0, 10));
      endUtc = event.endStr ? this.dateToUtcMidnight(this.addDays(event.endStr.slice(0, 10), -1)) : null;
      if (endUtc === startUtc) {
        endUtc = null;
      }
    } else {
      startUtc = event.start ? event.start.toISOString() : item.startUtc!;
      endUtc = event.end ? event.end.toISOString() : null;
    }

    const payload: UpdateAgendaItemRequest = {
      agendaItemKey: item.agendaItemKey,
      kurinKey: item.kurinKey,
      kind: item.kind,
      title: item.title,
      description: item.description,
      location: item.location,
      startUtc,
      endUtc,
      isAllDay: allDay,
      agendaCategoryKey: item.categoryKey,
      recurrenceFrequency: item.recurrenceFrequency,
      recurrenceInterval: item.recurrenceInterval,
      recurrenceByWeekday: item.recurrenceByWeekday,
      recurrenceEndUtc: item.recurrenceEndUtc,
      recurrenceCount: item.recurrenceCount,
      // The mode rides along: an update re-states every target, and a missing mode would reshape it.
      targets: item.assignments.map(a => ({ targetType: a.targetType, targetKey: a.targetKey, completionMode: a.completionMode }))
    };

    this.agendaService.update(payload).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: 'Дати оновлено' });
        this.reload();
      },
      error: () => {
        this.messages.add({ severity: 'error', summary: 'Не вдалося оновити дати' });
        revert();
      }
    });
  }

  /**
   * New series start/end when a recurring occurrence is dragged/resized: the series base is shifted by the
   * delta between the occurrence's old and new position (whole days for all-day, real duration for timed).
   */
  private shiftedSeriesDates(item: AgendaItemDto, event: EventApi, allDay: boolean): { startUtc: string; endUtc: string | null } {
    if (allDay) {
      const dayDelta = this.daysBetween(item.startUtc!.slice(0, 10), event.startStr.slice(0, 10));
      const seriesStartDay = this.addDays(item.seriesStartUtc!.slice(0, 10), dayDelta);
      // Occurrence span in days (FullCalendar end is exclusive); a single day has no stored end.
      const spanDays = event.endStr ? this.daysBetween(event.startStr.slice(0, 10), event.endStr.slice(0, 10)) : 1;
      const endUtc = spanDays > 1 ? this.dateToUtcMidnight(this.addDays(seriesStartDay, spanDays - 1)) : null;
      return { startUtc: this.dateToUtcMidnight(seriesStartDay), endUtc };
    }

    const startDelta = event.start!.getTime() - new Date(item.startUtc!).getTime();
    const newStart = new Date(new Date(item.seriesStartUtc!).getTime() + startDelta);
    const endUtc = event.end ? new Date(newStart.getTime() + (event.end.getTime() - event.start!.getTime())).toISOString() : null;
    return { startUtc: newStart.toISOString(), endUtc };
  }

  /** Whole-day difference between two 'YYYY-MM-DD' strings. */
  private daysBetween(fromDateStr: string, toDateStr: string): number {
    return Math.round((new Date(`${toDateStr}T00:00:00Z`).getTime() - new Date(`${fromDateStr}T00:00:00Z`).getTime()) / DAY_MS);
  }

  /** 'YYYY-MM-DD' → that calendar day at UTC midnight, as an ISO string. */
  private dateToUtcMidnight(dateStr: string): string {
    return new Date(`${dateStr}T00:00:00Z`).toISOString();
  }

  /** Shift a 'YYYY-MM-DD' string by whole days, staying in the date domain (no timezone drift). */
  private allDayEnd(endUtc: string | null | undefined): string | undefined {
    return endUtc ? this.addDays(endUtc.slice(0, 10), 1) : undefined;
  }

  private addDays(dateStr: string, days: number): string {
    const d = new Date(`${dateStr}T00:00:00Z`);
    d.setUTCDate(d.getUTCDate() + days);
    return d.toISOString().slice(0, 10);
  }
}
