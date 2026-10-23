import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { apiUrl } from '../runtime-config';
import {
  AuthState,
  KurinScopeOption,
  LoginOutcome,
  LoginResponse,
  MfaEnabled,
  MfaRecoveryCodes,
  MfaSetup,
  MfaStatus,
  RefreshResponse,
  SavedAccount,
} from './auth.models';

/**
 * The PWA lives on the web's origin (/m/), so it shares the web's session: the same `authState`
 * entry in localStorage and the same httpOnly refresh cookie on the API. Signing in or out in one
 * signs in or out of the other, which is the only consistent option while the cookie is shared.
 * Like the web, the access token stays in memory only and is renewed from the cookie after a reload.
 */
const STORAGE_KEY = 'authState';

const ERROR_TEXT: Record<string, string> = {
  InvalidCredentials: 'Неправильний email або пароль.',
  Unauthorized: 'Неправильний email або пароль.',
  TooManyAttempts: 'Забагато невдалих спроб для цієї пошти. Зачекай 15 хвилин або віднови пароль.',
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = apiUrl();
  private readonly state = signal<AuthState | null>(readStoredState());
  private refreshing: Promise<string> | null = null;

  readonly user = this.state.asReadonly();
  readonly signedIn = computed(() => this.state() !== null);

  accessToken(): string | null {
    return this.state()?.accessToken ?? null;
  }

  /**
   * `addAccount` keeps the account in use in the switcher instead of ending it: the server moves its
   * session aside before the new one takes the refresh cookie.
   */
  async login(email: string, password: string, addAccount = false): Promise<LoginOutcome> {
    const response = await this.post<LoginResponse>(withAddAccount('auth/login', addAccount), { email, password });
    if (response.requiresMfa || !response.tokens) {
      return { kind: 'mfa', mfaToken: response.mfaToken ?? null };
    }
    this.apply(toState(response));
    return { kind: 'signed-in' };
  }

  /** The second step; `code` is a 6-digit authenticator code or a recovery code. */
  async verifyMfa(email: string, code: string, mfaToken: string | null, addAccount = false): Promise<void> {
    const response = await this.post<LoginResponse>(withAddAccount('auth/mfa/login-verify', addAccount), {
      email,
      code,
      rememberMe: true,
      mfaToken,
    });
    this.apply(toState(response));
  }

  /** The other accounts this browser is signed in to; the httpOnly cookies say which. */
  savedAccounts(): Promise<SavedAccount[]> {
    return firstValueFrom(
      this.http.get<SavedAccount[]>(`${this.api}/auth/accounts`, { withCredentials: true }),
    );
  }

  /**
   * Makes a kept account the one in use; the account being left is kept in its place. The caller
   * reloads the app afterwards: screens and services still hold the previous account's data.
   */
  async switchAccount(userKey: string): Promise<void> {
    const response = await this.post<LoginResponse>('auth/accounts/switch', { userKey });
    this.mfaEnabled.set(null);
    this.apply(toState(response));
  }

  /**
   * One request at a time: every caller waiting on a 401 or a stale-access answer shares it. What
   * the account may do comes back with the token, so an office given or taken elsewhere reaches the
   * menu without signing in again.
   */
  refresh(): Promise<string> {
    this.refreshing ??= this.post<RefreshResponse>('auth/refresh', {})
      .then(({ accessToken, access }) => {
        const current = this.state();
        if (current) {
          this.apply({
            ...current,
            accessToken,
            ...(access && {
              isAdmin: access.isAdmin,
              roles: access.roles ?? [],
              permissions: access.permissions ?? [],
              kurinKey: access.kurinKey ?? null,
            }),
          });
        }
        return accessToken;
      })
      .finally(() => (this.refreshing = null));
    return this.refreshing;
  }

  /**
   * The access token once a renewal already under way has finished (null if it failed or none is
   * running), so requests sent while the app opens don't go out without one only to be retried.
   */
  async tokenWhenReady(): Promise<string | null> {
    if (this.refreshing) await this.refreshing.catch(() => null);
    return this.accessToken();
  }

  /**
   * True when there is a session to open the app with. A stored session counts straight away, like
   * a native app that opens signed in; the access token is renewed from the cookie in the background,
   * and only a server that refuses it signs the person out. Offline or a slow network never does.
   */
  ensureSession(): boolean {
    if (!this.state()) return false;
    if (!this.accessToken()) {
      this.refresh().catch((error: unknown) => {
        if (isOffline(error)) return;
        this.forget();
        void this.router.navigateByUrl('/login', { replaceUrl: true });
      });
    }
    return true;
  }

  /** Ends the session on the server first, so the request still carries the token. */
  async logout(): Promise<void> {
    try {
      if (this.state()) {
        if (!this.accessToken()) await this.refresh();
        await this.post('auth/logout', {}, 'text');
      }
    } catch {
      // Signed out locally either way; the server session expires on its own.
    } finally {
      this.forget();
    }
  }

  /** Whether the account has the second factor on, as last heard from the server; null until asked. */
  readonly mfaEnabled = signal<boolean | null>(null);

  async mfaStatus(): Promise<MfaStatus> {
    const status = await firstValueFrom(
      this.http.get<MfaStatus>(`${this.api}/auth/mfa/status`, { withCredentials: true }),
    );
    this.mfaEnabled.set(status.isMfaEnabled);
    return status;
  }

  mfaSetup(): Promise<MfaSetup> {
    return firstValueFrom(this.http.get<MfaSetup>(`${this.api}/auth/mfa/setup`, { withCredentials: true }));
  }

  /**
   * Turning the second factor on ends every session and hands this device a new one. Its token has
   * to replace the old one: the old token is still valid for minutes, but the privileged-MFA gate
   * reads the second factor off the token and would refuse every save.
   */
  async enableMfa(code: string): Promise<string[]> {
    const response = await this.post<MfaEnabled>('auth/mfa/enable', { code });
    const accessToken = response.tokens?.accessToken;
    const current = this.state();
    if (accessToken && current) this.apply({ ...current, accessToken });
    sessionStorage.setItem(mfaCheckedKey(current?.userKey), 'true');
    this.mfaEnabled.set(true);
    return response.recoveryCodes ?? [];
  }

  /**
   * Whether this account still has to turn on the second factor before using the app. Asked once
   * per browser session; a failed check lets the person in, and the server still refuses changes.
   */
  async mfaSetupRequired(): Promise<boolean> {
    const key = mfaCheckedKey(this.state()?.userKey);
    if (sessionStorage.getItem(key) === 'true') return false;
    try {
      const status = await this.mfaStatus();
      const required = status.isMfaRequired && !status.isMfaEnabled;
      if (!required) sessionStorage.setItem(key, 'true');
      return required;
    } catch {
      return false;
    }
  }

  /**
   * A new set of recovery codes; the old set stops working. Asks for the password, as the web does,
   * so a phone left unlocked cannot hand them out.
   */
  async rotateRecoveryCodes(currentPassword: string): Promise<string[]> {
    const response = await this.post<MfaRecoveryCodes>('auth/mfa/recovery-codes', { currentPassword });
    return response.recoveryCodes ?? [];
  }

  /**
   * The second factor was switched off or reset from the account page. Провід must set it up anew
   * before going on, so the guard asks the server again this session.
   */
  mfaTurnedOff(): void {
    this.mfaEnabled.set(false);
    sessionStorage.removeItem(mfaCheckedKey(this.state()?.userKey));
  }

  /** The kurins this account may act in, from the server, so the choice matches what it allows. */
  kurinScopeOptions(): Promise<KurinScopeOption[]> {
    return firstValueFrom(
      this.http.get<KurinScopeOption[]>(`${this.api}/auth/kurin-scope/options`, { withCredentials: true }),
    );
  }

  /**
   * Acts in another kurin. Rights depend on the kurin, so this is a new token rather than a filter:
   * nothing changes until the server has issued it (and rotated the refresh cookie with it).
   */
  async setKurinScope(kurinKey: string): Promise<void> {
    const response = await this.post<LoginResponse>('auth/kurin-scope', { kurinKey });
    this.apply(toState(response));
  }

  /** The account's email once the server has changed it (not while it waits for confirmation). */
  updateEmail(email: string): void {
    const current = this.state();
    if (current) this.apply({ ...current, email });
  }

  forget(): void {
    this.state.set(null);
    this.mfaEnabled.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private apply(state: AuthState): void {
    this.state.set(state);
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...state, accessToken: null }));
  }

  private post<T>(path: string, body: unknown, responseType: 'json' | 'text' = 'json'): Promise<T> {
    return firstValueFrom(
      this.http.post<T>(`${this.api}/${path}`, body, {
        withCredentials: true,
        responseType: responseType as 'json',
      }),
    );
  }
}

