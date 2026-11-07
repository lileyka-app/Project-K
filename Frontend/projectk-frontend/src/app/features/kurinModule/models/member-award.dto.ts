import { BadgeProgressStatus } from "./enums/badge-progress-status.enum";
import { MemberAwardLevel } from "./enums/member-award-level.enum";

export interface MemberAwardDto {
    memberAwardKey: string;
    memberKey: string;
    kurinKey: string;
    /** An УПЮ відзначення's level; null for a senior one. */
    level?: MemberAwardLevel | null;
    /** A senior відзначення's catalogue id; null for an УПЮ one. */
    catalogId?: string | null;
    dateAcquired: string;
    /** What it was given for. */
    note?: string;
    /** The person pinned it to the front of their card. */
    isPinned?: boolean;
    status: BadgeProgressStatus;
    imageUrl?: string;
    submittedAtUtc?: string;
    submittedByUserKey?: string;
    reviewedAtUtc?: string;
    reviewedByUserKey?: string;
}

/** An УПЮ відзначення: it goes by level. */
export type YouthAwardDto = MemberAwardDto & { level: MemberAwardLevel };

export function isYouthAward(award: MemberAwardDto): award is YouthAwardDto {
    return award.level != null;
}
