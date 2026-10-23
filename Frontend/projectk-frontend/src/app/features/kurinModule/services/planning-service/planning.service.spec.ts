import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { PlanningService } from './planning.service';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { PLANNING_CACHE_PREFIX } from '../client-cache/cache-policy';
import { PlanningSessionDto } from '../../models/planning-session.dto';

describe('PlanningService', () => {
  let service: PlanningService;
  let httpMock: HttpTestingController;
  let cache: ClientCacheService;

  const apiUrl = `${environment.apiUrl}/planning`;
  const session: PlanningSessionDto = {
    planningSessionKey: 'session-1',
    name: 'Session',
    kurinKey: 'kurin-1',
    searchStart: '2026-05-18',
    searchEnd: '2026-05-25',
    durationDays: 2,
    isCalculated: false,
    optimalStartDate: null,
    optimalEndDate: null,
    conflictScore: 0,
    canDelete: false,
    participants: []
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PlanningService);
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

  it('getSessions should reuse cached response within TTL', () => {
    service.getSessions('kurin-1').subscribe(response => {
      expect(response).toEqual([session]);
    });

    httpMock.expectOne(`${apiUrl}/kurin-1`).flush([session]);

    service.getSessions('kurin-1').subscribe(response => {
      expect(response).toEqual([session]);
    });

    httpMock.expectNone(`${apiUrl}/kurin-1`);
  });

  it('createSession should invalidate planning cache', () => {
    vi.spyOn(cache, 'invalidateByPrefix');

    service.createSession({ name: 'Session' }).subscribe();

    httpMock.expectOne(apiUrl).flush(session);

    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(PLANNING_CACHE_PREFIX);
  });
});
