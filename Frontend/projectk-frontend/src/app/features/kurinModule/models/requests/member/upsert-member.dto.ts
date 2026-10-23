import { PlastLevelHistoryDto } from "../../plast-level-history.dto";

export interface UpsertMemberDto {
    groupKey?: string;
    kurinKey?: string;
    createUserAccount?: boolean;
    firstName: string;
    middleName: string;
    lastName: string;
    /** Left out by someone the kurin hides the contacts from: the server keeps what it has. */
    email?: string;
    phoneNumber?: string;
    /** Left out by someone the kurin hides the date of birth from. */
    dateOfBirth?: string;
    address?: string | null;
    school?: string | null;
    motherFirstName?: string | null;
    motherLastName?: string | null;
    motherPhone?: string | null;
    fatherFirstName?: string | null;
    fatherLastName?: string | null;
    fatherPhone?: string | null;
    /** Values of the край's own card fields by key; an empty one clears it. */
    cardExtras?: Record<string, string>;
    removeProfilePhoto?: boolean;
    plastLevelHistories?: PlastLevelHistoryDto[];
}
