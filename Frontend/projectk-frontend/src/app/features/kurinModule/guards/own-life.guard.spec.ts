import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, convertToParamMap, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from '../../authModule/services/auth-service/auth.service';
import { ownLifeGuard } from './own-life.guard';

describe('ownLifeGuard', () => {
  const run = (routeKey: string, ownKey: string | null) => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { getAuthStateValue: () => ({ memberKey: ownKey }) } }]
    });
    const route = { paramMap: convertToParamMap({ memberKey: routeKey }) } as unknown as ActivatedRouteSnapshot;
    return TestBed.runInInjectionContext(() => ownLifeGuard(route, {} as RouterStateSnapshot));
  };

  it('пускає людину до свого життєпису', () => {
    expect(run('ABC', 'abc')).toBe(true);
  });

  it('чужий життєпис веде на картку людини', () => {
    const result = run('abc', 'other') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/member/abc');
  });

  it('без своєї картки життєпису немає', () => {
    expect(run('abc', null)).toBeInstanceOf(UrlTree);
  });
});
