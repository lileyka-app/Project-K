import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of, shareReplay } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { CatalogHonour } from '../../models/honour.dto';

/** The catalogue's ступені and senior відзначення, read once: names and pictures for the card. */
@Injectable({
  providedIn: 'root'
})
export class HonoursService {
  private readonly http = inject(HttpClient);
  private honours$?: Observable<CatalogHonour[]>;

  /** Everything, ступені first; empty when the catalogue cannot be read, and read again next time. */
  all(): Observable<CatalogHonour[]> {
    this.honours$ ??= this.http.get<CatalogHonour[]>(`${environment.apiUrl}/catalog/honours`).pipe(
      catchError(() => {
        this.honours$ = undefined;
        return of([]);
      }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    return this.honours$;
  }
}
