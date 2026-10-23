/** The part of the kurin a journal entry is about; the journal filters by it. */
export type JournalArea =
  | 'Probe'
  | 'Badge'
  | 'Award'
  | 'Warning'
  | 'Agenda'
  | 'Member'
  | 'Office'
  | 'Membership'
  | 'Settings';

/**
 * One field of an entry: what it was and what it became. A person's private data comes back
 * `redacted`, without either value — the journal says it changed, never to what.
 */
export interface JournalChangeDto {
  field: string;
  from?: string | null;
  to?: string | null;
  redacted?: boolean;
}

/** One entry of the «Журнал дій»: a snapshot from the time, complete without a second lookup. */
export interface KurinJournalEntryDto {
  key: string;
  occurredAtUtc: string;
  area: JournalArea;
  /** The action as stored, e.g. `Probe.PointSigned`. */
  action: string;
  actorUserKey: string | null;
  actorName: string | null;
  /** The system role that authorised it, e.g. `KV.Vykhovnyk`, `Admin`, `Member`. */
  actorRole: string;
  subjectMemberKey: string | null;
  subjectMemberName: string | null;
  subjectKey: string;
  subjectLabel: string;
  changes: JournalChangeDto[];
  note: string | null;
}

/** One page of the journal, newest first. */
export interface KurinJournalPageDto {
  items: KurinJournalEntryDto[];
  hasMore: boolean;
}

/** What to read; every filter that is set narrows the page. `toUtc` is exclusive. */
export interface KurinJournalQuery {
  area?: JournalArea | null;
  memberKey?: string | null;
  actorUserKey?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  skip?: number;
  take?: number;
}
