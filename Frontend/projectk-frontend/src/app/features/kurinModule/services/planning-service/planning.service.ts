import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { PlanningSessionDto } from '../../models/planning-session.dto';
import { tap } from 'rxjs';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { ENTITY_CACHE_TTL_MS, PLANNING_CACHE_PREFIX } from '../client-cache/cache-policy';

@Injectable({ providedIn: 'root' })
export class PlanningService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/planning`;

  getSessions(kurinKey: string) {
    return this.cache.get(
      `${PLANNING_CACHE_PREFIX}list:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<PlanningSessionDto[]>(`${this.apiUrl}/${kurinKey}`)
    );
  }

  getSessionByKey(sessionKey: string) {
    return this.cache.get(
      `${PLANNING_CACHE_PREFIX}by-key:${sessionKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<PlanningSessionDto>(`${this.apiUrl}/session/${sessionKey}`)
    );
  }

  createSession(payload: unknown) {
    return this.http.post(`${this.apiUrl}`, payload).pipe(
      tap(() => this.invalidatePlanningData())
    );
  }

  deleteSession(id: string) {
    return this.http.delete(`${this.apiUrl}/${id}`, {
      responseType: 'text'
    }).pipe(
      tap(() => this.invalidatePlanningData())
    );
  }

  private invalidatePlanningData(): void {
    this.cache.invalidateByPrefix(PLANNING_CACHE_PREFIX);
  }
}
