import { MemberDto } from '../models/member.dto';
import { PlastLevel } from '../models/enums/plast-level.enum';
import { PlastLevelHistoryDto } from '../models/plast-level-history.dto';
import { parseDateOnlyString } from './to-date-only-string.function';

export const PLAST_LEVEL_DISPLAY_MAP: Record<PlastLevel, string> = {
  // Joining is a date, not a title: someone who has only just come has nothing to put before their
  // name yet. Empty here means "print no prefix", which localizeLatestPlastLevel turns into null.
  [PlastLevel.Entry]: '',
  [PlastLevel.Prykhylnyk]: 'пл. прих.',
  [PlastLevel.Uchasnyk]: 'пл. уч.',
  [PlastLevel.Rozviduvach]: 'пл. розв.',
  [PlastLevel.Skob]: 'пл. скоб',
  [PlastLevel.HetmanskiySkob]: 'пл. гетьм. скоб',
  [PlastLevel.Starshoplastun]: 'ст. пл.',
  [PlastLevel.Senior]: 'пл. сен.',
  [PlastLevel.SeniorPratsi]: 'пл. сен. пр.',
  [PlastLevel.SeniorDovirja]: 'пл. сен. дов.',
  [PlastLevel.SeniorKerivnytstva]: 'пл. сен. кер.'
};

const STARSHOPLASTUN_SUFFIX_BY_LEVEL: Record<PlastLevel.Skob | PlastLevel.HetmanskiySkob, string> = {
  [PlastLevel.Skob]: 'скоб',
  [PlastLevel.HetmanskiySkob]: 'гетьм. скоб'
};

function toPlastLevel(value: string | null | undefined): PlastLevel | null {
  if (!value) {
    return null;
  }

  return (Object.values(PlastLevel) as string[]).includes(value) ? (value as PlastLevel) : null;
}

function resolveStarshoplastunSuffix(histories: PlastLevelHistoryDto[] | null | undefined): string | null {
  if (!histories?.length) {
    return null;
  }

  const hasHetmanskiySkob = histories.some(history => history.plastLevel === PlastLevel.HetmanskiySkob);
  if (hasHetmanskiySkob) {
    return STARSHOPLASTUN_SUFFIX_BY_LEVEL[PlastLevel.HetmanskiySkob];
  }

  const hasSkob = histories.some(history => history.plastLevel === PlastLevel.Skob);
  if (hasSkob) {
    return STARSHOPLASTUN_SUFFIX_BY_LEVEL[PlastLevel.Skob];
  }

  return null;
}

export function localizeLatestPlastLevel(member: Pick<MemberDto, 'latestPlastLevel' | 'plastLevelHistories'>): string | null {
  const latestPlastLevel = toPlastLevel(member.latestPlastLevel);
  if (!latestPlastLevel) {
    return member.latestPlastLevel ?? null;
  }

  if (latestPlastLevel === PlastLevel.Starshoplastun) {
    const suffix = resolveStarshoplastunSuffix(member.plastLevelHistories);
    if (suffix) {
      return `${PLAST_LEVEL_DISPLAY_MAP[latestPlastLevel]} ${suffix}`;
    }
  }

  return PLAST_LEVEL_DISPLAY_MAP[latestPlastLevel] || null;
}

export function mapMemberForView(member: MemberDto): MemberDto {
  const latestPlastLevelDisplay = localizeLatestPlastLevel(member);
  const normalizedMember = {
    ...member,
    dateOfBirth: parseDateOnlyString(member.dateOfBirth),
    plastLevelHistories: (member.plastLevelHistories ?? []).map(history => ({
      ...history,
      dateAchieved: parseDateOnlyString(history.dateAchieved)
    }))
  };

  if (!latestPlastLevelDisplay) {
    return normalizedMember;
  }

  return {
    ...normalizedMember,
    latestPlastLevelDisplay
  };
}
