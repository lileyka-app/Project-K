import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { KraiProfileDocument, KraiProfileEditorDto, KraiProfileRevisionDto } from '../../models/krai-profile.dto';
import { KraiDto } from '../../../kurinModule/models/krai.dto';
import { KraiConcessionReportDto } from '../../../duesModule/models/concession.dto';
import { QuarterDto } from '../../../duesModule/models/group-dues.dto';

/** The admin's view of the краї: adding and renaming one, its stored profile and its history. */
@Injectable({ providedIn: 'root' })
export class KraiProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/krai`;

  /** Adds a край; 409 when its code is taken. Without a document it reads Ukraine's вмілості. */
  create(code: string, name: string): Observable<KraiDto> {
    return this.http.post<KraiDto>(this.apiUrl, { code, name });
  }

  rename(kraiCode: string, name: string): Observable<KraiDto> {
    return this.http.put<KraiDto>(`${this.apiUrl}/${kraiCode}`, { name });
  }

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

  /** What the край's пільги took off its kurins' вкладка in one quarter; without one, the quarter running now by the край's clock. */
  concessionReport(kraiCode: string, quarter: QuarterDto | null): Observable<KraiConcessionReportDto> {
    return this.http.get<KraiConcessionReportDto>(`${this.apiUrl}/${kraiCode}/dues/concessions`, {
      params: quarter ? { year: quarter.year, number: quarter.number } : {}
    });
  }

  restore(kraiCode: string, revision: number): Observable<KraiProfileEditorDto> {
    return this.http.post<KraiProfileEditorDto>(`${this.apiUrl}/${kraiCode}/profile/revisions/${revision}/restore`, {});
  }
}
