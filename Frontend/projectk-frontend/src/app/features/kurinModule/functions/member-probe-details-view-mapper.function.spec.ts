import { ProbeProgressStatus } from '../models/enums/probe-progress-status.enum';
import { GroupedProbeDto } from '../models/probes-and-badges/grouped-probe.dto';
import { ProbeProgressDto } from '../models/probes-and-badges/probe-progress.dto';
import { buildMemberProbeDetailPointRows } from './member-probe-details-view-mapper.function';

function createGroupedProbe(): GroupedProbeDto {
  return {
    id: 'probe-1',
    title: 'Перша проба',
    pointsCount: 2,
    sectionsCount: 1,
    sections: [
      {
        id: 'sec-1',
        code: 'A',
        title: 'Розділ A',
        points: [
          { id: 'point-1', title: 'Точка 1' },
          { id: 'point-2', title: 'Точка 2' }
        ]
      }
    ]
  };
}

function createProgress(overrides: Partial<ProbeProgressDto>): ProbeProgressDto {
  return {
    probeProgressKey: null,
    memberKey: 'member-1',
    kurinKey: 'kurin-1',
    probeId: 'probe-1',
    status: ProbeProgressStatus.NotStarted,
    completedAtUtc: null,
    completedByUserKey: null,
    completedByName: null,
    completedByRole: null,
    verifiedAtUtc: null,
    verifiedByUserKey: null,
    verifiedByName: null,
    verifiedByRole: null,
    auditTrail: [],
    pointSignatures: [],
    ...overrides
  };
}

describe('memberProbeDetailsViewMapper', () => {
  it('should return empty rows when grouped probe is absent', () => {
    const rows = buildMemberProbeDetailPointRows(null, null);
    expect(rows).toEqual([]);
  });

  it('should mark all points as signed when probe is completed', () => {
    const rows = buildMemberProbeDetailPointRows(
      createGroupedProbe(),
      createProgress({
        status: ProbeProgressStatus.Completed,
        completedByUserKey: 'f0f4a2bd-1530-4ba3-b5b2-56f337f7f70d',
        completedByName: 'Впорядник',
        completedByRole: 'Mentor',
        completedAtUtc: '2026-04-16T10:00:00Z'
      })
    );

    expect(rows).toHaveSize(2);
    expect(rows.every(row => row.isSigned)).toBeTrue();
    expect(rows[0].signedByUserKey).toBe('f0f4a2bd-1530-4ba3-b5b2-56f337f7f70d');
    expect(rows[0].signedByName).toBe('Впорядник');
    expect(rows[0].signedAtUtc).toBe('2026-04-16T10:00:00Z');
  });

  it('should keep points unsigned for in-progress probe', () => {
    const rows = buildMemberProbeDetailPointRows(
      createGroupedProbe(),
      createProgress({ status: ProbeProgressStatus.InProgress })
    );

    expect(rows.every(row => !row.isSigned)).toBeTrue();
    expect(rows[0].signedByUserKey).toBeNull();
    expect(rows[0].signedByName).toBeNull();
    expect(rows[0].signedAtUtc).toBeNull();
  });

  it('should support string enum statuses from backend payload', () => {
    const rows = buildMemberProbeDetailPointRows(
      createGroupedProbe(),
      createProgress({
        status: 'Verified' as unknown as ProbeProgressStatus,
        verifiedByUserKey: '87c30f2f-6aaf-43e8-b9d3-74f3efd85fc7',
        verifiedByName: 'Станичний',
        verifiedByRole: 'Manager',
        verifiedAtUtc: '2026-04-16T12:30:00Z'
      })
    );

    expect(rows.every(row => row.isSigned)).toBeTrue();
    expect(rows[0].signedByUserKey).toBe('87c30f2f-6aaf-43e8-b9d3-74f3efd85fc7');
    expect(rows[0].signedByName).toBe('Станичний');
    expect(rows[0].signedAtUtc).toBe('2026-04-16T12:30:00Z');
  });

  it('should prefer point-level signatures over aggregate status when present', () => {
    const rows = buildMemberProbeDetailPointRows(
      createGroupedProbe(),
      createProgress({
        status: ProbeProgressStatus.Completed,
        completedByName: 'Агрегований підписант',
        completedAtUtc: '2026-04-16T08:00:00Z',
        pointSignatures: [
          {
            probePointProgressKey: 'point-progress-1',
            pointId: 'point-1',
            isSigned: true,
            signedAtUtc: '2026-04-16T12:00:00Z',
            signedByUserKey: '8b9a3380-885f-447a-9120-c9fa5f17f2f6',
            signedByName: 'Точковий підписант',
            signedByRole: 'Mentor'
          }
        ]
      })
    );

    expect(rows[0].isSigned).toBeTrue();
    expect(rows[0].signedByName).toBe('Точковий підписант');
    expect(rows[1].isSigned).toBeFalse();
    expect(rows[1].signedByName).toBeNull();
  });
});

