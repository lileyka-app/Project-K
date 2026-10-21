import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../../../kurinModule/services/client-cache/client-cache.service';
import { DUES_CACHE_PREFIX, ENTITY_CACHE_TTL_MS } from '../../../kurinModule/services/client-cache/cache-policy';
import {
  GroupDuesDto,
  DuesGroupLinkDto,
  KurinDuesDto,
  MemberDuesDto,
  SetDuesConcessionRequest,
  SetGroupDuesRateRequest,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest
} from '../../models/group-dues.dto';
import { GrantDuesConcessionRequest, UpdateDuesConcessionGrantRequest } from '../../models/concession.dto';

@Injectable({ providedIn: 'root' })
export class DuesService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/group`;
  private readonly kurinApiUrl = `${environment.apiUrl}/kurin`;

  getGroupDues(groupKey: string): Observable<GroupDuesDto> {
    return this.cache.get(
      `${DUES_CACHE_PREFIX}group:${groupKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<GroupDuesDto>(`${this.apiUrl}/${groupKey}/dues`)
    );
  }

  getMemberDues(memberKey: string): Observable<MemberDuesDto> {
    return this.cache.get(
      `${DUES_CACHE_PREFIX}member:${memberKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MemberDuesDto>(`${environment.apiUrl}/member/${memberKey}/dues`)
    );
  }

  setGroupRate(groupKey: string, request: SetGroupDuesRateRequest): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${groupKey}/dues/rate`, request).pipe(this.invalidate());
  }

  getKurinDues(kurinKey: string): Observable<KurinDuesDto> {
    return this.cache.get(
      `${DUES_CACHE_PREFIX}kurin:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<KurinDuesDto>(`${this.kurinApiUrl}/${kurinKey}/dues`)
    );
  }

  /** The гуртки whose box the caller may open; empty for a youth. */
  getReadableGroups(kurinKey: string): Observable<DuesGroupLinkDto[]> {
    return this.cache.get(
      `${DUES_CACHE_PREFIX}groups:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<DuesGroupLinkDto[]>(`${this.kurinApiUrl}/${kurinKey}/dues/groups`)
    );
  }

  setTransferReceived(kurinKey: string, entryKey: string, isReceived: boolean): Observable<unknown> {
    return this.http.put(`${this.kurinApiUrl}/${kurinKey}/dues/transfers/${entryKey}/received`, { isReceived }).pipe(this.invalidate());
  }

  createKurinEntry(kurinKey: string, request: UpsertDuesEntryRequest): Observable<unknown> {
    return this.http.post(`${this.kurinApiUrl}/${kurinKey}/dues/entries`, request).pipe(this.invalidate());
  }

  updateKurinEntry(kurinKey: string, entryKey: string, request: UpsertDuesEntryRequest): Observable<unknown> {
    return this.http.put(`${this.kurinApiUrl}/${kurinKey}/dues/entries/${entryKey}`, request).pipe(this.invalidate());
  }

  deleteKurinEntry(kurinKey: string, entryKey: string): Observable<unknown> {
    return this.http.delete(`${this.kurinApiUrl}/${kurinKey}/dues/entries/${entryKey}`).pipe(this.invalidate());
  }

  setKurinEntryVerified(kurinKey: string, entryKey: string, isVerified: boolean): Observable<unknown> {
    return this.http.put(`${this.kurinApiUrl}/${kurinKey}/dues/entries/${entryKey}/verified`, { isVerified }).pipe(this.invalidate());
  }

  setKurinRate(kurinKey: string, request: SetKurinDuesRateRequest): Observable<unknown> {
    return this.http.put(`${this.kurinApiUrl}/${kurinKey}/dues/rate`, request).pipe(this.invalidate());
  }

  setConcession(groupKey: string, membershipKey: string, request: SetDuesConcessionRequest): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${groupKey}/dues/members/${membershipKey}/concession`, request).pipe(this.invalidate());
  }

  grantConcession(groupKey: string, membershipKey: string, request: GrantDuesConcessionRequest): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/${groupKey}/dues/members/${membershipKey}/concessions`, request).pipe(this.invalidate());
  }

  updateConcessionGrant(groupKey: string, grantKey: string, request: UpdateDuesConcessionGrantRequest): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${groupKey}/dues/concessions/${grantKey}`, request).pipe(this.invalidate());
  }

  deleteConcessionGrant(groupKey: string, grantKey: string): Observable<unknown> {
    return this.http.delete(`${this.apiUrl}/${groupKey}/dues/concessions/${grantKey}`).pipe(this.invalidate());
  }

  createEntry(groupKey: string, request: UpsertDuesEntryRequest): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/${groupKey}/dues/entries`, request).pipe(this.invalidate());
  }

  updateEntry(groupKey: string, entryKey: string, request: UpsertDuesEntryRequest): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${groupKey}/dues/entries/${entryKey}`, request).pipe(this.invalidate());
  }

  deleteEntry(groupKey: string, entryKey: string): Observable<unknown> {
    return this.http.delete(`${this.apiUrl}/${groupKey}/dues/entries/${entryKey}`).pipe(this.invalidate());
  }

  setEntryVerified(groupKey: string, entryKey: string, isVerified: boolean): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${groupKey}/dues/entries/${entryKey}/verified`, { isVerified }).pipe(this.invalidate());
  }

  /** Every write changes balances and boxes, so every cached read of dues goes. */
  private invalidate<T>() {
    return tap<T>(() => this.cache.invalidateByPrefix(DUES_CACHE_PREFIX));
  }
}
