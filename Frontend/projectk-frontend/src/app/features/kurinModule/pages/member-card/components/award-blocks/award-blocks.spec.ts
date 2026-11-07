import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AwardBlocksComponent } from './award-blocks';
import { AwardBlock, groupGrants, orderBlocks } from './award-blocks.function';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { BadgeProgressStatus } from '../../../../models/enums/badge-progress-status.enum';
import { MemberAwardLevel } from '../../../../models/enums/member-award-level.enum';
import { seniorAwardBlocks } from '../member-senior-awards-tile/senior-awards.function';
import { CatalogHonour } from '../../../../models/honour.dto';

const award = (key: string, dateAcquired: string, extra: Partial<MemberAwardDto> = {}): MemberAwardDto => ({
  memberAwardKey: key, memberKey: 'm', kurinKey: 'k', dateAcquired, status: BadgeProgressStatus.Confirmed, ...extra
});

const block = (key: string, date: string, pinned = false, grants = 1): AwardBlock => ({
  key, title: key, imageUrl: null, section: '', pinned, pending: false, pin: { catalogId: key },
  grants: Array.from({ length: grants }, (_, i) => ({ award: award(`${key}${i}`, date), pending: false, stamp: null, canOpen: true }))
});

const honours: CatalogHonour[] = [
  { id: 'usp-hramota-zasluhy', kind: 'Award', title: 'Грамота заслуги', group: 'usp-vidznachennia', ulad: 'usp', order: 0, imageUrl: '' },
  { id: 'ups-hramota-zasluhy', kind: 'Award', title: 'Грамота заслуги', group: 'ups-vidznachennia', ulad: 'ups', order: 1, imageUrl: '' }
];

describe('блоки відзначень', () => {
  it('те саме відзначення — один блок з усіма наданнями, новіше першим', () => {
    const groups = groupGrants([
      award('a', '2015-01-01T00:00:00', { level: MemberAwardLevel.First }),
      award('b', '2019-06-01T00:00:00', { level: MemberAwardLevel.First }),
      award('c', '2017-03-01T00:00:00', { level: MemberAwardLevel.Second })
    ], a => a.level!);

    expect([...groups.keys()]).toEqual([MemberAwardLevel.First, MemberAwardLevel.Second]);
    expect(groups.get(MemberAwardLevel.First)!.map(a => a.memberAwardKey)).toEqual(['b', 'a']);
  });

  it('закріплені першими, далі останні здобуті', () => {
    const ordered = orderBlocks([block('old', '2010-01-01T00:00:00'), block('new', '2024-01-01T00:00:00'), block('liked', '2005-01-01T00:00:00', true)]);
    expect(ordered.map(b => b.key)).toEqual(['liked', 'new', 'old']);
  });

  it('старші: грамота УСП і грамота УПС — різні блоки зі своїм уладом; закріплене, якщо закріплене будь-яке надання', () => {
    const blocks = seniorAwardBlocks([
      award('a', '2012-10-20T00:00:00', { catalogId: 'usp-hramota-zasluhy', note: 'За табір' }),
      award('b', '2015-03-14T00:00:00', { catalogId: 'usp-hramota-zasluhy', isPinned: true }),
      award('c', '2020-01-01T00:00:00', { catalogId: 'ups-hramota-zasluhy' }),
      award('d', '2020-01-01T00:00:00', { level: MemberAwardLevel.First })
    ], honours, false);

    expect(orderBlocks(blocks).map(b => [b.key, b.section, b.grants.length, b.pinned])).toEqual([
      ['usp-hramota-zasluhy', 'УСП', 2, true],
      ['ups-hramota-zasluhy', 'УПС', 1, false]
    ]);
    expect(orderBlocks(blocks)[0].pin).toEqual({ catalogId: 'usp-hramota-zasluhy' });
  });
});

describe('AwardBlocksComponent', () => {
  let fixture: ComponentFixture<AwardBlocksComponent>;
  let component: AwardBlocksComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [AwardBlocksComponent] });
    fixture = TestBed.createComponent(AwardBlocksComponent);
    component = fixture.componentInstance;
  });

  const render = (blocks: AwardBlock[], ownCard = false) => {
    fixture.componentRef.setInput('blocks', blocks);
    fixture.componentRef.setInput('ownCard', ownCard);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('у плитці — перші чотири, решта за «Переглянути всі»; повтор — з лічильником', () => {
    const element = render([1, 2, 3, 4, 5].map(i => block(`b${i}`, `202${i}-01-01T00:00:00`, false, i === 5 ? 3 : 1)));

    expect(element.querySelectorAll('[data-testid="award-block"]').length).toBe(4);
    expect(element.querySelector('[data-testid="awards-all"]')?.textContent).toContain('Переглянути всі (5)');
    expect(element.textContent).toContain('×3');
  });

  it('блок відкриває всі надання; закріпити може лише власник', () => {
    render([block('b', '2020-01-01T00:00:00', false, 2)], true);
    const pins: AwardBlock[] = [];
    const opened: string[] = [];
    component.togglePin.subscribe(b => pins.push(b));
    component.openGrant.subscribe(a => opened.push(a.memberAwardKey));

    component.openDetails(component.ordered()[0]);
    expect(component.selected?.grants.length).toBe(2);
    component.pin(component.selected!);
    component.open(component.selected!.grants[1].award);

    expect(pins.map(p => p.key)).toEqual(['b']);
    expect(opened).toEqual(['b1']);
    expect(component.detailsVisible).toBe(false);
  });
});
