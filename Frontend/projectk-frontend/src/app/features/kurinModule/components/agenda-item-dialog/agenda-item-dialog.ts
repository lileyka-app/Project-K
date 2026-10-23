import { ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { SelectModule } from '@openng/optimus-ui/select';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { MessageService } from '@openng/optimus-ui/api';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaAssignSelectComponent } from '../agenda-assign-select/agenda-assign-select';
import { AgendaProgressComponent } from '../agenda-progress/agenda-progress';
import {
  AgendaCategoryDto,
  AgendaCompletionMode,
  AgendaItemDto,
  AgendaItemKind,
  AgendaResponsesResponse,
  AgendaRsvpStatus,
  AgendaTargetInput,
  COMPLETION_MODE_OPTIONS,
  RecurrenceFrequency,
  WEEKDAY_BITS
} from '../../models/agenda';

/**
 * Create/edit dialog for an agenda item, per BRANDBOOK §5 (12px, actions bottom-right, Скасувати → primary).
 * Dates are day-only: they are sent as UTC midnight so the calendar and board agree.
 */
@Component({
  selector: 'app-agenda-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe, FormsModule, DialogModule, ButtonModule, InputTextModule, TextareaModule,
    SelectButtonModule, SelectModule, DatePickerModule, ToggleSwitchModule, AgendaAssignSelectComponent,
    AgendaProgressComponent
  ],
  templateUrl: './agenda-item-dialog.html',
  styleUrl: './agenda-item-dialog.css'
})
export class AgendaItemDialogComponent {
  private readonly agendaService = inject(AgendaService);
  private readonly messages = inject(MessageService);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly visible = model<boolean>(false);
  readonly kurinKey = input.required<string>();
  readonly item = input<AgendaItemDto | null>(null);
  /** Preselected kind when creating: Event from the calendar, Task from the board. */
  readonly defaultKind = input<AgendaItemKind>('Event');
  /** False for plain members: the dialog then opens read-only (view + RSVP), never the edit form. */
  readonly canManage = input<boolean>(true);
  /** Prefill a fresh item from a calendar slot selection (date/time + all-day-ness). */
  readonly presetStart = input<Date | null>(null);
  readonly presetEnd = input<Date | null>(null);
  readonly presetAllDay = input<boolean>(true);
  readonly saved = output<void>();

  protected readonly kindOptions = [
    { label: 'Подія', value: 'Event' as AgendaItemKind },
    { label: 'Задача', value: 'Task' as AgendaItemKind }
  ];

  protected kind: AgendaItemKind = 'Event';
  protected title = '';
  protected description = '';
  protected location = '';
  protected allDay = true;
  protected startDate: Date | null = null;
  protected endDate: Date | null = null;
  protected readonly targets = signal<AgendaTargetInput[]>([]);
  protected readonly saving = signal(false);
  protected readonly deleting = signal(false);

  /** Event groups for the picker (events only). */
  protected readonly categories = signal<AgendaCategoryDto[]>([]);
  protected categoryKey: string | null = null;

  /** RSVP picture for an already-saved event; null for tasks and unsaved items. */
  protected readonly rsvp = signal<AgendaResponsesResponse | null>(null);
  protected readonly rsvpSaving = signal(false);
  protected readonly rsvpOptions = [
    { label: 'Іду', value: 'Going' as AgendaRsvpStatus },
    { label: 'Можливо', value: 'Maybe' as AgendaRsvpStatus },
    { label: 'Не йду', value: 'NotGoing' as AgendaRsvpStatus }
  ];

  /** Recurrence rule (v1: freq + interval + weekly weekday mask + end date). */
  protected recurrenceFrequency: RecurrenceFrequency = 'None';
  protected recurrenceInterval = 1;
  protected recurrenceByWeekday = 0;
  protected recurrenceEndDate: Date | null = null;
  protected readonly weekdays = WEEKDAY_BITS;
  protected readonly frequencyOptions = [
    { label: 'Не повторюється', value: 'None' as RecurrenceFrequency },
    { label: 'Щотижня', value: 'Weekly' as RecurrenceFrequency },
    { label: 'Щомісяця', value: 'Monthly' as RecurrenceFrequency },
    { label: 'Щороку', value: 'Yearly' as RecurrenceFrequency }
  ];

  /** One way of doing it for the whole task, applied to each target aimed at more than one person. */
  protected completionMode: AgendaCompletionMode = 'Shared';
  protected readonly completionModeOptions = COMPLETION_MODE_OPTIONS;

  /**
   * The item as it stands now. Starts as the one handed in and is re-read after a part moves, so the
   * progress shown is never the one from before the click.
   */
  protected readonly live = signal<AgendaItemDto | null>(null);

  protected readonly canSave = computed(() => this.targets().length > 0);

