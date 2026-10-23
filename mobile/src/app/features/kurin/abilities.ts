import { awardWeight, progressStatus } from './kurin.labels';
import { MemberAwardDto, WarningLevel } from './kurin.models';

/**
 * `GET member/{memberKey}/abilities` (web member-abilities.dto): what the kurin's policy lets the
 * viewer do to one member, level by level, so the card offers the buttons the server would accept.
 */
export interface MemberAbilities {
  warnings: { give: number[]; cancel: 'None' | 'OwnIssued' | 'Any' };
  awards: { confirm: number[]; remove: boolean };
  probes: { sign: boolean; unsign: boolean };
  badges: { review: boolean };
  journal: boolean;
}

/** Before the answer arrives, or when it fails: nothing. */
export const NO_ABILITIES: MemberAbilities = {
  warnings: { give: [], cancel: 'None' },
  awards: { confirm: [], remove: false },
  probes: { sign: false, unsign: false },
  badges: { review: false },
  journal: false,
};

const WARNING_NUMBER: Record<WarningLevel, number> = { Level1: 1, Level2: 2, Level3: 3 };

/** Whether the kurin lets the viewer give a пересторога of this level. */
export function mayGiveWarning(abilities: MemberAbilities, level: WarningLevel): boolean {
  return abilities.warnings.give.includes(WARNING_NUMBER[level]);
}

/** Whether the viewer may lift this one: anyone's with the right to, or their own while they may still give it. */
export function mayCancelWarning(
  abilities: MemberAbilities,
  level: WarningLevel,
  issuedByUserKey: string | null | undefined,
  viewerUserKey: string | null | undefined,
): boolean {
  if (abilities.warnings.cancel === 'Any') return true;
  return (
    abilities.warnings.cancel === 'OwnIssued' &&
    mayGiveWarning(abilities, level) &&
    !!viewerUserKey &&
    (issuedByUserKey ?? '').toLowerCase() === viewerUserKey.toLowerCase()
  );
}

/** What the viewer may do with one award (web member-awards-tile): the server's rules, client-side. */
export interface AwardRights {
  /** Change the date and note; a confirmed one only with the right to remove it. */
  edit: boolean;
  confirm: boolean;
  remove: boolean;
}

export function awardRights(
  award: MemberAwardDto,
  abilities: MemberAbilities,
  canEditCard: boolean,
  viewerUserKey: string | null | undefined,
  ownCard: boolean,
): AwardRights {
  const status = progressStatus(award.status);
  const confirmed = status === 'Confirmed';
  const viewer = viewerUserKey?.toLowerCase();
  const withdraws = !confirmed && (ownCard || (!!viewer && award.submittedByUserKey?.toLowerCase() === viewer));
  return {
    edit: canEditCard && (!confirmed || abilities.awards.remove),
    confirm: status === 'Submitted' && abilities.awards.confirm.includes(awardWeight(award.level)),
    remove: abilities.awards.remove || withdraws,
  };
}
