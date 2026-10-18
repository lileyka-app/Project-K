import { DuesEntryKind, DuesPaymentMethod, DuesRecipient, DuesUpstream } from './dues.enums';

export interface QuarterDto {
  year: number;
  number: number;
}

export interface PlastYearDto {
  startYear: number;
  label: string;
  quarters: QuarterDto[];
}

/** One стаття of the kurin's вкладка, such as «Станична вкладка» or «Табірний фонд». */
export interface DuesArticleDto {
  name: string;
  recipient: DuesRecipient;
  amount: number;
  /** What a person with a пільга pays for it. */
  reducedAmount: number;
}

export interface KurinDuesRateDto {
  fromQuarter: QuarterDto;
  /** The kurin's статті, in its order; the amounts below are their totals. */
  articles: DuesArticleDto[];
  stanytsiaFull: number;
  stanytsiaReduced: number;
  kurinShare: number;
  kurinReduced: number;
  /** What goes to the край on its own transfer; zero until the kurin names a край стаття. */
  kraiFull: number;
  kraiReduced: number;
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

export type DuesAccountStanding = 'Current' | 'Moved' | 'Left';

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
  receivedAtUtc: string | null;
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

/** `GET api/group/{groupKey}/dues` — the гурток's whole box in one read. */
export interface GroupDuesDto {
  groupKey: string;
  kurinKey: string;
  /** ISO 4217 code of what the amounts are counted in: the край's currency. */
  currency: string;
  /** How the край writes numbers, such as uk-UA. */
  numberLocale: string;
  /** Where the край's kurins hand the вкладка up. */
  upstream: DuesUpstream;
  groupName: string;
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

/** A гурток whose box the caller may open. */
export interface DuesGroupLinkDto {
  groupKey: string;
  groupName: string;
}

export interface KurinDuesViewerDto {
  canKeep: boolean;
  canVerify: boolean;
  canSetRates: boolean;
}

/** `GET api/kurin/{kurinKey}/dues` — the kurin's own box, the гуртки's handovers and transfers. */
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
  quarters: KurinDuesQuarterDto[];
  transfers: DuesTransferDto[];
  entries: DuesEntryDto[];
  people: DuesPersonDto[];
  viewer: KurinDuesViewerDto;
}

export interface MemberDuesAccountDto {
  groupKey: string;
  groupName: string;
  standing: DuesAccountStanding;
  quarters: DuesAccountQuarterDto[];
  charged: number;
  payments: number;
  balance: number;
}

/** `GET api/member/{memberKey}/dues` — one person's вкладка in the kurin they are looked at in. */
export interface MemberDuesDto {
  hasAccount: boolean;
  kurinKey: string;
  /** ISO 4217 code of what the amounts are counted in: the край's currency. */
  currency: string;
  /** How the край writes numbers, such as uk-UA. */
  numberLocale: string;
  currentQuarter: QuarterDto;
  balance: number;
  quarterRate: DuesAmountDto | null;
  isConcessionNow: boolean;
  currentGroupKey: string | null;
  currentGroupName: string | null;
  canOpenGroupDues: boolean;
  accounts: MemberDuesAccountDto[];
  entries: DuesEntryDto[];
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

export interface SetKurinDuesRateRequest {
  fromQuarter: QuarterDto;
  articles: DuesArticleRequest[];
}

export interface DuesArticleRequest {
  name: string;
  recipient: DuesRecipient;
  amount: number;
  reducedAmount: number;
}

export interface SetDuesConcessionRequest {
  fromQuarter: QuarterDto;
  isConcession: boolean;
}
