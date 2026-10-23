import { AgendaRsvpStatus } from '../../me/me.models';

// Kept in step with the web's scoreModule/models/score.dto.ts and score.enums.ts, which mirror
// ProjectK.Common/Models/Dtos/ScoreModule and the KurinScoreController (api/kurin/{kurinKey}/score).

/** Mirrors `ScoreSource` on the backend; enums travel as strings. */
export type ScoreSource = 'Attendance' | 'Item' | 'Free' | 'Skill' | 'ProbePoint' | 'Probe' | 'Dues' | 'Warning';

export type ScoreAlgorithm = 'Average' | 'Sum';

export const SCORE_SOURCE_LABELS: Record<ScoreSource, string> = {
  Attendance: 'Присутність',
  Item: 'Позиції',
  Free: 'Від судді',
  Skill: 'Вмілості',
  ProbePoint: 'Точки проби',
  Probe: 'Проби',
  Dues: 'Вкладка',
  Warning: 'Перестороги',
};

/** What is given by hand first, then what the system knows (the web's column order). */
export const SCORE_SOURCE_ORDER: readonly ScoreSource[] = [
  'Attendance',
  'Item',
  'Free',
  'Skill',
  'ProbePoint',
  'Probe',
  'Dues',
  'Warning',
];

export const SCORE_ALGORITHM_LABELS: Record<ScoreAlgorithm, string> = {
  Average: 'Середнє на юнака',
  Sum: 'Сума балів',
};

export const SCORE_ALGORITHM_HINTS: Record<ScoreAlgorithm, string> = {
  Average: 'Бали юнаків ділимо на кількість юнаків у гуртку. Малий активний гурток не програє великому пасивному.',
  Sum: 'Усі бали юнаків складаємо. Великий гурток має перевагу.',
};

/** Which days a read totals; neither field means the current пластовий рік. */
export interface ScorePeriodQuery {
  year?: number | null;
  stageKey?: string | null;
}

export interface ScorePeriodDto {
  kind: 'Year' | 'Stage';
  year: number | null;
  stageKey: string | null;
  label: string;
  from: string;
  to: string;
}

export interface ScorePeriodsDto {
  years: ScorePeriodDto[];
  stages: ScorePeriodDto[];
}

export interface ScoreItemDto {
  scoreItemKey: string;
  name: string;
  points: number;
  isArchived: boolean;
}

export interface ScoreEntryDto {
  scoreEntryKey: string;
  membershipKey: string | null;
  memberKey: string | null;
  memberName: string | null;
  groupKey: string | null;
  groupName: string;
  isForGroup: boolean;
  scoreItemKey: string | null;
  itemName: string | null;
  points: number;
  reason: string | null;
  agendaItemKey: string | null;
  occurrenceStartUtc: string | null;
  occurredOn: string;
  createdByName: string | null;
  createdAtUtc: string;
}

export interface ScoreGroupRowDto {
  groupKey: string;
  groupName: string;
  place: number;
  score: number;
  otherScore: number;
  youthPoints: number;
  youthCount: number;
  average: number;
  groupPoints: number;
  canOpen: boolean;
}

export interface KurinScoreDto {
  kurinKey: string;
  algorithm: ScoreAlgorithm;
  period: ScorePeriodDto;
  periods: ScorePeriodsDto;
  groups: ScoreGroupRowDto[];
  viewer: { canScore: boolean; canManage: boolean };
}

export type ScorePersonStanding = 'Current' | 'Moved' | 'Left';

export interface ScorePersonRowDto {
  membershipKey: string;
  memberKey: string;
  fullName: string;
  standing: ScorePersonStanding;
  total: number;
  bySource: Partial<Record<ScoreSource, number>>;
}

export interface GroupScoreDto {
  groupKey: string;
  kurinKey: string;
  groupName: string;
  algorithm: ScoreAlgorithm;
  period: ScorePeriodDto;
  periods: ScorePeriodsDto;
  standing: ScoreGroupRowDto;
  groupCount: number;
  people: ScorePersonRowDto[];
  entries: ScoreEntryDto[];
  items: ScoreItemDto[];
  canScore: boolean;
}

export interface AttendanceMarkDto {
  markedByName: string | null;
  markedAtUtc: string;
}

/** Who said an occurrence did not take place, and when (the web's OccurrenceNotHeldDto). */
export interface OccurrenceNotHeldDto {
  markedByName: string | null;
  markedAtUtc: string;
}

export interface SheetPersonDto {
  membershipKey: string;
  memberKey: string;
  fullName: string;
  groupKey: string | null;
  groupName: string;
  rsvp: AgendaRsvpStatus | null;
  isAssigned: boolean;
  attendance: AttendanceMarkDto | null;
  canScore: boolean;
  entries: ScoreEntryDto[];
}

export interface SheetGroupDto {
  groupKey: string;
  groupName: string;
  canScore: boolean;
  entries: ScoreEntryDto[];
}

export interface AttendanceSheetDto {
  kurinKey: string;
  agendaItemKey: string;
  occurrenceStartUtc: string;
  occurrenceEndUtc: string | null;
  isAllDay: boolean;
  isRecurring: boolean;
  title: string;
  categoryKey: string | null;
  categoryName: string | null;
  categoryColorHex: string | null;
  categoryIcon: string | null;
  attendancePoints: number;
  hasOwnRate: boolean;
  people: SheetPersonDto[];
  groups: SheetGroupDto[];
  items: ScoreItemDto[];
  canManage: boolean;
  /** Set when someone said the occurrence did not take place; nobody can be marked then. */
  notHeld: OccurrenceNotHeldDto | null;
}

export interface MarkAttendanceResultDto {
  membershipKey: string;
  outcome: 'Marked' | 'AlreadyMarked';
  markedByName: string | null;
}

export interface UpsertScoreEntryRequest {
  membershipKey: string | null;
  groupKey: string | null;
  scoreItemKey: string | null;
  points: number | null;
  reason: string | null;
  agendaItemKey: string | null;
  occurrenceStartUtc: string | null;
  occurredOn: string;
}

export interface SetScoreAttendanceRateRequest {
  agendaCategoryKey: string | null;
  agendaItemKey: string | null;
  points: number | null;
}

/** The one field of the agenda item the sheet needs when no occurrence came with the link. */
export interface AgendaItemStartDto {
  agendaItemKey: string;
  startUtc: string | null;
}

/** Whom points go to: a person or a whole гурток (the web's ScoreEntryTarget). */
export interface ScoreEntryTarget {
  membershipKey: string | null;
  groupKey: string | null;
  name: string;
}

/** The event points are given at, if any (the web's ScoreEntryEvent). */
export interface ScoreEntryEvent {
  agendaItemKey: string;
  occurrenceStartUtc: string;
  title: string;
}

/** The API's error codes for score writes, in the web's words. */
export const SCORE_ERRORS: Record<string, string> = {
  ItemAlreadyGiven: 'Цю позицію тут уже дав хтось інший.',
  EntryTargetFixed: 'Бал лишається за тим, кому його дали. Запиши новий.',
  BadOccurrence: 'Ця подія того дня не збирається.',
  OccurrenceHasAttendance: 'Хтось уже відмічений на цих сходинах — отже, вони відбулися.',
};
