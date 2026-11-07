import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { MemberSeniorAwardsTileComponent } from './member-senior-awards-tile';
import { seniorAwardRows } from './senior-awards.function';
import { HonoursService } from '../../../../services/honours-service/honours.service';
import { CatalogHonour } from '../../../../models/honour.dto';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { BadgeProgressStatus } from '../../../../models/enums/badge-progress-status.enum';
import { MemberAwardLevel } from '../../../../models/enums/member-award-level.enum';
import { degreeRows, SENIOR_LADDER, YOUTH_LADDER } from '../member-degrees-tile/member-degrees.function';
import { PlastLevel } from '../../../../models/enums/plast-level.enum';

const honours: CatalogHonour[] = [
  { id: 'usp-starsha-plastunka', kind: 'Degree', title: 'Старша пластунка', group: 'usp', ulad: 'usp', order: 2, imageUrl: 'https://catalog.test/spl.svg' },
  { id: 'upyu-virlytsia', kind: 'Degree', title: 'Пластунка вірлиця', group: 'upyu', ulad: 'upyu', order: 7, imageUrl: 'https://catalog.test/v.svg' },
  { id: 'orden-sv-yuriia-bronza', kind: 'Award', title: 'Орден святого Юрія в бронзі', group: 'orden-sv-yuriia', ulad: 'usp', order: 0, imageUrl: 'https://catalog.test/o.png' },
  { id: 'usp-hramota-podiaky', kind: 'Award', title: 'Грамота подяки', group: 'usp-vidznachennia', ulad: 'usp', order: 1, imageUrl: '' }
];

const award = (key: string, catalogId: string | null, dateAcquired: string, level: MemberAwardLevel | null = null): MemberAwardDto => ({
  memberAwardKey: key, memberKey: 'm', kurinKey: 'k', level, catalogId, dateAcquired, status: BadgeProgressStatus.Confirmed
});

describe('ступені на картці', () => {
  const histories = [
    { plastLevel: PlastLevel.Skob, dateAchieved: '2014-06-10', isPlastunka: true, catalogId: 'upyu-virlytsia' },
    { plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2016-12-03', isPlastunka: true, catalogId: 'usp-starsha-plastunka' },
    { plastLevel: PlastLevel.Entry, dateAchieved: '2009-09-01', catalogId: null }
  ];

  it('юнацька картка — драбина УПЮ, старша — УСП/УПС; назва й відзнака з каталогу, для пластунки свої', () => {
    expect(degreeRows(histories, YOUTH_LADDER, honours).map(r => r.title)).toEqual(['Пластунка вірлиця']);
    const senior = degreeRows(histories, SENIOR_LADDER, honours);
    expect(senior.map(r => [r.title, r.imageUrl])).toEqual([['Старша пластунка', 'https://catalog.test/spl.svg']]);
    expect(senior[0].date?.getFullYear()).toBe(2016);
  });

  it('без каталогу ступінь лишається зі своєю назвою й без рисунка', () => {
    expect(degreeRows(histories, SENIOR_LADDER, [])).toEqual([expect.objectContaining({ title: 'ст. пл.', imageUrl: '' })]);
  });
});

describe('старші відзначення', () => {
  it('лише ті, що з каталогу, новіші вгорі; невідоме каталогу лишається під своїм id', () => {
    const rows = seniorAwardRows([
      award('a', 'usp-hramota-podiaky', '2024-06-09T00:00:00'),
      award('b', null, '2015-01-01T00:00:00', MemberAwardLevel.First),
      award('c', 'orden-sv-yuriia-bronza', '2018-04-23T00:00:00'),
      award('d', 'zabute', '2020-01-01T00:00:00')
    ], honours);

    expect(rows.map(r => r.title)).toEqual(['Грамота подяки', 'zabute', 'Орден святого Юрія в бронзі']);
    expect(rows[2].imageUrl).toBe('https://catalog.test/o.png');
  });
});

describe('MemberSeniorAwardsTileComponent', () => {
  let fixture: ComponentFixture<MemberSeniorAwardsTileComponent>;
  let component: MemberSeniorAwardsTileComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MemberSeniorAwardsTileComponent],
      providers: [{ provide: HonoursService, useValue: { all: () => of(honours) } }]
    });
    fixture = TestBed.createComponent(MemberSeniorAwardsTileComponent);
    component = fixture.componentInstance;
  });

  const render = (inputs: { canAdd: boolean; ownCard: boolean; canRemove: boolean }) => {
    fixture.componentRef.setInput('awards', [award('c', 'orden-sv-yuriia-bronza', '2018-04-23T00:00:00')]);
    fixture.componentRef.setInput('canAdd', inputs.canAdd);
    fixture.componentRef.setInput('ownCard', inputs.ownCard);
    fixture.componentRef.setInput('canRemove', inputs.canRemove);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('у вибір — лише відзначення з каталогу, без ступенів', () => {
    render({ canAdd: true, ownCard: true, canRemove: false });
    expect(component.options()).toEqual([{ label: 'УСП', items: [honours[2], honours[3]] }]);
  });

  it('людина вносить своє з каталогу: рівня немає, лише id', () => {
    render({ canAdd: true, ownCard: true, canRemove: false });
    const saved: unknown[] = [];
    component.saveAward.subscribe(request => saved.push(request));

    component.open(null);
    component.catalogId = 'usp-hramota-podiaky';
    component.dateAcquired = new Date(2024, 5, 9);
    component.save();

    expect(saved).toEqual([{ memberAwardKey: undefined, catalogId: 'usp-hramota-podiaky', dateAcquired: '2024-06-09', note: undefined }]);
  });

  it('хто лише дивиться — без «Додати», і рядок не відкривається', () => {
    const element = render({ canAdd: false, ownCard: false, canRemove: false });

    expect(element.querySelector('[data-testid="senior-award-add"]')).toBeNull();
    expect(element.textContent).toContain('Орден святого Юрія в бронзі');
    component.open(component.awards()[0]);
    expect(component.dialogVisible).toBe(false);
  });

  it('провід із правом прибирати відкриває чуже й видаляє', () => {
    render({ canAdd: true, ownCard: false, canRemove: true });
    const removed: string[] = [];
    component.deleteAward.subscribe(key => removed.push(key));

    component.open(component.awards()[0]);
    component.remove();

    expect(removed).toEqual(['c']);
  });
});

describe('MemberDegreesTileComponent', () => {
  it('на картці лише найвищий ступінь, великим планом', async () => {
    const { MemberDegreesTileComponent } = await import('../member-degrees-tile/member-degrees-tile');
    TestBed.configureTestingModule({
      imports: [MemberDegreesTileComponent],
      providers: [{ provide: HonoursService, useValue: { all: () => of(honours) } }]
    });
    const fixture = TestBed.createComponent(MemberDegreesTileComponent);
    fixture.componentRef.setInput('histories', [
      { plastLevel: PlastLevel.Starshoplastun, dateAchieved: '2016-12-03', isPlastunka: true, catalogId: 'usp-starsha-plastunka' },
      { plastLevel: PlastLevel.SeniorPratsi, dateAchieved: '2020-05-14', catalogId: 'ups-senior-pratsi' }
    ]);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelectorAll('[data-testid="degree-highest"]').length).toBe(1);
    // The catalogue fixture has no picture of сеніор праці, so the ступінь keeps its own short name.
    expect(element.textContent).toContain('пл. сен. пр.');
    expect(element.textContent).not.toContain('Старша пластунка');
  });
});
