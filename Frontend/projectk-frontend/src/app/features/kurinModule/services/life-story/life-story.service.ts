import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { browserTimeZone } from '../../functions/browser-time-zone.function';
import { LifeEntryDto, SaveLifeEntryRequest } from '../../models/life-entry.dto';

/** The caller's own life story: what they wrote into it, and the whole of it as a PDF. Nobody else's. */
@Injectable({ providedIn: 'root' })
export class LifeStoryService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/me/life`;

  entries(): Observable<LifeEntryDto[]> {
    return this.http.get<LifeEntryDto[]>(this.apiUrl);
  }

  save(key: string | null, request: SaveLifeEntryRequest): Observable<LifeEntryDto> {
    return key
      ? this.http.put<LifeEntryDto>(`${this.apiUrl}/${key}`, request)
      : this.http.post<LifeEntryDto>(this.apiUrl, request);
  }

  remove(key: string): Observable<unknown> {
    return this.http.delete(`${this.apiUrl}/${key}`);
  }

  /** The PDF, with the reader's time zone, so the stamp shows their clock. */
  downloadPdf(): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.apiUrl}/pdf`, {
      params: { timeZone: browserTimeZone() },
      observe: 'response',
      responseType: 'blob'
    });
  }
}
