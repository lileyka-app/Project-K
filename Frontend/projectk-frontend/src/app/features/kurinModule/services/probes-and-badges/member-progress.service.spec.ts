import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { BadgeProgressStatus } from '../../models/enums/badge-progress-status.enum';
import { ProbeProgressStatus } from '../../models/enums/probe-progress-status.enum';
import { BadgeProgressDto } from '../../models/probes-and-badges/badge-progress.dto';
import { ProbeProgressDto } from '../../models/probes-and-badges/probe-progress.dto';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { MEMBER_PROGRESS_CACHE_PREFIX } from '../client-cache/cache-policy';
import { MemberProgressService } from './member-progress.service';

describe('MemberProgressService', () => {
  let service: MemberProgressService;
  let httpMock: HttpTestingController;
  let cache: ClientCacheService;

  const apiUrl = `${environment.apiUrl}/member`;
  const memberKey = '11111111-1111-1111-1111-111111111111';

  const badgeProgress: BadgeProgressDto = {
    badgeProgressKey: '22222222-2222-2222-2222-222222222222',
    memberKey,
    kurinKey: '33333333-3333-3333-3333-333333333333',
    badgeId: 'badge-1',
    status: BadgeProgressStatus.Submitted,
    submittedAtUtc: '2026-04-15T10:00:00Z',
    reviewedAtUtc: null,
    reviewedByUserKey: null,
    reviewedByName: null,
    reviewedByRole: null,
    reviewNote: null,
    auditTrail: []
  };

  const probeProgress: ProbeProgressDto = {
    probeProgressKey: null,
    memberKey,
    kurinKey: '33333333-3333-3333-3333-333333333333',
    probeId: 'probe-1',
    status: ProbeProgressStatus.NotStarted,
    completedAtUtc: null,
    completedByUserKey: null,
    completedByName: null,
    completedByRole: null,
    verifiedAtUtc: null,
    verifiedByUserKey: null,
    verifiedByName: null,
    verifiedByRole: null,
    auditTrail: []
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MemberProgressService
      ]
    });

    service = TestBed.inject(MemberProgressService);
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

  it('getBadgeProgresses should call member badge progress endpoint', () => {
    service.getBadgeProgresses(memberKey).subscribe(response => {
      expect(response).toEqual([badgeProgress]);
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}/badges/progress`);
    expect(req.request.method).toBe('GET');
    req.flush([badgeProgress]);
  });

  it('getBadgeProgresses should reuse cached response within TTL', () => {
    service.getBadgeProgresses(memberKey).subscribe(response => {
      expect(response).toEqual([badgeProgress]);
    });

    httpMock.expectOne(`${apiUrl}/${memberKey}/badges/progress`).flush([badgeProgress]);

    service.getBadgeProgresses(memberKey).subscribe(response => {
      expect(response).toEqual([badgeProgress]);
    });

    httpMock.expectNone(`${apiUrl}/${memberKey}/badges/progress`);
  });

  it('submitBadgeProgress should call submit endpoint with payload', () => {
    const payload = { note: 'please review' };

    service.submitBadgeProgress(memberKey, 'badge-1', payload).subscribe(response => {
      expect(response.badgeId).toBe('badge-1');
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}/badges/badge-1/submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(badgeProgress);
  });

  it('submitBadgeProgress should invalidate member progress caches', () => {
    vi.spyOn(cache, 'invalidate');
    vi.spyOn(cache, 'invalidateByPrefix');

    service.submitBadgeProgress(memberKey, 'badge-1', { note: 'please review' }).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/badges/badge-1/submit`).flush(badgeProgress);

    expect(cache.invalidate).toHaveBeenCalledWith(`${MEMBER_PROGRESS_CACHE_PREFIX}badges:${memberKey}`);
    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(`${MEMBER_PROGRESS_CACHE_PREFIX}badge-review-queue:`);
    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(`${MEMBER_PROGRESS_CACHE_PREFIX}probe:${memberKey}:`);
  });

  it('reviewBadgeProgress should call review endpoint with payload', () => {
    const payload = { isApproved: true, note: 'approved' };

    service.reviewBadgeProgress(memberKey, 'badge-1', payload).subscribe(response => {
      expect(response.badgeId).toBe('badge-1');
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}/badges/badge-1/review`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ ...badgeProgress, status: BadgeProgressStatus.Confirmed });
  });

  it('getProbeProgress should call probe progress endpoint', () => {
    service.getProbeProgress(memberKey, 'probe-1').subscribe(response => {
      expect(response.probeId).toBe('probe-1');
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}/probes/probe-1/progress`);
    expect(req.request.method).toBe('GET');
    req.flush(probeProgress);
  });

  it('getProbeProgress should reuse cached response within TTL', () => {
    service.getProbeProgress(memberKey, 'probe-1').subscribe(response => {
      expect(response.probeId).toBe('probe-1');
    });

    httpMock.expectOne(`${apiUrl}/${memberKey}/probes/probe-1/progress`).flush(probeProgress);

    service.getProbeProgress(memberKey, 'probe-1').subscribe(response => {
      expect(response.probeId).toBe('probe-1');
    });

    httpMock.expectNone(`${apiUrl}/${memberKey}/probes/probe-1/progress`);
  });

  it('updateProbeProgressStatus should call update endpoint with payload', () => {
    const payload = { status: ProbeProgressStatus.Completed, note: 'done' };

    service.updateProbeProgressStatus(memberKey, 'probe-1', payload).subscribe(response => {
      expect(response.status).toBe(ProbeProgressStatus.Completed);
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}/probes/probe-1/progress/status`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush({ ...probeProgress, status: ProbeProgressStatus.Completed, completedAtUtc: '2026-04-15T12:00:00Z' });
  });

  it('updateProbeProgressStatus should invalidate targeted probe progress cache', () => {
    vi.spyOn(cache, 'invalidate');
    vi.spyOn(cache, 'invalidateByPrefix');

    service.updateProbeProgressStatus(memberKey, 'probe-1', { status: ProbeProgressStatus.Completed, note: 'done' }).subscribe();

    httpMock.expectOne(`${apiUrl}/${memberKey}/probes/probe-1/progress/status`)
      .flush({ ...probeProgress, status: ProbeProgressStatus.Completed });

    expect(cache.invalidate).toHaveBeenCalledWith(`${MEMBER_PROGRESS_CACHE_PREFIX}badges:${memberKey}`);
    expect(cache.invalidate).toHaveBeenCalledWith(`${MEMBER_PROGRESS_CACHE_PREFIX}probe:${memberKey}:probe-1`);
    expect(cache.invalidateByPrefix).toHaveBeenCalledWith(`${MEMBER_PROGRESS_CACHE_PREFIX}badge-review-queue:`);
  });
});
