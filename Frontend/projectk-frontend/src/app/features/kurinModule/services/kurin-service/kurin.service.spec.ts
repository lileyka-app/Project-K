import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { KurinService } from './kurin.service';
import { environment } from '../../../../../environments/environment';
import { KurinDto } from '../../models/kurin.dto';

describe('KurinService', () => {
  let service: KurinService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/kurin`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        KurinService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(KurinService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getKurins should call GET /kurins and return data', () => {
    const mockKurins = [
      { kurinKey: 'k1', number: 1 },
      { kurinKey: 'k2', number: 2 }
    ] as KurinDto[];

    let result: KurinDto[] | undefined;
    service.getKurins().subscribe(res => (result = res));

    const req = httpMock.expectOne(`${baseUrl}/kurins`);
    expect(req.request.method).toBe('GET');
    req.flush(mockKurins);

    expect(result).toEqual(mockKurins);
  });

  it('getKurins should reuse cached response within TTL', () => {
    const mockKurins = [
      { kurinKey: 'k1', number: 1 },
      { kurinKey: 'k2', number: 2 }
    ] as KurinDto[];

    service.getKurins().subscribe(res => {
      expect(res).toEqual(mockKurins);
    });

    httpMock.expectOne(`${baseUrl}/kurins`).flush(mockKurins);

    service.getKurins().subscribe(res => {
      expect(res).toEqual(mockKurins);
    });

    httpMock.expectNone(`${baseUrl}/kurins`);
  });

  it('createKurin should POST to /kurin with the number and край and return created entity', () => {
    const input = { kurinKey: 'k1', number: 3, kraiCode: 'cz' } as KurinDto;
    const created = { kurinKey: 'k1', number: 3 } as KurinDto;

    let result: KurinDto | undefined;
    service.createKurin(input).subscribe(res => (result = res));

    const req = httpMock.expectOne(`${baseUrl}`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ number: 3, kraiCode: 'cz' });
    expect(req.request.headers.get('Content-Type')).toBe('application/json');

    req.flush(created);

    expect(result).toEqual(created);
  });

  it('setKurinKrai should PUT the край to /kurin/{key}/krai', () => {
    service.setKurinKrai('k1', 'cz').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/k1/krai`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ kraiCode: 'cz' });
    req.flush({ kurinKey: 'k1', number: 3, kraiCode: 'cz' });
  });

  it('downloadReportPdf should request report as blob', () => {
    const kurinKey = 'k1';
    let result: Blob | null | undefined;

    service.downloadReportPdf(kurinKey).subscribe(response => {
      result = response.body;
    });

    const req = httpMock.expectOne(request => request.url === `${baseUrl}/${kurinKey}/report/pdf`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    // Пояс їде з запитом: години в документі мають бути ті, що показує годинник читача, а сервер
    // його поясу не знає. Порівнюємо з тим, що каже браузер, а не з жорстко вписаним «Europe/Kyiv»:
    // тест має проходити і в CI, який стоїть у UTC.
    expect(req.request.params.get('timeZone')).toBe(Intl.DateTimeFormat().resolvedOptions().timeZone);

    const blob = new Blob(['%PDF'], { type: 'application/pdf' });
    req.flush(blob);

    expect(result).toBe(blob);
  });

  it('updateKurin should PUT to /kurin/:kurinKey with profile payload and return updated entity', () => {
    const input = {
      kurinKey: 'k1',
      number: 5,
      stanytsia: 'Kyiv',
      regionOrCountry: 'Ukraine',
      namedAfter: 'Some Patron',
      description: 'Some longer notes',
      profileVerificationEnabled: true
    } as KurinDto;
    const updated = { ...input } as KurinDto;

    let result: KurinDto | undefined;
    service.updateKurin(input).subscribe(res => (result = res));

    const req = httpMock.expectOne(`${baseUrl}/${input.kurinKey}`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      number: 5,
      stanytsia: 'Kyiv',
      regionOrCountry: 'Ukraine',
      namedAfter: 'Some Patron',
      description: 'Some longer notes',
      profileVerificationEnabled: true
    });
    expect(req.request.headers.get('Content-Type')).toBe('application/json');

    req.flush(updated);

    expect(result).toEqual(updated);
  });

  it('updateKurin should invalidate kurin list cache', () => {
    const input = { kurinKey: 'k1', number: 5 } as KurinDto;
    const mockKurins = [{ kurinKey: 'k1', number: 1 }] as KurinDto[];

    service.getKurins().subscribe();
    httpMock.expectOne(`${baseUrl}/kurins`).flush(mockKurins);

    service.updateKurin(input).subscribe();
    httpMock.expectOne(`${baseUrl}/${input.kurinKey}`).flush(input);

    service.getKurins().subscribe();
    const req = httpMock.expectOne(`${baseUrl}/kurins`);
    expect(req.request.method).toBe('GET');
    req.flush([input]);
  });

  it('deleteKurin should call DELETE /kurin/:kurinKey', () => {
    const kurinKey = 'k1';

    let completed = false;
    service.deleteKurin(kurinKey).subscribe(() => (completed = true));

    const req = httpMock.expectOne(`${baseUrl}/${kurinKey}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    expect(completed).toBe(true);
  });
});
