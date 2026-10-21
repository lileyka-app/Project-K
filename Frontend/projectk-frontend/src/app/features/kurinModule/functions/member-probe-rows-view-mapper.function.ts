import { ProbeProgressStatus } from '../models/enums/probe-progress-status.enum';
import { MemberProbeRowView } from '../models/probes-and-badges/member-probe-row.view';
import { ProbeProgressDto } from '../models/probes-and-badges/probe-progress.dto';
import { ProbeSummaryDto } from '../models/probes-and-badges/probe-summary.dto';

export type ProbeProgressStatusLike = ProbeProgressStatus | string | number;

interface ProbeRowTemplate {
  probeId: string;
  label: string;
  isPolicyDisabled: boolean;
}

const PROBE_ROW_TEMPLATES: ProbeRowTemplate[] = [
  { probeId: 'probe-1', label: 'Перша проба', isPolicyDisabled: false },
  { probeId: 'probe-2', label: 'Друга проба', isPolicyDisabled: false },
  { probeId: 'probe-3', label: 'Третя проба', isPolicyDisabled: true }
];

function resolveCompletedAtUtc(progress: ProbeProgressDto | undefined): string | null {
  if (!progress) {
    return null;
  }

  return progress.completedAtUtc ?? progress.verifiedAtUtc;
}

/** Скільки точок цієї проби підписано. Непідписані приходять у списку теж, тож рахуємо прапорець. */
function countSignedPoints(progress: ProbeProgressDto | undefined): number {
  return (progress?.pointSignatures ?? []).filter(point => point.isSigned).length;
}

function isCompletedStatus(status: ProbeProgressStatus): boolean {
  return status === ProbeProgressStatus.Completed || status === ProbeProgressStatus.Verified;
}

/**
 * A закрита проба reads as full even when the point-by-point data never arrived: the probe is
 * done, and showing a half-filled bar under the word "завершено" would contradict it.
 */
function completionPercentOf(signedPoints: number, totalPoints: number, status: ProbeProgressStatus): number | null {
  if (totalPoints) {
    return Math.round((signedPoints / totalPoints) * 100);
  }
  return isCompletedStatus(status) ? 100 : null;
}

export function normalizeProbeProgressStatus(status: ProbeProgressStatusLike): ProbeProgressStatus {
  if (typeof status === 'number' && ProbeProgressStatus[status] !== undefined) {
    return status as ProbeProgressStatus;
  }

  if (typeof status === 'string') {
    const enumValue = ProbeProgressStatus[status as keyof typeof ProbeProgressStatus];
    if (typeof enumValue === 'number') {
      return enumValue;
    }
  }

  return ProbeProgressStatus.NotStarted;
}

export function buildMemberProbeRows(
  probes: ProbeSummaryDto[],
  progresses: ProbeProgressDto[]
): MemberProbeRowView[] {
  const probeById = new Map<string, ProbeSummaryDto>(probes.map(probe => [probe.id, probe]));
  const progressByProbeId = new Map<string, ProbeProgressDto>(progresses.map(progress => [progress.probeId, progress]));

  const rows = PROBE_ROW_TEMPLATES.map(template => {
    const probe = probeById.get(template.probeId);
    const progress = progressByProbeId.get(template.probeId);
    const status = normalizeProbeProgressStatus(progress?.status ?? ProbeProgressStatus.NotStarted);
    const totalPoints = probe?.pointsCount ?? 0;
    const signedPoints = isCompletedStatus(status) && countSignedPoints(progress) === 0
      ? totalPoints
      : countSignedPoints(progress);

    return {
      probeId: probe?.id ?? template.probeId,
      label: template.label,
      title: probe?.title ?? template.label,
      status,
      completedAtUtc: resolveCompletedAtUtc(progress),
      isCompleted: isCompletedStatus(status),
      isDisabled: template.isPolicyDisabled,
      canOpenDetails: !template.isPolicyDisabled && !!probe,
      pointsCount: probe?.pointsCount ?? null,
      sectionsCount: probe?.sectionsCount ?? null,
      signedPointsCount: signedPoints,
      completionPercent: completionPercentOf(signedPoints, totalPoints, status)
    };
  });

  const firstProbe = rows[0];
  const isFirstProbeClosed = !!firstProbe && firstProbe.isCompleted;

  return rows.map(row => {
    if (row.probeId !== 'probe-2') {
      return row;
    }

    const hasOwnProgress = row.status !== ProbeProgressStatus.NotStarted || !!row.completedAtUtc;
    const isUnlocked = isFirstProbeClosed || hasOwnProgress;
    const isDisabled = !isUnlocked;

    return {
      ...row,
      isDisabled,
      canOpenDetails: !isDisabled && row.canOpenDetails
    };
  });
}