/** What to tell a person whose sign-in failed. The API's own messages are English and technical. */
export function loginErrorText(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    if (isOffline(error)) return 'Немає зв’язку з сервером. Перевір інтернет.';
    // A pause on the email (429 TooManyAttempts) lasts 15 minutes; only the address limiter is a minute.
    const code = (error.error as { error?: string } | null)?.error;
    if (code && ERROR_TEXT[code]) return ERROR_TEXT[code];
    if (error.status === 429) return 'Забагато спроб. Спробуй за хвилину.';
  }
  return fallback;
}

/**
 * No answer from the API. In the browser that is status 0; once the service worker is in charge,
 * it answers a failed network request with its own 504 instead.
 */
export function isOffline(error: unknown): boolean {
  return error instanceof HttpErrorResponse && (error.status === 0 || error.status === 504);
}

function withAddAccount(path: string, addAccount: boolean): string {
  return addAccount ? `${path}?addAccount=true` : path;
}

function mfaCheckedKey(userKey: string | undefined): string {
  return `mfa-status-checked:${userKey ?? ''}`;
}

function toState(response: LoginResponse): AuthState {
  if (!response.tokens) throw new Error('No tokens in the sign-in response');
  return {
    userKey: response.userKey,
    memberKey: response.memberKey,
    email: response.email,
    isAdmin: response.isAdmin,
    permissions: response.permissions ?? [],
    roles: response.roles ?? [],
    kurinKey: response.kurinKey ?? null,
    accessToken: response.tokens.accessToken,
  };
}

function readStoredState(): AuthState | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as AuthState;
    return parsed?.userKey ? { ...parsed, accessToken: null } : null;
  } catch {
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}
