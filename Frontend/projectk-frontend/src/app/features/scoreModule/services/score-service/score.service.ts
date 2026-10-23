import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../../../kurinModule/services/client-cache/client-cache.service';
import { ENTITY_CACHE_TTL_MS, SCORE_CACHE_PREFIX } from '../../../kurinModule/services/client-cache/cache-policy';
import {
  AttendanceSheetDto,
  GroupScoreDto,
  KurinScoreDto,
  KurinScoreSettingsDto,
  MarkAttendanceResultDto,
  ScorePeriodQuery,
  SetScoreAttendanceRateRequest,
  SetScoreRuleRequest,
  UpsertScoreEntryRequest,
  UpsertScoreItemRequest,
  UpsertScoreStageRequest
} from '../../models/score.dto';
import { ScoreAlgorithm } from '../../models/score.enums';
import { PrivateScoreDto, UpsertPrivateScoreCriterionRequest, UpsertPrivateScoreEntryRequest } from '../../models/private-score.dto';

@Injectable({ providedIn: 'root' })
export class ScoreService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);

  private url(kurinKey: string): string {
    return `${environment.apiUrl}/kurin/${kurinKey}/score`;
  }

  getKurinScore(kurinKey: string, period: ScorePeriodQuery = {}): Observable<KurinScoreDto> {
    return this.cache.get(
      `${SCORE_CACHE_PREFIX}kurin:${kurinKey}:${periodKey(period)}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<KurinScoreDto>(this.url(kurinKey), { params: periodHttpParams(period) })
    );
  }

  getGroupScore(kurinKey: string, groupKey: string, period: ScorePeriodQuery = {}): Observable<GroupScoreDto> {
    return this.cache.get(
      `${SCORE_CACHE_PREFIX}group:${groupKey}:${periodKey(period)}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<GroupScoreDto>(`${this.url(kurinKey)}/groups/${groupKey}`, { params: periodHttpParams(period) })
    );
  }

  /** The sheet is what судді edit on the spot, so it is never served from the cache. */
  getSheet(kurinKey: string, agendaItemKey: string, occurrence: string): Observable<AttendanceSheetDto> {
    return this.http.get<AttendanceSheetDto>(`${this.url(kurinKey)}/events/${agendaItemKey}/${occurrence}`);
  }

  markAttendance(kurinKey: string, agendaItemKey: string, occurrence: string, membershipKeys: string[]): Observable<MarkAttendanceResultDto[]> {
    return this.http
      .post<MarkAttendanceResultDto[]>(`${this.url(kurinKey)}/events/${agendaItemKey}/${occurrence}/attendance`, { membershipKeys })
      .pipe(this.invalidate());
  }

  unmarkAttendance(kurinKey: string, agendaItemKey: string, occurrence: string, membershipKey: string): Observable<unknown> {
    return this.http
      .delete(`${this.url(kurinKey)}/events/${agendaItemKey}/${occurrence}/attendance/${membershipKey}`)
      .pipe(this.invalidate());
  }

  /** «Сходин не було», or taking it back; the server refuses the first (409) while anyone is marked. */
  setNotHeld(kurinKey: string, agendaItemKey: string, occurrence: string, notHeld: boolean): Observable<unknown> {
    const url = `${this.url(kurinKey)}/events/${agendaItemKey}/${encodeURIComponent(occurrence)}/not-held`;
    return notHeld ? this.http.put(url, {}) : this.http.delete(url);
  }

  createEntry(kurinKey: string, request: UpsertScoreEntryRequest): Observable<unknown> {
    return this.http.post(`${this.url(kurinKey)}/entries`, request).pipe(this.invalidate());
  }

  updateEntry(kurinKey: string, entryKey: string, request: UpsertScoreEntryRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/entries/${entryKey}`, request).pipe(this.invalidate());
  }

  deleteEntry(kurinKey: string, entryKey: string): Observable<unknown> {
    return this.http.delete(`${this.url(kurinKey)}/entries/${entryKey}`).pipe(this.invalidate());
  }

  getSettings(kurinKey: string): Observable<KurinScoreSettingsDto> {
    return this.cache.get(
      `${SCORE_CACHE_PREFIX}settings:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<KurinScoreSettingsDto>(`${this.url(kurinKey)}/settings`)
    );
  }

  setAlgorithm(kurinKey: string, algorithm: ScoreAlgorithm): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/settings/algorithm`, { algorithm }).pipe(this.invalidate());
  }

  setRule(kurinKey: string, request: SetScoreRuleRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/settings/rules`, request).pipe(this.invalidate());
  }

  setAttendanceRate(kurinKey: string, request: SetScoreAttendanceRateRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/settings/attendance-rates`, request).pipe(this.invalidate());
  }

  createItem(kurinKey: string, request: UpsertScoreItemRequest): Observable<unknown> {
    return this.http.post(`${this.url(kurinKey)}/settings/items`, request).pipe(this.invalidate());
  }

  updateItem(kurinKey: string, itemKey: string, request: UpsertScoreItemRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/settings/items/${itemKey}`, request).pipe(this.invalidate());
  }

  createStage(kurinKey: string, request: UpsertScoreStageRequest): Observable<unknown> {
    return this.http.post(`${this.url(kurinKey)}/settings/stages`, request).pipe(this.invalidate());
  }

  updateStage(kurinKey: string, stageKey: string, request: UpsertScoreStageRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/settings/stages/${stageKey}`, request).pipe(this.invalidate());
  }

  deleteStage(kurinKey: string, stageKey: string): Observable<unknown> {
    return this.http.delete(`${this.url(kurinKey)}/settings/stages/${stageKey}`).pipe(this.invalidate());
  }

  // The КВ's own book is never cached: it is private.

  getPrivateScore(kurinKey: string, period: ScorePeriodQuery = {}): Observable<PrivateScoreDto> {
    return this.http.get<PrivateScoreDto>(`${this.url(kurinKey)}/private`, { params: periodHttpParams(period) });
  }

  createPrivateEntry(kurinKey: string, request: UpsertPrivateScoreEntryRequest): Observable<unknown> {
    return this.http.post(`${this.url(kurinKey)}/private/entries`, request);
  }

  updatePrivateEntry(kurinKey: string, entryKey: string, request: UpsertPrivateScoreEntryRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/private/entries/${entryKey}`, request);
  }

  deletePrivateEntry(kurinKey: string, entryKey: string): Observable<unknown> {
    return this.http.delete(`${this.url(kurinKey)}/private/entries/${entryKey}`);
  }

  createPrivateCriterion(kurinKey: string, request: UpsertPrivateScoreCriterionRequest): Observable<unknown> {
    return this.http.post(`${this.url(kurinKey)}/private/criteria`, request);
  }

  updatePrivateCriterion(kurinKey: string, criterionKey: string, request: UpsertPrivateScoreCriterionRequest): Observable<unknown> {
    return this.http.put(`${this.url(kurinKey)}/private/criteria/${criterionKey}`, request);
  }

  /** Every write moves a total somewhere, so every cached read of the score goes. */
  private invalidate<T>() {
    return tap<T>(() => this.cache.invalidateByPrefix(SCORE_CACHE_PREFIX));
  }
}

function periodKey(period: ScorePeriodQuery): string {
  if (period.stageKey) {
    return `stage:${period.stageKey}`;
  }
  return period.year ? `year:${period.year}` : 'now';
}

function periodHttpParams(period: ScorePeriodQuery): HttpParams {
  let params = new HttpParams();
  if (period.stageKey) {
    params = params.set('stageKey', period.stageKey);
  } else if (period.year) {
    params = params.set('year', String(period.year));
  }
  return params;
}
