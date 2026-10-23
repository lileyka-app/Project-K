import { CountryCode } from 'libphonenumber-js';
import { DEFAULT_PHONE_COUNTRY, phoneCountryOfKrai } from '../../../../shared/functions/phone.function';
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { KurinDto } from '../../models/kurin.dto';
import { KraiDto } from '../../models/krai.dto';
import { CardFieldsDto, DEFAULT_CARD_FIELDS, KurinCardFieldDto } from '../../models/card-fields.dto';
import { ApplyRosterRequest, RosterImportReport, RosterPreview } from '../../pages/import/import-model';
import { Observable } from 'rxjs/internal/Observable';
import { catchError, map, of, tap } from 'rxjs';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { browserTimeZone } from '../../functions/browser-time-zone.function';
import { ACCESS_CACHE_PREFIX, ENTITY_CACHE_TTL_MS, GROUP_CACHE_PREFIX, KURIN_CACHE_PREFIX, MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';
import { KurinAccessPolicyDto, SetKurinAccessPolicyRequest } from '../../models/kurin-access-policy.dto';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';

@Injectable({
  providedIn: 'root'
})
export class KurinService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/kurin`;

  getByKey(kurinKey: string): Observable<KurinDto> {
    return this.cache.get(
      `${KURIN_CACHE_PREFIX}by-key:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<KurinDto>(`${this.apiUrl}/${kurinKey}`)
    );
  }

  getKurins(): Observable<KurinDto[]> {
    return this.cache.get(
      `${KURIN_CACHE_PREFIX}list`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<KurinDto[]>(`${this.apiUrl}/kurins`)
    );
  }

  /**
   * Звіт куреня в PDF. Разом із запитом їде часовий пояс браузера: години в документі мають бути
   * ті, що показує годинник читача, а сервер його поясу не знає — раніше там стояв UTC.
   */
  downloadReportPdf(kurinKey: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.apiUrl}/${kurinKey}/report/pdf`, {
      params: { timeZone: browserTimeZone() },
      observe: 'response',
      responseType: 'blob'
    });
  }

  /** Реєстр у .xlsx — саме тими колонками, які зараз видно на екрані. */
  exportRegistry(kurinKey: string, columns: string[]): Observable<HttpResponse<Blob>> {
    return this.http.post(`${this.apiUrl}/${kurinKey}/registry/export`, { columns }, {
      observe: 'response',
      responseType: 'blob',
      context: requestFeedback('errors')
    });
  }

  /** Читає завантажену таблицю й каже, на що вона схожа. Нічого не пише. */
  previewRoster(kurinKey: string, file: File): Observable<RosterPreview> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<RosterPreview>(`${this.apiUrl}/${kurinKey}/import/preview`, form, { context: requestFeedback('errors') });
  }

  /** Застосовує зіставлений склад — або, з `dryRun`, лише звітує, що б зробив. */
  importRoster(kurinKey: string, request: ApplyRosterRequest): Observable<RosterImportReport> {
    return this.http.post<RosterImportReport>(`${this.apiUrl}/${kurinKey}/import`, request, {
      context: requestFeedback(request.dryRun ? 'errors' : 'auto')
    }).pipe(
      tap(report => {
        // Імпорт міняє склад, гуртки й розміщення разом — простіше скинути кеш цілком, ніж
        // перелічувати, що саме застаріло.
        if (!report.dryRun) {
          this.cache.clear();
        }
      })
    );
  }

  /** The краї of this instance, for the admin's kurin list. */
  getKrais(): Observable<KraiDto[]> {
    return this.http.get<KraiDto[]>(`${environment.apiUrl}/krai`);
  }

  /**
   * The card fields of the kurin's край. A profile that does not load leaves the card as it
   * always was; the server still checks the край's rules on save.
   */
  getCardFields(kurinKey: string): Observable<CardFieldsDto> {
    return this.kurinProfile(kurinKey).pipe(
      map(profile => profile.cardFields ?? DEFAULT_CARD_FIELDS),
      catchError(() => of(DEFAULT_CARD_FIELDS))
    );
  }

  /** The country new phone numbers in this kurin start in: its край's (CZ for cz). */
  getPhoneCountry(kurinKey: string): Observable<CountryCode> {
    return this.kurinProfile(kurinKey).pipe(
      map(profile => phoneCountryOfKrai(profile.kraiCode)),
      catchError(() => of(DEFAULT_PHONE_COUNTRY))
    );
  }

  private kurinProfile(kurinKey: string): Observable<{ kraiCode?: string; cardFields?: CardFieldsDto }> {
    return this.cache.get(
      `${KURIN_CACHE_PREFIX}card-fields:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<{ kraiCode?: string; cardFields?: CardFieldsDto }>(`${this.apiUrl}/${kurinKey}/profile`)
    );
  }

  /** The card fields the kurin may leave off, each saying whether it does. */
  getHideableCardFields(kurinKey: string): Observable<KurinCardFieldDto[]> {
    return this.http.get<KurinCardFieldDto[]>(`${this.apiUrl}/${kurinKey}/card-fields`);
  }

  /** Saves which of them the kurin hides; the forms read the narrowed card afresh. */
  setHiddenCardFields(kurinKey: string, hidden: string[]): Observable<KurinCardFieldDto[]> {
    return this.http.put<KurinCardFieldDto[]>(`${this.apiUrl}/${kurinKey}/card-fields`, { hidden }).pipe(
      tap(() => this.cache.invalidate(`${KURIN_CACHE_PREFIX}card-fields:${kurinKey}`))
    );
  }

  /** «Права й видимість»: every cell with what the kurin has, its default and whether it is locked. */
  getAccessPolicy(kurinKey: string): Observable<KurinAccessPolicyDto> {
    return this.http.get<KurinAccessPolicyDto>(`${this.apiUrl}/${kurinKey}/access-policy`);
  }

  /**
   * Saves the whole matrix. What people see of a card and which buttons it offers change at once, so
   * the cards and the abilities read before are dropped.
   */
  setAccessPolicy(kurinKey: string, request: SetKurinAccessPolicyRequest): Observable<KurinAccessPolicyDto> {
    return this.http.put<KurinAccessPolicyDto>(`${this.apiUrl}/${kurinKey}/access-policy`, request).pipe(
      tap(() => {
        this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
        this.cache.invalidateByPrefix(ACCESS_CACHE_PREFIX);
      })
    );
  }

  /** Moves a kurin to another край. Admin-only; refused when its number is taken there. */
  setKurinKrai(kurinKey: string, kraiCode: string): Observable<KurinDto> {
    return this.http.put<KurinDto>(`${this.apiUrl}/${kurinKey}/krai`, { kraiCode }).pipe(
      tap(() => this.invalidateKurinDataCache())
    );
  }

  createKurin(kurin: KurinDto): Observable<KurinDto> {
    return this.http.post<KurinDto>(
      `${this.apiUrl}`,
      { number: kurin.number, kraiCode: kurin.kraiCode || null },
      {
        headers: {
          'Content-Type': 'application/json'
        }
      }
    ).pipe(
      tap(() => this.invalidateKurinDataCache())
    );
  }

  updateKurin(kurin: KurinDto): Observable<KurinDto> {
    return this.http.put<KurinDto>(
      `${this.apiUrl}/${kurin.kurinKey}`,
      {
        number: kurin.number,
        stanytsia: kurin.stanytsia,
        regionOrCountry: kurin.regionOrCountry,
        namedAfter: kurin.namedAfter,
        description: kurin.description,
        profileVerificationEnabled: kurin.profileVerificationEnabled ?? false
      },
      {
        headers: {
          'Content-Type': 'application/json'
        }
      }
    ).pipe(
      tap(() => this.invalidateKurinDataCache())
    );
  }

  deleteKurin(kurinKey: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${kurinKey}`).pipe(
      tap(() => this.invalidateKurinDataCache())
    );
  }

  private invalidateKurinDataCache(): void {
    this.cache.invalidateByPrefix(KURIN_CACHE_PREFIX);
    this.cache.invalidateByPrefix(GROUP_CACHE_PREFIX);
    this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
  }
}
