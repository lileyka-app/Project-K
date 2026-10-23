/**
 * The agenda's shapes, kept in step with the web's kurinModule/models/agenda.ts and the API's
 * AgendaResponse.cs / AgendaRsvpModels.cs / CreateAgendaItemCommand.cs (enums travel as strings).
 */
export type AgendaItemKind = 'Event' | 'Task';
export type AgendaItemStatus = 'Todo' | 'InProgress' | 'Done';
export type AgendaTargetType = 'Kurin' | 'Group' | 'Member' | 'Leadership';
export type AgendaRsvpStatus = 'Going' | 'NotGoing' | 'Maybe';
export type RecurrenceFrequency = 'None' | 'Weekly' | 'Monthly' | 'Yearly';
/** Once for all, closed by its провід (`Shared`) or by anyone in it, or by each person on their own. */
export type AgendaCompletionMode = 'Shared' | 'SharedByAnyone' | 'PerMember';
export type AgendaBoardSort = 'Due' | 'Recent' | 'Created' | 'Title';

export interface AgendaPartDto {
  memberKey: string;
  name: string;
  status: AgendaItemStatus;
  changedByName: string | null;
  changedAtUtc: string | null;
  canChangeStatus: boolean;
}

export interface AgendaAssignmentDto {
  agendaAssignmentKey: string;
  targetType: AgendaTargetType;
  targetKey: string;
  label: string | null;
  completionMode: AgendaCompletionMode;
  status: AgendaItemStatus;
  statusChangedByName: string | null;
  statusChangedAtUtc: string | null;
  canChangeStatus: boolean;
  doneCount: number | null;
  peopleCount: number | null;
  /** «Кожному окремо», for those who run the target only. */
  parts: AgendaPartDto[] | null;
}

export interface AgendaItemDto {
  agendaItemKey: string;
  kurinKey: string;
  kind: AgendaItemKind;
  title: string;
  description: string | null;
  location: string | null;
  status: AgendaItemStatus;
  /** The column this viewer sees it in: their own part's state, or the whole task's. */
  viewerStatus: AgendaItemStatus;
  startUtc: string | null;
  endUtc: string | null;
  isAllDay: boolean;
  createdByUserKey: string;
  createdByName: string | null;
  createdUtc: string;
  updatedUtc: string;
  completedAtUtc: string | null;
  archivedAtUtc: string | null;
  archivedByName: string | null;
  canEdit: boolean;
  canChangeStatus: boolean;
  /** False when the viewer sees the item only as its author: it is someone else's to do. */
  addressedToViewer: boolean;
  categoryKey: string | null;
  categoryName: string | null;
  categoryColorHex: string | null;
  categoryIcon: string | null;
  isKurinSchedule: boolean;
  /** `Schedule`: on the calendar only through the kurin's schedule — nothing to answer. */
  audience: 'Assigned' | 'Schedule';
  recurrenceFrequency: RecurrenceFrequency;
  recurrenceInterval: number;
  /** Bit 0 = Sunday … bit 6 = Saturday. */
  recurrenceByWeekday: number;
  recurrenceEndUtc: string | null;
  recurrenceCount: number | null;
  isRecurrenceInstance: boolean;
  seriesStartUtc: string | null;
  seriesEndUtc: string | null;
  assignments: AgendaAssignmentDto[];
}

export interface AgendaBoardFilter {
  search?: string | null;
  targetType?: AgendaTargetType | null;
  targetKey?: string | null;
  onlyMine?: boolean;
  sort?: AgendaBoardSort;
  status?: AgendaItemStatus | null;
  skip?: number;
  take?: number;
}

export interface AgendaBoardColumn {
  status: AgendaItemStatus;
  total: number;
  items: AgendaItemDto[];
}

export interface AgendaBoardTarget {
  targetType: AgendaTargetType;
  targetKey: string;
  label: string;
}

export interface AgendaBoardResponse {
  columns: AgendaBoardColumn[];
  targets: AgendaBoardTarget[];
}

/** A kurin's archive rules (AgendaArchivePolicyDto); an empty period switches that step off. */
export interface AgendaArchivePolicy {
  autoArchiveAfterDays: number | null;
  purgeAfterDays: number | null;
}

/** A page of archived tasks, newest first (AgendaArchivePageResponse). */
export interface AgendaArchivePage {
  total: number;
  items: AgendaItemDto[];
  policy: AgendaArchivePolicy;
}

export interface AgendaCategoryDto {
  agendaCategoryKey: string;
  kurinKey: string;
  name: string;
  colorHex: string;
  icon: string | null;
  defaultDescription: string | null;
  rsvpRequired: boolean;
  defaultDurationMinutes: number | null;
  isArchived: boolean;
  isKurinSchedule: boolean;
}

export interface AgendaRsvpDto {
  userKey: string;
  displayName: string;
  status: AgendaRsvpStatus;
  respondedAtUtc: string;
  isWaitlisted: boolean;
}

export interface AgendaResponsesResponse {
  agendaItemKey: string;
  occurrenceStartUtc: string | null;
  capacity: number | null;
  waitlistEnabled: boolean;
  myStatus: AgendaRsvpStatus | null;
  goingConfirmedCount: number;
  goingWaitlistCount: number;
  notGoingCount: number;
  maybeCount: number;
  responses: AgendaRsvpDto[];
}

export interface AgendaTargetInput {
  targetType: AgendaTargetType;
  targetKey: string;
  /** Ignored for a member target. */
  completionMode?: AgendaCompletionMode;
}

export interface CreateAgendaItemRequest {
  kurinKey: string;
  kind: AgendaItemKind;
  title: string;
  description: string | null;
  location: string | null;
  startUtc: string | null;
  endUtc: string | null;
  isAllDay: boolean;
  agendaCategoryKey: string | null;
  recurrenceFrequency: RecurrenceFrequency;
  recurrenceInterval: number;
  recurrenceByWeekday: number;
  recurrenceEndUtc: string | null;
  recurrenceCount: number | null;
  targets: AgendaTargetInput[];
}

export interface UpdateAgendaItemRequest extends CreateAgendaItemRequest {
  agendaItemKey: string;
}

export interface AgendaLeadershipTarget {
  leadershipKey: string;
  label: string;
  canTarget: boolean;
}

export interface AgendaGroupTarget {
  groupKey: string;
  name: string;
  canTargetGroup: boolean;
  leadership: AgendaLeadershipTarget | null;
  /** `canTarget` is false for a person listed only beside a гурток or провід the user may pick. */
  members: { memberKey: string; fullName: string; canTarget: boolean }[];
}

export interface AgendaAssignTargets {
  canTargetKurin: boolean;
  kurinKey: string;
  kurinLabel: string;
  kurinLeaderships: AgendaLeadershipTarget[];
  groups: AgendaGroupTarget[];
}
