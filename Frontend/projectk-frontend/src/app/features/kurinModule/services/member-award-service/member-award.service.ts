import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { MemberAwardDto } from '../../models/member-award.dto';
import { MemberAwardLevel } from '../../models/enums/member-award-level.enum';
import { ReviewBadgeProgressRequestDto } from '../../models/probes-and-badges/requests/review-badge-progress-request.dto';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';

export interface UpsertMemberAwardRequest {
    memberAwardKey?: string;
    level: MemberAwardLevel;
    dateAcquired: string;
    note?: string;
}

@Injectable({
    providedIn: 'root'
})
export class MemberAwardService {
    private readonly apiUrl = `${environment.apiUrl}/member`;
    private readonly http = inject(HttpClient);
    private readonly cache = inject(ClientCacheService);

    upsertAward(memberKey: string, request: UpsertMemberAwardRequest): Observable<MemberAwardDto> {
        return this.http.post<MemberAwardDto>(`${this.apiUrl}/${memberKey}/awards`, request).pipe(
            tap(() => this.invalidateMemberData())
        );
    }

    deleteAward(memberKey: string, awardKey: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${memberKey}/awards/${awardKey}`).pipe(
            tap(() => this.invalidateMemberData())
        );
    }

    reviewAward(memberKey: string, awardKey: string, request: ReviewBadgeProgressRequestDto): Observable<MemberAwardDto> {
        return this.http.post<MemberAwardDto>(`${this.apiUrl}/${memberKey}/awards/${awardKey}/review`, request).pipe(
            tap(() => this.invalidateMemberData())
        );
    }

    private invalidateMemberData(): void {
        this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
    }
}
