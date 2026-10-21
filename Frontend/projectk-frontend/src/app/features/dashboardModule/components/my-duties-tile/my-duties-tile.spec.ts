import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MyDutyDto } from '../../models/me.dto';
import { MyDutiesTileComponent } from './my-duties-tile';

function duty(overrides: Partial<MyDutyDto>): MyDutyDto {
  return {
    kind: 'BadgesToReview',
    kurin: { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true },
    count: 1,
    groupKey: null,
    groupName: null,
    agendaItemKey: null,
    occurrenceStartUtc: null,
    title: null,
    ...overrides
  };
}

describe('MyDutiesTileComponent', () => {
  let fixture: ComponentFixture<MyDutiesTileComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MyDutiesTileComponent], providers: [provideRouter([])] }).compileComponents();
    fixture = TestBed.createComponent(MyDutiesTileComponent);
  });

  it('leads each duty to the page where it is done', () => {
    fixture.componentRef.setInput('duties', [
      duty({ kind: 'BadgesToReview', count: 3 }),
      duty({ kind: 'TransfersToConfirm', count: 2 }),
      duty({ kind: 'EntriesToVerify', count: 4, groupKey: 'g1', groupName: 'Соколи' }),
      duty({ kind: 'EntriesToVerify', count: 1 }),
      duty({ kind: 'EventWithoutAttendance', agendaItemKey: 'e1', occurrenceStartUtc: '2026-10-05T14:00:00Z', title: 'Сходини куреня' })
    ]);
    fixture.detectChanges();

    const rows = fixture.componentInstance.rows();
    expect(rows.map(r => r.link)).toEqual([
      ['/kurin', 'k1', 'review', 'skills'],
      ['/kurin', 'k1', 'dues'],
      ['/group', 'g1', 'dues'],
      ['/kurin', 'k1', 'dues'],
      ['/kurin', 'k1', 'score', 'events', 'e1', '2026-10-05T14:00:00Z']
    ]);
    expect(rows[2].detail).toBe('Соколи');
    expect(rows[3].detail).toBe('каса куреня');
    expect(rows[4].label).toBe('Відмітити присутність: Сходини куреня');
    expect(fixture.componentInstance.total()).toBe(11);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('a.duties-tile__row')).toHaveSize(5);
  });

  it('says a duty in another kurin without opening it, naming the kurin', () => {
    fixture.componentRef.setInput('namesKurin', true);
    fixture.componentRef.setInput('duties', [duty({ kurin: { kurinKey: 'k2', kurinNumber: 7, namedAfter: null, isCurrent: false }, count: 2 })]);
    fixture.detectChanges();

    const row = fixture.componentInstance.rows()[0];
    expect(row.link).toBeNull();
    expect(row.detail).toBe('к. ч. 7');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('a.duties-tile__row')).toHaveSize(0);
  });
});
