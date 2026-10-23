import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { of } from 'rxjs';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { AgendaItemDialogComponent } from '../../components/agenda-item-dialog/agenda-item-dialog';
import { AgendaBoardResponse, AgendaItemDto } from '../../models/agenda';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaBoardComponent } from './agenda-board';

describe('AgendaBoardComponent', () => {
  let fixture: ComponentFixture<AgendaBoardComponent>;
  let agenda: MockedObject<AgendaService>;

  const task = (title: string): AgendaItemDto => ({
    agendaItemKey: title, title, kind: 'Task', viewerStatus: 'Todo', status: 'Todo', assignments: [],
    canEdit: false, canChangeStatus: false, addressedToViewer: true, startUtc: null, endUtc: null
  } as unknown as AgendaItemDto);

  const board = (todo: AgendaItemDto[], total: number): AgendaBoardResponse => ({
    columns: [
      { status: 'Todo', total, items: todo },
      { status: 'InProgress', total: 0, items: [] },
      { status: 'Done', total: 0, items: [] }
    ],
    targets: [{ targetType: 'Group', targetKey: 'g1', label: 'Соколи' }]
  });

  beforeEach(() => {
    agenda = {
      getBoard: vi.fn(),
      getArchive: vi.fn(),
      setArchived: vi.fn(),
      changeStatus: vi.fn(),
      delete: vi.fn()
    } as MockedObject<AgendaService>;
    agenda.getBoard.mockReturnValue(of(board([task('Перша')], 21)));

    TestBed.configureTestingModule({
      imports: [AgendaBoardComponent],
      providers: [
        provideNoopAnimations(),
        { provide: AgendaService, useValue: agenda },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ kurinKey: 'k1' })) } },
        { provide: PermissionService, useValue: { canManageAgenda: () => false } },
        { provide: MessageService, useValue: {
            add: vi.fn()
          } }
      ]
    });
    // The dialog has its own dependencies and is not under test here.
    TestBed.overrideComponent(AgendaBoardComponent, { remove: { imports: [AgendaItemDialogComponent] } });
    TestBed.overrideTemplate(AgendaBoardComponent, '');
    fixture = TestBed.createComponent(AgendaBoardComponent);
    fixture.detectChanges();
  });

  const component = () => fixture.componentInstance as unknown as {
    columns: () => { status: string; total: number; items: AgendaItemDto[] }[];
    targets: () => { label: string; value: string }[];
    loadMore(column: unknown): void;
    loadData(): void;
    targetValue: string | null;
    onlyMine: boolean;
  };

  it('shows each column with its count under the filter, and offers the targets on the board', () => {
    const todo = component().columns()[0];
    expect(todo.total).toBe(21);
    expect(todo.items).toHaveLength(1);
    expect(component().targets()).toEqual([{ label: 'Соколи', value: 'Group:g1' }]);
  });

  it('loads the next page of that column only, and keeps a repeated card once', () => {
    agenda.getBoard.mockReturnValue(of({ columns: [{ status: 'Todo', total: 21, items: [task('Перша'), task('Друга')] }], targets: [] }));

    component().loadMore(component().columns()[0]);

    expect(agenda.getBoard).toHaveBeenCalledWith('k1', expect.objectContaining({ status: 'Todo', skip: 1, take: 20 }));
    expect(component().columns()[0].items.map(i => i.title)).toEqual(['Перша', 'Друга']);
  });

  it('sends the target and «моє» filters to the server', () => {
    component().targetValue = 'Group:g1';
    component().onlyMine = true;
    component().loadData();

    expect(agenda.getBoard).toHaveBeenCalledWith('k1', expect.objectContaining({ targetType: 'Group', targetKey: 'g1', onlyMine: true }));
  });
});
