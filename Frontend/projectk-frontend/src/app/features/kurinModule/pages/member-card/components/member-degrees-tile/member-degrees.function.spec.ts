import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { CatalogHonour } from '../../../../models/honour.dto';
import { currentDegree } from './member-degrees.function';

const honours: CatalogHonour[] = [
  { id: 'upyu-skob', kind: 'Degree', title: 'Пластун скоб', group: 'upyu', ulad: 'upyu', order: 4, imageUrl: 'https://catalog.test/skob.png' },
  { id: 'ups-senior-prykhylnyk', kind: 'Degree', title: 'Пластун сеніор прихильник', group: 'ups', ulad: 'ups', order: 1, imageUrl: 'https://catalog.test/senior.png' },
  { id: 'ups-seniorka-prykhylnytsia', kind: 'Degree', title: 'Пластунка сеніорка прихильниця', group: 'ups', ulad: 'ups', order: 1, imageUrl: 'https://catalog.test/seniorka.png' }
] as CatalogHonour[];

describe('currentDegree', () => {
  it('is the highest on the whole ladder, not on the kurin’s: a сеніор over the скоб of 2002', () => {
    const degree = currentDegree([
      { plastLevel: PlastLevel.Skob, dateAchieved: '2002-06-01', catalogId: 'upyu-skob' },
      { plastLevel: PlastLevel.Senior, dateAchieved: '2015-04-26', catalogId: 'ups-senior-prykhylnyk' }
    ] as never, honours);

    expect(degree?.title).toBe('Пластун сеніор прихильник');
    expect(degree?.imageUrl).toBe('https://catalog.test/senior.png');
  });

  it('draws a пластунка’s ступінь from her own record', () => {
    const degree = currentDegree([
      { plastLevel: PlastLevel.Senior, dateAchieved: '2015-04-26', catalogId: 'ups-seniorka-prykhylnytsia', isPlastunka: true }
    ] as never, honours);

    expect(degree?.title).toBe('Пластунка сеніорка прихильниця');
  });

  it('is none for someone with no ступінь yet', () => {
    expect(currentDegree([], honours)).toBeNull();
  });
});
