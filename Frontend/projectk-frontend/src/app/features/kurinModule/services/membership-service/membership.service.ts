import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { MEMBER_CACHE_PREFIX, GROUP_CACHE_PREFIX, KURIN_CACHE_PREFIX } from '../client-cache/cache-policy';
import { KurinNumbering } from '../../models/enums/kurin-branch.enum';

/**
 * Той, хто був у курені й пішов. Курінь пам'ятає рівно свій відтинок: хто, в якому гуртку, з коли
 * до коли. Ні контактів, ні того, що людина робить тепер; де вона стоїть зараз, бачить лише Звʼязковий, коли повертає її.
 */
export interface FormerMemberDto {
  memberKey: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  groupKey?: string | null;
  groupName?: string | null;
  joinedAtUtc: string;
  leftAtUtc: string;
}

/** Що курінь просить: «перевести» до себе чи «прийняти ще й до нас». */
export type MembershipRequestKind = 'Transfer' | 'AlsoJoin';
export type MembershipRequestStatus = 'Pending' | 'Approved' | 'Declined' | 'Cancelled' | 'Expired';
export type MembershipKindName = 'Youth' | 'Staff';

export interface MembershipRequestKurinDto {
  kurinKey: string;
  number: number | null;
  namedAfter: string | null;
  branches: KurinNumbering[];
}

/**
 * Запит між куренями. `owedToLeave` — для куреня, що відповідає, на «перевести»: поки він більший
 * за нуль, підтвердити не можна.
 */
export interface MembershipRequestDto {
  membershipRequestKey: string;
  kind: MembershipRequestKind;
  status: MembershipRequestStatus;
  memberKey: string;
  memberName: string;
  fromKurin: MembershipRequestKurinDto;
  toKurin: MembershipRequestKurinDto;
  membershipKind: MembershipKindName;
  createdAtUtc: string;
  expiresAtUtc: string;
  decidedAtUtc: string | null;
  owedToLeave: number;
}

export interface MembershipRequestsDto {
  incoming: MembershipRequestDto[];
  outgoing: MembershipRequestDto[];
}

/** Людина за кодом: картка й курені, де вона стоїть зараз (лише номери). */
export interface MembershipCandidateDto {
  member: { memberKey: string; publicId: string; firstName: string; lastName: string; fullName: string };
  alreadyInThisKurin: boolean;
  currentKurins: MembershipRequestKurinDto[];
}

export interface CreateMembershipRequest {
  publicId?: string | null;
  memberKey?: string | null;
  kind: MembershipRequestKind;
  membershipKind: MembershipKindName;
  fromKurinKey?: string | null;
}

/** Що зробив запит: людину, яка ніде не стоїть, прийнято одразу (`joined`). */
export interface MembershipRequestSentDto {
  membershipRequestKey: string | null;
  joined: boolean;
}

/** Колишній член і курені, де він стоїть зараз (лише номери). Порожньо — ніде: повертаємо без запиту. */
export interface FormerMemberStandingDto {
  former: FormerMemberDto;
  standsIn: MembershipRequestKurinDto[];
}

/**
 * Належність до куреня — окремо від профілю людини. Профіль редагується один раз і скрізь, а
 * членство відкривається й закривається в кожному курені окремо, і це різні дії з різними правами.
 */
@Injectable({ providedIn: 'root' })
export class MembershipService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/kurin`;

  /**
   * Прийняти назад того, кого курінь уже знає, — за ключем.
   * Відкривається нове членство, старе лишається в історії.
   */
  takeBack(kurinKey: string, memberKey: string, groupKey: string | null = null): Observable<string> {
    return this.http.post<string>(`${this.apiUrl}/${kurinKey}/memberships`, { memberKey, groupKey }).pipe(
      tap(() => this.forgetPlacement())
    );
  }

  leave(kurinKey: string, memberKey: string): Observable<void> {
    // 409 — людина винна вкладку; картка пояснює це сама.
    return this.http.delete<void>(`${this.apiUrl}/${kurinKey}/memberships/${memberKey}`, {
      context: requestFeedback('auto', [409])
    }).pipe(
      tap(() => this.forgetPlacement())
    );
  }

  moveToGroup(kurinKey: string, memberKey: string, groupKey: string | null): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/${kurinKey}/memberships/${memberKey}/group`,
      { groupKey }
    ).pipe(tap(() => this.forgetPlacement()));
  }

  /** Людина за кодом, який вона дала. 404 — коду немає; сторінка каже це сама. */
  candidate(kurinKey: string, publicId: string): Observable<MembershipCandidateDto> {
    return this.http.get<MembershipCandidateDto>(`${this.apiUrl}/${kurinKey}/memberships/candidate`, {
      params: { publicId },
      context: requestFeedback('errors', [404])
    });
  }

  /** Запити куреня в обидва боки. */
  requests(kurinKey: string): Observable<MembershipRequestsDto> {
    return this.http.get<MembershipRequestsDto>(`${this.apiUrl}/${kurinKey}/membership-requests`);
  }

  /** Скільки запитів чекає відповіді куреня: лише число, для крапки в меню. */
  waitingRequests(kurinKey: string): Observable<number> {
    return this.http.get<number>(`${this.apiUrl}/${kurinKey}/membership-requests/waiting`);
  }

  /** Колишні члени для Звʼязкового — з куренями, де вони стоять зараз. */
  formerStanding(kurinKey: string): Observable<FormerMemberStandingDto[]> {
    return this.http.get<FormerMemberStandingDto[]>(`${this.apiUrl}/${kurinKey}/membership-requests/former`);
  }

  /** 409 сторінка пояснює сама: людина вже тут, запит уже чекає, або треба вибрати курінь. */
  createRequest(kurinKey: string, request: CreateMembershipRequest): Observable<MembershipRequestSentDto> {
    return this.http.post<MembershipRequestSentDto>(`${this.apiUrl}/${kurinKey}/membership-requests`, request, {
      context: requestFeedback('auto', [409])
    }).pipe(tap(sent => sent.joined && this.forgetPlacement()));
  }

  /** 409 — людина винна вкладку або запит уже закрито; сторінка пояснює це сама. */
  approveRequest(kurinKey: string, requestKey: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${kurinKey}/membership-requests/${requestKey}/approve`, {}, {
      context: requestFeedback('auto', [409])
    }).pipe(tap(() => this.forgetPlacement()));
  }

  declineRequest(kurinKey: string, requestKey: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${kurinKey}/membership-requests/${requestKey}/decline`, {});
  }

  cancelRequest(kurinKey: string, requestKey: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${kurinKey}/membership-requests/${requestKey}`);
  }

  /** Членство змінює склад списків куреня й гуртка так само, як самої людини. */
  private forgetPlacement(): void {
    this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
    this.cache.invalidateByPrefix(GROUP_CACHE_PREFIX);
    this.cache.invalidateByPrefix(KURIN_CACHE_PREFIX);
  }
}
