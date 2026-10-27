import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GroupMemberStandingDto, KurinProbeEditionsDto, probeEditionLabel } from '../../models/group-probe-edition.dto';
import { GroupService } from '../../services/group-service/group.service';
import { KurinProbeEditionsComponent, memberOutcome } from './kurin-probe-editions';

const NEW = 'upyu-2026';
/** Не те, що можна вивести з id: назву дає лише каталог. */
const NEW_TITLE = 'Проба УПЮ, редакція 2026';

function member(overrides: Partial<GroupMemberStandingDto>): GroupMemberStandingDto {
  return { memberKey: 'm', fullName: 'Олесь Коваль', hasStarted: false, edition: null, probeTitle: null, isHeld: false, ...overrides };
}

function kurinState(count = 2): KurinProbeEditionsDto {
  return {
    editions: [
      { id: null, title: null, isTrial: false, probeTitles: ['Перша проба', 'Друга проба'] },
      { id: NEW, title: NEW_TITLE, isTrial: true, probeTitles: ['Знак прихильника', 'Перша проба'] }
    ].slice(0, count),
    groups: [
      {
        groupKey: 'g1',
        name: 'Соколи',
        edition: null,
        canChange: true,
        members: [
          member({ memberKey: 'm1', fullName: 'Олесь Коваль', hasStarted: true, probeTitle: 'Перша проба', isHeld: true }),
          member({ memberKey: 'm2', fullName: 'Ірина Бойко', hasStarted: true, probeTitle: 'Перша проба' }),
          member({ memberKey: 'm3', fullName: 'Тарас Мельник' })
        ]
      },
      { groupKey: 'g2', name: 'Орли', edition: NEW, canChange: false, members: [] }
    ]
  };
}

describe('memberOutcome', () => {
  it('keeps whoever closed a first probe on its edition', () => {
    const outcome = memberOutcome(member({ hasStarted: true, isHeld: true, probeTitle: 'Перша проба' }), NEW);
    expect(outcome.kind).toBe('held');
    expect(outcome.edition).toBeNull();
  });

  it('moves someone with a first probe only under way, and says what they leave', () => {
    const outcome = memberOutcome(member({ hasStarted: true, probeTitle: 'Перша проба' }), NEW);
    expect(outcome.kind).toBe('abandons');
    expect(outcome.edition).toBe(NEW);
    expect(outcome.note).toContain('не продовжує розпочату «Перша проба»');
  });

  it('lets someone go on in the edition they already walk', () => {
    expect(memberOutcome(member({ hasStarted: true, probeTitle: 'Перша проба' }), null).kind).toBe('continues');
    expect(memberOutcome(member({}), NEW)).toMatchObject({ kind: 'follows', edition: NEW, note: null });
  });

  it('names an edition as the catalogue does, and the regular probes itself', () => {
    const editions = kurinState().editions;
    expect(probeEditionLabel(editions, null)).toBe('Звичайна проба');
    expect(probeEditionLabel(editions, NEW)).toBe(NEW_TITLE);
    expect(probeEditionLabel(editions, 'upyu-2030')).toBe('upyu-2030');
  });
});

describe('KurinProbeEditionsComponent', () => {
  let fixture: ComponentFixture<KurinProbeEditionsComponent>;
  let groups: { getKurinProbeEditions: ReturnType<typeof vi.fn>; setProbeEdition: ReturnType<typeof vi.fn> };

  async function create(state: KurinProbeEditionsDto): Promise<HTMLElement> {
    groups = { getKurinProbeEditions: vi.fn(() => of(state)), setProbeEdition: vi.fn(() => of(undefined)) };
    await TestBed.configureTestingModule({
      imports: [KurinProbeEditionsComponent],
      providers: [{ provide: GroupService, useValue: groups }]
    }).compileComponents();
    fixture = TestBed.createComponent(KurinProbeEditionsComponent);
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('offers no choice while the catalogue has one edition', async () => {
    await create(kurinState(1));
    expect(fixture.componentInstance.offersChoice()).toBe(false);
  });

  it('lists every гурток, and a select only where the caller may switch', async () => {
    const element = await create(kurinState());

    expect(fixture.componentInstance.offersChoice()).toBe(true);
    expect(element.textContent).toContain('Соколи');
    expect(element.textContent).toContain('Орли');
    expect(element.querySelectorAll('p-select').length).toBe(1);
    expect(element.textContent).toContain(NEW_TITLE);
  });

  it('saves only the гуртки whose edition changed, with no edition sent as null', async () => {
    await create(kurinState());
    const component = fixture.componentInstance as unknown as { choose(group: { groupKey: string }, value: string): void };

    expect(fixture.componentInstance.pendingCount()).toBe(0);
    component.choose({ groupKey: 'g1' }, NEW);
    expect(fixture.componentInstance.pendingCount()).toBe(1);

    fixture.componentInstance.save().subscribe();
    expect(groups.setProbeEdition).toHaveBeenCalledTimes(1);
    expect(groups.setProbeEdition).toHaveBeenCalledWith('g1', NEW);
  });

  it('shows before saving who stays and who leaves a started probe', async () => {
    const element = await create(kurinState());
    const component = fixture.componentInstance as unknown as { choose(group: { groupKey: string }, value: string): void };

    component.choose({ groupKey: 'g1' }, NEW);
    fixture.detectChanges();

    const summary = element.querySelector('.editions__counts')?.textContent ?? '';
    expect(summary).toContain('за гуртком: 2');
    expect(summary).toContain('на своїй: 1');
    expect(summary).toContain('не продовжують розпочату пробу: 1');
    expect(element.textContent).toContain('зміниться після «Зберегти»');
  });
});
