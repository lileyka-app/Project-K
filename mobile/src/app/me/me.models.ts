/** The web's dashboard shapes (Frontend dashboardModule/models/me.dto.ts), the parts the phone reads. */
export type AgendaItemStatus = 'Todo' | 'InProgress' | 'Done';
export type AgendaRsvpStatus = 'Going' | 'NotGoing' | 'Maybe';

export interface MyKurinRefDto {
  kurinKey: string;
  /** Null for a підготовчий курінь. */
  kurinNumber: number | null;
  namedAfter: string | null;
  isCurrent: boolean;
}

export interface MyEventDto {
  agendaItemKey: string;
  kurin: MyKurinRefDto;
  title: string;
  startUtc: string;
  endUtc: string | null;
  isAllDay: boolean;
  isRecurring: boolean;
  location: string | null;
  categoryName: string | null;
  categoryColorHex: string | null;
  rsvpRequired: boolean;
  myResponse: AgendaRsvpStatus | null;
}

export interface MyTaskDto {
  agendaItemKey: string;
  kurin: MyKurinRefDto;
  title: string;
  status: AgendaItemStatus;
  startUtc: string | null;
  endUtc: string | null;
  canChangeStatus: boolean;
}

export interface MyProbeDto {
  probeId: string;
  title: string;
  status: 'NotStarted' | 'InProgress' | 'Completed' | 'Verified';
  signedPoints: number;
  totalPoints: number;
  nextPoints: { pointId: string; sectionCode: string; title: string }[];
}

export interface MyBadgeDto {
  badgeId: string;
  title: string;
  status: 'Draft' | 'Submitted' | 'Confirmed' | 'Rejected';
}

export interface MyGrowthDto {
  memberKey: string;
  hasYouthProgram: boolean;
  probe: MyProbeDto | null;
  badges: { onReview: MyBadgeDto[]; inWork: MyBadgeDto[]; confirmed: MyBadgeDto[]; confirmedCount: number };
}

export interface MyDuesDto {
  kurin: MyKurinRefDto;
  groupName: string | null;
  /** ISO 4217 code of what the amounts are counted in: the край's currency. */
  currency: string;
  /** How the край writes numbers, such as uk-UA. */
  numberLocale: string;
  quarterYear: number;
  quarterNumber: number;
  /** Negative is debt, positive a surplus. */
  balance: number;
  quarterRate: number | null;
  isConcession: boolean;
}

export interface MyScoreDto {
  kurin: MyKurinRefDto;
  groupKey: string;
  groupName: string;
  periodLabel: string;
  total: number;
  groupPlace: number;
  groupCount: number;
}

export type MyDutyKind = 'BadgesToReview' | 'TransfersToConfirm' | 'EntriesToVerify' | 'EventWithoutAttendance';

/** One thing that waits on a провід (api/me/duties; the web's MyDutyDto). */
export interface MyDutyDto {
  kind: MyDutyKind;
  kurin: MyKurinRefDto;
  count: number;
  groupKey: string | null;
  groupName: string | null;
  agendaItemKey: string | null;
  occurrenceStartUtc: string | null;
  title: string | null;
}

/** The member card (Frontend kurinModule/models/member.dto.ts), the fields the profile shows. */
export interface MemberDto {
  memberKey: string;
  firstName: string;
  middleName: string | null;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  groupName?: string | null;
  school?: string | null;
  latestPlastLevelDisplay?: string | null;
  profilePhotoUrl: string | null;
}
