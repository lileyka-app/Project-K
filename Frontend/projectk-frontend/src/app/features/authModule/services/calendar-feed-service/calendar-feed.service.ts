import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';

export type CalendarFeedState = 'None' | 'Active' | 'Revoked';
export type CalendarFeedRevokeReason = 'Reissued' | 'Disabled' | 'SecurityChange' | 'Suspended';

export interface CalendarFeedOptions {
  includeSchedules: boolean;
  includeTasks: boolean;
}

export interface CalendarFeedStatus extends CalendarFeedOptions {
  state: CalendarFeedState;
  createdUtc: string | null;
  lastFetchedUtc: string | null;
  revokedUtc: string | null;
  revokedReason: CalendarFeedRevokeReason | null;
}

/** The only answer that ever carries the link: the server keeps its hash alone. */
export interface IssuedCalendarFeed {
  feedUrl: string;
  status: CalendarFeedStatus;
}

/** The account's calendar link for Google, Apple and Outlook (`api/me/calendar-feed`). */
@Injectable({ providedIn: 'root' })
export class CalendarFeedService {
  private readonly url = `${environment.apiUrl}/me/calendar-feed`;
  private readonly http = inject(HttpClient);

  getStatus(): Observable<CalendarFeedStatus> {
    return this.http.get<CalendarFeedStatus>(this.url, { withCredentials: true });
  }

  issue(options: CalendarFeedOptions): Observable<IssuedCalendarFeed> {
    return this.http.post<IssuedCalendarFeed>(this.url, options, { withCredentials: true });
  }

  update(options: CalendarFeedOptions): Observable<CalendarFeedStatus> {
    return this.http.patch<CalendarFeedStatus>(this.url, options, { withCredentials: true });
  }

  disable(): Observable<CalendarFeedStatus> {
    return this.http.delete<CalendarFeedStatus>(this.url, { withCredentials: true });
  }
}

/** `webcal://` makes Apple Calendar and Outlook open their own «subscribe» dialog. */
export function webcalUrlOf(feedUrl: string): string {
  return feedUrl.replace(/^https?:\/\//, 'webcal://');
}

/** Google's «add by URL», prefilled. */
export function googleCalendarUrlOf(feedUrl: string): string {
  return `https://calendar.google.com/calendar/r?cid=${encodeURIComponent(webcalUrlOf(feedUrl))}`;
}

export function revokeReasonText(reason: CalendarFeedRevokeReason | null): string {
  switch (reason) {
    case 'Reissued':
      return 'його замінило нове посилання';
    case 'Disabled':
      return 'його вимкнули в налаштуваннях';
    case 'Suspended':
      return 'акаунт було призупинено';
    default:
      return 'після зміни пароля, пошти чи двофакторного входу';
  }
}
