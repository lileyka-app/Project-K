import { inject, Injectable } from "@angular/core";
import { BehaviorSubject, catchError, finalize, map, Observable, of, shareReplay, switchMap, tap, throwError } from "rxjs";
import { HttpClient } from "@angular/common/http";
import { environment } from "../../../../../environments/environment";
import { LoginRequest } from "../../models/login-request.model";
import { LoginResponse } from "../../models/login-response.model";
import { AuthState } from "../../models/auth-state.model";
import { AccessSnapshot, RefreshResponse } from "../../models/access-snapshot.model";
import { KurinScopeOption } from "../../../kurinModule/models/kurin-scope-option.model";
import { SavedAccount } from "../../models/saved-account.model";
import { clearMfaSessionState } from "../mfa-session-state";
import { clearTileLayoutStorage } from "../../../../shared/tile-board/tile-layout-storage";
import { ClientCacheService } from "../../../kurinModule/services/client-cache/client-cache.service";
import { KURIN_SCOPED_CACHE_PREFIXES } from "../../../kurinModule/services/client-cache/cache-policy";
import { requestFeedback } from "../../../../shared/functions/request-feedback.function";

export interface MfaSetupResponse {
  sharedKey: string;
  authenticatorUri: string;
  qrCodeBase64: string;
}

export interface MfaStatusResponse {
  isMfaEnabled: boolean;
  isMfaRequired: boolean;
}

export interface MfaEnableResponse {
  enabled: boolean;
  recoveryCodes: string[];
  /** The session that replaces the ended ones; its token already says the account has a second factor. */
  tokens?: { accessToken: string } | null;
}

export interface MfaRecoveryCodesResponse {
  recoveryCodes: string[];
}

export interface SetupStatusResponse {
  isInitialized: boolean;
}

