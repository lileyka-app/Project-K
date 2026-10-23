import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { MEMBER_CACHE_PREFIX, MEMBER_WARNING_CACHE_PREFIX } from '../client-cache/cache-policy';
import { MemberWarningLevel } from '../../models/enums/member-warning-level.enum';
import { MemberWarningDto } from '../../models/member-warning.dto';
import { MemberWarningService } from './member-warning.service';

describe('MemberWarningService', () => {
  let service: MemberWarningService;
  let httpMock: HttpTestingController;
  let cache: ClientCacheService;

  const apiUrl = `${environment.apiUrl}/member`;
  const memberKey = 'member-1';
  const warning: MemberWarningDto = {
    memberWarningKey: 'warning-1',
    memberKey,
    level: MemberWarningLevel.Level1,
    issuedAtUtc: '2026-05-18T00:00:00Z',
    expiresAtUtc: '2026-06-18T00:00:00Z',
    issuedByUserKey: 'user-1',
    revokedAtUtc: null,
    revokedByUserKey: null
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MemberWarningService
      ]
    });

    service = TestBed.inject(MemberWarningService);
    httpMock = TestBed.inject(HttpTestingController);
    cache = TestBed.inject(ClientCacheService);
  });

  afterEach(() => {
    httpMock.verify();
    cache.clear();
  });

  it('getWarnings should reuse cached response within TTL', () => {
    service.getWarnings(memberKey).subscribe(response => {
      expect(response).toEqual([warning]);
    });

    httpMock.expectOne(`${apiUrl}/${memberKey}/warnings`).flush([warning]);

    service.getWarnings(memberKey).subscribe(response => {
      expect(response).toEqual([warning]);
    });

    httpMock.expectNone(`${apiUrl}/${memberKey}/warnings`);
  });

  it('assignWarning should invalidate warning and member caches', () => {
    vi.spyOn(cache, 'invalidate');
    vi.spyOn(cache, 'invalidateByPrefix');

    service.assignWarning(memberKey, { level: MemberWarningLevel.Level1 }).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/warnings`).flush(warning);

    expect(cache.invalidate).toHaveBeenCalledWith(`${MEMBER_WARNING_CACHE_PREFIX}${memberKey}`);
    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(MEMBER_CACHE_PREFIX);
  });

  it('cancelWarning should invalidate warning and member caches', () => {
    vi.spyOn(cache, 'invalidate');
    vi.spyOn(cache, 'invalidateByPrefix');

    service.cancelWarning(memberKey, warning.memberWarningKey).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/warnings/${warning.memberWarningKey}`).flush(warning);

    expect(cache.invalidate).toHaveBeenCalledWith(`${MEMBER_WARNING_CACHE_PREFIX}${memberKey}`);
    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(MEMBER_CACHE_PREFIX);
  });
});
