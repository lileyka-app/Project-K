import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from '@openng/optimus-ui/api';
import { BehaviorSubject, of } from 'rxjs';
import { PrivateScoreDto } from '../../models/private-score.dto';
import { ScoreService } from '../../services/score-service/score.service';
import { PrivateScoreComponent } from './private-score';

describe('PrivateScoreComponent', () => {
  let fixture: ComponentFixture<PrivateScoreComponent>;
  let component: PrivateScoreComponent;
  let scores: MockedObject<ScoreService>;

  const data: PrivateScoreDto = {
    kurinKey: 'k1',
    period: { kind: 'Year', year: 2026, stageKey: null, label: '26–27', from: '2026-10-01', to: '2027-09-30' },
    periods: { years: [], stages: [] },
    criteria: [
      { privateScoreCriterionKey: 'lead', name: 'Лідерство', isArchived: false },
      { privateScoreCriterionKey: 'old', name: 'Старе', isArchived: true }
    ],
    people: [
      { membershipKey: 'oksana', memberKey: 'p1', fullName: 'Оксана', groupKey: 'g', groupName: 'Соколи', publicTotal: 4, privateTotal: 2, byCriterion: { lead: 3 }, uncategorised: -1 },
      { membershipKey: 'taras', memberKey: 'p2', fullName: 'Тарас', groupKey: 'g', groupName: 'Леви', publicTotal: 0, privateTotal: 0, byCriterion: {}, uncategorised: 0 }
    ],
    entries: [{
      privateScoreEntryKey: 'e1', membershipKey: 'oksana', memberKey: 'p1', memberName: 'Оксана', privateScoreCriterionKey: 'lead', criterionName: 'Лідерство',
      points: 3, note: null, occurredOn: '2026-10-06', createdByName: 'Звʼязковий', createdAtUtc: '2026-10-06T10:00:00Z'
    }]
  };

  beforeEach(() => {
    scores = {
      getPrivateScore: vi.fn(),
      createPrivateEntry: vi.fn(),
      updatePrivateEntry: vi.fn(),
      deletePrivateEntry: vi.fn(),
      createPrivateCriterion: vi.fn(),
      updatePrivateCriterion: vi.fn()
    } as MockedObject<ScoreService>;
    scores.getPrivateScore.mockReturnValue(of(data));
    scores.createPrivateEntry.mockReturnValue(of({}));

    TestBed.configureTestingModule({
      imports: [PrivateScoreComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: ScoreService, useValue: scores },
        { provide: MessageService, useValue: {
            add: vi.fn()
          } },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ kurinKey: 'k1' })), queryParamMap: new BehaviorSubject(convertToParamMap({})) } }
      ]
    });
    fixture = TestBed.createComponent(PrivateScoreComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('показує лише чинні критерії колонками, публічні бали поруч', () => {
    expect(component.activeCriteria().map(c => c.name)).toEqual(['Лідерство']);
    expect(component.hasUncategorised()).toBe(true);
    expect(text()).toContain('Лідерство');
    expect(text()).not.toContain('Старе');
    expect(text()).toContain('записано про 1 з 2');
  });

  // Without a criterion the note is the only word on what it was for — so it is required then.
  it('запис без критерію потребує нотатки', () => {
    component.openEntryDialog(data.people[1]);
    component.entryPoints.set(2);
    expect(component.entryValid()).toBe(false);

    component.entryNote.set('взяв на себе ватру');
    expect(component.entryValid()).toBe(true);

    component.saveEntry();
    expect(scores.createPrivateEntry).toHaveBeenCalledWith('k1', expect.objectContaining({ membershipKey: 'taras', privateScoreCriterionKey: null, points: 2, note: 'взяв на себе ватру' }));
  });
});
