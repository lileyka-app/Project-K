import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AgendaAssignSelectComponent } from './agenda-assign-select';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaAssignTargets } from '../../models/agenda';

describe('AgendaAssignSelectComponent', () => {
  let fixture: ComponentFixture<AgendaAssignSelectComponent>;
  let agendaService: MockedObject<AgendaService>;

  /** What the backend hands a Гуртковий: the kurin is off limits, his гурток and its people are not. */
  const targetsForGroupProvid: AgendaAssignTargets = {
    canTargetKurin: false,
    kurinKey: 'k1',
    kurinLabel: 'Весь курінь',
    kurinLeaderships: [{ leadershipKey: 'l-kv', label: 'КВ', canTarget: false }],
    groups: [
      {
        groupKey: 'g1', name: 'Соколи', canTargetGroup: true,
        leadership: { leadershipKey: 'l-g1', label: 'Провід гуртка', canTarget: true },
        members: [{ memberKey: 'm1', fullName: 'Марта Коваль', canTarget: true }]
      },
      {
        groupKey: 'g2', name: 'Ведмеді', canTargetGroup: false, leadership: null,
        members: [{ memberKey: 'm2', fullName: 'Ігор Бондар', canTarget: false }]
      }
    ]
  };

  beforeEach(async () => {
    agendaService = {
      getAssignTargets: vi.fn()
    } as MockedObject<AgendaService>;
    agendaService.getAssignTargets.mockReturnValue(of(targetsForGroupProvid));

    await TestBed.configureTestingModule({
      imports: [AgendaAssignSelectComponent],
      providers: [{ provide: AgendaService, useValue: agendaService }]
    }).compileComponents();

    fixture = TestBed.createComponent(AgendaAssignSelectComponent);
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();
  });

  // A node the person cannot pick, with nothing pickable under it, is not drawn at all: no
  // «весь курінь» to stare at when the choice is not theirs.
  it('draws only what can be picked, or what holds something that can', () => {
    const nodes = (fixture.componentInstance as unknown as { nodes: () => { key?: string; selectable?: boolean; children?: unknown[] }[] }).nodes();

    expect(nodes.map(node => node.key)).toEqual(['group:g1']);
    expect(nodes[0].selectable).toBe(true);
    expect((nodes[0].children ?? []).map(child => (child as { key?: string }).key)).toEqual(['leadership:l-g1', 'member:m1']);
  });

  // A person is picked only when the kurin lets this user assign to them; one who is not is left
  // out together with a гурток that then holds nothing pickable.
  it('offers a person only when the backend says so', () => {
    const nodes = (fixture.componentInstance as unknown as { nodes: () => { key?: string; children?: { key?: string; selectable?: boolean }[] }[] }).nodes();

    expect(nodes.find(node => node.key === 'group:g2')).toBeUndefined();
    expect(nodes[0].children?.find(child => child.key === 'member:m1')?.selectable).toBe(true);
  });
});
