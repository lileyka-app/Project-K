import { PlastLevelHistoryDto } from "./plast-level-history.dto";
import { LeadershipHistoryDto } from "./requests/leadership/leadership.dto";
import { MemberWarningDto } from "./member-warning.dto";
import { MemberAwardDto } from "./member-award.dto";
import { MemberProfileVerificationStatus } from "./enums/member-profile-verification-status.enum";
import { AccountStatus } from "./enums/account-status.enum";

/** Група даних людини, яку курінь показує або приховує цілком. Назви — як у `hiddenData` з сервера. */
export type MemberDataGroup = 'contacts' | 'birthDate' | 'birthday' | 'address' | 'parents' | 'extras';

export interface MemberDto {
    memberKey: string;
    /** Код, який людина віддає проводу іншого куреня, щоб та прийняла її. Свій — бачить лише вона. */
    publicId?: string;
    groupKey: string;
    /** Назва гуртка. Її знає лише читання списку — картка бере гурток із членства. */
    groupName?: string | null;
    kurinKey: string;
    userKey?: string | null;
    /** Чи прийнято запрошення. Приходить лише з картки однієї людини і лише тому, хто бачить її приватні дані. */
    accountStatus?: AccountStatus | null;
    /** Чи пішов лист-запрошення, який мав піти після цього збереження. Null — лист не мав іти. */
    invitationSent?: boolean | null;
    userRole?: string | null;
    /**
     * Чи людина в кадрі виховників цього куреня. Заповнює лише читання списку — картка про одну
     * людину не знає, про яке саме членство йдеться.
     */
    isStaff?: boolean;
    /**
     * Гуртки, за якими людина закріплена як виховник. Порожньо в юнака. Картка отримує їх для
     * куреня, у якому людину дивляться.
     */
    mentoredGroupNames?: string[];
    /** Ті самі гуртки з ключами, щоб картка могла їх відкрити. Приходить лише з картки однієї людини. */
    mentoredGroups?: { groupKey: string; name: string }[];
    firstName: string;
    middleName: string;
    lastName: string;
    email: string;
    phoneNumber: string;
    dateOfBirth: Date | null;
    /** День і місяць народження як `MM-DD`, без року. Приходить, коли курінь показує день народження. */
    birthday?: string | null;
    /**
     * Групи даних, які сервер приховав від того, хто дивиться: `contacts`, `birthDate`, `birthday`,
     * `address`, `parents`, `extras`. Поля такої групи картка й форма не показують узагалі.
     */
    hiddenData?: MemberDataGroup[];
    address?: string | null;
    school?: string | null;
    motherFirstName?: string | null;
    motherLastName?: string | null;
    motherPhone?: string | null;
    fatherFirstName?: string | null;
    fatherLastName?: string | null;
    fatherPhone?: string | null;
    /** Values of the край's own card fields by key; empty when the viewer may not see them. */
    cardExtras?: Record<string, string>;
    latestPlastLevel?: string | null;
    latestPlastLevelDisplay?: string | null;
    plastLevelHistories: PlastLevelHistoryDto[];
    leadershipHistories: LeadershipHistoryDto[];
    warnings?: MemberWarningDto[];
    awards?: MemberAwardDto[];
    profilePhotoUrl: string | null;
    profileVerificationStatus?: MemberProfileVerificationStatus;
    profileVerifiedAtUtc?: string | Date | null;
    profileVerifiedByUserKey?: string | null;
    profileVerificationNote?: string | null;
}
