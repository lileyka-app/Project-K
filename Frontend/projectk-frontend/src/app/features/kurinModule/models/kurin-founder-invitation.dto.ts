/** An admin opens a kurin and invites its Зв'язковий; the person sets their own password on activation. */
export interface InviteKurinFounderRequest {
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
  /** Null opens a підготовчий курінь. */
  kurinNumber: number | null;
  kraiCode?: string | null;
}

export interface KurinFounderInvitationDto {
  kurinKey: string;
  userKey: string;
  email: string;
  /** False when the kurin and the account were stored but the letter did not go. */
  invitationSent: boolean;
}
