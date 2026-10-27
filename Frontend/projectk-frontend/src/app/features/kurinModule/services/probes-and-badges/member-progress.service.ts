import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { BadgeProgressDto } from '../../models/probes-and-badges/badge-progress.dto';
import { ProbeProgressDto } from '../../models/probes-and-badges/probe-progress.dto';
import { ProbeSummaryDto } from '../../models/probes-and-badges/probe-summary.dto';
import { ReviewBadgeProgressRequestDto } from '../../models/probes-and-badges/requests/review-badge-progress-request.dto';
import { SubmitBadgeProgressRequestDto } from '../../models/probes-and-badges/requests/submit-badge-progress-request.dto';
import { UpdateProbePointSignatureRequestDto } from '../../models/probes-and-badges/requests/update-probe-point-signature-request.dto';
import { UpdateProbeProgressStatusRequestDto } from '../../models/probes-and-badges/requests/update-probe-progress-status-request.dto';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { ENTITY_CACHE_TTL_MS, MEMBER_PROGRESS_CACHE_PREFIX } from '../client-cache/cache-policy';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';

@Injectable({
  providedIn: 'root'
})
export class MemberProgressService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/member`;

  getBadgeProgresses(memberKey: string): Observable<BadgeProgressDto[]> {
    return this.cache.get(
      `${MEMBER_PROGRESS_CACHE_PREFIX}badges:${memberKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<BadgeProgressDto[]>(`${this.apiUrl}/${memberKey}/badges/progress`)
    );
  }

  submitBadgeProgress(
    memberKey: string,
    badgeId: string,
    request: SubmitBadgeProgressRequestDto
  ): Observable<BadgeProgressDto> {
    return this.http.post<BadgeProgressDto>(
      `${this.apiUrl}/${memberKey}/badges/${badgeId}/submit`,
      request
    ).pipe(
      tap(() => this.invalidateMemberProgress(memberKey))
    );
  }

  reviewBadgeProgress(
    memberKey: string,
    badgeId: string,
    request: ReviewBadgeProgressRequestDto
  ): Observable<BadgeProgressDto> {
    return this.http.post<BadgeProgressDto>(
      `${this.apiUrl}/${memberKey}/badges/${badgeId}/review`,
      request,
      { context: requestFeedback('auto', [403, 409]) }
    ).pipe(
      tap(() => this.invalidateMemberProgress(memberKey))
    );
  }

  getBadgeReviewQueue(kurinKey: string): Observable<BadgeProgressDto[]> {
    return this.cache.get(
      `${MEMBER_PROGRESS_CACHE_PREFIX}badge-review-queue:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<BadgeProgressDto[]>(`${environment.apiUrl}/kurin/${kurinKey}/badges/review`)
    );
  }

  /**
   * Проби, якими людина йде, по черзі: редакція її гуртка, якщо закрита проба не тримає її в іншій.
   * Не кешується — програму гуртка можуть перемкнути будь-коли.
   */
  getProbeLine(memberKey: string): Observable<ProbeSummaryDto[]> {
    return this.http.get<ProbeSummaryDto[]>(`${this.apiUrl}/${memberKey}/probes/line`);
  }

  getProbeProgress(memberKey: string, probeId: string): Observable<ProbeProgressDto> {
    return this.cache.get(
      `${MEMBER_PROGRESS_CACHE_PREFIX}probe:${memberKey}:${probeId}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<ProbeProgressDto>(`${this.apiUrl}/${memberKey}/probes/${probeId}/progress`)
    );
  }

  updateProbeProgressStatus(
    memberKey: string,
    probeId: string,
    request: UpdateProbeProgressStatusRequestDto
  ): Observable<ProbeProgressDto> {
    return this.http.put<ProbeProgressDto>(
      `${this.apiUrl}/${memberKey}/probes/${probeId}/progress/status`,
      request,
      { context: requestFeedback('auto', [409]) }
    ).pipe(
      tap(() => this.invalidateMemberProgress(memberKey, probeId))
    );
  }

  signProbePoint(
    memberKey: string,
    probeId: string,
    pointId: string,
    request: UpdateProbePointSignatureRequestDto
  ): Observable<ProbeProgressDto> {
    return this.http.put<ProbeProgressDto>(
      `${this.apiUrl}/${memberKey}/probes/${probeId}/points/${pointId}/sign`,
      request,
      { context: requestFeedback('auto', [409]) }
    ).pipe(
      tap(() => this.invalidateMemberProgress(memberKey, probeId))
    );
  }

  unsignProbePoint(
    memberKey: string,
    probeId: string,
    pointId: string,
    request: UpdateProbePointSignatureRequestDto
  ): Observable<ProbeProgressDto> {
    return this.http.put<ProbeProgressDto>(
      `${this.apiUrl}/${memberKey}/probes/${probeId}/points/${pointId}/unsign`,
      request,
      { context: requestFeedback('auto', [409]) }
    ).pipe(
      tap(() => this.invalidateMemberProgress(memberKey, probeId))
    );
  }

  private invalidateMemberProgress(memberKey: string, probeId?: string): void {
    this.cache.invalidate(`${MEMBER_PROGRESS_CACHE_PREFIX}badges:${memberKey}`);
    this.cache.invalidateByPrefix(`${MEMBER_PROGRESS_CACHE_PREFIX}badge-review-queue:`);

    if (probeId) {
      this.cache.invalidate(`${MEMBER_PROGRESS_CACHE_PREFIX}probe:${memberKey}:${probeId}`);
    } else {
      this.cache.invalidateByPrefix(`${MEMBER_PROGRESS_CACHE_PREFIX}probe:${memberKey}:`);
    }
  }
}