  /** The choice only exists for a task aimed at a гурток, the kurin or a провід. */
  protected hasSharedTargets(): boolean {
    return this.kind === 'Task' && this.targets().some(t => t.targetType !== 'Member');
  }

  /** Once anyone has moved a target, its way of being done stays — the server would refuse the change. */
  protected readonly completionModeLocked = computed(() =>
    (this.item()?.assignments ?? []).some(a => a.targetType !== 'Member' && (a.status !== 'Todo' || !!a.statusChangedAtUtc)));

  protected completionModeHint(): string {
    return this.completionModeOptions.find(o => o.value === this.completionMode)?.hint ?? '';
  }

  onProgressChanged(): void {
    const current = this.live();
    if (!current) {
      return;
    }
    this.agendaService.getItem(current.agendaItemKey).subscribe(fresh => {
      this.live.set(fresh);
      this.saved.emit();
      this.cdr.markForCheck();
    });
  }

  /** The sheet of this occurrence, for those who score: a saved event with a day, never a task. */
  protected readonly canOpenSheet = computed(() => {
    const current = this.item();
    return !!current && current.kind === 'Event' && !!current.startUtc && this.permissions.canScore();
  });

  protected openSheet(): void {
    const current = this.item();
    if (!current?.startUtc) {
      return;
    }
    this.visible.set(false);
    this.router.navigate(['/kurin', this.kurinKey(), 'score', 'events', current.agendaItemKey, current.startUtc]);
  }

  /** View-only mode: a plain member, or anyone opening an item they may not edit. Shows details + RSVP only. */
  /** «Графік куреня · Кельти» — whose schedule this is, for an event the viewer sees only through it. */
  protected readonly scheduleOf = computed(() => {
    const current = this.item();
    if (current?.audience !== 'Schedule') {
      return null;
    }
    return current.assignments.map(a => a.label).filter(Boolean).join(', ') || null;
  });

