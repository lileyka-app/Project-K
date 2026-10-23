import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { groupByDay, KurinJournalComponent } from './kurin-journal';
import { KurinJournalService } from '../../services/kurin-journal-service/kurin-journal.service';
import { MemberService } from '../../services/member-service/member.service';
import { KurinJournalEntryDto } from '../../models/kurin-journal.dto';
import { MemberDto } from '../../models/member.dto';

describe('KurinJournalComponent', () => {
  let fixture: ComponentFixture<KurinJournalComponent>;
  let component: KurinJournalComponent;
  let journal: MockedObject<KurinJournalService>;
  let members: MockedObject<MemberService>;

  function entry(overrides: Partial<KurinJournalEntryDto> = {}): KurinJournalEntryDto {
    return {
      key: 'e1',
      occurredAtUtc: new Date().toISOString(),
      area: 'Probe',
      action: 'Probe.PointSigned',
      actorUserKey: 'u-vykh',
      actorName: 'Коваль Олена',
      actorRole: 'KV.Vykhovnyk',
      subjectMemberKey: 'm-1',
      subjectMemberName: 'Шевченко Тарас',
      subjectKey: 's1',
      subjectLabel: 'Перша проба',
      changes: [{ field: 'point', to: 'Знає гімн' }],
      note: null,
      ...overrides
    };
  }

  function create(queryParams: Record<string, string> = {}): void {
    TestBed.overrideProvider(ActivatedRoute, {
      useValue: {
        snapshot: {
          paramMap: convertToParamMap({ kurinKey: 'k1' }),
          queryParamMap: convertToParamMap(queryParams)
        }
      }
    });
    fixture = TestBed.createComponent(KurinJournalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  beforeEach(async () => {
    journal = { getPage: vi.fn() } as MockedObject<KurinJournalService>;
    members = { getAll: vi.fn() } as MockedObject<MemberService>;
    journal.getPage.mockReturnValue(of({ items: [entry()], hasMore: false }));
    members.getAll.mockReturnValue(of([
      { memberKey: 'm-1', userKey: 'u-1', firstName: 'Тарас', lastName: 'Шевченко' } as MemberDto
    ]));

    await TestBed.configureTestingModule({
      imports: [KurinJournalComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: {} },
        { provide: KurinJournalService, useValue: journal },
        { provide: MemberService, useValue: members }
      ]
    }).compileComponents();
  });

  it('shows who did what, to whom and what changed', () => {
    create();

    expect(journal.getPage).toHaveBeenCalledWith('k1', expect.objectContaining({ skip: 0, take: 20 }));
    expect(text()).toContain('Сьогодні');
    expect(text()).toContain('Коваль Олена');
    expect(text()).toContain('Впорядник');
    expect(text()).toContain('Підписано точку проби');
    expect(text()).toContain('Шевченко Тарас');
    expect(text()).toContain('Знає гімн');
  });

  it('opens already filtered to the person the link names', () => {
    create({ member: 'm-1' });

    expect(component.memberKey()).toBe('m-1');
    expect(journal.getPage).toHaveBeenCalledWith('k1', expect.objectContaining({ memberKey: 'm-1' }));
  });

  it('reloads from the first page when a filter changes', () => {
    create();
    journal.getPage.mockClear();

    component.setArea('Award');
    component.setPeriod('week');

    const last = journal.getPage.mock.calls.at(-1)?.[1];
    expect(last).toEqual(expect.objectContaining({ area: 'Award', skip: 0 }));
    expect(last?.fromUtc).toEqual(expect.any(String));
    expect(component.activeFilterCount()).toBe(2);

    component.resetFilters();
    expect(component.activeFilterCount()).toBe(0);
  });

  it('appends the next page after the ones already shown', () => {
    journal.getPage.mockReturnValue(of({ items: [entry()], hasMore: true }));
    create();
    expect(fixture.nativeElement.querySelector('[data-testid="journal-more"]')).not.toBeNull();

    journal.getPage.mockReturnValue(of({ items: [entry({ key: 'e2' })], hasMore: false }));
    component.loadMore();

    expect(journal.getPage).toHaveBeenLastCalledWith('k1', expect.objectContaining({ skip: 1 }));
    expect(component.entries().map(item => item.key)).toEqual(['e1', 'e2']);
    expect(component.hasMore()).toBe(false);
  });

  it('says private data changed without saying to what', () => {
    journal.getPage.mockReturnValue(of({
      items: [entry({ area: 'Member', action: 'Member.Changed', changes: [{ field: 'phoneNumber', redacted: true }] })],
      hasMore: false
    }));
    create();

    expect(text()).toContain('Телефон');
    expect(text()).toContain('змінено');
  });

  it('lists the actors of the kurin and of the entries for «Хто»', () => {
    journal.getPage.mockReturnValue(of({ items: [entry({ actorUserKey: 'u-admin', actorName: 'Адмін Системи' })], hasMore: false }));
    create();

    expect(component.actorOptions().map(option => option.label)).toEqual(['Адмін Системи', 'Шевченко Тарас']);
  });

  it('shows the empty state when nothing is there', () => {
    journal.getPage.mockReturnValue(of({ items: [], hasMore: false }));
    create();

    expect(text()).toContain('Тут поки тихо');
  });

  it('tells when the journal could not be read', () => {
    journal.getPage.mockReturnValue(throwError(() => new Error('403')));
    create();

    expect(component.loadFailed()).toBe(true);
    expect(text()).toContain('Не вдалося завантажити журнал');
  });

  it('groups entries by the reader’s day, newest first', () => {
    const now = new Date(2026, 9, 23, 12, 0);
    const days = groupByDay([
      entry({ key: 'a', occurredAtUtc: new Date(2026, 9, 23, 9, 5).toISOString() }),
      entry({ key: 'b', occurredAtUtc: new Date(2026, 9, 22, 18, 0).toISOString() }),
      entry({ key: 'c', occurredAtUtc: new Date(2026, 9, 1, 8, 0).toISOString() })
    ], now);

    expect(days.map(day => day.label)).toEqual(['Сьогодні', 'Вчора', '1 жовтня']);
    expect(days[0].entries[0].time).toBe('09:05');
  });
});
