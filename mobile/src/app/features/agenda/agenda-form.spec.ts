import { targetNames, targetSections } from './agenda-form';
import { AgendaAssignTargets } from './agenda.models';

describe('agenda form targets', () => {
  const tree: AgendaAssignTargets = {
    canTargetKurin: false,
    kurinKey: 'k1',
    kurinLabel: '5 курінь',
    kurinLeaderships: [],
    groups: [],
    tags: [{ kurinTagKey: 't-full', name: 'Повні члени', colorHex: '#2F855A' }]
  };

  // A senior kurin has no гуртки: its tags are what an event is aimed at, under a heading of their own.
  it('lists the kurin tags under their own heading', () => {
    const sections = targetSections(tree);

    expect(sections.map((section) => section.title)).toEqual(['Теги']);
    expect(sections[0].rows).toEqual([
      expect.objectContaining({ targetType: 'Tag', targetKey: 't-full', label: 'Усі з тегом «Повні члени»' })
    ]);
  });

  it('names a chosen tag the same way in the summary', () => {
    expect([...targetNames(tree).values()]).toContain('Усі з тегом «Повні члени»');
  });

  // With no гуртки, the people stand on their own; a КВ member is offered under the kurin, next to the КВ.
  it('lists people outside гуртки one by one, and the КВ members next to the КВ', () => {
    const sections = targetSections({
      ...tree,
      kurinLeaderships: [{ leadershipKey: 'kv', label: 'КВ', canTarget: true, members: [{ memberKey: 'm-kv', fullName: 'Олег Кравець', canTarget: true }] }],
      unplacedMembers: [{ memberKey: 'm-1', fullName: 'Ірина Сорока', canTarget: true }],
      tags: []
    });

    expect(sections.map((section) => section.title)).toEqual(['5 курінь', 'Люди куреня']);
    expect(sections[0].rows.map((r) => r.label)).toEqual(['КВ', 'Олег Кравець']);
    expect(sections[1].rows.map((r) => r.targetType)).toEqual(['Member']);
  });

  it('shows no tags heading when there are none to pick', () => {
    expect(targetSections({ ...tree, tags: [] })).toEqual([]);
  });
});
