import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from '@openng/optimus-ui/api';
import { of } from 'rxjs';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaAssignmentDto, AgendaItemDto } from '../../models/agenda';
import { AgendaProgressComponent } from './agenda-progress';

describe('AgendaProgressComponent', () => {
  let fixture: ComponentFixture<AgendaProgressComponent>;
  let agenda: MockedObject<AgendaService>;

  const target = (over: Partial<AgendaAssignmentDto>): AgendaAssignmentDto => ({
    agendaAssignmentKey: crypto.randomUUID(),
    targetType: 'Group',
    targetKey: crypto.randomUUID(),
    label: 'Соколи',
    completionMode: 'Shared',
    status: 'Todo',
    statusChangedByName: null,
    statusChangedAtUtc: null,
    canChangeStatus: false,
    doneCount: null,
    peopleCount: null,
    parts: null,
    ...over
  });

  const task = (assignments: AgendaAssignmentDto[], over: Partial<AgendaItemDto> = {}): AgendaItemDto => ({
    agendaItemKey: 'i1', kind: 'Task', title: 'Здати вкладку', canChangeStatus: false, viewerStatus: 'Todo', assignments, ...over
  } as AgendaItemDto);

  const render = (item: AgendaItemDto) => {
    fixture.componentRef.setInput('item', item);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(() => {
    agenda = {
      changeStatus: vi.fn(),
      changeTargetStatus: vi.fn()
    } as MockedObject<AgendaService>;
    agenda.changeStatus.mockReturnValue(of({}));
    agenda.changeTargetStatus.mockReturnValue(of({}));
    TestBed.configureTestingModule({
      imports: [AgendaProgressComponent],
      providers: [
        provideNoopAnimations(),
        { provide: AgendaService, useValue: agenda },
        { provide: MessageService, useValue: {
            add: vi.fn()
          } }
      ]
    });
    fixture = TestBed.createComponent(AgendaProgressComponent);
  });

  it('names whoever closed a target — never a bare «зроблено»', () => {
    const root = render(task([target({ status: 'Done', statusChangedByName: 'Оксана Паливода', statusChangedAtUtc: '2026-10-12T10:00:00Z' })]));

    expect(root.textContent).toContain('Закрито: Оксана Паливода');
  });

  it('counts a target done «кожному окремо» and lists its people to the one who runs it', () => {
    const root = render(task([target({
      completionMode: 'PerMember', status: 'InProgress', doneCount: 1, peopleCount: 2,
      parts: [
        { memberKey: 'm1', name: 'Марта', status: 'Done', changedByName: 'Богдан Гончар', changedAtUtc: '2026-10-12T10:00:00Z', canChangeStatus: true },
        { memberKey: 'm2', name: 'Петро', status: 'Todo', changedByName: null, changedAtUtc: null, canChangeStatus: true }
      ]
    })]));

    expect(root.textContent).toContain('1 з 2');
    expect(root.textContent).toContain('Кожному окремо');
    expect(root.textContent).toContain('відмітка: Богдан Гончар');
    expect(root.querySelectorAll('.agenda-progress__parts li')).toHaveLength(2);
  });

  it('gives a youth their own part when no row is theirs to move, through the board endpoint', () => {
    const root = render(task([target({ completionMode: 'PerMember', doneCount: 0, peopleCount: 8 })], { canChangeStatus: true }));

    expect(root.textContent).toContain('Твоя частина');
    fixture.componentInstance['moveOwn']('Done');
    expect(agenda.changeStatus).toHaveBeenCalledWith('i1', 'Done');
  });

  it('shows no controls to someone who only sees the task', () => {
    const root = render(task([target({})]));

    expect(root.textContent).not.toContain('Твоя частина');
    expect(root.querySelector('p-selectbutton')).toBeNull();
  });
});
