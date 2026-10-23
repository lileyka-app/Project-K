import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AgendaRsvpStatus } from '../../kurinModule/models/agenda';
import { MyDuesDto, MyDutiesDto, MyDutyDto, MyEventDto, MyGroupDto, MyGrowthDto, MyScoreDto, MyTaskDto } from '../models/me.dto';

/**
 * What the dashboard reads about the person behind the token, across every kurin. Not cached: a
 * dashboard is opened to see what is new, and these reads are small.
 */
@Injectable({ providedIn: 'root' })
export class MeService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/me`;

  getEvents(days = 14): Observable<MyEventDto[]> {
    return this.http.get<MyEventDto[]>(`${this.apiUrl}/events`, { params: new HttpParams().set('days', String(days)) });
  }

  /** The answer to one occurrence; `occurrenceStartUtc` is null for a one-off event. */
  setEventResponse(agendaItemKey: string, status: AgendaRsvpStatus, occurrenceStartUtc: string | null): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/events/${agendaItemKey}/response`, { status, occurrenceStartUtc });
  }

  getTasks(): Observable<MyTaskDto[]> {
    return this.http.get<MyTaskDto[]>(`${this.apiUrl}/tasks`);
  }

  getGroups(): Observable<MyGroupDto[]> {
    return this.http.get<MyGroupDto[]>(`${this.apiUrl}/groups`);
  }

  getGrowth(): Observable<MyGrowthDto> {
    return this.http.get<MyGrowthDto>(`${this.apiUrl}/growth`);
  }

  getDues(): Observable<MyDuesDto[]> {
    return this.http.get<MyDuesDto[]>(`${this.apiUrl}/dues`);
  }

  getScore(): Observable<MyScoreDto[]> {
    return this.http.get<MyScoreDto[]>(`${this.apiUrl}/score`);
  }

  getDuties(): Observable<MyDutiesDto> {
    return this.http.get<MyDutiesDto | LegacyDuty[]>(`${this.apiUrl}/duties/board`).pipe(map(dutiesOf));
  }

  /** Hides one duty from the person's own «Справи» until it grows. */
  dismissDuty(duty: MyDutyDto): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/duties/dismissed`, { key: duty.key, fingerprint: duty.fingerprint });
  }

  /** Brings hidden duties back: one by its key, or all of them. */
  restoreDuties(key?: string): Observable<unknown> {
    const params = key ? new HttpParams().set('key', key) : undefined;
    return this.http.delete(`${this.apiUrl}/duties/dismissed`, { params });
  }
}

/** A duty as a server that cannot hide one sends it. */
type LegacyDuty = Omit<MyDutyDto, 'key' | 'fingerprint'> & Partial<Pick<MyDutyDto, 'key' | 'fingerprint'>>;

/**
 * The static demo's recordings, made before duties could be hidden, answer with the bare list; so
 * would a server a deploy has not reached yet. Either reads as nothing hidden and nothing to hide.
 */
function dutiesOf(reply: MyDutiesDto | LegacyDuty[]): MyDutiesDto {
  if (Array.isArray(reply)) {
    return { items: reply.map(duty => ({ ...duty, key: duty.key ?? '', fingerprint: duty.fingerprint ?? '' })), hiddenCount: 0 };
  }
  return reply;
}
