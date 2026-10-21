import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../../../kurinModule/services/client-cache/client-cache.service';
import { DuesEntryKind, DuesPaymentMethod } from '../../models/dues.enums';
import { GroupDuesDto } from '../../models/group-dues.dto';
import { DuesService } from './dues.service';

describe('DuesService', () => {
  let service: DuesService;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/group/g1/dues`;

  const box = { groupKey: 'g1', groupName: 'Соколи', accounts: [], entries: [] } as unknown as GroupDuesDto;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(DuesService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(ClientCacheService).clear();
  });

  afterEach(() => http.verify());

  it('читає касу гуртка один раз, поки кеш живий', () => {
    service.getGroupDues('g1').subscribe();
    http.expectOne(baseUrl).flush(box);

    let second: GroupDuesDto | undefined;
    service.getGroupDues('g1').subscribe(result => second = result);

    http.expectNone(baseUrl);
    expect(second?.groupName).toBe('Соколи');
  });

  // Кожен запис рухає баланси, тож наступне читання має піти на сервер, а не в кеш.
  it('після запису операції кеш каси скидається', () => {
    service.getGroupDues('g1').subscribe();
    http.expectOne(baseUrl).flush(box);

    service.createEntry('g1', {
      kind: DuesEntryKind.Contribution,
      method: DuesPaymentMethod.Cash,
      counterMethod: null,
      amount: 300,
      occurredOn: '2026-05-01',
      membershipKey: 'm1',
      collectedByMemberKey: null,
      note: null
    }).subscribe();
    const post = http.expectOne(`${baseUrl}/entries`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body.amount).toBe(300);
    post.flush({});

    service.getGroupDues('g1').subscribe();
    http.expectOne(baseUrl).flush(box);
  });

  it('позначка «Перевірено» іде на свій маршрут', () => {
    service.setEntryVerified('g1', 'e1', true).subscribe();

    const put = http.expectOne(`${baseUrl}/entries/e1/verified`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ isVerified: true });
    put.flush({});
  });

  it('ставка й пільга беруть квартал як рік і номер', () => {
    service.setGroupRate('g1', { fromQuarter: { year: 2026, number: 2 }, groupShare: 45 }).subscribe();
    http.expectOne(`${baseUrl}/rate`).flush({});

    service.setConcession('g1', 'm1', { fromQuarter: { year: 2026, number: 3 }, isConcession: true }).subscribe();
    const put = http.expectOne(`${baseUrl}/members/m1/concession`);
    expect(put.request.body.fromQuarter).toEqual({ year: 2026, number: 3 });
    put.flush({});
  });

  it('пільги краю надаються людині, а змінюються й видаляються за ключем наданої', () => {
    const request = { concessionKey: 'sibling', fromQuarter: { year: 2026, number: 3 }, toQuarter: null, reason: null };
    service.grantConcession('g1', 'm1', request).subscribe();
    const post = http.expectOne(`${baseUrl}/members/m1/concessions`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual(request);
    post.flush('k1');

    service.updateConcessionGrant('g1', 'k1', { toQuarter: { year: 2026, number: 4 }, reason: 'брат' }).subscribe();
    expect(http.expectOne(`${baseUrl}/concessions/k1`).request.method).toBe('PUT');

    service.deleteConcessionGrant('g1', 'k1').subscribe();
    expect(http.expectOne(`${baseUrl}/concessions/k1`).request.method).toBe('DELETE');
  });
});
