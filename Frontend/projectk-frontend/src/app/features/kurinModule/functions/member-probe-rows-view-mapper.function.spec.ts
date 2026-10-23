import { ProbeProgressStatus } from '../models/enums/probe-progress-status.enum';
import { ProbeProgressDto } from '../models/probes-and-badges/probe-progress.dto';
import { ProbeSummaryDto } from '../models/probes-and-badges/probe-summary.dto';
import { buildMemberProbeRows, normalizeProbeProgressStatus } from './member-probe-rows-view-mapper.function';

function createProbeSummary(overrides: Partial<ProbeSummaryDto>): ProbeSummaryDto {
  return {
    id: 'probe-1',
    title: 'Перша проба (Скобине крило)',
    pointsCount: 10,
    sectionsCount: 3,
    ...overrides
  };
}

function createProbeProgress(overrides: Partial<ProbeProgressDto>): ProbeProgressDto {
  return {
    probeProgressKey: 'progress-1',
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
    ...overrides
  };
}

function signedPoint(pointId: string, isSigned: boolean) {
  return {
    probePointProgressKey: `pp-${pointId}`,
    pointId,
    isSigned,
    signedAtUtc: isSigned ? '2026-01-01T00:00:00Z' : null,
    signedByUserKey: null,
    signedByName: null,
    signedByRole: null
  };
}

describe('memberProbeRowsViewMapper', () => {
  it('should count signed points against the probe total', () => {
    const rows = buildMemberProbeRows(
      [createProbeSummary({ id: 'probe-1', pointsCount: 10 })],
      [createProbeProgress({
        probeId: 'probe-1',
        status: ProbeProgressStatus.InProgress,
        pointSignatures: [
          signedPoint('a', true),
          signedPoint('b', true),
          signedPoint('c', false),
          signedPoint('d', true)
        ]
      })]
    );

    const first = rows.find(row => row.probeId === 'probe-1')!;
    expect(first.signedPointsCount).toBe(3);
    expect(first.completionPercent).toBe(30);
  });

  it('should report no percent when the probe has no points to count', () => {
    const rows = buildMemberProbeRows(
      [createProbeSummary({ id: 'probe-1', pointsCount: 0 })],
      [createProbeProgress({ probeId: 'probe-1', status: ProbeProgressStatus.InProgress })]
    );

    // Not zero: a bar at nothing would claim "none of it done", which is a different statement
    // from "there is nothing here to measure".
    expect(rows.find(row => row.probeId === 'probe-1')!.completionPercent).toBeNull();
  });

  it('should show a closed probe as full even when point-by-point data never arrived', () => {
    const rows = buildMemberProbeRows(
      [createProbeSummary({ id: 'probe-1', pointsCount: 10 })],
      [createProbeProgress({
        probeId: 'probe-1',
        status: ProbeProgressStatus.Completed,
        completedAtUtc: '2026-01-01T00:00:00Z'
      })]
    );

    const first = rows.find(row => row.probeId === 'probe-1')!;
    expect(first.completionPercent).toBe(100);
    expect(first.signedPointsCount).toBe(10);
  });

  it('should return 3 rows in canonical order and keep third probe disabled', () => {
    const probes = [
      createProbeSummary({ id: 'probe-2', title: 'Друга проба', pointsCount: 12, sectionsCount: 2 }),
      createProbeSummary({ id: 'probe-1', title: 'Перша проба', pointsCount: 11, sectionsCount: 3 }),
      createProbeSummary({ id: 'probe-3', title: 'Третя проба', pointsCount: 15, sectionsCount: 4 })
    ];

    const progresses = [
      createProbeProgress({ probeId: 'probe-1', status: ProbeProgressStatus.Completed, completedAtUtc: '2026-04-10T00:00:00Z' }),
      createProbeProgress({ probeId: 'probe-2', status: ProbeProgressStatus.InProgress })
    ];

    const rows = buildMemberProbeRows(probes, progresses);

    expect(rows.map(row => row.probeId)).toEqual(['probe-1', 'probe-2', 'probe-3']);
    expect(rows[0].isCompleted).toBe(true);
    expect(rows[0].completedAtUtc).toBe('2026-04-10T00:00:00Z');
    expect(rows[0].canOpenDetails).toBe(true);

    expect(rows[2].isDisabled).toBe(true);
    expect(rows[2].canOpenDetails).toBe(false);
  });

  it('should use fallback labels and not-started status when probe data is missing', () => {
    const rows = buildMemberProbeRows([], []);

    expect(rows[0].title).toBe('Перша проба');
    expect(rows[0].status).toBe(ProbeProgressStatus.NotStarted);
    expect(rows[0].canOpenDetails).toBe(false);
  });

  it('normalizeProbeProgressStatus should support string enum values from backend JSON', () => {
    expect(normalizeProbeProgressStatus('InProgress')).toBe(ProbeProgressStatus.InProgress);
    expect(normalizeProbeProgressStatus('Verified')).toBe(ProbeProgressStatus.Verified);
  });

  it('should mark probe completed when backend status is provided as string', () => {
    const rows = buildMemberProbeRows(
      [createProbeSummary({ id: 'probe-1', title: 'Перша проба' })],
      [createProbeProgress({ probeId: 'probe-1', status: 'Completed' as unknown as ProbeProgressStatus })]
    );

    expect(rows[0].isCompleted).toBe(true);
    expect(rows[0].status).toBe(ProbeProgressStatus.Completed);
  });

  it('should keep second probe locked until first probe is completed', () => {
    const probes = [
      createProbeSummary({ id: 'probe-1', title: 'Перша проба' }),
      createProbeSummary({ id: 'probe-2', title: 'Друга проба' })
    ];

    const rows = buildMemberProbeRows(probes, [
      createProbeProgress({ probeId: 'probe-1', status: ProbeProgressStatus.InProgress })
    ]);

    expect(rows[1].isDisabled).toBe(true);
    expect(rows[1].canOpenDetails).toBe(false);
  });

  it('should unlock second probe when first probe is completed', () => {
    const probes = [
      createProbeSummary({ id: 'probe-1', title: 'Перша проба' }),
      createProbeSummary({ id: 'probe-2', title: 'Друга проба' })
    ];

    const rows = buildMemberProbeRows(probes, [
      createProbeProgress({ probeId: 'probe-1', status: ProbeProgressStatus.Completed, completedAtUtc: '2026-04-16T08:00:00Z' })
    ]);

    expect(rows[1].isDisabled).toBe(false);
    expect(rows[1].canOpenDetails).toBe(true);
  });
});
