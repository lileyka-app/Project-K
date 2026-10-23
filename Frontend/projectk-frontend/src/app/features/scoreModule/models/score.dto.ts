import { AgendaRsvpStatus } from '../../kurinModule/models/agenda';
import { ScoreAlgorithm, ScoreSource } from './score.enums';

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

export interface ScoreRuleDto {
  source: ScoreSource;
  variant: number;
  fromDate: string;
  points: number;
}

export interface ScoreAttendanceRateDto {
  agendaCategoryKey: string | null;
  categoryName: string | null;
  categoryColorHex: string | null;
  categoryIcon: string | null;
  points: number;
}

export interface ScoreStageDto {
  scoreStageKey: string;
  name: string;
  fromDate: string;
  toDate: string;
}

export interface KurinScoreSettingsDto {
  kurinKey: string;
  algorithm: ScoreAlgorithm;
  attendanceRates: ScoreAttendanceRateDto[];
  rules: ScoreRuleDto[];
  items: ScoreItemDto[];
  stages: ScoreStageDto[];
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

/** Who said an occurrence did not take place, and when. */
export interface OccurrenceNotHeldDto {
  markedByName: string | null;
  markedAtUtc: string;
}

export interface AttendanceMarkDto {
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

export interface SetScoreRuleRequest {
  source: ScoreSource;
  variant: number;
  fromDate: string;
  points: number;
}

export interface SetScoreAttendanceRateRequest {
  agendaCategoryKey: string | null;
  agendaItemKey: string | null;
  points: number | null;
}

export interface UpsertScoreItemRequest {
  name: string;
  points: number;
  isArchived: boolean;
}

export interface UpsertScoreStageRequest {
  name: string;
  fromDate: string;
  toDate: string;
}