export interface InitializeSetupRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  enforcePrivilegedMfa: boolean;
  seedDemoData: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = environment.apiUrl;
  private readonly authState$ = new BehaviorSubject<AuthState | null>(null);
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private refreshTokenRequest$: Observable<string> | null = null;

  constructor() {
    const savedState = localStorage.getItem('authState');
    if (savedState) {
      this.authState$.next(this.deserializeStoredState(savedState));
    }
  }

  getAuthState() {
    return this.authState$.asObservable();
  }

  getAuthStateValue() {
    return this.authState$.value;
  } 

  /**
   * `addAccount` keeps the account in use one tap away in the account switcher instead of ending
   * it: the server moves its session aside before the new one takes the refresh cookie.
   */
  login(credentials: LoginRequest, addAccount = false): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/login${addAccount ? '?addAccount=true' : ''}`,
      credentials,
      { withCredentials: true, context: requestFeedback('errors') }
    ).pipe(
      tap(response => {
        if (!response.requiresMfa && response.tokens) {
          clearMfaSessionState();
          this.applyState(this.toAuthState(response));
        }
      })
    );
  }

  /**
   * The second step of a sign-in. `mfaToken` is what the password step answered with; the server
   * refuses a code that arrives without it.
   */
  verifyMfaLogin(email: string, code: string, mfaToken: string | null, addAccount = false): Observable<AuthState> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/mfa/login-verify${addAccount ? '?addAccount=true' : ''}`,
      { email, code, rememberMe: true, mfaToken },
      { withCredentials: true, context: requestFeedback('errors') }
    ).pipe(
      map(response => this.toAuthState(response)),
      tap(state => {
        clearMfaSessionState();
        this.applyState(state);
      })
    );
  }

  /** The other accounts this browser is signed in to; the httpOnly cookies say which. */
  getSavedAccounts(): Observable<SavedAccount[]> {
    return this.http.get<SavedAccount[]>(
      `${this.apiUrl}/auth/accounts`,
      { withCredentials: true, context: requestFeedback('silent') }
    );
  }

  /**
   * Makes a kept account the one in use; the account being left is kept in its place. Everything
   * remembered for the previous account is dropped here, but the caller should still reload the
   * page: screens and services hold that account's data in memory too.
   */
  switchAccount(userKey: string): Observable<AuthState> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/accounts/switch`,
      { userKey },
      { withCredentials: true, context: requestFeedback('errors') }
    ).pipe(
      map(response => {
        clearTileLayoutStorage();
        return this.applyLoginResponse(response);
      })
    );
  }

  getMfaSetup(): Observable<MfaSetupResponse> {
    return this.http.get<MfaSetupResponse>(
      `${this.apiUrl}/auth/mfa/setup`,
      { withCredentials: true }
    );
  }

  enableMfa(code: string): Observable<MfaEnableResponse> {
    return this.http.post<MfaEnableResponse>(
      `${this.apiUrl}/auth/mfa/enable`,
      { code },
      { withCredentials: true }
    ).pipe(
      // Enabling ends every session and hands this device a new one. Without taking its token the
      // old one would stay in use: still valid for minutes, but refused on every save by the
      // privileged-MFA gate, which reads the second factor off the token.
      tap(response => {
        if (response.tokens?.accessToken) {
          this.applyAccessToken(response.tokens.accessToken);
        }
      })
    );
  }

  rotateMfaRecoveryCodes(currentPassword: string): Observable<MfaRecoveryCodesResponse> {
    return this.http.post<MfaRecoveryCodesResponse>(
      `${this.apiUrl}/auth/mfa/recovery-codes`,
      { currentPassword },
      { withCredentials: true }
    );
  }

  getMfaStatus(): Observable<MfaStatusResponse> {
    return this.http.get<MfaStatusResponse>(
      `${this.apiUrl}/auth/mfa/status`,
      { withCredentials: true }
    );
  }

  getSetupStatus(): Observable<SetupStatusResponse> {
    return this.http.get<SetupStatusResponse>(
      `${this.apiUrl}/auth/setup/status`,
      { withCredentials: true }
    );
  }

  initializeSetup(command: InitializeSetupRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/setup/initialize`,
      command,
      { withCredentials: true, context: requestFeedback('errors') }
    ).pipe(
      tap(response => {
        if (!response.requiresMfa && response.tokens) {
          clearMfaSessionState();
          this.applyState(this.toAuthState(response));
        }
      })
    );
  }

  clearLocalState(): void {
    this.authState$.next(null);
    localStorage.removeItem('authState');
    clearMfaSessionState();
    clearTileLayoutStorage();
    // Otherwise the next user on this tab is served the previous user's entities.
    this.forgetKurinScopedEntities();
  }

  /**
   * Ends this browser's session on the server, then forgets it locally.
   *
   * Order matters: the local state used to be cleared first, which sent the request without a
   * bearer token — every sign-out was a 401, the refresh token stayed valid for a week, and the
   * kurin the admin had stepped into was never cleared. An access token that has expired in the
   * meantime is refreshed once; the interceptor deliberately does not do that for this route.
   */
  logout() {
    const signOut = () => this.http.post(`${this.apiUrl}/auth/logout`, {}, {
      withCredentials: true,
      responseType: 'text',
      context: requestFeedback('silent')
    });

    return signOut().pipe(
      catchError((error: unknown) =>
        (error as { status?: number })?.status === 401 && this.authState$.value?.userKey
          ? this.refreshToken().pipe(switchMap(() => signOut()))
          : throwError(() => error)),
      // Before the caller hears the answer, not after: whoever subscribes expects to be signed out
      // by the time `next` or `error` arrives.
      tap({ next: () => this.clearLocalState(), error: () => this.clearLocalState() })
    );
  }

  refreshToken(): Observable<string> {
    if (this.refreshTokenRequest$) {
      return this.refreshTokenRequest$;
    }

    this.refreshTokenRequest$ = this.http.post<RefreshResponse>(
      `${this.apiUrl}/auth/refresh`,
      {},
      { withCredentials: true, context: requestFeedback('silent') }
    ).pipe(
      tap(res => {
        this.applyAccessToken(res.accessToken);
        if (res.access) {
          this.applyAccess(res.access);
        }
      }),
      map(res => res.accessToken),
      finalize(() => {
        this.refreshTokenRequest$ = null;
      }),
      shareReplay({ bufferSize: 1, refCount: true })
    );

    return this.refreshTokenRequest$;
  }

  getAccessToken(): string | null {
    return this.authState$.value?.accessToken ?? null;
  }

  ensureAccessToken(): Observable<boolean> {
    const state = this.authState$.value;
    if (!state?.userKey) {
      return of(false);
    }

    if (state.accessToken) {
      return of(true);
    }

    return this.refreshToken().pipe(
      map(() => true),
      catchError(() => {
        this.clearLocalState();
        return of(false);
      })
    );
  }

  /**
   * Курені, у яких цей акаунт може стояти зараз. Список приходить із сервера, а не збирається на
   * фронті, щоб запропонований вибір збігався з тим, що сервер справді дозволить.
   */
  getKurinScopeOptions(): Observable<KurinScopeOption[]> {
    return this.http.get<KurinScopeOption[]>(
      `${this.apiUrl}/auth/kurin-scope/options`,
      { withCredentials: true }
    );
  }

  setKurinScope(kurinKey: string | null): Observable<AuthState> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/kurin-scope`,
      { kurinKey },
      { withCredentials: true, context: requestFeedback('errors') }
    ).pipe(
      map(response => this.toAuthState(response)),
      tap(state => {
        this.applyState(state);
        this.forgetKurinScopedEntities();
      })
    );
  }

  setKurinKey(kurinKey: string | null): void {
    const state = this.authState$.value;
    if (state) {
      const kurinChanged = state.kurinKey !== kurinKey;
      const newState = { ...state, kurinKey };
      this.authState$.next(newState);
      this.persistAuthState(newState);

      // Entity cache keys carry no kurin scope, so the previous kurin would survive the switch.
      if (kurinChanged) {
        this.forgetKurinScopedEntities();
      }
    }
  }

  updateEmail(email: string): void {
    const state = this.authState$.value;
    if (state) {
      const newState = { ...state, email };
      this.authState$.next(newState);
      this.persistAuthState(newState);
    }
  }

  updatePermissions(permissions: string[], roles: string[], isAdmin: boolean, kurinKey?: string | null): void {
    const state = this.authState$.value;
    if (state) {
      const newState = { ...state, permissions, roles, isAdmin, kurinKey: kurinKey === undefined ? state.kurinKey : kurinKey };
      this.authState$.next(newState);
      this.persistAuthState(newState);
    }
  }

  /**
   * Takes what a refresh says this person may do now. Nothing happens when it matches what is
   * already held; a change drops every cached answer given for the old access (check-access, member
   * data masked for the old roles) before the new state goes out, so whatever reacts to it — the
   * menu, the open page — reads afresh. Answers whether anything changed.
   */
  applyAccess(access: AccessSnapshot): boolean {
    const state = this.authState$.value;
    if (!state) {
      return false;
    }

    const kurinKey = access.kurinKey ?? null;
    const changed = state.isAdmin !== access.isAdmin
      || state.kurinKey !== kurinKey
      || !sameMembers(state.roles, access.roles)
      || !sameMembers(state.permissions, access.permissions);
    if (!changed) {
      return false;
    }

    this.forgetKurinScopedEntities();
    this.updatePermissions(access.permissions ?? [], access.roles ?? [], access.isAdmin, kurinKey);
    return true;
  }

  clearKurinKey(): void {
    this.setKurinKey(null);
  }

  /**
   * Takes a sign-in the caller obtained some other way (the dev role switcher) as the current
   * session. Same path as the login form, so nothing about the session is remembered differently.
   */
  applyLoginResponse(response: LoginResponse): AuthState {
    const state = this.toAuthState(response);
    clearMfaSessionState();
    this.forgetKurinScopedEntities();
    this.applyState(state);
    return state;
  }

  private toAuthState(response: LoginResponse): AuthState {
    if (!response.tokens) {
      throw new Error('No tokens in response');
    }

    return {
      userKey: response.userKey,
      memberKey: response.memberKey,
      email: response.email,
      isAdmin: response.isAdmin,
      permissions: response.permissions ?? [],
      roles: response.roles ?? [],
      kurinKey: response.kurinKey ?? null,
      accessToken: response.tokens.accessToken
    };
  }

  private applyState(state: AuthState): void {
    this.authState$.next(state);
    this.persistAuthState(state);
  }

  /** A new access token for the signed-in person; nothing to do when nobody is signed in. */
  private applyAccessToken(accessToken: string): void {
    const state = this.authState$.value;
    if (state) {
      this.applyState({ ...state, accessToken });
    }
  }

  /**
   * Drops everything the current kurin scope could have coloured. Catalogues are global
   * reference data on a 6-hour TTL, so a scope switch must not force them to reload.
   */
  private forgetKurinScopedEntities(): void {
    for (const prefix of KURIN_SCOPED_CACHE_PREFIXES) {
      this.cache.invalidateByPrefix(prefix);
    }
  }

  private persistAuthState(state: AuthState): void {
    localStorage.setItem('authState', JSON.stringify(this.toStoredState(state)));
  }

  private deserializeStoredState(rawState: string): AuthState | null {
    try {
      const parsed = JSON.parse(rawState) as AuthState;
      return this.toStoredState(parsed);
    } catch {
      localStorage.removeItem('authState');
      return null;
    }
  }

  private toStoredState(state: AuthState): AuthState {
    return {
      userKey: state.userKey,
      memberKey: state.memberKey,
      email: state.email,
      isAdmin: state.isAdmin,
      permissions: state.permissions ?? [],
      roles: state.roles ?? [],
      kurinKey: state.kurinKey,
      accessToken: null
    };
  }
}

/** The same strings regardless of order; the server sends permissions in no promised order. */
function sameMembers(held: readonly string[] | null | undefined, given: readonly string[] | null | undefined): boolean {
  const a = new Set(held ?? []);
  const b = new Set(given ?? []);
  return a.size === b.size && [...a].every(value => b.has(value));
}
