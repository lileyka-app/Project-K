import { utcDate } from '../kurin/kurin.labels';

/** Where an application stands (API WaitlistVerificationStatus, sent as its name). */
export type WaitlistStatus = 'Submitted' | 'NeedsManualVerification' | 'Verified' | 'Rejected' | 'ApprovedForInvitation';

/** An application to join (API WaitlistEntry; web onboarding.service WaitlistEntry). */
export interface WaitlistEntry {
  waitlistEntryKey: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
  stanytsia?: string | null;
  regionOrCountry?: string | null;
  isKurinLeaderCandidate: boolean;
  claimedKurinNameOrNumber?: string | null;
  /** A `WaitlistStatus` name, or the enum's number from an older API (web normalizeWaitlistStatus). */
  verificationStatus: number | string;
  requestedAtUtc: string;
  approvedAtUtc?: string | null;
  invitationSentAtUtc?: string | null;
}

const BY_NUMBER: WaitlistStatus[] = ['Submitted', 'NeedsManualVerification', 'Verified', 'Rejected', 'ApprovedForInvitation'];

export function waitlistStatus(entry: Pick<WaitlistEntry, 'verificationStatus'>): WaitlistStatus | null {
  const value = String(entry.verificationStatus);
  if (/^\d+$/.test(value)) return BY_NUMBER[Number(value)] ?? null;
  return (BY_NUMBER as string[]).includes(value) ? (value as WaitlistStatus) : null;
}

/**
 * The same words as the web's getWaitlistStatusLabel. Tones are Лілейка's
 * tags (info is the brand green, the web's «success»).
 */
export const WAITLIST_STATUS: Record<WaitlistStatus, { label: string; tone: 'info' | 'warn' | 'danger' | 'secondary' }> = {
  Submitted: { label: 'Подано', tone: 'secondary' },
  NeedsManualVerification: { label: 'Потрібна перевірка', tone: 'warn' },
  Verified: { label: 'Перевірено', tone: 'info' },
  Rejected: { label: 'Відхилено', tone: 'danger' },
  ApprovedForInvitation: { label: 'Схвалено', tone: 'info' },
};

/** Approve or reject: only a fresh application, as the web offers it (isWaitlistInitial). */
export function canDecide(entry: WaitlistEntry): boolean {
  return waitlistStatus(entry) === 'Submitted';
}

/** Send the invitation again: an approved one (isWaitlistApproved). */
export function canResend(entry: WaitlistEntry): boolean {
  return waitlistStatus(entry) === 'ApprovedForInvitation';
}

/** Still waits for an administrator's word (web isWaitlistAwaitingDecision): the menu's dot. */
export function awaitsDecision(entry: WaitlistEntry): boolean {
  const status = waitlistStatus(entry);
  return status === 'Submitted' || status === 'NeedsManualVerification';
}

export interface WaitlistSection {
  key: 'waiting' | 'approved' | 'done';
  title: string;
  entries: WaitlistEntry[];
}

/**
 * The web's one table split the way a phone reads it: what waits for a word first, then who has
 * an invitation, then the rest; newest first in each. Empty sections are left out.
 */
export function waitlistSections(entries: WaitlistEntry[]): WaitlistSection[] {
  const newest = (a: WaitlistEntry, b: WaitlistEntry) =>
    (utcDate(b.requestedAtUtc)?.getTime() ?? 0) - (utcDate(a.requestedAtUtc)?.getTime() ?? 0);
  const sections: WaitlistSection[] = [
    { key: 'waiting', title: 'Чекають рішення', entries: entries.filter(awaitsDecision) },
    { key: 'approved', title: 'Схвалені', entries: entries.filter(canResend) },
    { key: 'done', title: 'Розглянуті', entries: entries.filter((e) => !awaitsDecision(e) && !canResend(e)) },
  ];
  return sections.filter((s) => s.entries.length).map((s) => ({ ...s, entries: [...s.entries].sort(newest) }));
}

/** The line under a name: where they are from and what they claim. */
export function waitlistLine(entry: WaitlistEntry): string {
  const place = [entry.stanytsia, entry.regionOrCountry].map((part) => part?.trim()).filter(Boolean).join(', ');
  const claim = entry.isKurinLeaderCandidate
    ? `претендує на курінь ${entry.claimedKurinNameOrNumber?.trim() || '—'}`
    : 'звичайний учасник';
  return [place, claim].filter(Boolean).join(' · ');
}
