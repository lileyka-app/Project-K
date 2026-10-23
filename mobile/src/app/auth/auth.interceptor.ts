import { HttpErrorResponse, HttpHeaders, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, of, switchMap, tap, throwError } from 'rxjs';
import { AuthService, isOffline } from './auth.service';

/** Sign-in steps answer 401 for a wrong password or code; that is not an expired session. */
const NO_REFRESH = ['/auth/login', '/auth/mfa/login-verify', '/auth/logout', '/auth/refresh', '/auth/accounts'];

/** Set by the API when the token predates a change to what the caller may do (web auth.interceptor). */
export const ACCESS_STALE_HEADER = 'X-Access-Stale';

/**
 * Adds the access token (waiting for a renewal already under way when there is none yet) and, on a
 * 401, renews it from the refresh cookie once and retries. An answer saying the token is stale —
 * an office or the kurin's rights changed since it was minted — renews it in the background.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const retryable = !NO_REFRESH.some((path) => req.url.includes(path));

  const token = auth.accessToken();
  const ready = retryable && !token ? from(auth.tokenWhenReady()) : of(token);

  return ready.pipe(
    switchMap((current) =>
      next(withToken(req, current)).pipe(
        // Not on the auth routes themselves: the refresh answer to an old token says stale too.
        tap((event) => {
          if (retryable && event instanceof HttpResponse) refreshIfStale(auth, current, event.headers);
        }),
        catchError((error: unknown) => {
          if (retryable && error instanceof HttpErrorResponse) refreshIfStale(auth, current, error.headers);
          return throwError(() => error);
        }),
      ),
    ),
    catchError((error: unknown) => {
      // A wrong current password on the account screens is a 401 too, but it names itself.
      const expired =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        (error.error as { error?: string } | null)?.error !== 'InvalidCredentials';
      if (!retryable || !expired || !auth.signedIn()) {
        return throwError(() => error);
      }
      return from(auth.refresh()).pipe(
        catchError((refreshError: unknown) => {
          if (!isOffline(refreshError)) {
            auth.forget();
            void router.navigateByUrl('/login', { replaceUrl: true });
          }
          return throwError(() => error);
        }),
        switchMap((token) => next(withToken(req, token))),
      );
    }),
  );
};

/**
 * Only an answer to the token still in use counts: once a refresh has replaced it, the answers to
 * requests sent before would each ask for another. The refresh is shared, so a burst costs one; a
 * failure is left to the next request's 401.
 */
function refreshIfStale(auth: AuthService, sentWith: string | null, headers: HttpHeaders | null | undefined): void {
  if (headers?.get(ACCESS_STALE_HEADER) !== '1' || !sentWith || sentWith !== auth.accessToken()) return;
  auth.refresh().catch(() => undefined);
}

function withToken<T>(req: HttpRequest<T>, token: string | null): HttpRequest<T> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}
