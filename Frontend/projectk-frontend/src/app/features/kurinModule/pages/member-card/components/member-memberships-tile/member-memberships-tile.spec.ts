import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { MemberMembershipsTileComponent } from './member-memberships-tile';
import { MembershipDto } from '../../../../models/membership.dto';
import { KurinBranch } from '../../../../models/enums/kurin-branch.enum';
import { MembershipKind } from '../../../../models/enums/membership-kind.enum';

describe('MemberMembershipsTileComponent', () => {
  let fixture: ComponentFixture<MemberMembershipsTileComponent>;
  let component: MemberMembershipsTileComponent;

  const membership = (over: Partial<MembershipDto>): MembershipDto => ({
    membershipKey: crypto.randomUUID(),
    kurinKey: crypto.randomUUID(),
    kurinNumber: 7,
    branch: KurinBranch.UPYu,
    kurinNamedAfter: null,
    groupKey: null,
    groupName: null,
    kind: MembershipKind.Youth,
    joinedAtUtc: '2022-09-01T00:00:00Z',
    leftAtUtc: null,
    isCurrent: true,
    ...over
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MemberMembershipsTileComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(MemberMembershipsTileComponent);
    component = fixture.componentInstance;
  });

  it('розділяє теперішні курені й ті, які людина покинула', () => {
    const here = membership({});
    const left = membership({ isCurrent: false, leftAtUtc: '2024-06-01T00:00:00Z', kurinNumber: 42 });
    fixture.componentRef.setInput('memberships', [here, left]);
    fixture.detectChanges();

    expect(component.current()).toEqual([here]);
    expect(component.past()).toEqual([left]);
  });

  // Переводить у гурток і виводить з куреня лише Звʼязковий.
  it('дає «Гурток» і «Вивести» лише тому, хто розставляє людей у курені', () => {
    const kurinKey = crypto.randomUUID();
    fixture.componentRef.setInput('memberships', [membership({ kurinKey })]);
    fixture.componentRef.setInput('scopedKurinKey', kurinKey);
    fixture.detectChanges();
    const labels = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.membership-actions button'))
      .map(b => b.textContent?.trim());

    expect(labels()).toEqual([]);

    fixture.componentRef.setInput('canManage', true);
    fixture.detectChanges();

    expect(labels()).toEqual(['Гурток', 'Вивести']);
  });

  it('веде до куреня й гуртка, де діє той, хто дивиться, а до іншого свого куреня — через перемикання', () => {
    const scoped = crypto.randomUUID();
    const other = membership({ kurinNumber: 9 });
    const foreign = membership({ kurinNumber: 12 });
    fixture.componentRef.setInput('memberships', [
      membership({ kurinKey: scoped, groupKey: 'g1', groupName: 'Кельти' }), other, foreign
    ]);
    fixture.componentRef.setInput('scopedKurinKey', scoped);
    fixture.componentRef.setInput('reachableKurinKeys', [other.kurinKey]);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const opened: MembershipDto[] = [];
    component.openKurin.subscribe(m => opened.push(m));

    expect(Array.from(root.querySelectorAll('a.membership-link')).map(a => a.getAttribute('href'))).toEqual(['/kurin', '/group/g1']);
    const buttons = root.querySelectorAll<HTMLButtonElement>('button.membership-link');
    expect(buttons).toHaveLength(1);
    buttons[0].click();
    expect(opened).toEqual([other]);
  });

  it('теперішнє членство показує лише рік початку', () => {
    expect(component.period(membership({}))).toBe('з 2022');
  });

  it('закрите членство показує обидва роки, а однорічне — один', () => {
    expect(component.period(membership({ isCurrent: false, leftAtUtc: '2024-06-01T00:00:00Z' })))
      .toBe('2022 — 2024');
    expect(component.period(membership({
      isCurrent: false,
      joinedAtUtc: '2024-01-01T00:00:00Z',
      leftAtUtc: '2024-06-01T00:00:00Z'
    }))).toBe('2024');
  });
});
