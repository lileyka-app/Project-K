import { MemberAbilities, NO_ABILITIES, awardRights, mayCancelWarning, mayGiveWarning } from './abilities';
import { MemberAwardDto } from './kurin.models';

function award(overrides: Partial<MemberAwardDto> = {}): MemberAwardDto {
  return {
    memberAwardKey: 'a1',
    memberKey: 'm1',
    kurinKey: 'k1',
    level: 'Second',
    dateAcquired: '2026-05-01',
    status: 'Submitted',
    submittedByUserKey: 'someone',
    ...overrides,
  };
}

const leader: MemberAbilities = {
  ...NO_ABILITIES,
  warnings: { give: [1], cancel: 'OwnIssued' },
  awards: { confirm: [1, 2], remove: false },
};

describe('abilities', () => {
  it('gives only the levels the kurin lists', () => {
    expect(mayGiveWarning(leader, 'Level1')).toBe(true);
    expect(mayGiveWarning(leader, 'Level2')).toBe(false);
  });

  it('lets the issuer lift a level they may still give, and anyone lift with the right to', () => {
    expect(mayCancelWarning(leader, 'Level1', 'ME', 'me')).toBe(true);
    expect(mayCancelWarning(leader, 'Level2', 'me', 'me')).toBe(false);
    expect(mayCancelWarning(leader, 'Level1', 'other', 'me')).toBe(false);
    expect(mayCancelWarning({ ...NO_ABILITIES, warnings: { give: [], cancel: 'Any' } }, 'Level3', 'other', 'me')).toBe(true);
  });

  it('confirms by level, changes a confirmed award only with removal rights', () => {
    expect(awardRights(award(), leader, true, 'me', false)).toEqual({ edit: true, confirm: true, remove: false });
    expect(awardRights(award({ level: 'Fourth' }), leader, true, 'me', false).confirm).toBe(false);
    expect(awardRights(award({ status: 'Confirmed' }), leader, true, 'me', false)).toEqual({ edit: false, confirm: false, remove: false });
    expect(awardRights(award({ status: 'Confirmed' }), { ...leader, awards: { confirm: [], remove: true } }, true, 'me', false).edit).toBe(true);
  });

  it('lets the submitter and the member withdraw what still waits', () => {
    expect(awardRights(award({ submittedByUserKey: 'ME' }), NO_ABILITIES, false, 'me', false).remove).toBe(true);
    expect(awardRights(award(), NO_ABILITIES, false, 'me', true).remove).toBe(true);
    expect(awardRights(award({ status: 'Confirmed' }), NO_ABILITIES, false, 'me', true).remove).toBe(false);
  });
});
