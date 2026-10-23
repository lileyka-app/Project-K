import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { BadgeProgressStatus } from '../../models/enums/badge-progress-status.enum';
import { MemberAwardLevel } from '../../models/enums/member-award-level.enum';
import { MemberAwardDto } from '../../models/member-award.dto';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';
import { MemberAwardService } from './member-award.service';

describe('MemberAwardService', () => {
  let service: MemberAwardService;
  let httpMock: HttpTestingController;
  let cache: ClientCacheService;

  const apiUrl = `${environment.apiUrl}/member`;
  const memberKey = 'member-1';
  const award: MemberAwardDto = {
    memberAwardKey: 'award-1',
    memberKey,
    kurinKey: 'kurin-1',
    level: MemberAwardLevel.First,
    dateAcquired: '2026-05-18',
    status: BadgeProgressStatus.Submitted
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MemberAwardService
      ]
    });

    service = TestBed.inject(MemberAwardService);
    httpMock = TestBed.inject(HttpTestingController);
    cache = TestBed.inject(ClientCacheService);
  });

  afterEach(() => {
    httpMock.verify();
    cache.clear();
  });

  it('upsertAward should invalidate member cache', () => {
    vi.spyOn(cache, 'invalidateByPrefix');

    service.upsertAward(memberKey, {
      level: MemberAwardLevel.First,
      dateAcquired: '2026-05-18'
    }).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/awards`).flush(award);

    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(MEMBER_CACHE_PREFIX);
  });

  it('deleteAward should invalidate member cache', () => {
    vi.spyOn(cache, 'invalidateByPrefix');

    service.deleteAward(memberKey, award.memberAwardKey).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/awards/${award.memberAwardKey}`).flush(null);

    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(MEMBER_CACHE_PREFIX);
  });

  it('reviewAward should invalidate member cache', () => {
    vi.spyOn(cache, 'invalidateByPrefix');

    service.reviewAward(memberKey, award.memberAwardKey, { isApproved: true, note: 'ok' }).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/awards/${award.memberAwardKey}/review`).flush(award);

    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(MEMBER_CACHE_PREFIX);
  });
});
