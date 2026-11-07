import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { BadgeProgressDto } from '../../models/probes-and-badges/badge-progress.dto';
import { ProbeProgressDto } from '../../models/probes-and-badges/probe-progress.dto';
import { ProbeSummaryDto } from '../../models/probes-and-badges/probe-summary.dto';
import {
  ProbeProjectImageDto,
  ProbeProjectSavedDto,
  SaveProbeProjectRequestDto,
  SaveThirdProbeFileRequestDto,
  SetThirdProbeStagesRequestDto,
  ThirdProbeDto
} from '../../models/probes-and-badges/probe-project.dto';
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

  /** The «Третя проба» tab; not cached, since its file links expire. Every write answers with it too. */
  getThirdProbe(memberKey: string, probeId: string): Observable<ThirdProbeDto> {
    return this.http.get<ThirdProbeDto>(`${this.probeUrl(memberKey, probeId)}/third-probe`);
  }

  saveThirdProbeFile(memberKey: string, probeId: string, request: SaveThirdProbeFileRequestDto): Observable<ThirdProbeDto> {
    return this.http.put<ThirdProbeDto>(`${this.probeUrl(memberKey, probeId)}/third-probe`, request, { context: requestFeedback('auto', [400, 403]) });
  }

  /** Confirms (`sign`) or takes back the reflection with the Звʼязковий. */
  confirmReflection(memberKey: string, probeId: string, sign: boolean): Observable<ThirdProbeDto> {
    const url = `${this.probeUrl(memberKey, probeId)}/third-probe/reflection/confirmation`;
    return sign ? this.http.put<ThirdProbeDto>(url, {}, { context: requestFeedback('auto', [400]) }) : this.http.delete<ThirdProbeDto>(url);
  }

  /** The referent's stages, whole: a null clears the stage. */
  setThirdProbeStages(memberKey: string, probeId: string, stages: SetThirdProbeStagesRequestDto): Observable<ThirdProbeDto> {
    return this.http.put<ThirdProbeDto>(`${this.probeUrl(memberKey, probeId)}/third-probe/stages`, stages, {
      context: requestFeedback('auto', [400, 403])
    });
  }

  uploadCampCertificate(memberKey: string, probeId: string, file: File): Observable<ThirdProbeDto> {
    const form = new FormData();
    form.append('file', file, file.name || 'posvidka');
    return this.http.post<ThirdProbeDto>(`${this.probeUrl(memberKey, probeId)}/third-probe/camp/certificate`, form, {
      context: requestFeedback('silent', [400, 403, 413])
    });
  }

  deleteCampCertificate(memberKey: string, probeId: string): Observable<ThirdProbeDto> {
    return this.http.delete<ThirdProbeDto>(`${this.probeUrl(memberKey, probeId)}/third-probe/camp/certificate`);
  }

  addProbeProject(memberKey: string, probeId: string, request: SaveProbeProjectRequestDto): Observable<ProbeProjectSavedDto> {
    return this.http.post<ProbeProjectSavedDto>(this.projectsUrl(memberKey, probeId), request, { context: requestFeedback('auto', [400, 403]) });
  }

  updateProbeProject(memberKey: string, probeId: string, projectKey: string, request: SaveProbeProjectRequestDto): Observable<ProbeProjectSavedDto> {
    return this.http.put<ProbeProjectSavedDto>(
      `${this.projectsUrl(memberKey, probeId)}/${projectKey}`,
      request,
      { context: requestFeedback('auto', [400, 403, 404]) }
    );
  }

  deleteProbeProject(memberKey: string, probeId: string, projectKey: string): Observable<ThirdProbeDto> {
    return this.http.delete<ThirdProbeDto>(`${this.projectsUrl(memberKey, probeId)}/${projectKey}`);
  }

  /** Puts the spare in place of a main project, which becomes the spare. */
  swapSpareProject(memberKey: string, probeId: string, spareKey: string, mainKey: string): Observable<ThirdProbeDto> {
    return this.http.post<ThirdProbeDto>(`${this.projectsUrl(memberKey, probeId)}/${spareKey}/swap/${mainKey}`, {}, {
      context: requestFeedback('auto', [400])
    });
  }

  /** «Дозвіл» (`approval`) or «Успішно виконав» (`completion`), given or taken back. */
  signProbeProject(
    memberKey: string,
    probeId: string,
    projectKey: string,
    part: 'approval' | 'completion',
    sign: boolean,
    coordinatorComment: string | null = null
  ): Observable<ThirdProbeDto> {
    const url = `${this.projectsUrl(memberKey, probeId)}/${projectKey}/${part}`;
    if (!sign) {
      return this.http.delete<ThirdProbeDto>(url);
    }
    const body = part === 'completion' ? { coordinatorComment } : {};
    return this.http.put<ThirdProbeDto>(url, body, { context: requestFeedback('auto', [400]) });
  }

  uploadProbeProjectImage(memberKey: string, probeId: string, projectKey: string, file: File): Observable<ProbeProjectImageDto> {
    const form = new FormData();
    form.append('file', file, file.name || 'image.png');
    return this.http.post<ProbeProjectImageDto>(
      `${this.projectsUrl(memberKey, probeId)}/${projectKey}/images`,
      form,
      { context: requestFeedback('silent', [400, 403, 404, 413]) }
    );
  }

  private probeUrl(memberKey: string, probeId: string): string {
    return `${this.apiUrl}/${memberKey}/probes/${encodeURIComponent(probeId)}`;
  }

  private projectsUrl(memberKey: string, probeId: string): string {
    return `${this.probeUrl(memberKey, probeId)}/projects`;
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
