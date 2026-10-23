import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';
import { LoginRequest } from '../../models/login-request.model';
import { LoginResponse } from '../../models/login-response.model';
import { AuthState } from '../../models/auth-state.model';
import { environment } from '../../../../../environments/environment';
import { ClientCacheService } from '../../../kurinModule/services/client-cache/client-cache.service';
import { ACCESS_CACHE_PREFIX, MEMBER_CACHE_PREFIX } from '../../../kurinModule/services/client-cache/cache-policy';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const apiUrl = environment.apiUrl;

  beforeEach(() => {
    localStorage.clear();
    
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('constructor', () => {
    it('should load auth state from localStorage on initialization', () => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };
      const expectedHydratedState: AuthState = {
        ...mockAuthState,
        accessToken: null
      };

      localStorage.setItem('authState', JSON.stringify(mockAuthState));

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [AuthService, provideHttpClient(), provideHttpClientTesting()]
      });

      const newService = TestBed.inject(AuthService);
      
      newService.getAuthState().subscribe(state => {
        expect(state).toEqual(expectedHydratedState);
      });
    });

    it('should have null auth state when localStorage is empty', () => {
      service.getAuthState().subscribe(state => {
        expect(state).toBeNull();
      });
    });
  });

  describe('getAuthState', () => {
    it('should return auth state as observable', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe();

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });

      service.getAuthState().subscribe(state => {
        expect(state).toEqual(mockAuthState);
        done();
      });
    }));
  });

  describe('getAuthStateValue', () => {
    it('should return current auth state value', () => {
      expect(service.getAuthStateValue()).toBeNull();
    });

    it('should return updated auth state value after login', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        expect(service.getAuthStateValue()).toEqual(mockAuthState);
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));
  });

  describe('login', () => {
    it('should send login request and update auth state', () => new Promise<void>(done => {
      const credentials: LoginRequest = {
        email: 'test@example.com',
        password: 'password123'
      };

      const mockResponse: LoginResponse = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        requiresMfa: false,
        tokens: {
          accessToken: 'access-token-789'
        }
      };

      const expectedState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'access-token-789'
      };

      service.login(credentials).subscribe(state => {
        expect(state).toEqual(mockResponse);
        expect(service.getAuthStateValue()).toEqual(expectedState);
        expect(localStorage.getItem('authState')).toBe(JSON.stringify({ ...expectedState, accessToken: null }));
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(credentials);
      expect(req.request.withCredentials).toBe(true);
      req.flush(mockResponse);
    }));

    it('should not update auth state when login requires mfa', () => new Promise<void>(done => {
      const credentials: LoginRequest = {
        email: 'mfa@example.com',
        password: 'password123'
      };

      const mockResponse: LoginResponse = {
        userKey: 'user-123',
        memberKey: null,
        email: 'mfa@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        requiresMfa: true,
        tokens: null
      };

      service.login(credentials).subscribe(response => {
        expect(response).toEqual(mockResponse);
        expect(service.getAuthStateValue()).toBeNull();
        expect(localStorage.getItem('authState')).toBeNull();
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      expect(req.request.method).toBe('POST');
      expect(req.request.withCredentials).toBe(true);
      req.flush(mockResponse);
    }));

    it('should verify mfa login and update auth state', () => new Promise<void>(done => {
      const mockResponse: LoginResponse = {
        userKey: 'user-123',
        memberKey: 'member-123',
        email: 'mfa@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        requiresMfa: false,
        tokens: {
          accessToken: 'mfa-access-token'
        }
      };

      const expectedState: AuthState = {
        userKey: 'user-123',
        memberKey: 'member-123',
        email: 'mfa@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'mfa-access-token'
      };

      service.verifyMfaLogin('mfa@example.com', '123456', 'challenge').subscribe(state => {
        expect(state).toEqual(expectedState);
        expect(service.getAuthStateValue()).toEqual(expectedState);
        expect(localStorage.getItem('authState')).toBe(JSON.stringify({ ...expectedState, accessToken: null }));
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/mfa/login-verify`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'mfa@example.com', code: '123456', rememberMe: true, mfaToken: 'challenge' });
      expect(req.request.withCredentials).toBe(true);
      req.flush(mockResponse);
    }));

    it('should fail mfa login when tokens are missing', () => new Promise<void>(done => {
      const mockResponse: LoginResponse = {
        userKey: 'user-123',
        memberKey: null,
        email: 'mfa@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        requiresMfa: false,
        tokens: null
      };

      service.verifyMfaLogin('mfa@example.com', '123456', 'challenge').subscribe({
        next: () => expect.fail('should have failed'),
        error: error => {
          expect(error.message).toBe('No tokens in response');
          expect(service.getAuthStateValue()).toBeNull();
          done();
        }
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/mfa/login-verify`);
      req.flush(mockResponse);
    }));

    it('should request mfa setup with credentials', () => new Promise<void>(done => {
      const setup = {
        sharedKey: 'shared-key',
        authenticatorUri: 'otpauth://totp/Project-K:user@example.com',
        qrCodeBase64: 'qr-base64'
      };

      service.getMfaSetup().subscribe(response => {
        expect(response).toEqual(setup);
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/mfa/setup`);
      expect(req.request.method).toBe('GET');
      expect(req.request.withCredentials).toBe(true);
      req.flush(setup);
    }));

    it('should enable mfa with verification code', () => new Promise<void>(done => {
      const response = { enabled: true, recoveryCodes: ['code-1', 'code-2'] };

      service.enableMfa('123456').subscribe(result => {
        expect(result).toEqual(response);
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/mfa/enable`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ code: '123456' });
      expect(req.request.withCredentials).toBe(true);
      req.flush(response);
    }));

    it('takes the fresh session enabling mfa hands back', () => new Promise<void>(done => {
      const initialState: AuthState = {
        userKey: 'user-123',
        memberKey: null,
        email: 'test@example.com',
        isAdmin: false,
        permissions: [],
        roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'password-only-token'
      };
      localStorage.setItem('authState', JSON.stringify(initialState));
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [AuthService, provideHttpClient(), provideHttpClientTesting()]
      });
      service = TestBed.inject(AuthService);
      httpMock = TestBed.inject(HttpTestingController);

      service.enableMfa('123456').subscribe(() => {
        expect(service.getAuthStateValue()?.accessToken).toBe('mfa-token');
        expect(service.getAuthStateValue()?.userKey).toBe('user-123');
        done();
      });

      httpMock.expectOne(`${apiUrl}/auth/mfa/enable`).flush({
        enabled: true,
        recoveryCodes: ['code-1'],
        tokens: { accessToken: 'mfa-token', refreshToken: { token: 'r', expires: '2027-01-01T00:00:00Z' } }
      });
    }));

    it('should rotate mfa recovery codes with current password', () => new Promise<void>(done => {
      const response = { recoveryCodes: ['code-1', 'code-2'] };

      service.rotateMfaRecoveryCodes('current-password').subscribe(result => {
        expect(result).toEqual(response);
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/mfa/recovery-codes`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ currentPassword: 'current-password' });
      expect(req.request.withCredentials).toBe(true);
      req.flush(response);
    }));

    it('should request mfa status with credentials', () => new Promise<void>(done => {
      service.getMfaStatus().subscribe(response => {
        expect(response).toEqual({ isMfaEnabled: true, isMfaRequired: true });
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/mfa/status`);
      expect(req.request.method).toBe('GET');
      expect(req.request.withCredentials).toBe(true);
      req.flush({ isMfaEnabled: true, isMfaRequired: true });
    }));

    it('should handle login error', () => new Promise<void>(done => {
      const credentials: LoginRequest = {
        email: 'test@example.com',
        password: 'wrong-password'
      };

      service.login(credentials).subscribe({
        next: () => expect.fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(401);
          expect(service.getAuthStateValue()).toBeNull();
          done();
        }
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    }));
  });

  describe('logout', () => {
    it('should send logout request and clear auth state', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.logout().subscribe(() => {
          expect(service.getAuthStateValue()).toBeNull();
          expect(localStorage.getItem('authState')).toBeNull();
          done();
        });

        const logoutReq = httpMock.expectOne(`${apiUrl}/auth/logout`);
        expect(logoutReq.request.method).toBe('POST');
        expect(logoutReq.request.withCredentials).toBe(true);
        expect(logoutReq.request.responseType).toBe('text');
        logoutReq.flush('Logged out successfully');
      });

      const loginReq = httpMock.expectOne(`${apiUrl}/auth/login`);
      loginReq.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));

    it('sends the bearer token with the sign-out and only then forgets the session', () => new Promise<void>(done => {
      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.logout().subscribe(() => {
          expect(service.getAuthStateValue()).toBeNull();
          done();
        });

        // The token is still in hand while the request is in flight: that is what the
        // interceptor puts in the Authorization header.
        expect(service.getAccessToken()).toBe('token-789');
        httpMock.expectOne(`${apiUrl}/auth/logout`).flush('Logged out successfully');
      });

      httpMock.expectOne(`${apiUrl}/auth/login`).flush({
        userKey: 'user-123', memberKey: 'm', email: 'test@example.com', isAdmin: false,
        permissions: [], roles: [], kurinKey: null, tokens: { accessToken: 'token-789' }
      });
    }));

    it('refreshes an expired token once and signs out again before forgetting the session', () => new Promise<void>(done => {
      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.logout().subscribe(() => {
          expect(service.getAuthStateValue()).toBeNull();
          done();
        });

        httpMock.expectOne(`${apiUrl}/auth/logout`).flush('expired', { status: 401, statusText: 'Unauthorized' });
        httpMock.expectOne(`${apiUrl}/auth/refresh`).flush({ accessToken: 'fresh-token' });
        expect(service.getAccessToken()).toBe('fresh-token');
        httpMock.expectOne(`${apiUrl}/auth/logout`).flush('Logged out successfully');
      });

      httpMock.expectOne(`${apiUrl}/auth/login`).flush({
        userKey: 'user-123', memberKey: 'm', email: 'test@example.com', isAdmin: false,
        permissions: [], roles: [], kurinKey: null, tokens: { accessToken: 'token-789' }
      });
    }));

    it('should clear auth state if logout request fails', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.logout().subscribe({
          next: () => expect.fail('should have failed'),
          error: () => {
            expect(service.getAuthStateValue()).toBeNull();
            expect(localStorage.getItem('authState')).toBeNull();
            done();
          }
        });

        const logoutReq = httpMock.expectOne(`${apiUrl}/auth/logout`);
        logoutReq.flush('Server error', { status: 500, statusText: 'Internal Server Error' });
      });

      const loginReq = httpMock.expectOne(`${apiUrl}/auth/login`);
      loginReq.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));
  });

  describe('refreshToken', () => {
    it('should refresh access token and update auth state', () => new Promise<void>(done => {
      const initialState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'old-token'
      };

      const newAccessToken = 'new-access-token';

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.refreshToken().subscribe(token => {
          expect(token).toBe(newAccessToken);
          
          const updatedState = service.getAuthStateValue();
          expect(updatedState?.accessToken).toBe(newAccessToken);
          expect(updatedState?.userKey).toBe(initialState.userKey);
          expect(updatedState?.email).toBe(initialState.email);
          
          const savedState = localStorage.getItem('authState');
          expect(savedState).toBeTruthy();
          const parsedState = JSON.parse(savedState!);
          expect(parsedState.accessToken).toBeNull();
          done();
        });

        const refreshReq = httpMock.expectOne(`${apiUrl}/auth/refresh`);
        expect(refreshReq.request.method).toBe('POST');
        expect(refreshReq.request.withCredentials).toBe(true);
        refreshReq.flush({ accessToken: newAccessToken });
      });

      const loginReq = httpMock.expectOne(`${apiUrl}/auth/login`);
      loginReq.flush({
        userKey: initialState.userKey,
        email: initialState.email,
        isAdmin: initialState.isAdmin, permissions: initialState.permissions, roles: initialState.roles,
        kurinKey: initialState.kurinKey,
        tokens: { accessToken: initialState.accessToken }
      });
    }));

    it('should not update state if no auth state exists', () => new Promise<void>(done => {
      service.refreshToken().subscribe(token => {
        expect(token).toBe('new-token');
        expect(service.getAuthStateValue()).toBeNull();
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/refresh`);
      req.flush({ accessToken: 'new-token' });
    }));

    it('should handle refresh token error', () => new Promise<void>(done => {
      service.refreshToken().subscribe({
        next: () => expect.fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(401);
          done();
        }
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/refresh`);
      req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    }));

    it('should coalesce concurrent refresh token requests', () => new Promise<void>(done => {
      const results: string[] = [];

      service.refreshToken().subscribe(token => {
        results.push(token);
      });
      service.refreshToken().subscribe(token => {
        results.push(token);
      });

      const requests = httpMock.match(`${apiUrl}/auth/refresh`);
      expect(requests).toHaveLength(1);
      requests[0].flush({ accessToken: 'shared-access-token' });

      setTimeout(() => {
        expect(results).toEqual(['shared-access-token', 'shared-access-token']);
        done();
      });
    }));
  });

  describe('applyAccess', () => {
    const youth: LoginResponse = {
      userKey: 'user-1',
      memberKey: 'member-1',
      email: 'youth@example.com',
      isAdmin: false,
      permissions: ['Member:Read:KurinWide', 'Group:Read:KurinWide'],
      roles: ['Member'],
      kurinKey: 'kurin-1',
      requiresMfa: false,
      tokens: { accessToken: 'token-1' }
    };

    let invalidated: string[];

    beforeEach(() => {
      service.applyLoginResponse(youth);
      const cache = TestBed.inject(ClientCacheService);
      invalidated = [];
      vi.spyOn(cache, 'invalidateByPrefix').mockImplementation(prefix => {
        invalidated.push(prefix);
      });
    });

    it('leaves the state and the caches alone when nothing changed, whatever the order', () => {
      const emitted: (AuthState | null)[] = [];
      const subscription = service.getAuthState().subscribe(state => emitted.push(state));

      const changed = service.applyAccess({
        isAdmin: false,
        roles: ['Member'],
        permissions: ['Group:Read:KurinWide', 'Member:Read:KurinWide'],
        kurinKey: 'kurin-1'
      });
      subscription.unsubscribe();

      expect(changed).toBe(false);
      expect(invalidated).toEqual([]);
      expect(emitted).toHaveLength(1);
    });

    it('takes a new office, persists it and drops the access and member caches', () => {
      const changed = service.applyAccess({
        isAdmin: false,
        roles: ['Member', 'Group.Hurtkoviy'],
        permissions: [...youth.permissions, 'Group:Update:OwnGroups'],
        kurinKey: 'kurin-1'
      });

      expect(changed).toBe(true);
      expect(service.getAuthStateValue()?.roles).toEqual(['Member', 'Group.Hurtkoviy']);
      expect(service.getAuthStateValue()?.permissions).toContain('Group:Update:OwnGroups');
      expect(service.getAuthStateValue()?.accessToken).toBe('token-1');
      expect(JSON.parse(localStorage.getItem('authState')!).permissions).toContain('Group:Update:OwnGroups');
      expect(invalidated).toEqual(expect.arrayContaining([ACCESS_CACHE_PREFIX, MEMBER_CACHE_PREFIX]));
    });

    it('follows a kurin the server moved the account to', () => {
      expect(service.applyAccess({ ...accessOf(youth), kurinKey: 'kurin-2' })).toBe(true);
      expect(service.getAuthStateValue()?.kurinKey).toBe('kurin-2');
    });

    it('is applied by a refresh that carries it', () => {
      service.refreshToken().subscribe();
      httpMock.expectOne(`${apiUrl}/auth/refresh`).flush({
        accessToken: 'token-2',
        access: { ...accessOf(youth), permissions: [...youth.permissions, 'MemberWarning:Create:OwnGroups'] }
      });

      expect(service.getAuthStateValue()?.accessToken).toBe('token-2');
      expect(service.getAuthStateValue()?.permissions).toContain('MemberWarning:Create:OwnGroups');
      expect(invalidated).toContain(ACCESS_CACHE_PREFIX);
    });

    it('keeps the access as it was when an older server answers without it', () => {
      service.refreshToken().subscribe();
      httpMock.expectOne(`${apiUrl}/auth/refresh`).flush({ accessToken: 'token-2' });

      expect(service.getAuthStateValue()?.accessToken).toBe('token-2');
      expect(service.getAuthStateValue()?.permissions).toEqual(youth.permissions);
      expect(invalidated).toEqual([]);
    });

    function accessOf(response: LoginResponse) {
      return {
        isAdmin: response.isAdmin,
        roles: response.roles,
        permissions: response.permissions,
        kurinKey: response.kurinKey
      };
    }
  });

  describe('ensureAccessToken', () => {
    it('should refresh token when hydrated state has no access token', () => new Promise<void>(done => {
      const storedState: AuthState = {
        userKey: 'user-123',
        memberKey: 'member-123',
        email: 'admin@example.com',
        isAdmin: true, permissions: [], roles: ['Admin'],
        kurinKey: null,
        accessToken: null
      };
      localStorage.setItem('authState', JSON.stringify(storedState));

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [AuthService, provideHttpClient(), provideHttpClientTesting()]
      });

      const hydratedService = TestBed.inject(AuthService);
      const hydratedHttpMock = TestBed.inject(HttpTestingController);

      hydratedService.ensureAccessToken().subscribe(isAuthenticated => {
        expect(isAuthenticated).toBe(true);
        expect(hydratedService.getAuthStateValue()?.accessToken).toBe('new-access-token');
        hydratedHttpMock.verify();
        done();
      });

      const req = hydratedHttpMock.expectOne(`${apiUrl}/auth/refresh`);
      expect(req.request.method).toBe('POST');
      expect(req.request.withCredentials).toBe(true);
      req.flush({ accessToken: 'new-access-token' });
    }));
  });

  describe('getAccessToken', () => {
    it('should return null when no auth state exists', () => {
      expect(service.getAccessToken()).toBeNull();
    });

    it('should return access token when auth state exists', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        expect(service.getAccessToken()).toBe('token-789');
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));
  });

  describe('setKurinKey', () => {
    it('should update kurin key in auth state', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.setKurinKey('new-kurin-key');

        const updatedState = service.getAuthStateValue();
        expect(updatedState?.kurinKey).toBe('new-kurin-key');
        expect(updatedState?.userKey).toBe(mockAuthState.userKey);
        
        const savedState = localStorage.getItem('authState');
        expect(savedState).toBeTruthy();
        const parsedState = JSON.parse(savedState!);
        expect(parsedState.kurinKey).toBe('new-kurin-key');
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));

    it('should set kurin key to null', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.setKurinKey(null);

        const updatedState = service.getAuthStateValue();
        expect(updatedState?.kurinKey).toBeNull();
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));

    it('should not update state if no auth state exists', () => {
      service.setKurinKey('new-kurin-key');
      expect(service.getAuthStateValue()).toBeNull();
    });
  });

  describe('clearKurinKey', () => {
    it('should clear kurin key from auth state', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
      memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.clearKurinKey();

        const updatedState = service.getAuthStateValue();
        expect(updatedState?.kurinKey).toBeNull();
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));

    it('should not affect state if no auth state exists', () => {
      service.clearKurinKey();
      expect(service.getAuthStateValue()).toBeNull();
    });
  });

  describe('updatePermissions', () => {
    it('should update role in auth state and persisted storage', () => new Promise<void>(done => {
      const mockAuthState: AuthState = {
        userKey: 'user-123',
        memberKey: 'test-member-key',
        email: 'test@example.com',
        isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
        kurinKey: 'kurin-456',
        accessToken: 'token-789'
      };

      service.login({ email: 'test@example.com', password: 'password' }).subscribe(() => {
        service.updatePermissions(['Group:Update:OwnGroups'], ['Group.Hurtkoviy'], false);

        expect(service.getAuthStateValue()?.roles).toEqual(['Group.Hurtkoviy']);
        expect(JSON.parse(localStorage.getItem('authState')!).roles).toEqual(['Group.Hurtkoviy']);
        done();
      });

      const req = httpMock.expectOne(`${apiUrl}/auth/login`);
      req.flush({
        userKey: mockAuthState.userKey,
        memberKey: mockAuthState.memberKey,
        email: mockAuthState.email,
        isAdmin: mockAuthState.isAdmin, permissions: mockAuthState.permissions, roles: mockAuthState.roles,
        kurinKey: mockAuthState.kurinKey,
        tokens: { accessToken: mockAuthState.accessToken }
      });
    }));
  });
});

