import { ThirdProbeDto } from './kurin.models';
import { countdownParts, dayWord, reachLabels, sharedWhilePending, thirdProbeChecklist, youthEndsOn } from './third-probe';

function tab(overrides: Partial<ThirdProbeDto> = {}): ThirdProbeDto {
  return {
    isProjectProbe: true,
    canEdit: false,
    canSign: false,
    canUnsign: false,
    file: null,
    projects: [],
    badges: [],
    limits: {
      maxMainProjects: 5, maxSpareProjects: 1, maxSharedMainProjects: 2, maxImagesPerProject: 5, maxImageBytes: 1048576,
      maxCertificateBytes: 2097152, maxStoredBytes: 10485760, maxTextLength: 10000, requiredBadges: 4, youthEndsAtAge: 18,
    },
    ...overrides,
  };
}

describe('third probe on the phone', () => {
  it('counts down to the 18th birthday', () => {
    const end = youthEndsOn('2010-06-03', 18)!;
    expect(end).toEqual(new Date(2028, 5, 3));
    expect(countdownParts(end, new Date(2028, 5, 1, 21, 58, 30))).toEqual({ days: 1, hours: 2, minutes: 1, seconds: 30, isOver: false });
    expect(youthEndsOn(null, 18)).toBeNull();
  });

  it('says days and reaches in Ukrainian', () => {
    expect([1, 3, 11, 581].map(dayWord)).toEqual(['день', 'дні', 'днів', 'день']);
    expect(reachLabels(['Stanytsia', 'WiderSociety'])).toEqual(['станиця', 'ширша громада']);
  });

  it('reads what is done from the tab, the spare and earlier вмілості not counting', () => {
    const checklist = thirdProbeChecklist(
      tab({
        projects: [{ isSpare: true, completedAtUtc: '2026-10-01', area: 'Nature' } as never],
        badges: [{ badgeId: 'b', title: 'b', imagePath: null, specialization: null, confirmedAtUtc: null, duringProbe: false }],
      }),
    );
    expect(checklist.map((item) => item.done)).toEqual([false, false, false, false, false]);
    expect(checklist[1].detail).toBe('виконано 0 з 5 · ділянок 0 з 5');
    expect(checklist[3].detail).toBe('0 з 4 · без тіловиховання');
    expect(checklist[0].label).toBe('Рефлексія');
    expect(checklist[2].detail).toBe('ще не вказано');
  });

  it('saves a new project once for pictures dropped together, and again after a failure', async () => {
    let calls = 0;
    let fail = false;
    const create = sharedWhilePending(async () => {
      calls++;
      if (fail) throw new Error('offline');
      return `p-${calls}`;
    });

    expect(await Promise.all([create(), create()])).toEqual(['p-1', 'p-1']);
    expect(calls).toBe(1);

    fail = true;
    await expect(create()).rejects.toThrow('offline');
    fail = false;
    expect(await create()).toBe('p-3');
  });
});
