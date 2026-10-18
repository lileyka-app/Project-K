/**
 * The shapes the leader screens read, kept in step with the web (notificationsModule/models,
 * kurinModule/models/probes-and-badges, duesModule/models) and the API's DTOs they mirror.
 */

// ── Notifications (api/notifications; AppNotificationDto) ─────────────────────────────────────

export type AppNotificationType =
  | 'MemberProfileVerified'
  | 'MemberProfileChangedAfterVerification'
  | 'MemberSkillSubmittedForReview'
  | 'MemberAwardSubmitted'
  | 'MemberAwardReviewed'
  | 'MemberWarningAssigned'
  | 'LeadershipChanged'
  | 'MemberSkillReviewed'
  | 'AgendaItemAssigned'
  | 'AgendaItemUpdated'
  | 'AgendaItemStatusChanged'
  | 'AgendaItemDeleted'
  | 'WaitlistEntrySubmitted';

export type AppNotificationSeverity = 'Info' | 'Success' | 'Warn' | 'Error';

export interface AppNotification {
  notificationKey: string;
  type: AppNotificationType;
  severity: AppNotificationSeverity;
  title: string;
  body: string;
  entityType: string | null;
  entityKey: string | null;
  /** The web's screen path, e.g. `/member/{key}`; see notification-route.ts for the phone's. */
  route: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
  isRead: boolean;
}

// ── Skills review (api/kurin/{k}/badges/review, api/catalog/badges) ───────────────────────────

export interface BadgeProgressDto {
  badgeProgressKey: string;
  memberKey: string;
  kurinKey: string;
  badgeId: string;
  status: string;
  submittedAtUtc: string | null;
  memberFirstName?: string | null;
  memberLastName?: string | null;
}

export interface BadgeCatalogItemDto {
  id: string;
  title: string;
  specialization: string;
  level: number | null;
  seekerRequirements: string;
}

export interface ReviewBadgeProgressRequest {
  isApproved: boolean;
  note: string | null;
}

// ── Group dues (api/group/{groupKey}/dues; GroupDuesDto) ──────────────────────────────────────

export type DuesEntryKind =
  | 'Contribution'
  | 'Refund'
  | 'TransferToKurin'
  | 'TransferToStanytsia'
  | 'Expense'
  | 'OtherIncome'
  | 'Exchange'
  | 'Correction'
  | 'TransferToKrai';

export type DuesPaymentMethod = 'Cash' | 'Card';

/** Through the станиця, straight to the край, or both, by стаття (the web's DuesUpstream). */
export type DuesUpstream = 'stanytsia' | 'krai' | 'both';

export type DuesAccountStanding = 'Current' | 'Moved' | 'Left';

export interface QuarterDto {
  year: number;
  number: number;
}

export interface PlastYearDto {
  startYear: number;
  label: string;
  quarters: QuarterDto[];
}

/** A стаття of the kurin's вкладка; reducedAmount is what a person with a пільга pays. */
export interface DuesArticleDto {
  name: string;
  amount: number;
  reducedAmount: number;
  recipient: 'Stanytsia' | 'Krai' | 'Kurin';
}

/** The kurin's rate from a quarter on: the totals by recipient, and the статті they add up from. */
export interface KurinDuesRateDto {
  fromQuarter: QuarterDto;
  stanytsiaFull: number;
  stanytsiaReduced: number;
  kurinShare: number;
  kurinReduced: number;
  kraiFull: number;
  kraiReduced: number;
  articles: DuesArticleDto[];
}

export interface GroupDuesRateDto {
  fromQuarter: QuarterDto;
  groupShare: number;
}

export interface DuesAmountDto {
  stanytsia: number;
  krai: number;
  kurin: number;
  group: number;
  total: number;
}

export interface DuesAccountQuarterDto {
  quarter: QuarterDto;
  charged: DuesAmountDto;
  paid: DuesAmountDto;
  balance: number;
  isConcession: boolean;
}

export interface DuesAccountDto {
  membershipKey: string;
  memberKey: string;
  fullName: string;
  standing: DuesAccountStanding;
  isConcessionNow: boolean;
  quarters: DuesAccountQuarterDto[];
  charged: number;
  payments: number;
  balance: number;
}

export interface DuesEntryDto {
  duesEntryKey: string;
  kind: DuesEntryKind;
  method: DuesPaymentMethod;
  counterMethod: DuesPaymentMethod | null;
  amount: number;
  occurredOn: string;
  membershipKey: string | null;
  memberName: string | null;
  collectedByMemberKey: string | null;
  collectedByName: string | null;
  note: string | null;
  isVerified: boolean;
  verifiedAtUtc: string | null;
  verifiedByName: string | null;
  createdAtUtc: string;
}

