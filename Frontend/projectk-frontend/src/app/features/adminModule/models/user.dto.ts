import { KurinBranch, KurinNumbering } from '../../kurinModule/models/enums/kurin-branch.enum';

/**
 * The system-level roles, by name. The admin screen used to post the enum's ordinals
 * (Admin = 0, Member = 1), so reordering UserRole on the backend would have silently
 * assigned the wrong one.
 */
export type SystemUserRole = 'Admin' | 'Member';

export interface UserDto {
    userId: string;
    email: string;
    role: string;
    kurinKey: string | null;
    kurinNumber: number | null;
    /** The kurin's main улад; with the number it names the kurin, since each улад counts its own. */
    kurinBranch?: KurinBranch | null;
    kurinBranches?: KurinNumbering[] | null;
    kurinNamedAfter?: string | null;
    /** The kurin's край; it tells apart two kurins of one улад and number in different краї. */
    kurinKraiCode?: string | null;
    firstName: string;
    lastName: string;
    /** Refused at sign-in by an administrator; reversible, unlike deletion. */
    isSuspended?: boolean;
}