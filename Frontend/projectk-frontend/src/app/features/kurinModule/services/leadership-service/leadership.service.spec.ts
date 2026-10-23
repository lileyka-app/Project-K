import { TestBed } from '@angular/core/testing';

import { LeadershipService } from './leadership.service';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { LEADERSHIP_CACHE_PREFIX, MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';
import { LeadershipDto } from '../../models/requests/leadership/leadership.dto';

describe('LeadershipService', () => {
  let service: LeadershipService;
  let httpMock: HttpTestingController;
  let cache: ClientCacheService;

  const apiUrl = `${environment.apiUrl}/leadership`;
  const leadership: LeadershipDto = {
    leadershipKey: 'leadership-1',
    type: 'kurin',
    entityKey: 'kurin-1',
    startDate: '2026-05-18',
    endDate: null,
    leadershipHistories: []
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(LeadershipService);
    httpMock = TestBed.inject(HttpTestingController);
    cache = TestBed.inject(ClientCacheService);
  });

  afterEach(() => {
    httpMock.verify();
    cache.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getLeadershipByTypeAndKey should reuse cached response within TTL', () => {
    service.getLeadershipByTypeAndKey('kurin', 'kurin-1').subscribe(response => {
      expect(response).toEqual(leadership);
    });

    httpMock.expectOne(`${apiUrl}/type/kurin/kurin-1`).flush(leadership);

    service.getLeadershipByTypeAndKey('kurin', 'kurin-1').subscribe(response => {
      expect(response).toEqual(leadership);
    });

    httpMock.expectNone(`${apiUrl}/type/kurin/kurin-1`);
  });

  it('update should invalidate leadership and member caches', () => {
    vi.spyOn(cache, 'invalidateByPrefix');

    service.update('leadership-1', leadership).subscribe();

    httpMock.expectOne(`${apiUrl}/leadership-1`).flush(leadership);

    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(LEADERSHIP_CACHE_PREFIX);
    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(MEMBER_CACHE_PREFIX);
  });
});
