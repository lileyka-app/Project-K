import { ProbeProgressStatus } from '../models/enums/probe-progress-status.enum';
import { MemberProbeRowView } from '../models/probes-and-badges/member-probe-row.view';
import { ProbeProgressDto } from '../models/probes-and-badges/probe-progress.dto';
import { ProbeSummaryDto } from '../models/probes-and-badges/probe-summary.dto';

export type ProbeProgressStatusLike = ProbeProgressStatus | string | number;

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

/** Чи людина вже щось почала в цій пробі: рядок поступу існує або вона не «не почата». */
function hasProgress(progress: ProbeProgressDto | undefined): boolean {
  return !!progress
    && (!!progress.probeProgressKey || normalizeProbeProgressStatus(progress.status) !== ProbeProgressStatus.NotStarted);
}

/** Проби, в яких людина вже щось почала. */
export function startedProbeIds(progresses: ProbeProgressDto[]): Set<string> {
  return new Set(progresses.filter(hasProgress).map(progress => progress.probeId));
}

/**
 * Запасна черга, коли сервер не віддав `api/member/{key}/probes/line` (старий API, записане демо):
 * редакція без назви, а на місці, де людина почала пробу іншої редакції, — саме вона. Справжнє
 * правило, з програмою гуртка й редакцією, що тримає закрита проба, — `ProbeLine` на сервері.
 */
export function probeLine(probes: ProbeSummaryDto[], started: ReadonlySet<string>): ProbeSummaryDto[] {
  const places = new Map<number, ProbeSummaryDto[]>();
  probes.forEach((probe, index) => {
    if (probe.edition && !started.has(probe.id)) {
      return;
    }
    // Відповідь без `order` (старий API, записане демо) уже йде в порядку каталогу.
    const order = probe.order ?? index;
    places.set(order, [...(places.get(order) ?? []), probe]);
  });

  return [...places.entries()]
    .sort(([a], [b]) => a - b)
    .map(([, place]) => place.find(probe => started.has(probe.id)) ?? place.find(probe => !probe.edition)!);
}

/** Рядки картки з уже розставленої черги проб (`probeLine` або відповідь сервера). */
export function buildMemberProbeRows(
  line: ProbeSummaryDto[],
  progresses: ProbeProgressDto[]
): MemberProbeRowView[] {
  const progressByProbeId = new Map<string, ProbeProgressDto>(progresses.map(progress => [progress.probeId, progress]));
  const started = startedProbeIds(progresses);

  const rows = line.map((probe): MemberProbeRowView => {
    const progress = progressByProbeId.get(probe.id);
    const status = normalizeProbeProgressStatus(progress?.status ?? ProbeProgressStatus.NotStarted);
    const totalPoints = probe.pointsCount;
    const signedPoints = isCompletedStatus(status) && countSignedPoints(progress) === 0
      ? totalPoints
      : countSignedPoints(progress);
    const hasNoPoints = totalPoints === 0;

    return {
      probeId: probe.id,
      label: probe.title,
      title: probe.title,
      status,
      completedAtUtc: resolveCompletedAtUtc(progress),
      isCompleted: isCompletedStatus(status),
      isDisabled: hasNoPoints,
      lockedBehindTitle: null,
      hasNoPoints,
      canOpenDetails: !hasNoPoints,
      pointsCount: probe.pointsCount,
      sectionsCount: probe.sectionsCount,
      signedPointsCount: signedPoints,
      completionPercent: completionPercentOf(signedPoints, totalPoints, status)
    };
  });

  // Наступна проба відкривається, коли закрито попередню, — або вже почата, бо почате не ховаємо.
  return rows.map((row, index) => {
    const previous = rows[index - 1];
    if (!previous || row.isDisabled || previous.isCompleted || started.has(row.probeId) || row.completedAtUtc) {
      return row;
    }

    return { ...row, isDisabled: true, lockedBehindTitle: previous.title, canOpenDetails: false };
  });
}
