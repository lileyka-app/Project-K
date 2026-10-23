import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { KurinJournalService } from './kurin-journal.service';
import { environment } from '../../../../../environments/environment';

describe('KurinJournalService', () => {
  let service: KurinJournalService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(KurinJournalService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends only the filters that are set', () => {
    service.getPage('k1', { area: 'Probe', memberKey: null, actorUserKey: '', skip: 20, take: 20 }).subscribe();

    const request = http.expectOne(req => req.url === `${environment.apiUrl}/kurin/k1/journal`);
    expect(request.request.params.keys().sort()).toEqual(['area', 'skip', 'take']);
    expect(request.request.params.get('area')).toBe('Probe');
    expect(request.request.params.get('skip')).toBe('20');
    request.flush({ items: [], hasMore: false });
  });
});
