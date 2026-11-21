import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { CatalogHonour } from '../../../../models/honour.dto';
import { currentDegree } from './member-degrees.function';

const honours: CatalogHonour[] = [
  { id: 'upyu-skob', kind: 'Degree', title: 'Пластун скоб', group: 'upyu', ulad: 'upyu', order: 4, imageUrl: 'https://catalog.test/skob.png' },
  { id: 'upyu-hetmanska-virlytsia', kind: 'Degree', title: 'Гетьманська пластунка вірлиця', group: 'upyu', ulad: 'upyu', order: 5, imageUrl: 'https://catalog.test/hetmanska.png' },
  { id: 'usp-starshyi-plastun', kind: 'Degree', title: 'Старший пластун', group: 'usp', ulad: 'usp', order: 1, imageUrl: 'https://catalog.test/stpl.png' },
  { id: 'usp-starsha-plastunka', kind: 'Degree', title: 'Старша пластунка', group: 'usp', ulad: 'usp', order: 1, imageUrl: 'https://catalog.test/stpl-f.png' },
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

  it('names a старший пластун by the скоб they carry, draws the скоб, dates the старшопластунство', () => {
    const degree = currentDegree([
      { plastLevel: PlastLevel.Skob, dateAchieved: '2020-07-18', catalogId: 'upyu-skob' },
      { plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2022-10-01', catalogId: 'usp-starshyi-plastun' }
    ] as never, honours);

    expect(degree?.title).toBe('Старший пластун скоб');
    expect(degree?.imageUrl).toBe('https://catalog.test/skob.png');
    expect(degree?.date?.getFullYear()).toBe(2022);
  });

  it('gives the гетьманська вірлиця the lead over the plain one, for a пластунка', () => {
    const degree = currentDegree([
      { plastLevel: PlastLevel.Skob, dateAchieved: '2019-07-18', catalogId: 'upyu-virlytsia', isPlastunka: true },
      { plastLevel: PlastLevel.HetmanskiySkob, dateAchieved: '2021-07-18', catalogId: 'upyu-hetmanska-virlytsia', isPlastunka: true },
      { plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2022-10-01', catalogId: 'usp-starsha-plastunka', isPlastunka: true }
    ] as never, honours);

    expect(degree?.title).toBe('Старша пластунка гетьманська вірлиця');
    expect(degree?.imageUrl).toBe('https://catalog.test/hetmanska.png');
  });

  it('leaves a старший пластун without a скоб as he is', () => {
    const degree = currentDegree([
      { plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2022-10-01', catalogId: 'usp-starshyi-plastun' }
    ] as never, honours);

    expect(degree?.title).toBe('Старший пластун');
    expect(degree?.imageUrl).toBe('https://catalog.test/stpl.png');
  });

  it('is none for someone with no ступінь yet', () => {
    expect(currentDegree([], honours)).toBeNull();
  });
});
