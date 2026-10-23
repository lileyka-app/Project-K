import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from '@openng/optimus-ui/api';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { AttendanceSheetDto, SheetPersonDto } from '../../models/score.dto';
import { ScoreService } from '../../services/score-service/score.service';
import { AttendanceSheetComponent } from './attendance-sheet';

describe('AttendanceSheetComponent', () => {
  let keys = 0;
  const nextKey = () => String(++keys);

  let fixture: ComponentFixture<AttendanceSheetComponent>;
  let component: AttendanceSheetComponent;
  let scores: MockedObject<ScoreService>;
  let messages: MockedObject<MessageService>;

  const person = (over: Partial<SheetPersonDto>): SheetPersonDto => ({
    membershipKey: 'm-' + nextKey(),
    memberKey: 'p',
    fullName: 'Юнак',
    groupKey: 'sokoly',
    groupName: 'Соколи',
    rsvp: null,
    isAssigned: false,
    attendance: null,
    canScore: true,
    entries: [],
    ...over
  });

  const oksana = person({ membershipKey: 'oksana', fullName: 'Оксана', rsvp: 'Going', isAssigned: true });
  const taras = person({ membershipKey: 'taras', fullName: 'Тарас', rsvp: 'Maybe', isAssigned: true, attendance: { markedByName: 'Суддя', markedAtUtc: '2026-10-06T16:05:00Z' } });
  const marta = person({ membershipKey: 'marta', fullName: 'Марта', isAssigned: true });
  const ihor = person({ membershipKey: 'ihor', fullName: 'Ігор' });
  const lev = person({ membershipKey: 'lev', fullName: 'Лев', groupKey: 'levy', groupName: 'Леви', rsvp: 'Going', isAssigned: true, canScore: false });

  const data: AttendanceSheetDto = {
    kurinKey: 'k1',
    agendaItemKey: 'e1',
    occurrenceStartUtc: '2026-10-06T16:00:00Z',
    occurrenceEndUtc: '2026-10-06T18:00:00Z',
    isAllDay: false,
    isRecurring: true,
    title: 'Сходини',
    categoryKey: 'c1',
    categoryName: 'Сходини',
    categoryColorHex: '#0E6E4E',
    categoryIcon: null,
    attendancePoints: 1,
    hasOwnRate: false,
    people: [oksana, taras, marta, ihor, lev],
    groups: [{ groupKey: 'sokoly', groupName: 'Соколи', canScore: true, entries: [] }, { groupKey: 'levy', groupName: 'Леви', canScore: false, entries: [] }],
    items: [],
    canManage: false
  };

  function create(response: AttendanceSheetDto = data): void {
    scores = {
      getSheet: vi.fn(),
      markAttendance: vi.fn(),
      unmarkAttendance: vi.fn(),
      createEntry: vi.fn(),
      updateEntry: vi.fn(),
      deleteEntry: vi.fn(),
      setAttendanceRate: vi.fn()
    } as MockedObject<ScoreService>;
    scores.getSheet.mockReturnValue(of(response));
    messages = {
      add: vi.fn()
    } as MockedObject<MessageService>;

    TestBed.configureTestingModule({
      imports: [AttendanceSheetComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: ScoreService, useValue: scores },
        { provide: MessageService, useValue: messages },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ kurinKey: 'k1', itemKey: 'e1', occurrence: '2026-10-06T16:00:00Z' })) } }
      ]
    });
    fixture = TestBed.createComponent(AttendanceSheetComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const names = (people: SheetPersonDto[]) => people.map(p => p.fullName);

  it('розкладає людей по полицях: відповіли, призначені, решта — лише свій гурток', () => {
    create();

    expect(names(component.answered())).toEqual(['Оксана', 'Тарас']);
    expect(names(component.assigned())).toEqual(['Марта']);
    expect(names(component.others())).toEqual(['Ігор']);
    expect(component.hasOtherGroups()).toBe(true);

    component.showOthersGroups.set(true);
    expect(names(component.answered())).toContain('Лев');
  });

  it('«усі, хто відповів» відмічає лише своїх і ще не відмічених', () => {
    create();
    scores.markAttendance.mockReturnValue(of([{ membershipKey: 'oksana', outcome: 'Marked', markedByName: null }]));

    expect(names(component.answeredUnmarked())).toEqual(['Оксана']);
    component.markAnswered();

    expect(scores.markAttendance).toHaveBeenCalledWith('k1', 'e1', '2026-10-06T16:00:00Z', ['oksana']);
    expect(component.data()!.people.find(p => p.membershipKey === 'oksana')!.attendance).not.toBeNull();
    expect(component.markedCount()).toBe(2);
  });

  // The server says who got there first; the row shows the mark as theirs, not ours.
  it('уже відмічене кимось лишається його, з повідомленням', () => {
    create();
    scores.markAttendance.mockReturnValue(of([{ membershipKey: 'marta', outcome: 'AlreadyMarked', markedByName: 'Гуртковий' }]));

    component.toggle(marta);

    const marked = component.data()!.people.find(p => p.membershipKey === 'marta')!;
    expect(marked.attendance?.markedByName).toBe('Гуртковий');
    expect(messages.add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'info' }));
  });

  it('зняти відмітку — окремий запит, і рядок одразу порожній', () => {
    create();
    scores.unmarkAttendance.mockReturnValue(of({}));

    component.toggle(taras);

    expect(scores.unmarkAttendance).toHaveBeenCalledWith('k1', 'e1', '2026-10-06T16:00:00Z', 'taras');
    expect(component.data()!.people.find(p => p.membershipKey === 'taras')!.attendance).toBeNull();
  });

  it('пошук відкриває решту без розгортання', () => {
    create();

    component.search.set('ігор');
    fixture.detectChanges();

    expect(names(component.others())).toEqual(['Ігор']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ігор');
  });

  it('403 — це аркуш не для цієї людини', () => {
    scores = {
      getSheet: vi.fn()
    } as MockedObject<ScoreService>;
    scores.getSheet.mockReturnValue(throwError(() => ({ status: 403 })));
    TestBed.configureTestingModule({
      imports: [AttendanceSheetComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: ScoreService, useValue: scores },
        { provide: MessageService, useValue: {
            add: vi.fn()
          } },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ kurinKey: 'k1', itemKey: 'e1', occurrence: 'x' })) } }
      ]
    });
    fixture = TestBed.createComponent(AttendanceSheetComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.forbidden()).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Це аркуш судді');
  });
});
