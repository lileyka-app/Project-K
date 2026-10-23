/** Whose перестороги the viewer may cancel: none, the ones they gave themselves, or anyone's. */
export type WarningCancelAbility = 'None' | 'OwnIssued' | 'Any';

/**
 * What the viewer may do to one member under the kurin's policy (`GET member/{key}/abilities`), so
 * the card shows the buttons the server would accept and no others. Levels are listed, because a
 * policy may give the first пересторога and keep the third.
 */
export interface MemberAbilitiesDto {
  warnings: {
    /** The levels (1–3) the viewer may give. */
    give: number[];
    cancel: WarningCancelAbility;
  };
  awards: {
    /** The levels (1–4) the viewer may confirm. */
    confirm: number[];
    /** Remove a confirmed award or someone else's submission, and edit a confirmed one. */
    remove: boolean;
  };
  probes: {
    /** Sign points, close and verify a проба. */
    sign: boolean;
    /** Remove a signature, take a проба back from verified. */
    unsign: boolean;
  };
  badges: {
    /** Confirm or return a вмілість. */
    review: boolean;
  };
  /** Read this member's entries in the «Журнал дій». */
  journal: boolean;
}

/** What a viewer may do before the answer arrives, or when it fails: nothing. */
export const NO_ABILITIES: MemberAbilitiesDto = {
  warnings: { give: [], cancel: 'None' },
  awards: { confirm: [], remove: false },
  probes: { sign: false, unsign: false },
  badges: { review: false },
  journal: false
};
