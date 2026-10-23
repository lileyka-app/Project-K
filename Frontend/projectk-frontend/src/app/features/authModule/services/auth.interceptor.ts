/* eslint-disable @typescript-eslint/no-explicit-any */
import { inject, Injectable } from "@angular/core";
import {
    HttpInterceptor,
    HttpRequest,
    HttpHandler,
    HttpEvent,
    HttpErrorResponse,
    HttpHeaders,
    HttpResponse
} from "@angular/common/http";
import { AuthService } from "./auth-service/auth.service";
import { Observable, throwError, BehaviorSubject } from "rxjs";
import { catchError, switchMap, filter, take, finalize, tap } from "rxjs/operators";
import { Router } from "@angular/router";

/** Set by the API when the token predates a change to what the caller may do. */
export const ACCESS_STALE_HEADER = 'X-Access-Stale';

/** How often a refusal may trigger a refresh, so a page that is simply not this person's cannot loop. */
export const DENIED_REFRESH_THROTTLE_MS = 30 * 1000;

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);

    private isRefreshing = false;
    private readonly refreshTokenSubject = new BehaviorSubject<string | null>(null);
    private lastDeniedRefreshAt = Number.NEGATIVE_INFINITY;

    intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        // Skip refresh token requests to avoid loops
        if (req.url.includes('/api/auth/refresh') || req.url.includes('/health')) {
            return next.handle(req);
        }

        const authReq = this.addAuthHeader(req);
        return next.handle(authReq).pipe(
            tap(event => {
                if (event instanceof HttpResponse) {
                    this.refreshIfStale(authReq, event.headers);
                }
            }),
            catchError(error => this.handleError(error, req, authReq, next))
        );
    }

    private addAuthHeader(req: HttpRequest<any>): HttpRequest<any> {
        const token = this.authService.getAccessToken();
        if (!token) return req;

        return req.clone({
            setHeaders: { Authorization: `Bearer ${token}` }
        });
    }

    private handleError(error: any, req: HttpRequest<any>, authReq: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        if (error instanceof HttpErrorResponse && error.status === 401) {
            if (this.shouldSkipRefreshOnUnauthorized(req)) {
                return throwError(() => error);
            }
            return this.tryRefreshAndRepeat(req, next);
        }

        if (error instanceof HttpErrorResponse
            && !this.refreshIfStale(authReq, error.headers)
            && this.isResourceAccessDenied(error)) {
            this.refreshAfterDenial();
        }

        // 403 is passed through, not navigated on. Navigation belongs to the router's guards:
        // EntityGuard already turns a refusal on the page's own resource into /forbidden. Bouncing
        // the whole app from here meant any secondary request — a widget, a moderation action —
        // could yank the user off a page they legitimately hold, and it overrode the components
        // that deliberately answer 403 inline (member-card, skills-review-page), whose messages
        // were never seen.
        return throwError(() => error);
    }

    /**
     * Refreshes in the background when the server says the token is stale. Only a request sent with
     * the token still in use counts: once a refresh has replaced it, the answers to requests that
     * went out before would each ask for another one.
     */
    private refreshIfStale(authReq: HttpRequest<any>, headers: HttpHeaders | null | undefined): boolean {
        if (headers?.get(ACCESS_STALE_HEADER) !== '1') {
            return false;
        }

        const current = this.authService.getAccessToken();
        if (!current || authReq.headers.get('Authorization') !== `Bearer ${current}`) {
            return false;
        }

        this.refreshSilently();
        return true;
    }

    private isResourceAccessDenied(error: HttpErrorResponse): boolean {
        return error.status === 403 && error.error?.error === 'ResourceAccessDenied';
    }

    /**
     * A refusal can mean an office was taken away on another instance, where no stale mark reached
     * this one. A refresh brings the menu in line.
     */
    private refreshAfterDenial(): void {
        const now = Date.now();
        if (!this.authService.getAccessToken() || now - this.lastDeniedRefreshAt < DENIED_REFRESH_THROTTLE_MS) {
            return;
        }

        this.lastDeniedRefreshAt = now;
        this.refreshSilently();
    }

    // AuthService.refreshToken is single-flight, so a burst of stale answers costs one refresh. A
    // failure is left alone: the next request meets a 401 and the usual path takes over.
    private refreshSilently(): void {
        this.authService.refreshToken().subscribe({ error: () => undefined });
    }

    private shouldSkipRefreshOnUnauthorized(req: HttpRequest<any>): boolean {
        return req.url.includes('/api/auth/login')
            || req.url.includes('/api/auth/mfa/login-verify')
            || req.url.includes('/api/auth/logout')
            || req.url.includes('/api/auth/refresh');
    }

    private tryRefreshAndRepeat(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        if (this.isRefreshing) {
            return this.refreshTokenSubject.pipe(
                filter(token => token !== null),
                take(1),
                switchMap(token => {
                    if (token) {
                        return next.handle(this.addTokenToRequest(req, token));
                    }
                    return throwError(() => new Error('Session expired'));
                })
            );
        } else {
            this.isRefreshing = true;
            this.refreshTokenSubject.next(null);

            return this.authService.refreshToken().pipe(
                switchMap(newToken => {
                    this.isRefreshing = false;
                    this.refreshTokenSubject.next(newToken);
                    return next.handle(this.addTokenToRequest(req, newToken));
                }),
                catchError(() => {
                    this.isRefreshing = false;
                    this.refreshTokenSubject.next(null);
                    this.authService.clearLocalState();
                    this.router.navigate(['/login'], { replaceUrl: true });
                    return throwError(() => new Error('Session expired'));
                }),
                finalize(() => {
                    this.isRefreshing = false;
                })
            );
        }
    }

    private addTokenToRequest(req: HttpRequest<any>, token: string): HttpRequest<any> {
        return req.clone({
            setHeaders: { Authorization: `Bearer ${token}` }
        });
    }
}
