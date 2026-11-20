import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { MembershipService } from './membership.service';
import { environment } from '../../../../../environments/environment';

describe('MembershipService', () => {
  let service: MembershipService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MembershipService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(MembershipService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // The menu asks on every page, so it reads the count, not the requests with their debts.
  it('reads only the count of waiting requests for the menu dot', () => {
    let waiting = -1;
    service.waitingRequests('k1').subscribe(count => (waiting = count));

    const req = httpMock.expectOne(`${environment.apiUrl}/kurin/k1/membership-requests/waiting`);
    expect(req.request.method).toBe('GET');
    req.flush(3);
    expect(waiting).toBe(3);
  });
});
