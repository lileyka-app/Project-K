import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { KurinJournalPageDto, KurinJournalQuery } from '../../models/kurin-journal.dto';

/**
 * Reads the kurin's «Журнал дій». Not cached: the page is opened to see what just happened, and a
 * minute-old copy would hide exactly that.
 */
@Injectable({
  providedIn: 'root'
})
export class KurinJournalService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/kurin`;

  getPage(kurinKey: string, query: KurinJournalQuery = {}): Observable<KurinJournalPageDto> {
    let params = new HttpParams();
    for (const [name, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(name, String(value));
      }
    }
    return this.http.get<KurinJournalPageDto>(`${this.apiUrl}/${kurinKey}/journal`, { params });
  }
}
