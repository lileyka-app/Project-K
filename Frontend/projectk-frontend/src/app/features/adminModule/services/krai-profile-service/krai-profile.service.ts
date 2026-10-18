import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { KraiProfileDocument, KraiProfileEditorDto, KraiProfileRevisionDto } from '../../models/krai-profile.dto';
import { KraiConcessionReportDto } from '../../../duesModule/models/concession.dto';
import { QuarterDto } from '../../../duesModule/models/group-dues.dto';

/** The admin's view of a край's profile: the stored document and its history. */
@Injectable({ providedIn: 'root' })
export class KraiProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/krai`;

  get(kraiCode: string): Observable<KraiProfileEditorDto> {
    return this.http.get<KraiProfileEditorDto>(`${this.apiUrl}/${kraiCode}/profile`);
  }

  /** A new revision on top of baseRevision; 409 when someone saved in between. */
  save(kraiCode: string, baseRevision: number, document: KraiProfileDocument, note: string | null): Observable<KraiProfileEditorDto> {
    return this.http.put<KraiProfileEditorDto>(`${this.apiUrl}/${kraiCode}/profile`, { baseRevision, document, note });
  }

  revisions(kraiCode: string): Observable<KraiProfileRevisionDto[]> {
    return this.http.get<KraiProfileRevisionDto[]>(`${this.apiUrl}/${kraiCode}/profile/revisions`);
  }

  /** What the край's пільги took off its kurins' вкладка in one quarter. */
  concessionReport(kraiCode: string, quarter: QuarterDto): Observable<KraiConcessionReportDto> {
    return this.http.get<KraiConcessionReportDto>(`${this.apiUrl}/${kraiCode}/dues/concessions`, {
      params: { year: quarter.year, number: quarter.number }
    });
  }

  restore(kraiCode: string, revision: number): Observable<KraiProfileEditorDto> {
    return this.http.post<KraiProfileEditorDto>(`${this.apiUrl}/${kraiCode}/profile/revisions/${revision}/restore`, {});
  }
}
