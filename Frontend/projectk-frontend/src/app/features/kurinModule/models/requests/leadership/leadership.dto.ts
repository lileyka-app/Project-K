import { MemberLookupDto } from "../member/member-lookup.dto";

export interface LeadershipDto {
    leadershipKey: string | null;
    type?: 'kurin' | 'group' | 'kv';
    entityKey?: string;
    startDate: string;
    endDate: string | null;
    leadershipHistories: LeadershipHistoryDto[];
}

export interface LeadershipHistoryDto {
    // Null for a seat not saved yet: the API binds these as Guid?, and '' fails binding with a 400.
    leadershipHistoryKey: string | null;
    leadershipKey: string | null;
    role: string;
    leadershipType?: 'Kurin' | 'Group' | 'KV' | 'kurin' | 'group' | 'kv' | null;
    groupName?: string | null;
    /** The kurin the office is in; a card carries offices from every kurin the person stands in. */
    kurinKey?: string | null;
    startDate: string;
    endDate: string | null;
    member: MemberLookupDto;
}
