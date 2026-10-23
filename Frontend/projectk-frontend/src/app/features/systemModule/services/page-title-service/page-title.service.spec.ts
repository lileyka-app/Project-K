import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, TitleStrategy, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { PageTitleService } from './page-title.service';
import { ProjectKTitleStrategy } from '../page-title.strategy';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinService } from '../../../kurinModule/services/kurin-service/kurin.service';
import { GroupService } from '../../../kurinModule/services/group-service/group.service';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import { KurinDto } from '../../../kurinModule/models/kurin.dto';
import { GroupDto } from '../../../kurinModule/models/group.dto';
import { MemberDto } from '../../../kurinModule/models/member.dto';
import { environment } from '../../../../../environments/environment';

const APP_NAME = environment.appName;

interface RouteLevel {
  params?: Record<string, string>;
  data?: Record<string, unknown>;
}

const createState = (levels: RouteLevel[]): RouterStateSnapshot => {
  const snapshots = levels.map(level => ({
    params: level.params ?? {},
    data: level.data ?? {},
    firstChild: null
  })) as unknown as ActivatedRouteSnapshot[];

  snapshots.forEach((snapshot, index) => {
    (snapshot as { firstChild: ActivatedRouteSnapshot | null }).firstChild =
      snapshots[index + 1] ?? null;
  });

  return { root: snapshots[0] } as RouterStateSnapshot;
};

describe('PageTitleService', () => {
  let service: PageTitleService;
  let title: MockedObject<Title>;
  let authService: MockedObject<AuthService>;
  let kurinService: MockedObject<KurinService>;
  let groupService: MockedObject<GroupService>;
  let memberService: MockedObject<MemberService>;

  const currentTitle = (): string => vi.mocked(title.setTitle).mock.lastCall![0];

  beforeEach(() => {
    title = {
      setTitle: vi.fn(),
      getTitle: vi.fn()
    } as MockedObject<Title>;
    authService = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    kurinService = {
      getByKey: vi.fn()
    } as MockedObject<KurinService>;
    groupService = {
      getByKey: vi.fn()
    } as MockedObject<GroupService>;
    memberService = {
      getByKey: vi.fn()
    } as MockedObject<MemberService>;

    TestBed.configureTestingModule({
      providers: [
        PageTitleService,
        { provide: Title, useValue: title },
        { provide: AuthService, useValue: authService },
        { provide: KurinService, useValue: kurinService },
        { provide: GroupService, useValue: groupService },
        { provide: MemberService, useValue: memberService }
      ]
    });

    service = TestBed.inject(PageTitleService);
  });

  it('shows the app name alone for routes without a title', () => {
    service.applyRouteState(null, createState([{}]));

    expect(currentTitle()).toBe(APP_NAME);
  });

  it('appends the app name to a static route title', () => {
    service.applyRouteState('Користувачі', createState([{}]));

    expect(currentTitle()).toBe(`Користувачі · ${APP_NAME}`);
  });

  it('resolves the kurin number from the route param', () => {
    kurinService.getByKey.mockReturnValue(of({ kurinKey: 'k-1', number: 12 } as KurinDto));

    service.applyRouteState('Курінь', createState([
      {},
      { params: { kurinKey: 'k-1' }, data: { titleContext: 'kurin' } }
    ]));

    expect(kurinService.getByKey).toHaveBeenCalledWith('k-1');
    expect(currentTitle()).toBe(`к. ч. 12 · ${APP_NAME}`);
  });

  it('falls back to the signed-in kurin when the route carries no key', () => {
    authService.getAuthStateValue.mockReturnValue({ kurinKey: 'k-7' } as ReturnType<AuthService['getAuthStateValue']>);
    kurinService.getByKey.mockReturnValue(of({ kurinKey: 'k-7', number: 7 } as KurinDto));

    service.applyRouteState('Курінь', createState([{}, { data: { titleContext: 'kurin' } }]));

    expect(kurinService.getByKey).toHaveBeenCalledWith('k-7');
    expect(currentTitle()).toBe(`к. ч. 7 · ${APP_NAME}`);
  });

  it('resolves the group name', () => {
    groupService.getByKey.mockReturnValue(of({ groupKey: 'g-1', name: 'Соколи' } as GroupDto));

    service.applyRouteState('Гурток', createState([
      {},
      { params: { groupKey: 'g-1' }, data: { titleContext: 'group' } }
    ]));

    expect(currentTitle()).toBe(`г. Соколи · ${APP_NAME}`);
  });

  it('resolves the member as last name then first name', () => {
    memberService.getByKey.mockReturnValue(of({
      memberKey: 'm-1',
      lastName: 'Муха',
      firstName: 'Ростислав'
    } as MemberDto));

    service.applyRouteState('Картка учасника', createState([
      {},
      { params: { memberKey: 'm-1' }, data: { titleContext: 'member' } }
    ]));

    expect(currentTitle()).toBe(`Муха Ростислав · ${APP_NAME}`);
  });

  it('keeps the route title when the entity cannot be loaded', () => {
    memberService.getByKey.mockReturnValue(throwError(() => new Error('offline')));

    service.applyRouteState('Картка учасника', createState([
      {},
      { params: { memberKey: 'm-1' }, data: { titleContext: 'member' } }
    ]));

    expect(currentTitle()).toBe(`Картка учасника · ${APP_NAME}`);
  });

  it('drops the previous entity when the next navigation lands', () => {
    groupService.getByKey.mockReturnValue(of({ groupKey: 'g-1', name: 'Соколи' } as GroupDto));
    service.applyRouteState('Гурток', createState([
      {},
      { params: { groupKey: 'g-1' }, data: { titleContext: 'group' } }
    ]));

    service.applyRouteState('Адміністрація', createState([{}]));

    expect(currentTitle()).toBe(`Адміністрація · ${APP_NAME}`);
  });

  it('ignores a resolve that lands after the user navigated away', () => {
    const slowGroup = new Subject<GroupDto>();
    groupService.getByKey.mockReturnValue(slowGroup.asObservable());

    service.applyRouteState('Гурток', createState([
      {},
      { params: { groupKey: 'g-1' }, data: { titleContext: 'group' } }
    ]));

    service.applyRouteState('Адміністрація', createState([{}]));
    slowGroup.next({ groupKey: 'g-1', name: 'Соколи' } as GroupDto);

    expect(currentTitle()).toBe(`Адміністрація · ${APP_NAME}`);
  });

  it('lets a page publish its context directly', () => {
    service.applyRouteState('Гурток', createState([{}]));
    service.setContext('г. Беркути');

    expect(currentTitle()).toBe(`г. Беркути · ${APP_NAME}`);
  });
});

describe('ProjectKTitleStrategy wiring', () => {
  it('resolves through a real Router without closing a DI cycle', () => {
    // PageTitleService is built by the TitleStrategy the Router owns, so injecting the
    // Router into it would fail bootstrap with NG0200 — a stubbed Router hides that.
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: TitleStrategy, useClass: ProjectKTitleStrategy }
      ]
    });

    expect(() => TestBed.inject(Router)).not.toThrow();
    expect(TestBed.inject(TitleStrategy)).toBeInstanceOf(ProjectKTitleStrategy);
  });
});
