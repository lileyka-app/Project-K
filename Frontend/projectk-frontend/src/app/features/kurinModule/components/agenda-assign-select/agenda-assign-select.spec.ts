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

  // People in no гурток are picked one by one: a КВ member under the КВ, everyone else in a branch of their own.
  it('offers the КВ members under the КВ and people outside гуртки on their own', () => {
    agendaService.getAssignTargets.mockReturnValue(of({
      ...targetsForGroupProvid,
      canTargetKurin: true,
      kurinLeaderships: [{ leadershipKey: 'l-kv', label: 'КВ', canTarget: true, members: [{ memberKey: 'm-kv', fullName: 'Олег Кравець', canTarget: true }] }],
      unplacedMembers: [{ memberKey: 'm-out', fullName: 'Ірина Сорока', canTarget: true }]
    }));
    fixture = TestBed.createComponent(AgendaAssignSelectComponent);
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();

    interface Node { key?: string; label?: string; children?: Node[] }
    const nodes = (fixture.componentInstance as unknown as { nodes: () => Node[] }).nodes();
    const kv = nodes.find(node => node.key === 'kurin:k1')?.children?.find(child => child.key === 'leadership:l-kv');

    expect(kv?.children?.map(child => child.key)).toEqual(['member:m-kv']);
    expect(nodes.find(node => node.key === 'unplaced')?.label).toBe('Поза гуртками');
    expect(nodes.find(node => node.key === 'unplaced')?.children?.map(child => child.key)).toEqual(['member:m-out']);
  });

  // An older item aimed at a tag the tree no longer draws (tags off, or no right to pick tags) keeps
  // that target when the editor changes what they can see.
  it('keeps a target the tree does not draw when the selection changes', () => {
    fixture.componentRef.setInput('targets', [{ targetType: 'Tag', targetKey: 't-old' }]);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      nodes: () => { key?: string; children?: { key?: string; data?: unknown }[] }[];
      onSelectionChange: (nodes: unknown[]) => void;
    };
    const marta = component.nodes()[0].children!.find(child => child.key === 'member:m1')!;

    component.onSelectionChange([marta]);

    expect(fixture.componentInstance.targets()).toEqual([
      { targetType: 'Tag', targetKey: 't-old' },
      { targetType: 'Member', targetKey: 'm1' }
    ]);
  });

  // «Усі з тегом» stands beside the гуртки, as its own branch, and a chosen tag goes out as a Tag target.
  it('offers the kurin tags as their own branch and sends a chosen one as a Tag target', () => {
    agendaService.getAssignTargets.mockReturnValue(of({
      ...targetsForGroupProvid,
      tags: [{ kurinTagKey: 't-new', name: 'Новики', colorHex: '#2F855A' }]
    }));
    fixture = TestBed.createComponent(AgendaAssignSelectComponent);
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      nodes: () => { key?: string; selectable?: boolean; children?: { key?: string; label?: string; data?: unknown }[] }[];
      onSelectionChange: (nodes: unknown[]) => void;
    };
    const tags = component.nodes().find(node => node.key === 'tags');

    expect(tags?.selectable).toBe(false);
    expect(tags?.children?.map(child => child.label)).toEqual(['Усі з тегом «Новики»']);

    component.onSelectionChange([tags!.children![0]]);
    expect(fixture.componentInstance.targets()).toEqual([{ targetType: 'Tag', targetKey: 't-new' }]);
  });
});
