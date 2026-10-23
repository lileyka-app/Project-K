import { MemberProfileVerificationStatus } from "../../enums/member-profile-verification-status.enum";

export interface MemberLookupDto {
    memberKey: string;
    userKey?: string | null;
    userRole?: string | null;
    firstName: string;
    middleName?: string | null;
    lastName: string;
    fullNameSort?: string;
    roleSortWeight?: number;
    profilePhotoUrl?: string | null;
    latestPlastLevel?: string | null;
    latestPlastLevelDisplay?: string | null;
    phoneNumber?: string | null;
    dateOfBirth?: string | Date | null;
    /** День і місяць народження як `MM-DD`, коли рік приховано. */
    birthday?: string | null;
    profileVerificationStatus?: MemberProfileVerificationStatus;
    profileVerifiedAtUtc?: string | Date | null;
    profileVerifiedByUserKey?: string | null;
    profileVerificationNote?: string | null;
    leadershipHistories?: import('../leadership/leadership.dto').LeadershipHistoryDto[];
    /**
     * Гуртки, за якими людина закріплена як впорядник. Закріплення — не діловодство: у
     * `leadershipHistories` його немає, а впорядником людину воно робить так само.
     */
    mentoredGroupNames?: string[];
    warnings?: import('../../member-warning.dto').MemberWarningDto[];
}
