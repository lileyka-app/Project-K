import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { KurinBranchRequestDto, SubmitKurinBranchRequest } from '../../models/kurin-branch-request.dto';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { KURIN_CACHE_PREFIX } from '../client-cache/cache-policy';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';

/**
 * Запити старших куренів змінити гілки й числа: курінь подає зі своїх налаштувань, адміністратор
 * вирішує поруч із заявками.
 */
@Injectable({ providedIn: 'root' })
export class KurinBranchRequestService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/kurin`;

  /** Запит, що чекає рішення, або null. */
  getOpen(kurinKey: string): Observable<KurinBranchRequestDto | null> {
    return this.http.get<KurinBranchRequestDto | null>(`${this.apiUrl}/${kurinKey}/branch-request`, {
      context: requestFeedback('silent')
    });
  }

  submit(kurinKey: string, request: SubmitKurinBranchRequest): Observable<KurinBranchRequestDto> {
    return this.http.post<KurinBranchRequestDto>(`${this.apiUrl}/${kurinKey}/branch-request`, request, {
      context: requestFeedback('silent')
    });
  }

  getPending(): Observable<KurinBranchRequestDto[]> {
    return this.http.get<KurinBranchRequestDto[]>(`${this.apiUrl}/branch-requests`, { context: requestFeedback('silent') });
  }

  approve(requestKey: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/branch-requests/${requestKey}/approve`, {}, { context: requestFeedback('silent') })
      .pipe(tap(() => this.cache.invalidateByPrefix(KURIN_CACHE_PREFIX)));
  }

  reject(requestKey: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/branch-requests/${requestKey}/reject`, {}, { context: requestFeedback('silent') });
  }
}
