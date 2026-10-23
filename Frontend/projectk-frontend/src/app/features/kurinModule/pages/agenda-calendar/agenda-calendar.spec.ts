import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { of } from 'rxjs';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { AgendaItemDto } from '../../models/agenda';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaCalendarComponent } from './agenda-calendar';

interface Internals {
  showSchedules: boolean;
  currentRange: { from: string; to: string } | null;
  onShowSchedulesChange(): void;
  eventTitle(item: AgendaItemDto): string;
}

describe('AgendaCalendarComponent — графіки гуртків', () => {
  let fixture: ComponentFixture<AgendaCalendarComponent>;
  let agenda: MockedObject<AgendaService>;

  const component = () => fixture.componentInstance as unknown as Internals;

  const create = () => {
    fixture = TestBed.createComponent(AgendaCalendarComponent);
    fixture.detectChanges();
  };

  beforeEach(() => {
    localStorage.removeItem('lil.agenda.showSchedules');
    agenda = {
      getCalendar: vi.fn()
    } as MockedObject<AgendaService>;
    agenda.getCalendar.mockReturnValue(of([]));
    TestBed.configureTestingModule({
      imports: [AgendaCalendarComponent],
      providers: [
        { provide: AgendaService, useValue: agenda },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ kurinKey: 'k1' })) } },
        { provide: PermissionService, useValue: { canManageAgenda: () => false, canScore: () => false } },
        { provide: MessageService, useValue: {
            add: vi.fn()
          } }
      ]
    });
    // FullCalendar and the dialog are not under test here.
    TestBed.overrideTemplate(AgendaCalendarComponent, '');
  });

  afterEach(() => localStorage.removeItem('lil.agenda.showSchedules'));

  it('shows the schedules by default, and asks the server for them', () => {
    create();
    expect(component().showSchedules).toBe(true);

    component().currentRange = { from: 'a', to: 'b' };
    component().onShowSchedulesChange();
    expect(agenda.getCalendar).toHaveBeenCalledWith('k1', 'a', 'b', true);
  });

  it('remembers the switch turned off for the next visit', () => {
    create();
    component().currentRange = { from: 'a', to: 'b' };
    component().showSchedules = false;
    component().onShowSchedulesChange();
    expect(agenda.getCalendar).toHaveBeenCalledWith('k1', 'a', 'b', false);

    create();
    expect(component().showSchedules).toBe(false);
  });

  it('names the гурток on a schedule event, «+N» for more', () => {
    create();
    const item = (labels: string[], isKurinSchedule = true) => ({
      title: 'Сходини', isKurinSchedule,
      assignments: labels.map(label => ({ targetType: 'Group', label }))
    } as unknown as AgendaItemDto);

    expect(component().eventTitle(item(['Кельти']))).toBe('Кельти · Сходини');
    expect(component().eventTitle(item(['Кельти', 'Соколи']))).toBe('Кельти +1 · Сходини');
    expect(component().eventTitle(item(['Кельти'], false))).toBe('Сходини');
  });
});
