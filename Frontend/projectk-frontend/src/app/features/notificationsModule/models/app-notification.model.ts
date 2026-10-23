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
  | 'CalendarFeedRevoked';

export type AppNotificationSeverity = 'Info' | 'Success' | 'Warn' | 'Error';

export interface AppNotification {
  notificationKey: string;
  recipientUserKey: string;
  type: AppNotificationType;
  severity: AppNotificationSeverity;
  title: string;
  body: string;
  entityType: string | null;
  entityKey: string | null;
  route: string | null;
  payloadJson: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
  actorUserKey: string | null;
  deduplicationKey: string | null;
  expiresAtUtc: string | null;
  isRead: boolean;
}
