/** Mirrors the backend AgendaItemKind / AgendaItemStatus / AgendaTargetType enums (serialized as strings). */
export type AgendaItemKind = 'Event' | 'Task';
export type AgendaItemStatus = 'Todo' | 'InProgress' | 'Done';
export type AgendaTargetType = 'Kurin' | 'Group' | 'Member' | 'Leadership';
export type AgendaRsvpStatus = 'Going' | 'NotGoing' | 'Maybe';
export type RecurrenceFrequency = 'None' | 'Weekly' | 'Monthly' | 'Yearly';
/**
 * How a task aimed at a гурток, the kurin or a провід is done: once for all, closed by its провід
 * (`Shared`) or by anyone in it (`SharedByAnyone`), or by each person on their own (`PerMember`).
 */
export type AgendaCompletionMode = 'Shared' | 'SharedByAnyone' | 'PerMember';

export const COMPLETION_MODE_OPTIONS: { label: string; hint: string; value: AgendaCompletionMode }[] = [
  { label: 'Одна на всіх — закриває провід', hint: 'Гурток бачить задачу, закриває її гуртковий чи впорядник.', value: 'Shared' },
  { label: 'Одна на всіх — закриває будь-хто', hint: 'Досить, щоб зробив один; видно, хто саме.', value: 'SharedByAnyone' },
  { label: 'Кожному окремо', hint: 'Кожен робить свою частину; задача зроблена, коли зробили всі.', value: 'PerMember' }
];

/** Weekday bitmask helpers for RecurrenceByWeekday (bit 0 = Sunday … bit 6 = Saturday). */
export const WEEKDAY_BITS = [
  { label: 'Нд', bit: 1 << 0 },
  { label: 'Пн', bit: 1 << 1 },
  { label: 'Вт', bit: 1 << 2 },
  { label: 'Ср', bit: 1 << 3 },
  { label: 'Чт', bit: 1 << 4 },
  { label: 'Пт', bit: 1 << 5 },
  { label: 'Сб', bit: 1 << 6 }
];

export interface AgendaAssignmentDto {
  agendaAssignmentKey: string;
  targetType: AgendaTargetType;
  targetKey: string;
  label: string | null;
  completionMode: AgendaCompletionMode;
  /** The target's state; in «кожному окремо» what its people's parts add up to. */
  status: AgendaItemStatus;
  statusChangedByName: string | null;
  statusChangedAtUtc: string | null;
  canChangeStatus: boolean;
  doneCount: number | null;
  peopleCount: number | null;
  /** «Кожному окремо», for those who run the target only. */
  parts: AgendaPartDto[] | null;
}

/** One person's part of a target done «кожному окремо». */
export interface AgendaPartDto {
  memberKey: string;
  name: string;
  status: AgendaItemStatus;
  changedByName: string | null;
  changedAtUtc: string | null;
  canChangeStatus: boolean;
}

export interface AgendaItemDto {
  agendaItemKey: string;
  kurinKey: string;
  kind: AgendaItemKind;
  title: string;
  description: string | null;
  location: string | null;
  /** The task as a whole, over all its targets. */
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
  /** When the task as a whole was closed; what auto-archiving counts from. */
  completedAtUtc: string | null;
  /** Set for a task in the archive. */
  archivedAtUtc: string | null;
  /** Who archived it; null with `archivedAtUtc` set means automatically. */
  archivedByName: string | null;
  canEdit: boolean;
  canChangeStatus: boolean;
  /** False when the viewer sees the item only as its author: it is someone else's to do. */
  addressedToViewer: boolean;
  categoryKey: string | null;
  categoryName: string | null;
  categoryColorHex: string | null;
  categoryIcon: string | null;
  /** The item's group is «графік куреня». */
  isKurinSchedule: boolean;
  /** `Schedule`: on the calendar only through the kurin's schedule — nothing to answer. */
  audience: 'Assigned' | 'Schedule';
  recurrenceFrequency: RecurrenceFrequency;
  recurrenceInterval: number;
  recurrenceByWeekday: number;
  recurrenceEndUtc: string | null;
  recurrenceCount: number | null;
  isRecurrenceInstance: boolean;
  seriesStartUtc: string | null;
  seriesEndUtc: string | null;
  assignments: AgendaAssignmentDto[];
}

export type AgendaBoardSort = 'Due' | 'Recent' | 'Created' | 'Title';

export const BOARD_SORT_OPTIONS: { label: string; value: AgendaBoardSort }[] = [
  { label: 'За терміном', value: 'Due' },
  { label: 'Нещодавно змінені', value: 'Recent' },
  { label: 'Нові спершу', value: 'Created' },
  { label: 'За назвою', value: 'Title' }
];

/** What the board narrows to; `status` asks for one column's next page. */
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
  /** How many tasks the column holds under the filter. */
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

/** A kurin's archive rules; an empty period switches that step off. */
export interface AgendaArchivePolicy {
  autoArchiveAfterDays: number | null;
  purgeAfterDays: number | null;
}

export interface AgendaArchivePage {
  total: number;
  items: AgendaItemDto[];
  policy: AgendaArchivePolicy;
}

/** An event group (табір/захід/сходини) as the picker and management page see it. */
export interface AgendaCategoryDto {
  agendaCategoryKey: string;
  kurinKey: string;
  name: string;
  colorHex: string;
  icon: string | null;
  capacity: number | null;
  waitlistEnabled: boolean;
  defaultDescription: string | null;
  rsvpRequired: boolean;
  defaultDurationMinutes: number | null;
  reminderLeadMinutes: number | null;
  isArchived: boolean;
  /** «Графік куреня»: everyone in the kurin sees the group's events, read-only. */
  isKurinSchedule: boolean;
}

/** Body for creating/updating an event group. Omit agendaCategoryKey to create. */
export interface UpsertAgendaCategoryRequest {
  agendaCategoryKey?: string | null;
  kurinKey: string;
  name: string;
  colorHex: string;
  icon: string | null;
  capacity: number | null;
  waitlistEnabled: boolean;
  defaultDescription: string | null;
  rsvpRequired: boolean;
  defaultDurationMinutes: number | null;
  reminderLeadMinutes: number | null;
  isArchived: boolean;
  /** «Графік куреня»: everyone in the kurin sees the group's events, read-only. */
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
  /** The occurrence the picture is of; null for a one-off event. */
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

export interface AgendaMemberTarget {
  memberKey: string;
  fullName: string;
  /** False when the person is listed only beside a гурток or провід the viewer may pick. */
  canTarget: boolean;
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
  members: AgendaMemberTarget[];
}

export interface AgendaAssignTargets {
  canTargetKurin: boolean;
  kurinKey: string;
  kurinLabel: string;
  kurinLeaderships: AgendaLeadershipTarget[];
  groups: AgendaGroupTarget[];
}
