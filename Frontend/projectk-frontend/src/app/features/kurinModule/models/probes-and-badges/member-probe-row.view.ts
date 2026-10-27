import { ProbeProgressStatus } from '../enums/probe-progress-status.enum';

export interface MemberProbeRowView {
  probeId: string;
  label: string;
  title: string;
  status: ProbeProgressStatus;
  completedAtUtc: string | null;
  isCompleted: boolean;
  isDisabled: boolean;

  /** Назва проби, яку треба закрити, щоб відкрилась ця; null, коли ця не чекає на попередню. */
  lockedBehindTitle: string | null;

  /** Проба без точок у каталозі (зараз це третя): відстежувати її поточково нема чого. */
  hasNoPoints: boolean;
  canOpenDetails: boolean;
  pointsCount: number | null;
  sectionsCount: number | null;

  /** Скільки точок проби вже підписано. */
  signedPointsCount: number;

  /**
   * Скільки пройдено, у відсотках, або null коли рахувати нема з чого — проба без точок або без
   * даних про них. Смуга без знаменника показувала б нуль, а це інше твердження, ніж «невідомо».
   */
  completionPercent: number | null;
}