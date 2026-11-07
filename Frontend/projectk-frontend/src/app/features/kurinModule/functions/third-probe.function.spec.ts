import { probeProject, thirdProbeTab } from './third-probe.fixtures';
import {
  countdownMood,
  countdownParts,
  fromIsoDate,
  thirdProbeChecklist,
  toIsoDate,
  youthEndsOn
} from './third-probe.function';

describe('third probe helpers', () => {
  it('ends youth on the 18th birthday', () => {
    expect(youthEndsOn('2010-06-03', 18)).toEqual(new Date(2028, 5, 3));
    expect(youthEndsOn(null, 18)).toBeNull();
  });

  it('counts what is left down to the second, and stops at zero', () => {
    const deadline = new Date(2028, 5, 3);
    const parts = countdownParts(deadline, new Date(2028, 5, 1, 21, 58, 30));
    expect(parts).toEqual({ days: 1, hours: 2, minutes: 1, seconds: 30, isOver: false });
    expect(countdownParts(deadline, new Date(2029, 0, 1)).isOver).toBe(true);
  });

  it('gets more urgent near the end', () => {
    const deadline = new Date(2028, 5, 3);
    expect(countdownMood(countdownParts(deadline, new Date(2027, 0, 1)))).toBe('calm');
    expect(countdownMood(countdownParts(deadline, new Date(2028, 3, 1)))).toBe('soon');
    expect(countdownMood(countdownParts(deadline, new Date(2028, 4, 20)))).toBe('urgent');
  });

  it('turns picked days into the API dates and back without slipping a day', () => {
    expect(toIsoDate(new Date(2026, 10, 7, 23, 30))).toBe('2026-11-07');
    expect(fromIsoDate('2026-11-07')).toEqual(new Date(2026, 10, 7));
  });

  it('calls the probe done only when every requirement is', () => {
    const empty = thirdProbeChecklist(thirdProbeTab());
    expect(empty.every(item => !item.done)).toBe(true);

    const areas = ['OathAndIdea', 'Community', 'PlastActivities', 'Nature', 'Leadership'] as const;
    const full = thirdProbeTab({
      file: {
        ...thirdProbeTab().file!,
        reflectionConfirmedAtUtc: '2026-03-02T00:00:00Z', campName: 'КВТ',
        physicalBadgeId: 'b-1', resume: 'Мій шлях'
      },
      projects: areas.map((area, index) => probeProject({ projectKey: `p-${index}`, area, completedAtUtc: '2026-10-01T00:00:00Z' })),
      badges: ['b-1', 'b-2', 'b-3', 'b-4'].map(badgeId => ({
        badgeId, title: badgeId, imagePath: null, specialization: null, confirmedAtUtc: null, duringProbe: true
      }))
    });
    expect(thirdProbeChecklist(full).every(item => item.done)).toBe(true);
  });

  it('does not count the spare, nor a badge from before the probe', () => {
    const tab = thirdProbeTab({
      projects: [probeProject({ isSpare: true, completedAtUtc: '2026-10-01T00:00:00Z' })],
      badges: [{ badgeId: 'b-1', title: 'b', imagePath: null, specialization: null, confirmedAtUtc: null, duringProbe: false }]
    });
    const checklist = thirdProbeChecklist(tab);
    expect(checklist.find(item => item.key === 'projects')!.detail).toContain('виконано 0 з 5');
    expect(checklist.find(item => item.key === 'badges')!.detail).toContain('0 з 4');
  });
});
