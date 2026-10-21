import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../../../../environments/environment';
import { map, Observable, tap, throwError } from 'rxjs';
import { MemberDto } from '../../models/member.dto';
import { MembershipDto } from '../../models/membership.dto';
import { UpsertMemberDto } from '../../models/requests/member/upsert-member.dto';
import { PlastLevelHistoryDto } from '../../models/plast-level-history.dto';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';
import { mapMemberForView } from '../../functions/member-view-mapper.function';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { ENTITY_CACHE_TTL_MS, MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';
import { toDateOnlyString } from '../../functions/to-date-only-string.function';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';

@Injectable({
  providedIn: 'root'
})
export class MemberService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/member`;

  getByKey(id: string): Observable<MemberDto> {
    return this.cache.get(
      `${MEMBER_CACHE_PREFIX}by-key:${id}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MemberDto>(`${this.apiUrl}/${id}`).pipe(
        map(member => mapMemberForView(member))
      )
    );
  }

  /**
   * Тека «Членства» з архівної коробки людини. Окремий запит, бо це фолдер чужого модуля: картка
   * відкриває його тоді, коли він потрібен, а не разом із профілем.
   */
  getMemberships(memberKey: string): Observable<MembershipDto[]> {
    return this.cache.get(
      `${MEMBER_CACHE_PREFIX}memberships:${memberKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MembershipDto[]>(`${this.apiUrl}/${memberKey}/dossier/memberships`)
    );
  }

  getAll(groupKey?: string, kurinKey?: string): Observable<MemberDto[]> {
    if (!groupKey && !kurinKey) {
      return throwError(() => new Error('Either groupKey or kurinKey must be provided.'));
    }

    const requestUrl = groupKey
      ? `${this.apiUrl}/groups/${groupKey}/members`
      : `${this.apiUrl}/kurins/${kurinKey}/members`;

    const cacheKey = groupKey
      ? `${MEMBER_CACHE_PREFIX}list:group:${groupKey}`
      : `${MEMBER_CACHE_PREFIX}list:kurin:${kurinKey}`;

    return this.cache.get(
      cacheKey,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MemberDto[]>(requestUrl).pipe(
        map(members => members.map(member => mapMemberForView(member)))
      )
    );
  }

  update(memberKey: string, request: UpsertMemberDto, file: Blob | null): Observable<MemberDto> {
    const formData = this.buildFormData(request, file);
    return this.http.put<MemberDto>(`${this.apiUrl}/${memberKey}`, formData).pipe(
      tap(() => this.invalidateMemberCache()),
      map(member => mapMemberForView(member))
    );
  }

  /** Новий лист-запрошення для неактивованого акаунта; попередні посилання перестають діяти. */
  resendInvitation(memberKey: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${memberKey}/invitation`, null, {
      context: requestFeedback('errors', [409, 500])
    });
  }

  delete(memberKey: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${memberKey}`).pipe(
      tap(() => this.invalidateMemberCache())
    );
  }

  verifyProfile(memberKey: string, note?: string | null): Observable<MemberDto> {
    return this.http.put<MemberDto>(`${this.apiUrl}/${memberKey}/profile-verification`, { note: note ?? null }, {
      context: requestFeedback('auto', [400])
    }).pipe(
      tap(() => this.invalidateMemberCache()),
      map(member => mapMemberForView(member))
    );
  }

  resetProfileVerification(memberKey: string): Observable<MemberDto> {
    return this.http.delete<MemberDto>(`${this.apiUrl}/${memberKey}/profile-verification`, { context: requestFeedback('auto', [400]) }).pipe(
      tap(() => this.invalidateMemberCache()),
      map(member => mapMemberForView(member))
    );
  }

  create(request: UpsertMemberDto, file: Blob | null): Observable<MemberDto> {
    if (!request.groupKey && !request.kurinKey) {
      return throwError(() => new Error('Either groupKey or kurinKey must be provided for create.'));
    }

    const requestUrl = request.groupKey
      ? `${this.apiUrl}`
      : `${this.apiUrl}/kurins/${request.kurinKey}/members`;

    const formData = this.buildFormData(request, file);
    return this.http.post<MemberDto>(requestUrl, formData).pipe(
      tap(() => this.invalidateMemberCache()),
      map(member => mapMemberForView(member))
    );
  }

  getKVMembers(kurinKey: string): Observable<MemberLookupDto[]> {
    return this.cache.get(
      `${MEMBER_CACHE_PREFIX}kv:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MemberLookupDto[]>(`${this.apiUrl}/members/kv/${kurinKey}`)
    );
  }

  getMentorCandidates(kurinKey: string): Observable<MemberLookupDto[]> {
    return this.cache.get(
      `${MEMBER_CACHE_PREFIX}mentor-candidates:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MemberLookupDto[]>(`${this.apiUrl}/members/mentor-candidates/${kurinKey}`)
    );
  }

  invalidateMemberCache(): void {
    this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
  }

  private buildFormData(dto: UpsertMemberDto, file: Blob | null, blobFieldName = 'blob'): FormData {
      const formData = new FormData();

      Object.entries(dto).forEach(([key, value]) => {
          if (value === null || value === undefined) {
              return;
          }

        if (key === 'plastLevelHistories' && Array.isArray(value)) {
            (value as PlastLevelHistoryDto[]).forEach((historyItem, index) => {
                Object.entries(historyItem).forEach(([itemKey, itemValue]) => {
                    if (itemValue !== null && itemValue !== undefined) {
                        const formattedKey = `${key}[${index}].${itemKey}`;
                        const formattedValue = itemValue instanceof Date
                          ? toDateOnlyString(itemValue)
                          : String(itemValue);
                        formData.append(formattedKey, formattedValue ?? '');
                    }
                });
            });
        }
          else if (key === 'cardExtras') {
              Object.entries(value as Record<string, string>).forEach(([field, text]) => formData.append(`cardExtras[${field}]`, text));
          }
          else if (value instanceof Date) {
              formData.append(key, toDateOnlyString(value) ?? '');
          }
          else {
              formData.append(key, value.toString());
          }
      });

      if (file) {
          const filename = (file as File).name || 'file';
          formData.append(blobFieldName, file, filename);
      }

      return formData;
  }
}
