import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';
import { KurinNumbering } from '../../../kurinModule/models/enums/kurin-branch.enum';

/** What a shared card may show besides the name — flags, as the API sends them (`CardField`). */
export const CARD_FIELD = {
  Photo: 1,
  Kurin: 2,
  Level: 4,
  Awards: 8,
  Badges: 16,
  Probes: 32
} as const;
export type CardFieldName = keyof typeof CARD_FIELD;

/** In the order the card settings list them, with the words they use. */
export const CARD_FIELDS: { name: CardFieldName; label: string }[] = [
  { name: 'Photo', label: 'Фото' },
  { name: 'Kurin', label: 'Курінь і гурток' },
  { name: 'Level', label: 'Ступінь' },
  { name: 'Awards', label: 'Відзначення' },
  { name: 'Badges', label: 'Вмілості' },
  { name: 'Probes', label: 'Проби' }
];

export const hasCardField = (fields: number, name: CardFieldName): boolean => (fields & CARD_FIELD[name]) !== 0;

export interface CardAllowance {
  mayShare: boolean;
  isAdult: boolean;
  maxFields: number;
  mayLink: boolean;
}

export interface PublicCardDto {
  fullName: string;
  photoUrl: string | null;
  kurins: { number: number | null; namedAfter: string | null; groupName: string | null; branches: KurinNumbering[] }[] | null;
  level: string | null;
  /** An УПЮ відзначення by level; a senior one (no level) with the catalogue's title and picture. */
  awards: { level: 1 | 2 | 3 | 4 | null; title: string; imageUrl: string | null; dateAcquired: string }[] | null;
  badges: { title: string; imagePath: string | null; confirmedAtUtc: string | null }[] | null;
  probes: { title: string; verifiedAtUtc: string | null }[] | null;
  checkedAtUtc: string | null;
}

export interface MyCardDto {
  allowance: CardAllowance;
  fields: number;
  linkUrl: string | null;
  linkIssuedAtUtc: string | null;
  preview: PublicCardDto;
}

export interface CardCodeDto {
  url: string;
  qrCode: string;
  expiresAtUtc: string | null;
}

export interface KurinCardSharingDto {
  minorsMayShare: boolean;
  minorFields: number;
  minorsMayLink: boolean;
}

/** The person's shared card (`api/me/card`), the cards others share (`api/public/card`), and a kurin's rule for minors. */
@Injectable({ providedIn: 'root' })
export class CardService {
  private readonly me = `${environment.apiUrl}/me`;
  private readonly http = inject(HttpClient);

  mine(): Observable<MyCardDto> {
    return this.http.get<MyCardDto>(`${this.me}/card`);
  }

  setFields(fields: number): Observable<MyCardDto> {
    return this.http.put<MyCardDto>(`${this.me}/card/fields`, { fields });
  }

  issueLink(): Observable<MyCardDto> {
    return this.http.post<MyCardDto>(`${this.me}/card/link`, {});
  }

  disableLink(): Observable<MyCardDto> {
    return this.http.delete<MyCardDto>(`${this.me}/card/link`);
  }

  /** 403 the dialog explains itself: the card may not be shared. */
  checkCode(): Observable<CardCodeDto> {
    return this.http.get<CardCodeDto>(`${this.me}/card/check-code`, { context: requestFeedback('errors', [403]) });
  }

  transferCode(): Observable<CardCodeDto> {
    return this.http.get<CardCodeDto>(`${this.me}/transfer-code`);
  }

  /** 404 the page explains itself: expired, withdrawn and unknown all look the same. */
  checked(token: string): Observable<PublicCardDto> {
    return this.http.get<PublicCardDto>(`${environment.apiUrl}/public/card/check/${encodeURIComponent(token)}`, {
      context: requestFeedback('errors', [404])
    });
  }

  linked(token: string): Observable<PublicCardDto> {
    return this.http.get<PublicCardDto>(`${environment.apiUrl}/public/card/link/${encodeURIComponent(token)}`, {
      context: requestFeedback('errors', [404])
    });
  }

  kurinSharing(kurinKey: string): Observable<KurinCardSharingDto> {
    return this.http.get<KurinCardSharingDto>(`${environment.apiUrl}/kurin/${kurinKey}/card-sharing`);
  }

  setKurinSharing(kurinKey: string, sharing: KurinCardSharingDto): Observable<KurinCardSharingDto> {
    return this.http.put<KurinCardSharingDto>(`${environment.apiUrl}/kurin/${kurinKey}/card-sharing`, sharing);
  }
}
