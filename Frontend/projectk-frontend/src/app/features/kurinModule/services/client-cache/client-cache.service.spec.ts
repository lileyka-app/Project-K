import type { Mock } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ClientCacheService } from './client-cache.service';

describe('ClientCacheService', () => {
  let service: ClientCacheService;
  let debugSpy: Mock;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ClientCacheService);
    debugSpy = vi.spyOn(console, 'debug').mockReturnValue(undefined);
  });

  afterEach(() => {
    service.clear();
  });

  it('should log cache miss and hit', () => {
    let factoryCalls = 0;
    const factory = () => {
      factoryCalls++;
      return of('value');
    };

    service.get('members:group:1', 1000, factory).subscribe();
    service.get('members:group:1', 1000, factory).subscribe();

    expect(factoryCalls).toBe(1);
    expect(debugSpy).toHaveBeenCalledWith('[ClientCache]', 'miss', {
      key: 'members:group:1',
      reason: 'empty'
    });
    expect(debugSpy).toHaveBeenCalledWith('[ClientCache]', 'hit', expect.objectContaining({
      key: 'members:group:1'
    }));
  });

  it('should log expired cache miss', () => {
    service.get('members:group:1', -1, () => of('old')).subscribe();
    service.get('members:group:1', 1000, () => of('new')).subscribe();

    expect(debugSpy).toHaveBeenCalledWith('[ClientCache]', 'miss', {
      key: 'members:group:1',
      reason: 'expired'
    });
  });

  it('should log single-key invalidation', () => {
    service.get('members:group:1', 1000, () => of('value')).subscribe();

    service.invalidate('members:group:1');
    service.invalidate('members:group:1');

    expect(debugSpy).toHaveBeenCalledWith('[ClientCache]', 'invalidate', {
      key: 'members:group:1',
      invalidatedCount: 1
    });
    expect(debugSpy).toHaveBeenCalledWith('[ClientCache]', 'invalidate', {
      key: 'members:group:1',
      invalidatedCount: 0
    });
  });

  it('should log prefix invalidation count', () => {
    service.get('members:group:1', 1000, () => of('group')).subscribe();
    service.get('members:kurin:1', 1000, () => of('kurin')).subscribe();
    service.get('groups:kurin:1', 1000, () => of('groups')).subscribe();

    service.invalidateByPrefix('members:');

    expect(debugSpy).toHaveBeenCalledWith('[ClientCache]', 'invalidate', {
      prefix: 'members:',
      invalidatedCount: 2
    });
  });
});
