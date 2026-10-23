import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, convertToParamMap, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { KurinService } from '../services/kurin-service/kurin.service';
import { KurinBranch } from '../models/enums/kurin-branch.enum';
import { KurinDto } from '../models/kurin.dto';
import { youthProgramGuard } from './youth-program.guard';

type GuardResult = Observable<boolean | UrlTree> | boolean | UrlTree;

describe('youthProgramGuard', () => {
  let kurinServiceSpy: MockedObject<KurinService>;
  let router: Router;

  beforeEach(() => {
    kurinServiceSpy = {
      getByKey: vi.fn()
    } as MockedObject<KurinService>;
    TestBed.configureTestingModule({
      providers: [{ provide: KurinService, useValue: kurinServiceSpy }]
    });
    router = TestBed.inject(Router);
  });

  function run(kurinKey: string | null): GuardResult {
    const route = { paramMap: convertToParamMap(kurinKey ? { kurinKey } : {}) } as ActivatedRouteSnapshot;
    return TestBed.runInInjectionContext(() =>
      youthProgramGuard()(route, {} as RouterStateSnapshot)) as GuardResult;
  }

  function resolve(result: Observable<boolean | UrlTree> | boolean | UrlTree): boolean | UrlTree {
    let value: boolean | UrlTree = false;
    if (result instanceof Observable) {
      result.subscribe(v => value = v);
      return value;
    }
    return result;
  }

  it('пропускає в курінь УПЮ', () => {
    kurinServiceSpy.getByKey.mockReturnValue(of({ branch: KurinBranch.UPYu } as KurinDto));

    expect(resolve(run('k-1'))).toBe(true);
  });

  it('у курені УСП веде на «тут такого немає»', () => {
    kurinServiceSpy.getByKey.mockReturnValue(of({ branch: KurinBranch.USP } as KurinDto));

    const result = resolve(run('k-1'));

    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/no-youth-program');
  });

  it('не прочитав курінь — пропускає', () => {
    kurinServiceSpy.getByKey.mockReturnValue(throwError(() => new Error('down')));

    expect(resolve(run('k-1'))).toBe(true);
  });
});
