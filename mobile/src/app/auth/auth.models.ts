/** The web's shapes (Frontend authModule/models), kept in step so both read the same session. */
export interface AuthState {
  userKey: string;
  memberKey: string | null;
  email: string;
  isAdmin: boolean;
  permissions: string[];
  roles: string[];
  kurinKey: string | null;
  accessToken: string | null;
}

export interface LoginResponse {
  userKey: string;
  memberKey: string | null;
  email: string;
  isAdmin: boolean;
  permissions: string[] | null;
  roles: string[] | null;
  kurinKey: string | null;
  requiresMfa: boolean;
  mfaToken?: string | null;
  tokens: { accessToken: string } | null;
}

/** What the account may do now, as a refresh reports it (web access-snapshot.model.ts). */
export interface AccessSnapshot {
  isAdmin: boolean;
  roles: string[];
  permissions: string[];
  kurinKey: string | null;
}

/** The refresh answer; `access` is absent from servers older than 1.2.3. */
export interface RefreshResponse {
  accessToken: string;
  access?: AccessSnapshot | null;
}

/**
 * Another account this browser is signed in to, from `auth/accounts` (web saved-account.model.ts,
 * the API's SavedAccountDto).
 */
export interface SavedAccount {
  userKey: string;
  memberKey: string | null;
  email: string;
  /** Empty for an account without a member, such as a bare administrator. */
  fullName: string;
  profilePhotoUrl: string | null;
  isAdmin: boolean;
  /** System-role names for the kurin the account was last in, for display only. */
  roles: string[];
}

export type LoginOutcome = { kind: 'signed-in' } | { kind: 'mfa'; mfaToken: string | null };

export interface MfaStatus {
  isMfaEnabled: boolean;
  /** The account's offices require a second factor (admin, провід куреня). */
  isMfaRequired: boolean;
}

export interface MfaSetup {
  sharedKey: string;
  /** otpauth:// link; on a phone it opens the authenticator app directly. */
  authenticatorUri: string;
  qrCodeBase64: string;
}

export interface MfaEnabled {
  enabled: boolean;
  recoveryCodes: string[];
  /** The session that replaces the ended ones; its token already says the account has a second factor. */
  tokens?: { accessToken: string } | null;
}

export interface MfaRecoveryCodes {
  recoveryCodes: string[];
}

/** Пластова гілка куреня (web kurinModule/models/enums/kurin-branch.enum.ts). */
export type KurinBranch = 'UPYu' | 'USP' | 'UPS';

/** Юнацтво чи виховний склад (web kurinModule/models/enums/membership-kind.enum.ts). */
export type MembershipKind = 'Youth' | 'Staff';

/**
 * A kurin this account may act in now, from `auth/kurin-scope/options`. Kept in step with the
 * web's kurin-scope-option.model.ts and the API's KurinScopeOption record.
 */
export interface KurinScopeOption {
  kurinKey: string;
  /** Null for a підготовчий курінь. */
  kurinNumber: number | null;
  branch: KurinBranch;
  namedAfter?: string | null;
  kind: MembershipKind;
}
