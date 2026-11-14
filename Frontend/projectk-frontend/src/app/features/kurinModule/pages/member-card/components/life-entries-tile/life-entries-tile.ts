import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import { EmptyStateComponent } from '../../../../../../shared/empty-state/empty-state';
import { parseDateOnlyString, toDateOnlyString } from '../../../../functions/to-date-only-string.function';
import { LifeEntryDto, LifeEntryKind, lifeEntryTitle, SaveLifeEntryRequest, TRAINING_COURSES, TrainingCourse } from '../../../../models/life-entry.dto';

/** One tile of the life story the person fills in themself: the вишколи, or the табори and заходи. */
export type LifeEntriesMode = 'training' | 'outings';

export interface LifeEntrySave {
  key: string | null;
  request: SaveLifeEntryRequest;
}

const OUTING_KINDS: { value: LifeEntryKind; label: string }[] = [
  { value: 'Camp', label: 'Табір' },
  { value: 'Event', label: 'Захід' }
];

/**
 * Вишколи, or табори й заходи, newest first, each with its dates, place and комендант. Only the
 * person sees the life story, so whoever sees this tile may change it.
 */
@Component({
  selector: 'app-life-entries-tile',
  imports: [FormsModule, ButtonModule, DatePickerModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, EmptyStateComponent],
  templateUrl: './life-entries-tile.html',
  styleUrl: './life-entries-tile.css',
  changeDetection: ChangeDetectionStrategy.Eager
})
export class LifeEntriesTileComponent {
  readonly mode = input.required<LifeEntriesMode>();
  readonly entries = input<LifeEntryDto[]>([]);
  readonly saving = input(false);
  readonly save = output<LifeEntrySave>();
  readonly remove = output<string>();

  readonly courses = TRAINING_COURSES;
  readonly outingKinds = OUTING_KINDS;

  readonly rows = computed(() => this.entries()
    .filter(entry => this.mode() === 'training' ? entry.kind === 'Training' : entry.kind !== 'Training')
    .sort((a, b) => b.startDate.localeCompare(a.startDate))
    .map(entry => ({
      entry,
      title: lifeEntryTitle(entry),
      kind: OUTING_KINDS.find(kind => kind.value === entry.kind)?.label ?? null,
      meta: [this.period(entry), entry.place, entry.commandant ? `комендант ${entry.commandant}` : null, entry.role]
        .filter(Boolean)
        .join(' · ')
    })));

  dialogVisible = false;
  editing: LifeEntryDto | null = null;
  kind: LifeEntryKind = 'Training';
  course: TrainingCourse | null = null;
  title = '';
  number: number | null = null;
  startDate: Date | null = null;
  endDate: Date | null = null;
  place = '';
  commandant = '';
  role = '';

  get heading(): string {
    return this.mode() === 'training' ? 'Вишколи' : 'Табори й заходи';
  }

  get lifeTitle(): string {
    return this.editing ? lifeEntryTitle(this.editing) : '';
  }

  get needsTitle(): boolean {
    return this.kind !== 'Training' || this.course === 'Other';
  }

  get canSave(): boolean {
    const datesFit = !this.endDate || !this.startDate || this.endDate >= this.startDate;
    const named = this.kind === 'Training' ? !!this.course && (!this.needsTitle || !!this.title.trim()) : !!this.title.trim();
    return named && !!this.startDate && datesFit && !this.saving();
  }

  open(entry: LifeEntryDto | null): void {
    this.editing = entry;
    this.kind = entry?.kind ?? (this.mode() === 'training' ? 'Training' : 'Camp');
    this.course = entry?.course ?? null;
    this.title = entry?.title ?? '';
    this.number = entry?.number ?? null;
    this.startDate = parseDateOnlyString(entry?.startDate ?? null);
    this.endDate = parseDateOnlyString(entry?.endDate ?? null);
    this.place = entry?.place ?? '';
    this.commandant = entry?.commandant ?? '';
    this.role = entry?.role ?? '';
    this.dialogVisible = true;
  }

  submit(): void {
    const start = toDateOnlyString(this.startDate);
    if (!this.canSave || !start) {
      return;
    }

    this.save.emit({
      key: this.editing?.memberLifeEntryKey ?? null,
      request: {
        kind: this.kind,
        course: this.kind === 'Training' ? this.course : null,
        title: this.needsTitle ? this.title.trim() : null,
        number: this.kind === 'Training' ? this.number : null,
        startDate: start,
        endDate: toDateOnlyString(this.endDate),
        place: this.place.trim() || null,
        commandant: this.commandant.trim() || null,
        role: this.role.trim() || null
      }
    });
    this.dialogVisible = false;
  }

  delete(): void {
    if (this.editing) {
      this.remove.emit(this.editing.memberLifeEntryKey);
      this.dialogVisible = false;
    }
  }

  private period(entry: LifeEntryDto): string {
    const day = (value: string) => value.split('-').reverse().join('.');
    return entry.endDate && entry.endDate !== entry.startDate ? `${day(entry.startDate)} – ${day(entry.endDate)}` : day(entry.startDate);
  }
}