  protected readonly viewOnly = computed(() => {
    if (!this.canManage()) {
      return true;
    }
    const current = this.item();
    return !!current && !current.canEdit;
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const current = this.item();
      if (current) {
        this.kind = current.kind;
        this.title = current.title;
        this.description = current.description ?? '';
        this.location = current.location ?? '';
        this.completionMode = current.assignments.find(a => a.targetType !== 'Member')?.completionMode ?? 'Shared';
        this.allDay = current.isAllDay;
        this.startDate = this.parseForForm(current.startUtc, current.isAllDay);
        this.endDate = this.parseForForm(current.endUtc, current.isAllDay);
        this.categoryKey = current.categoryKey ?? null;
        this.recurrenceFrequency = current.recurrenceFrequency ?? 'None';
        this.recurrenceInterval = current.recurrenceInterval || 1;
        this.recurrenceByWeekday = current.recurrenceByWeekday ?? 0;
        this.recurrenceEndDate = current.recurrenceEndUtc ? new Date(current.recurrenceEndUtc) : null;
        this.targets.set(current.assignments.map(a => ({ targetType: a.targetType, targetKey: a.targetKey })));
      } else {
        this.resetForm();
      }
      this.live.set(current);

      this.loadCategories();
      this.loadResponses();
      // These are plain fields bound via ngModel; on OnPush the effect's writes need an explicit check.
      this.cdr.markForCheck();
    });
  }

  private loadCategories(): void {
    this.agendaService.getCategories(this.kurinKey()).subscribe(categories => {
      this.categories.set(categories);
      this.cdr.markForCheck();
    });
  }

  /** When a group is picked on a fresh event, inherit its template description and default duration. */
  onCategoryChange(): void {
    const category = this.categories().find(c => c.agendaCategoryKey === this.categoryKey);
    if (!category || this.item()) {
      return;
    }
    if (!this.description.trim() && category.defaultDescription) {
      this.description = category.defaultDescription;
    }
    if (category.defaultDurationMinutes && this.startDate && !this.endDate) {
      this.endDate = new Date(this.startDate.getTime() + category.defaultDurationMinutes * 60_000);
    }
    this.cdr.markForCheck();
  }

  private loadResponses(): void {
    const current = this.item();
    this.rsvp.set(null);
    // An event seen only through the kurin's schedule has nobody to answer to: the server refuses it.
    if (current?.kind !== 'Event' || current.audience === 'Schedule') {
      return;
    }
    this.agendaService.getResponses(current.agendaItemKey, this.occurrenceOf(current)).subscribe({
      next: picture => {
        this.rsvp.set(picture);
        this.cdr.markForCheck();
      },
      error: () => this.rsvp.set(null)
    });
  }

  /** The answer is to the occurrence that was clicked: a series row carries its own start, a one-off has none. */
  private occurrenceOf(item: AgendaItemDto): string | null {
    return item.isRecurrenceInstance ? item.startUtc : null;
  }

  setRsvp(status: AgendaRsvpStatus): void {
    const current = this.item();
    if (!current) {
      return;
    }
    this.rsvpSaving.set(true);
    this.agendaService.setResponse(current.agendaItemKey, status, this.occurrenceOf(current)).subscribe({
      next: picture => {
        this.rsvp.set(picture);
        this.rsvpSaving.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.rsvpSaving.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося зберегти відповідь' });
      }
    });
  }

  save(): void {
    if (!this.canSave() || !this.title.trim()) {
      this.messages.add({ severity: 'warn', summary: 'Заповніть назву та ціль' });
      return;
    }

    this.saving.set(true);
    const payload = {
      kurinKey: this.kurinKey(),
      kind: this.kind,
      title: this.title.trim(),
      description: this.description.trim() || null,
      location: this.kind === 'Event' ? (this.location.trim() || null) : null,
      startUtc: this.toWire(this.startDate),
      endUtc: this.toWire(this.endDate),
      isAllDay: this.allDay,
      agendaCategoryKey: this.kind === 'Event' ? this.categoryKey : null,
      recurrenceFrequency: this.recurrenceFrequency,
      recurrenceInterval: Math.max(1, this.recurrenceInterval || 1),
      recurrenceByWeekday: this.recurrenceFrequency === 'Weekly' ? this.recurrenceByWeekday : 0,
      recurrenceEndUtc: this.recurrenceFrequency !== 'None' ? this.toUtcMidnight(this.recurrenceEndDate) : null,
      recurrenceCount: null,
      targets: this.targets().map(t => ({
        ...t,
        completionMode: this.kind === 'Task' && t.targetType !== 'Member' ? this.completionMode : 'Shared' as AgendaCompletionMode
      }))
    };

    const current = this.item();
    const request$ = current
      ? this.agendaService.update({ ...payload, agendaItemKey: current.agendaItemKey })
      : this.agendaService.create(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: current ? 'Оновлено' : 'Створено' });
        this.saved.emit();
        this.close();
      },
      error: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося зберегти' });
      }
    });
  }

  remove(): void {
    const current = this.item();
    if (!current) {
      return;
    }
    const noun = current.kind === 'Task' ? 'задачу' : 'подію';
    if (!confirm(`Видалити ${noun} «${current.title}»?`)) {
      return;
    }
    this.deleting.set(true);
    this.agendaService.delete(current.agendaItemKey).subscribe({
      next: () => {
        this.deleting.set(false);
        this.messages.add({ severity: 'success', summary: 'Видалено' });
        this.saved.emit();
        this.close();
      },
      error: () => {
        this.deleting.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося видалити' });
      }
    });
  }

  close(): void {
    this.visible.set(false);
  }

  onHide(): void {
    this.resetForm();
  }

  private resetForm(): void {
    // New items default to the context kind (Event on calendar, Task on board). A calendar-slot
    // selection prefills the dates/time and all-day-ness; otherwise it's an all-day item starting today.
    this.kind = this.defaultKind();
    this.title = '';
    this.description = '';
    this.location = '';
    this.completionMode = 'Shared';
    this.live.set(null);
    this.allDay = this.presetAllDay();
    this.startDate = this.presetStart() ?? new Date();
    this.endDate = this.presetEnd();
    this.categoryKey = null;
    this.recurrenceFrequency = 'None';
    this.recurrenceInterval = 1;
    this.recurrenceByWeekday = 0;
    this.recurrenceEndDate = null;
    this.targets.set([]);
    this.rsvp.set(null);
  }

  toggleWeekday(bit: number): void {
    this.recurrenceByWeekday ^= bit;
  }

  isWeekdaySelected(bit: number): boolean {
    return (this.recurrenceByWeekday & bit) !== 0;
  }

  /**
   * Reads a stored value into the form: an all-day date is taken as its calendar day (built in local time
   * so the picker shows the same day for every viewer), a timed value as its real instant.
   */
  private parseForForm(iso: string | null, allDay: boolean): Date | null {
    if (!iso) {
      return null;
    }
    if (allDay) {
      const [year, month, day] = iso.slice(0, 10).split('-').map(Number);
      return new Date(year, month - 1, day);
    }
    return new Date(iso);
  }

  /**
   * All-day items are sent as UTC midnight (no local-time drift, every viewer sees the same day); timed
   * items keep their real instant. Recurrence-end always uses the day form.
   */
  private toWire(date: Date | null): string | null {
    if (!date) {
      return null;
    }
    return this.allDay ? this.toUtcMidnight(date) : date.toISOString();
  }

  private toUtcMidnight(date: Date | null): string | null {
    if (!date) {
      return null;
    }
    return new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate())).toISOString();
  }
}
