/**
 * What the account may do in the kurin it is in, as a refresh reports it. The same four facts a
 * sign-in carries, so an office or policy changed elsewhere reaches an open session.
 */
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
