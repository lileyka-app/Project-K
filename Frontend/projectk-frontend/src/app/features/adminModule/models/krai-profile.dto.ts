import { DuesUpstream } from '../../duesModule/models/dues.enums';
import { CardExtraFieldDto, CardFieldMode } from '../../kurinModule/models/card-fields.dto';

/** A край's profile as it is stored. Every value is optional: what is left out comes from code. */
export interface KraiProfileDocument {
  catalog?: {
    /** Which краї's вмілості the kurins see; unset means the край's own. */
    badgeSources?: string[];
    hiddenBadges?: string[];
  };
  locale?: {
    currency?: string;
    timeZone?: string;
    numberLocale?: string;
  };
  cardFields?: KraiCardFieldsDocument;
  dues?: {
    /** Where the kurins hand the вкладка up; unset means through the станиця. */
    upstream?: DuesUpstream;
  };
}

/** The card: modes of its own fields that differ from the default, and the край's own fields. */
export interface KraiCardFieldsDocument {
  fields?: Record<string, CardFieldMode>;
  extra?: CardExtraFieldDto[];
}

export interface KraiProfileEditorDto {
  kraiCode: string;
  kraiName: string;
  /** The revision in force; 0 when none was ever saved. */
  revision: number;
  document: KraiProfileDocument;
}

export interface KraiProfileRevisionDto {
  revision: number;
  createdAtUtc: string;
  createdByName: string | null;
  note: string | null;
}
