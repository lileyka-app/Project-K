import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { CdkDrag, CdkDropList, CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { Subject, debounceTime } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { SelectModule } from '@openng/optimus-ui/select';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { AgendaItemDialogComponent } from '../../components/agenda-item-dialog/agenda-item-dialog';
import {
  AgendaArchivePolicy,
  AgendaAssignmentDto,
  AgendaBoardFilter,
  AgendaBoardSort,
  AgendaBoardTarget,
  AgendaItemDto,
  AgendaItemStatus,
  BOARD_SORT_OPTIONS
} from '../../models/agenda';
import { AGENDA_BOARD_COLUMNS, AGENDA_STATUS_META, TagSeverity } from '../../models/agenda-status.config';

interface BoardColumn {
  status: AgendaItemStatus;
  label: string;
  severity: TagSeverity;
  total: number;
  items: AgendaItemDto[];
}

const PAGE = 20;

/**
 * The kurin's task board: three columns by the viewer's own state, each paged on its own with
 * «Завантажити ще», narrowed by search, target and «моє», and an archive beside it. Filtering runs on
 * the server, so a column's count is the count under the filter, not what happens to be loaded.
 */
@Component({
  selector: 'app-agenda-board',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // ConfirmationService is not provided globally; supply it here for the delete confirm dialog.
  providers: [ConfirmationService],
  imports: [
    DatePipe, FormsModule, CdkDropList, CdkDrag, ButtonModule, TagModule, InputTextModule, IconFieldModule,
    InputIconModule, SelectModule, ToggleSwitchModule, ConfirmDialogModule, EmptyStateComponent, AgendaItemDialogComponent
  ],
  templateUrl: './agenda-board.html',
  styleUrl: './agenda-board.css'
})
export class AgendaBoardComponent implements OnInit {
  private readonly agendaService = inject(AgendaService);
  private readonly permissionService = inject(PermissionService);
  private readonly messages = inject(MessageService);
  private readonly confirm = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly kurinKey = signal('');
  protected readonly view = signal<'board' | 'archive'>('board');
  protected readonly columns = signal<BoardColumn[]>([]);
  protected readonly loaded = signal(false);
  protected readonly loadingMore = signal<AgendaItemStatus | null>(null);
  protected readonly dialogVisible = signal(false);
  protected readonly editing = signal<AgendaItemDto | null>(null);

  protected search = '';
  protected targetValue: string | null = null;
  protected onlyMine = false;
  protected sort: AgendaBoardSort = 'Due';
  protected readonly targets = signal<{ label: string; value: string }[]>([]);
  protected readonly sortOptions = BOARD_SORT_OPTIONS;
  private readonly searchChanges = new Subject<void>();

  protected readonly archive = signal<AgendaItemDto[]>([]);
  protected readonly archiveTotal = signal(0);
  protected readonly archivePolicy = signal<AgendaArchivePolicy | null>(null);
  protected readonly archiveLoading = signal(false);
  protected archiveSearch = '';
  private readonly archiveSearchChanges = new Subject<void>();

  /** Each column accepts from the others and from the phone's tabs, which are drop targets too. */
  protected readonly columnIds = [...AGENDA_BOARD_COLUMNS, ...AGENDA_BOARD_COLUMNS.map(status => `tab-${status}`)];

  /** On a phone the columns are tabs: one at a time, so «В процесі» is not twenty cards down. */
  protected readonly activeColumn = signal<AgendaItemStatus>('Todo');

  /** A touch has to rest on a card before it lifts, or every scroll of the column would start a drag. */
  protected readonly dragDelay = { touch: 250, mouse: 0 };

  protected readonly filtered = computed(() => this.loaded() && this.hasFilter());
  protected readonly isEmpty = computed(() => this.loaded() && this.columns().every(column => column.total === 0));

  canManage(): boolean {
    return this.permissionService.canManageAgenda();
  }

  ngOnInit(): void {
    this.searchChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.loadData());
    this.archiveSearchChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.loadArchive());
    this.route.paramMap.subscribe(params => {
      this.kurinKey.set(params.get('kurinKey') ?? '');
      this.loadData();
    });
  }

  protected hasFilter(): boolean {
    return !!this.search.trim() || !!this.targetValue || this.onlyMine;
  }

  onSearch(): void {
    this.searchChanges.next();
  }

  resetFilters(): void {
    this.search = '';
    this.targetValue = null;
    this.onlyMine = false;
    this.loadData();
  }

  private filter(): AgendaBoardFilter {
    const [targetType, targetKey] = this.targetValue?.split(':') ?? [null, null];
    return {
      search: this.search.trim() || null,
      targetType: (targetType as AgendaBoardFilter['targetType']) ?? null,
      targetKey: targetKey ?? null,
      onlyMine: this.onlyMine || undefined,
      sort: this.sort
    };
  }

  loadData(): void {
    if (!this.kurinKey()) {
      return;
    }
    this.agendaService.getBoard(this.kurinKey(), { ...this.filter(), take: PAGE }).subscribe(board => {
      this.columns.set(AGENDA_BOARD_COLUMNS.map(status => {
        const column = board.columns.find(c => c.status === status);
        return {
          status,
          label: AGENDA_STATUS_META[status].label,
          severity: AGENDA_STATUS_META[status].severity,
          total: column?.total ?? 0,
          items: column?.items ?? []
        };
      }));
      this.targets.set(board.targets.map(t => ({ label: this.targetLabel(t), value: `${t.targetType}:${t.targetKey}` })));
      this.loaded.set(true);
    });
  }

  private targetLabel(target: AgendaBoardTarget): string {
    return target.targetType === 'Member' ? `${target.label} (особисто)` : target.label;
  }

  loadMore(column: BoardColumn): void {
    if (this.loadingMore()) {
      return;
    }
    this.loadingMore.set(column.status);
    this.agendaService.getBoard(this.kurinKey(), { ...this.filter(), status: column.status, skip: column.items.length, take: PAGE })
      .subscribe({
        next: page => {
          const more = page.columns[0];
          if (more) {
            this.columns.update(columns => columns.map(c => c.status === column.status ? this.append(c, more.items, more.total) : c));
          }
          this.loadingMore.set(null);
        },
        error: () => {
          this.loadingMore.set(null);
          this.messages.add({ severity: 'error', summary: 'Не вдалося завантажити' });
        }
      });
  }

  /** A page that arrives after a move may repeat a card already shown; it is kept once. */
  private append(column: BoardColumn, items: AgendaItemDto[], total: number): BoardColumn {
    const shown = new Set(column.items.map(i => i.agendaItemKey));
    return { ...column, total, items: [...column.items, ...items.filter(i => !shown.has(i.agendaItemKey))] };
  }

  onDrop(event: CdkDragDrop<AgendaItemDto[]>, targetStatus: AgendaItemStatus): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
      return;
    }

    const task = event.previousContainer.data[event.previousIndex];
    // Dropped on the tab of the column it already stands in: both lists hold the same array.
    if (!task.canChangeStatus || task.viewerStatus === targetStatus) {
      return;
    }

    transferArrayItem(event.previousContainer.data, event.container.data, event.previousIndex, event.currentIndex);
    const previousStatus = task.viewerStatus;
    task.viewerStatus = targetStatus;

    // Reloaded either way: the move may close a гурток's target and change the counts on the card.
    this.agendaService.changeStatus(task.agendaItemKey, targetStatus).subscribe({
      next: () => this.loadData(),
      error: () => {
        task.viewerStatus = previousStatus;
        this.messages.add({ severity: 'error', summary: 'Не вдалося змінити статус' });
        this.loadData();
      }
    });
  }

  openCreate(): void {
    this.editing.set(null);
    this.dialogVisible.set(true);
  }

  openEdit(task: AgendaItemDto): void {
    this.editing.set(task);
    this.dialogVisible.set(true);
  }

  onSaved(): void {
    if (this.view() === 'archive') {
      this.loadArchive();
    } else {
      this.loadData();
    }
  }

  /** «Соколи · закрито: Богдан Гончар» — a done target always carries the name of whoever closed it. */
  targetNote(assignment: AgendaAssignmentDto): string | null {
    if (assignment.peopleCount !== null) {
      return `${assignment.doneCount} з ${assignment.peopleCount}`;
    }
    if (assignment.status === 'Done' && assignment.statusChangedByName) {
      return `закрито: ${assignment.statusChangedByName}`;
    }
    return null;
  }

  targetIcon(assignment: AgendaAssignmentDto): string {
    switch (assignment.status) {
      case 'Done': return 'pi pi-check-circle';
      case 'InProgress': return 'pi pi-clock';
      default: return 'pi pi-circle';
    }
  }

  moveToArchive(task: AgendaItemDto): void {
    this.agendaService.setArchived(task.agendaItemKey, true).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: 'Перенесено в архів' });
        this.loadData();
      },
      error: () => this.messages.add({ severity: 'error', summary: 'Не вдалося перенести в архів' })
    });
  }

  remove(task: AgendaItemDto): void {
    this.confirm.confirm({
      message: `«${task.title}» зникне з дошки, і повернути її не вийде.`,
      header: 'Видалити задачу?',
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.agendaService.delete(task.agendaItemKey).subscribe({
          next: () => this.onSaved(),
          error: () => this.messages.add({ severity: 'error', summary: 'Не вдалося видалити' })
        });
      }
    });
  }

  openArchive(): void {
    this.view.set('archive');
    this.archiveSearch = '';
    this.loadArchive();
  }

  closeArchive(): void {
    this.view.set('board');
    this.loadData();
  }

  onArchiveSearch(): void {
    this.archiveSearchChanges.next();
  }

  loadArchive(append = false): void {
    if (this.archiveLoading()) {
      return;
    }
    this.archiveLoading.set(true);
    const skip = append ? this.archive().length : 0;
    this.agendaService.getArchive(this.kurinKey(), this.archiveSearch.trim() || null, skip, PAGE).subscribe({
      next: page => {
        this.archive.set(append ? [...this.archive(), ...page.items] : page.items);
        this.archiveTotal.set(page.total);
        this.archivePolicy.set(page.policy);
        this.archiveLoading.set(false);
      },
      error: () => {
        this.archiveLoading.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося відкрити архів' });
      }
    });
  }

  restore(task: AgendaItemDto): void {
    this.agendaService.setArchived(task.agendaItemKey, false).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: 'Повернено на дошку' });
        this.loadArchive();
      },
      error: () => this.messages.add({ severity: 'error', summary: 'Не вдалося повернути' })
    });
  }

  targetsLabel(task: AgendaItemDto): string {
    return task.assignments.map(a => a.label).join(', ');
  }

  /** When the nightly sweep deletes an archived task, by the kurin's retention; null when it keeps it. */
  purgeDate(task: AgendaItemDto): Date | null {
    const days = this.archivePolicy()?.purgeAfterDays;
    if (!days || !task.archivedAtUtc) {
      return null;
    }
    return new Date(new Date(task.archivedAtUtc).getTime() + days * 86_400_000);
  }
}
