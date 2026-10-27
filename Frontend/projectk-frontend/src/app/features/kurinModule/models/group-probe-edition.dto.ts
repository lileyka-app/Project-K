/** Проба кожного гуртка куреня (`GET api/kurin/{key}/probe-editions`). */
export interface KurinProbeEditionsDto {
  /** Редакції, які каталог пропонує краю куреня; звичайна — завжди перша. */
  editions: ProbeEditionDto[];
  groups: GroupProbeEditionDto[];
}

export interface ProbeEditionDto {
  /** Id редакції в каталозі (напр. `upyu-2026`); null — звичайна проба. */
  id: string | null;
  /** Назва редакції з каталогу; null — звичайна проба, яку каталог не називає. */
  title: string | null;
  isTrial: boolean;
  probeTitles: string[];
}

export interface GroupProbeEditionDto {
  groupKey: string;
  name: string;
  /** Проба, якою гурток іде насправді: обрана, поки каталог її пропонує, інакше звичайна. */
  edition: string | null;
  /** Чи може той, хто дивиться, перемкнути: право куреня «Обирати пробу гуртку». */
  canChange: boolean;
  members: GroupMemberStandingDto[];
}

/** Юнак гуртка і те, що визначає його пробу. Хто нічого не почав, просто йде за гуртком. */
export interface GroupMemberStandingDto {
  memberKey: string;
  fullName: string;
  hasStarted: boolean;
  /** Редакція проби `probeTitle`; має сенс лише коли `hasStarted`. */
  edition: string | null;
  probeTitle: string | null;
  /**
   * true — закрив цю пробу, а вона далі за першу: продовжує свою пробу, хоч що обере гурток.
   * false — проба лише розпочата: людина переходить разом із гуртком.
   */
  isHeld: boolean;
}

/** Проби без редакції: каталог для них запису не має, тож назву дає Лілейка. */
export const REGULAR_PROBE_LABEL = 'Звичайна проба';

/** Як редакцію називати людям: назва з каталогу, а якщо її там немає — id редакції. */
export function probeEditionLabel(editions: readonly ProbeEditionDto[], edition: string | null): string {
  if (!edition) {
    return REGULAR_PROBE_LABEL;
  }
  return editions.find(offer => offer.id === edition)?.title ?? edition;
}
