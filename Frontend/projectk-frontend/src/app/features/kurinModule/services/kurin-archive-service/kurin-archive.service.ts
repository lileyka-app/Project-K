import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { LeadershipRole } from '../../models/enums/leadership-role.enum';
import { MembershipKind } from '../../models/enums/membership-kind.enum';
import {
  ArchiveDangerZoneDto,
  ArchiveDangerZoneEnd,
  ArchiveDeletionDto,
  KurinArchiveDto
} from '../../models/kurin-archive.dto';

/** The header a deletion carries its danger-zone ticket in. */
export const DANGER_TICKET_HEADER = 'X-Archive-Danger';

/**
 * The kurin's archive: reads, corrections, and the danger zone. Not cached — a correction or a
 * deletion must show at once.
 */
@Injectable({
  providedIn: 'root'
})
export class KurinArchiveService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.apiUrl}/kurin`;

  get(kurinKey: string): Observable<KurinArchiveDto> {
    return this.http.get<KurinArchiveDto>(`${this.apiUrl}/${kurinKey}/archive`);
  }

  correctOffice(kurinKey: string, historyKey: string, body: { role: LeadershipRole; startDate: string; endDate: string }): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${kurinKey}/archive/offices/${historyKey}`, body);
  }

  correctMembership(kurinKey: string, membershipKey: string, body: { kind: MembershipKind; joinedOn: string; leftOn: string }): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${kurinKey}/archive/memberships/${membershipKey}`, body);
  }

  /** A wrong password or code (400) and a pause (429) are the page's to say. */
  enterDangerZone(kurinKey: string, proof: { password?: string; code?: string }): Observable<ArchiveDangerZoneDto> {
    return this.http.post<ArchiveDangerZoneDto>(`${this.apiUrl}/${kurinKey}/archive/danger`, proof, {
      context: requestFeedback('errors', [400, 429])
    });
  }

  endDangerZone(kurinKey: string, reason: ArchiveDangerZoneEnd): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${kurinKey}/archive/danger`, {
      params: { reason },
      context: requestFeedback('silent')
    });
  }

  /**
   * The same, from a page that is going away: a closing tab cancels ordinary requests, a
   * `keepalive` fetch outlives it. Best effort — the zone ends by its own clock anyway.
   */
  endDangerZoneOnUnload(kurinKey: string): void {
    const token = this.auth.getAccessToken();
    if (!token) {
      return;
    }
    void fetch(`${this.apiUrl}/${kurinKey}/archive/danger?reason=Left`, {
      method: 'DELETE',
      keepalive: true,
      credentials: 'include',
      headers: { Authorization: `Bearer ${token}` }
    }).catch(() => undefined);
  }

  deleteOffice(kurinKey: string, historyKey: string, ticket: string): Observable<ArchiveDeletionDto> {
    return this.http.delete<ArchiveDeletionDto>(`${this.apiUrl}/${kurinKey}/archive/danger/offices/${historyKey}`, this.danger(ticket));
  }

  deleteMembership(kurinKey: string, membershipKey: string, ticket: string): Observable<ArchiveDeletionDto> {
    return this.http.delete<ArchiveDeletionDto>(`${this.apiUrl}/${kurinKey}/archive/danger/memberships/${membershipKey}`, this.danger(ticket));
  }

  deleteGroup(kurinKey: string, groupKey: string, ticket: string): Observable<ArchiveDeletionDto> {
    return this.http.delete<ArchiveDeletionDto>(`${this.apiUrl}/${kurinKey}/archive/danger/groups/${groupKey}`, this.danger(ticket));
  }

  private danger(ticket: string) {
    return { headers: new HttpHeaders({ [DANGER_TICKET_HEADER]: ticket }) };
  }
}