export interface DuesBoxDto {
  cash: number;
  card: number;
  total: number;
  toForward: number;
  own: number;
  inTransit: number;
}

export interface DuesHandoverDto {
  owedUp: number;
  transferred: number;
  received: number;
  outstanding: number;
}

export interface DuesPersonDto {
  memberKey: string;
  fullName: string;
}

export interface DuesViewerDto {
  canKeep: boolean;
  canVerify: boolean;
  canSetKurinRates: boolean;
}

export interface GroupDuesDto {
  groupKey: string;
  kurinKey: string;
  groupName: string;
  /** ISO 4217 code of what the amounts are counted in: the край's currency. */
  currency: string;
  /** How the край writes numbers, such as uk-UA. */
  numberLocale: string;
  /** Where the край's kurins hand the вкладка up. */
  upstream: DuesUpstream;
  currentQuarter: QuarterDto;
  years: PlastYearDto[];
  kurinRates: KurinDuesRateDto[];
  groupRates: GroupDuesRateDto[];
  accounts: DuesAccountDto[];
  entries: DuesEntryDto[];
  box: DuesBoxDto;
  handover: DuesHandoverDto;
  people: DuesPersonDto[];
  viewer: DuesViewerDto;
}

export interface UpsertDuesEntryRequest {
  kind: DuesEntryKind;
  method: DuesPaymentMethod;
  counterMethod: DuesPaymentMethod | null;
  amount: number;
  occurredOn: string;
  membershipKey: string | null;
  collectedByMemberKey: string | null;
  note: string | null;
}

export interface SetGroupDuesRateRequest {
  fromQuarter: QuarterDto;
  groupShare: number;
}

export interface SetDuesConcessionRequest {
  fromQuarter: QuarterDto;
  isConcession: boolean;
}

// ── Kurin dues (api/kurin/{kurinKey}/dues; KurinDuesResponse) ─────────────────────────────────

/** How one гурток stands with the kurin. */
export interface KurinGroupHandoverDto {
  groupKey: string;
  groupName: string;
  owedUp: number;
  transferred: number;
  received: number;
  outstanding: number;
  inTransit: number;
}

/** A гурток's transfer to the kurin, as the kurin sees it. */
export interface DuesTransferDto {
  duesEntryKey: string;
  groupKey: string;
  groupName: string;
  amount: number;
  method: DuesPaymentMethod;
  occurredOn: string;
  collectedByName: string | null;
  note: string | null;
  isReceived: boolean;
  receivedAtUtc: string | null;
  receivedByName: string | null;
}

/** What one гурток's youth owe up for one quarter, and how far they have paid it. */
export interface KurinDuesQuarterGroupDto {
  groupKey: string;
  groupName: string;
  youthCount: number;
  expectedUp: number;
  collectedUp: number;
  debtUp: number;
  stanytsiaExpected: number;
  stanytsiaCollected: number;
  kraiExpected: number;
  kraiCollected: number;
}

export interface KurinDuesQuarterDto {
  quarter: QuarterDto;
  groups: KurinDuesQuarterGroupDto[];
  total: KurinDuesQuarterGroupDto;
}

export interface KurinDuesViewerDto {
  /** The курінний скарбник and the Звʼязковий: the kurin's own operations and «Отримано». */
  canKeep: boolean;
  /** The Звʼязковий: «Перевірено» on the kurin's operations. */
  canVerify: boolean;
  canSetRates: boolean;
}

export interface KurinDuesDto {
  kurinKey: string;
  /** ISO 4217 code of what the amounts are counted in: the край's currency. */
  currency: string;
  /** How the край writes numbers, such as uk-UA. */
  numberLocale: string;
  /** Where the край's kurins hand the вкладка up. */
  upstream: DuesUpstream;
  currentQuarter: QuarterDto;
  years: PlastYearDto[];
  rates: KurinDuesRateDto[];
  /** `toForward` is what still goes to the станиця and the край; `inTransit` — handed over by гуртки, not confirmed yet. */
  box: DuesBoxDto;
  /** Of `box.toForward`, the станиця's and the край's. */
  toStanytsia: number;
  toKrai: number;
  sentToStanytsia: number;
  sentToKrai: number;
  groups: KurinGroupHandoverDto[];
  /** Oldest quarter first; only quarters somebody was charged in. */
  quarters: KurinDuesQuarterDto[];
  transfers: DuesTransferDto[];
  entries: DuesEntryDto[];
  people: DuesPersonDto[];
  viewer: KurinDuesViewerDto;
}

export interface SetKurinDuesRateRequest {
  fromQuarter: QuarterDto;
  stanytsiaFull: number;
  stanytsiaReduced: number;
  kurinShare: number;
}
