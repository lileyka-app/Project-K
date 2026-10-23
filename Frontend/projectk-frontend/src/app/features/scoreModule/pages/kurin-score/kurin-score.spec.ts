import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, of } from 'rxjs';
import { KurinScoreDto, ScoreGroupRowDto } from '../../models/score.dto';
import { ScoreAlgorithm } from '../../models/score.enums';
import { ScoreService } from '../../services/score-service/score.service';
import { KurinScoreComponent } from './kurin-score';

describe('KurinScoreComponent', () => {
  let keys = 0;
  const nextKey = () => String(++keys);

  let fixture: ComponentFixture<KurinScoreComponent>;
  let component: KurinScoreComponent;
  let scores: MockedObject<ScoreService>;
  const queryParams = new BehaviorSubject(convertToParamMap({}));

  const row = (over: Partial<ScoreGroupRowDto>): ScoreGroupRowDto => ({
    groupKey: 'g-' + nextKey(),
    groupName: 'Гурток',
    place: 1,
    score: 0,
    otherScore: 0,
    youthPoints: 0,
    youthCount: 0,
    average: 0,
    groupPoints: 0,
    canOpen: false,
    ...over
  });

  const data: KurinScoreDto = {
    kurinKey: 'k1',
    algorithm: ScoreAlgorithm.Average,
    period: { kind: 'Year', year: 2026, stageKey: null, label: '26–27', from: '2026-10-01', to: '2027-09-30' },
    periods: { years: [{ kind: 'Year', year: 2026, stageKey: null, label: '26–27', from: '2026-10-01', to: '2027-09-30' }], stages: [] },
    groups: [
      row({ groupKey: 'sokoly', groupName: 'Соколи', place: 1, score: 10, otherScore: 20, youthPoints: 20, youthCount: 2, average: 10, canOpen: true }),
      row({ groupKey: 'levy', groupName: 'Леви', place: 2, score: 3, otherScore: 12, youthPoints: 12, youthCount: 4, average: 3, groupPoints: 5 })
    ],
    viewer: { canScore: true, canManage: false }
  };

  function create(response: KurinScoreDto = data): void {
    scores = {
      getKurinScore: vi.fn()
    } as MockedObject<ScoreService>;
    scores.getKurinScore.mockReturnValue(of(response));

    TestBed.configureTestingModule({
      imports: [KurinScoreComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: ScoreService, useValue: scores },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ kurinKey: 'k1' })), queryParamMap: queryParams } }
      ]
    });
    fixture = TestBed.createComponent(KurinScoreComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('ранжує гуртки і показує друге число поруч', () => {
    create();

    expect(scores.getKurinScore).toHaveBeenCalledWith('k1', {});
    expect(text()).toContain('Соколи');
    expect(text()).toMatch(/сума\s20/);
    expect(component.barWidth(data.groups[0])).toBe('100%');
    expect(component.barWidth(data.groups[1])).toBe('30%');
  });

  it('лише той, кому гурток відкритий, бачить посилання на його сторінку', () => {
    create();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a.score-row__name'));
    expect(links.map(a => a.textContent?.trim())).toEqual(['Соколи']);
  });

  it('читає період із рядка запиту', () => {
    queryParams.next(convertToParamMap({ year: '2025' }));
    create();

    expect(scores.getKurinScore).toHaveBeenCalledWith('k1', { year: 2025 });
    queryParams.next(convertToParamMap({}));
  });

  it('без балів пояснює, де їх ставити', () => {
    create({ ...data, groups: data.groups.map(g => row({ groupKey: g.groupKey, groupName: g.groupName })) });

    expect(text()).toContain('Балів за цей період ще немає');
  });
});
