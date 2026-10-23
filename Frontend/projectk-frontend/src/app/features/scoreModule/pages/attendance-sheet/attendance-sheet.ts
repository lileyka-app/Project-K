import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { ScoreEntryDialogComponent, ScoreEntryEvent, ScoreEntryTarget } from '../../components/score-entry-dialog/score-entry-dialog';
import { points } from '../../functions/score-format.function';
import { AttendanceSheetDto, ScoreEntryDto, SheetGroupDto, SheetPersonDto, UpsertScoreEntryRequest } from '../../models/score.dto';
import { ScoreService } from '../../services/score-service/score.service';

/** The three shelves of the sheet, in the order a суддя works through them. */
type Shelf = 'answered' | 'assigned' | 'others';

/**
 * Who was at one occurrence of an event: those who answered «Іду» or «Можливо» first, then those the
 * event was aimed at, then everyone else behind a search. Marks go straight to the server and the
 * row updates in place, without a full reload — it is used on a phone, on the spot.
 */
@Component({
  selector: 'app-attendance-sheet',
  imports: [
    DatePipe, NgTemplateOutlet, FormsModule, RouterLink, ButtonModule, TagModule, TooltipModule, SkeletonModule, InputTextModule, ToggleSwitchModule,
    DialogModule, ConfirmDialogModule, MessageModule, EmptyStateComponent, ScoreEntryDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './attendance-sheet.html',
  styleUrl: './attendance-sheet.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AttendanceSheetComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly scores = inject(ScoreService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);

  readonly points = points;

  kurinKey = '';
  agendaItemKey = '';
  occurrence = '';

  readonly data = signal<AttendanceSheetDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly forbidden = signal(false);

  readonly search = signal('');
  readonly showOthersGroups = signal(false);
  readonly othersOpen = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());

  /** People the caller may mark, unless they asked to see the rest of the kurin too. */
  readonly visiblePeople = computed(() => {
    const people = this.data()?.people ?? [];
    const term = this.search().trim().toLowerCase();
    return people
      .filter(p => this.showOthersGroups() || p.canScore)
      .filter(p => !term || p.fullName.toLowerCase().includes(term));
  });

  readonly hasOtherGroups = computed(() => (this.data()?.people ?? []).some(p => !p.canScore));

  readonly answered = computed(() => this.visiblePeople().filter(p => this.shelfOf(p) === 'answered'));
  readonly assigned = computed(() => this.visiblePeople().filter(p => this.shelfOf(p) === 'assigned'));
  readonly others = computed(() => this.visiblePeople().filter(p => this.shelfOf(p) === 'others'));

  readonly markedCount = computed(() => (this.data()?.people ?? []).filter(p => p.attendance).length);
  readonly goingCount = computed(() => (this.data()?.people ?? []).filter(p => p.rsvp === 'Going').length);
  readonly assignedCount = computed(() => (this.data()?.people ?? []).filter(p => p.isAssigned).length);

  /** Those who answered and are not yet marked and are mine: what «Усі, хто відповів» marks. */
  readonly answeredUnmarked = computed(() => this.answered().filter(p => p.canScore && !p.attendance));

  readonly scorableGroups = computed(() => (this.data()?.groups ?? []).filter(g => g.canScore));

  readonly entryDialogVisible = signal(false);
  readonly entryTarget = signal<ScoreEntryTarget | null>(null);
  readonly entryTargets = signal<ScoreEntryTarget[]>([]);
  readonly entryExisting = signal<ScoreEntryDto[]>([]);
  readonly entryToEdit = signal<ScoreEntryDto | null>(null);
  readonly savingEntry = signal(false);
  readonly entryError = signal<string | null>(null);

  readonly entryEvent = computed<ScoreEntryEvent | null>(() => {
    const d = this.data();
    return d ? { agendaItemKey: d.agendaItemKey, occurrenceStartUtc: d.occurrenceStartUtc, title: d.title } : null;
  });

  readonly rateDialogVisible = signal(false);
  readonly rateDraft = signal<number | null>(null);
  readonly savingRate = signal(false);

  /** Said not to have taken place: nobody can be marked until that is taken back. */
  readonly notHeld = computed(() => this.data()?.notHeld ?? null);
  readonly savingNotHeld = signal(false);

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.kurinKey = params.get('kurinKey') ?? '';
      this.agendaItemKey = params.get('itemKey') ?? '';
      this.occurrence = params.get('occurrence') ?? '';
      this.load();
    });
  }

  load(): void {
    if (!this.kurinKey || !this.agendaItemKey || !this.occurrence) {
      return;
    }
    this.loading.set(true);
    this.loadFailed.set(false);
    this.forbidden.set(false);
    this.scores.getSheet(this.kurinKey, this.agendaItemKey, this.occurrence).subscribe({
      next: data => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.forbidden.set(isStatus(error, 403));
        this.loadFailed.set(true);
        this.loading.set(false);
      }
    });
  }

  shelfOf(person: SheetPersonDto): Shelf {
    if (person.rsvp === 'Going' || person.rsvp === 'Maybe') {
      return 'answered';
    }
    return person.isAssigned ? 'assigned' : 'others';
  }

  rsvpLabel(person: SheetPersonDto): string | null {
    switch (person.rsvp) {
      case 'Going': return 'іде';
      case 'Maybe': return 'можливо';
      case 'NotGoing': return 'не іде';
      default: return null;
    }
  }

  isBusy(person: SheetPersonDto): boolean {
    return this.busy().has(person.membershipKey);
  }

  toggle(person: SheetPersonDto): void {
    if (this.notHeld()) {
      return;
    }
    if (person.attendance) {
      this.unmark(person);
    } else {
      this.mark([person]);
    }
  }

  markAnswered(): void {
    const people = this.answeredUnmarked();
    if (people.length) {
      this.mark(people);
    }
  }

  private mark(people: SheetPersonDto[]): void {
    const keys = people.map(p => p.membershipKey);
    this.setBusy(keys, true);
    this.scores.markAttendance(this.kurinKey, this.agendaItemKey, this.occurrence, keys).subscribe({
      next: results => {
        this.setBusy(keys, false);
        const now = new Date().toISOString();
        for (const result of results) {
          const taken = result.outcome === 'AlreadyMarked';
          this.patchPerson(result.membershipKey, p => ({
            ...p,
            attendance: { markedByName: taken ? result.markedByName : null, markedAtUtc: now }
          }));
          if (taken) {
            this.messages.add({ severity: 'info', summary: 'Уже відмічено', detail: `${this.nameOf(result.membershipKey)} — відмітив ${result.markedByName ?? 'хтось інший'}.` });
          }
        }
      },
      error: (error: unknown) => {
        this.setBusy(keys, false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося відмітити', detail: failureDetail(error, 'Спробуй ще раз.') });
      }
    });
  }

  private unmark(person: SheetPersonDto): void {
    this.setBusy([person.membershipKey], true);
    this.scores.unmarkAttendance(this.kurinKey, this.agendaItemKey, this.occurrence, person.membershipKey).subscribe({
      next: () => {
        this.setBusy([person.membershipKey], false);
        this.patchPerson(person.membershipKey, p => ({ ...p, attendance: null }));
      },
      error: (error: unknown) => {
        this.setBusy([person.membershipKey], false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося зняти', detail: failureDetail(error, 'Спробуй ще раз.') });
      }
    });
  }

  giveTo(person: SheetPersonDto, entry: ScoreEntryDto | null = null): void {
    this.entryTarget.set({ membershipKey: person.membershipKey, groupKey: null, name: person.fullName });
    this.entryTargets.set([]);
    this.entryExisting.set(person.entries);
    this.openEntryDialog(entry);
  }

  giveToGroup(group: SheetGroupDto | null = null): void {
    const groups = group ? [group] : this.scorableGroups();
    if (groups.length === 1) {
      this.entryTarget.set({ membershipKey: null, groupKey: groups[0].groupKey, name: `Гурток ${groups[0].groupName} цілим` });
      this.entryExisting.set(groups[0].entries);
    } else {
      this.entryTarget.set(null);
      this.entryExisting.set([]);
    }
    this.entryTargets.set(groups.map(g => ({ membershipKey: null, groupKey: g.groupKey, name: `Гурток ${g.groupName} цілим` })));
    this.openEntryDialog(null);
  }

  editGroupEntry(group: SheetGroupDto, entry: ScoreEntryDto): void {
    this.entryTarget.set({ membershipKey: null, groupKey: group.groupKey, name: `Гурток ${group.groupName} цілим` });
    this.entryTargets.set([]);
    this.entryExisting.set(group.entries.filter(e => e.scoreEntryKey !== entry.scoreEntryKey));
    this.openEntryDialog(entry);
  }

  private openEntryDialog(entry: ScoreEntryDto | null): void {
    this.entryToEdit.set(entry);
    this.entryError.set(null);
    this.entryDialogVisible.set(true);
  }

  saveEntry(request: UpsertScoreEntryRequest): void {
    const editing = this.entryToEdit();
    this.savingEntry.set(true);
    this.entryError.set(null);
    const call = editing
      ? this.scores.updateEntry(this.kurinKey, editing.scoreEntryKey, request)
      : this.scores.createEntry(this.kurinKey, request);
    call.subscribe({
      next: () => {
        this.savingEntry.set(false);
        this.entryDialogVisible.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.savingEntry.set(false);
        this.entryError.set(failureDetail(error, 'Не вдалося записати. Спробуй ще раз.'));
      }
    });
  }

  confirmDelete(entry: ScoreEntryDto): void {
    this.confirmation.confirm({
      header: 'Видалити бал',
      message: `${points(entry.points)} · ${entry.itemName ?? entry.reason} зникне з балів. Слід у історії змін лишиться.`,
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.savingEntry.set(true);
        this.scores.deleteEntry(this.kurinKey, entry.scoreEntryKey).subscribe({
          next: () => {
            this.savingEntry.set(false);
            this.entryDialogVisible.set(false);
            this.load();
          },
          error: (error: unknown) => {
            this.savingEntry.set(false);
            this.messages.add({ severity: 'error', summary: 'Не вдалося видалити', detail: failureDetail(error, 'Спробуй ще раз.') });
          }
        });
      }
    });
  }

  /** Cancelled сходини: off the провід's «Справи», and the sheet stays empty until taken back. */
  confirmNotHeld(): void {
    const d = this.data();
    if (!d) {
      return;
    }
    this.confirmation.confirm({
      header: 'Сходин не було?',
      message: `«${d.title}» цього дня не відбулися: відмічати нікого не треба, і зі «Справ» вони зникнуть. Це можна повернути.`,
      icon: 'pi pi-calendar-times',
      acceptButtonProps: { label: 'Сходин не було' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => this.saveNotHeld(true)
    });
  }

  saveNotHeld(notHeld: boolean): void {
    this.savingNotHeld.set(true);
    this.scores.setNotHeld(this.kurinKey, this.agendaItemKey, this.occurrence, notHeld).subscribe({
      next: () => {
        this.savingNotHeld.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.savingNotHeld.set(false);
        const detail = isStatus(error, 409)
          ? 'Хтось уже відмічений на цих сходинах — отже, вони відбулися.'
          : failureDetail(error, 'Спробуй ще раз.');
        this.messages.add({ severity: 'error', summary: 'Не вдалося зберегти', detail });
      }
    });
  }

  openRateDialog(): void {
    this.rateDraft.set(this.data()?.attendancePoints ?? 0);
    this.rateDialogVisible.set(true);
  }

  saveRate(reset: boolean): void {
    const value = reset ? null : Number(this.rateDraft());
    if (!reset && (!Number.isFinite(value) || value! < 0)) {
      return;
    }
    this.savingRate.set(true);
    this.scores.setAttendanceRate(this.kurinKey, { agendaCategoryKey: null, agendaItemKey: this.agendaItemKey, points: value }).subscribe({
      next: () => {
        this.savingRate.set(false);
        this.rateDialogVisible.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.savingRate.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося зберегти', detail: failureDetail(error, 'Спробуй ще раз.') });
      }
    });
  }

  private nameOf(membershipKey: string): string {
    return this.data()?.people.find(p => p.membershipKey === membershipKey)?.fullName ?? '';
  }

  private setBusy(keys: string[], busy: boolean): void {
    const next = new Set(this.busy());
    for (const key of keys) {
      if (busy) {
        next.add(key);
      } else {
        next.delete(key);
      }
    }
    this.busy.set(next);
  }

  private patchPerson(membershipKey: string, patch: (p: SheetPersonDto) => SheetPersonDto): void {
    const d = this.data();
    if (!d) {
      return;
    }
    this.data.set({ ...d, people: d.people.map(p => p.membershipKey === membershipKey ? patch(p) : p) });
  }
}

function isStatus(error: unknown, status: number): boolean {
  return typeof error === 'object' && error !== null && (error as { status?: number }).status === status;
}
