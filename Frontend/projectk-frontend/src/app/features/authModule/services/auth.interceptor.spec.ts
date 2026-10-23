import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HTTP_INTERCEPTORS, HttpClient, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { AuthInterceptor } from './auth.interceptor';
import { AuthService } from './auth-service/auth.service';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';

describe('AuthInterceptor', () => {
  let httpMock: HttpTestingController;
  let httpClient: HttpClient;
  let mockAuthService: MockedObject<AuthService>;
  let mockRouter: MockedObject<Router>;

  beforeEach(() => {
    mockAuthService = {
      getAccessToken: vi.fn(),
      refreshToken: vi.fn(),
      clearLocalState: vi.fn()
    } as MockedObject<AuthService>;
    mockRouter = {
      navigate: vi.fn()
    } as MockedObject<Router>;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter },
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
      ]
    });

    httpMock = TestBed.inject(HttpTestingController);
    httpClient = TestBed.inject(HttpClient);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should hand a 403 to the caller without navigating', () => {
    // Navigating from here bounced the whole app off a page the user legitimately held whenever any
    // secondary request was refused, and it fired before the components that answer 403 inline could
    // show their message. Refusing a page is the router guards' job.
    mockAuthService.getAccessToken.mockReturnValue('valid-token');
    const seen: number[] = [];

    httpClient.get('/api/leadership/some-key').subscribe({
      next: () => expect.fail('should have failed with 403'),
      error: (error) => seen.push(error.status)
    });

    httpMock.expectOne('/api/leadership/some-key')
      .flush('Forbidden', { status: 403, statusText: 'Forbidden' });

    expect(seen).toEqual([403]);
    expect(mockRouter.navigate).not.toHaveBeenCalled();
  });

  it('should not try to refresh token if 401 occurs on /api/auth/logout', () => {
    mockAuthService.getAccessToken.mockReturnValue('valid-token');

    httpClient.post('/api/auth/logout', {}).subscribe({
      next: () => expect.fail('should have failed with 401'),
      error: (error) => {
        expect(error.status).toBe(401);
      }
    });

    const req = httpMock.expectOne('/api/auth/logout');
    expect(req.request.headers.get('Authorization')).toBe('Bearer valid-token');
    
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(mockAuthService.refreshToken).not.toHaveBeenCalled();
  });

  it('should not try to refresh token if 401 occurs on /api/auth/login', () => {
    mockAuthService.getAccessToken.mockReturnValue(null);

    httpClient.post('/api/auth/login', { email: 'bad', password: 'bad' }).subscribe({
      next: () => expect.fail('should have failed with 401'),
      error: (error) => {
        expect(error.status).toBe(401);
      }
    });

    const req = httpMock.expectOne('/api/auth/login');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(mockAuthService.refreshToken).not.toHaveBeenCalled();
    expect(mockAuthService.clearLocalState).not.toHaveBeenCalled();
    expect(mockRouter.navigate).not.toHaveBeenCalled();
  });

  it('should call clearLocalState and navigate to login when refresh fails', () => {
    mockAuthService.getAccessToken.mockReturnValue('valid-token');
    mockAuthService.refreshToken.mockReturnValue(throwError(() => new Error('refresh failed')));

    httpClient.get('/api/data').subscribe({
      next: () => expect.fail('should have failed'),
      error: (error) => {
        expect(error.message).toBe('Session expired');
        expect(mockAuthService.clearLocalState).toHaveBeenCalled();
        expect(mockRouter.navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
      }
    });

    const req = httpMock.expectOne('/api/data');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
  });
});
