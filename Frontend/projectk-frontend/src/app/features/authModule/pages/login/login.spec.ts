import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login';
import { AuthState } from '../../models/auth-state.model';
import { LoginResponse } from '../../models/login-response.model';
import { AuthService } from '../../services/auth-service/auth.service';
import { DemoService } from '../../services/demo-service/demo.service';
import { environment } from '../../../../../environments/environment';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: MockedObject<AuthService>;
  let router: MockedObject<Router>;
  let messageService: MockedObject<MessageService>;

  beforeEach(async () => {
    authService = {
      login: vi.fn(),
      verifyMfaLogin: vi.fn(),
      getAuthStateValue: vi.fn(),
      getSetupStatus: vi.fn()
    } as MockedObject<AuthService>;
    // ngOnInit calls it on every fixture, so it has to emit before detectChanges.
    authService.getSetupStatus.mockReturnValue(of({ isInitialized: true }));
    router = {
      navigate: vi.fn()
    } as MockedObject<Router>;
    messageService = {
      add: vi.fn()
    } as MockedObject<MessageService>;

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        { provide: MessageService, useValue: messageService }
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should send an uninitialized system to setup', () => {
    authService.getSetupStatus.mockReturnValue(of({ isInitialized: false }));

    TestBed.createComponent(LoginComponent).detectChanges();

    expect(router.navigate).toHaveBeenCalledWith(['/setup']);
  });

  it('should stay on login when the setup status cannot be read', () => {
    authService.getSetupStatus.mockReturnValue(throwError(() => new Error('offline')));

    TestBed.createComponent(LoginComponent).detectChanges();

    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should submit credentials to auth service', () => {
    const state = createAuthState();
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'test@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      email: 'test@example.com',
      password: 'password123'
    }, false);
  });

  it('should show otp input when login requires mfa', () => {
    authService.login.mockReturnValue(of(createLoginResponse({ requiresMfa: true, tokens: null })));

    component.email = 'mfa@example.com';
    component.password = 'password123';
    component.otpValue = '123456';
    component.useRecoveryCode = true;

    component.onSubmit();

    expect(component.showOtpInput).toBe(true);
    expect(component.otpValue).toBe('');
    expect(component.useRecoveryCode).toBe(false);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should verify otp and navigate after mfa succeeds', () => {
    const state = createAuthState({ memberKey: 'member-123' });
    authService.verifyMfaLogin.mockReturnValue(of(state));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'mfa@example.com';
    component.showOtpInput = true;
    component.otpValue = '123456';
    component.mfaToken = 'challenge';

    component.onSubmit();

    expect(authService.verifyMfaLogin).toHaveBeenCalledWith('mfa@example.com', '123456', 'challenge', false);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should navigate to active kurin for an admin with a selected kurin and no card', () => {
    const state = createAuthState({ isAdmin: true, permissions: [], roles: ['Admin'], memberKey: null, kurinKey: 'kurin-123' });
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'admin@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/kurin']);
  });

  it('should navigate to admin panel for an admin without a kurin or a card', () => {
    const state = createAuthState({ isAdmin: true, permissions: [], roles: ['Admin'], memberKey: null, kurinKey: null });
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'admin@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/panel']);
  });

  it('should navigate to the dashboard when a person stands behind the account', () => {
    const state = createAuthState({ memberKey: 'member-123' });
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'manager@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should navigate to the dashboard even with no kurin chosen, when there is a card', () => {
    const state = createAuthState({ memberKey: 'member-123', kurinKey: null });
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'manager@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should navigate to kurin page when only kurin key is present', () => {
    const state = createAuthState({ memberKey: null, kurinKey: 'kurin-123' });
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'manager@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/kurin']);
  });

  it('should navigate to login when authenticated state has no destination keys', () => {
    const state = createAuthState({ memberKey: null, kurinKey: null });
    authService.login.mockReturnValue(of(createLoginResponse()));
    authService.getAuthStateValue.mockReturnValue(state);

    component.email = 'user@example.com';
    component.password = 'password123';

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should show our own wording for a known error code, not the English message from the API', () => {
    authService.login.mockReturnValue(throwError(() => ({
      error: { error: 'InvalidCredentials', message: 'Email or password is incorrect.' }
    })));

    component.email = 'user@example.com';
    component.password = 'wrong-password';

    component.onSubmit();

    expect(messageService.add).toHaveBeenCalledWith(expect.objectContaining({
      severity: 'error',
      detail: 'Неправильний email або пароль.'
    }));
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should fall back to our text rather than the transport message on an unmapped error', () => {
    // HttpErrorResponse.message is always populated with something like "Http failure response
    // for ...", so using it as a fallback would render that string to the user.
    authService.login.mockReturnValue(throwError(() => ({
      message: 'Http failure response for http://localhost/api/auth/login: 500 Internal Server Error',
      error: { error: 'SomethingElse', message: 'An unexpected error occurred.' }
    })));

    component.email = 'user@example.com';
    component.password = 'wrong-password';

    component.onSubmit();

    expect(messageService.add).toHaveBeenCalledWith(expect.objectContaining({
      severity: 'error',
      detail: 'Не вдалося увійти.'
    }));
  });

  it('should sanitize otp input to six digits', () => {
    component.onOtpChange('12a-34567');

    expect(component.otpValue).toBe('123456');
  });

  it('should switch between password and recovery code modes', () => {
    component.otpValue = '123456';

    component.toggleRecoveryCode();

    expect(component.useRecoveryCode).toBe(true);
    expect(component.otpValue).toBe('');
  });

  it('should return to password step from otp step', () => {
    component.showOtpInput = true;
    component.otpValue = '123456';
    component.useRecoveryCode = true;

    component.backToPassword();

    expect(component.showOtpInput).toBe(false);
    expect(component.otpValue).toBe('');
    expect(component.useRecoveryCode).toBe(false);
  });

  function createAuthState(overrides: Partial<AuthState> = {}): AuthState {
    return {
      userKey: 'user-123',
      memberKey: 'member-123',
      email: 'test@example.com',
      isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
      kurinKey: 'kurin-123',
      accessToken: 'token-123',
      ...overrides
    };
  }

  function createLoginResponse(overrides: Partial<LoginResponse> = {}): LoginResponse {
    return {
      userKey: 'user-123',
      memberKey: 'member-123',
      email: 'test@example.com',
      isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
      kurinKey: 'kurin-123',
      requiresMfa: false,
      tokens: { accessToken: 'token-123' },
      ...overrides
    };
  }
  // The demo's three chairs appear only when env.js says the stand is Demo; a live kurin's login
  // page must never offer a password-less way in.
  describe('demo stand', () => {
    const originalEnv = environment.envName;
    afterEach(() => { environment.envName = originalEnv; });

    it('offers no demo seats on an ordinary stand', () => {
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(component.isDemo).toBe(false);
      expect(text).not.toContain('Демо-курінь');
    });

    it('signs the visitor in as the chosen seat and goes home', () => {
      environment.envName = 'Demo';
      const demo = {
        enter: vi.fn()
      };
      demo.enter.mockReturnValue(of({} as LoginResponse));
      authService.getAuthStateValue.mockReturnValue({ kurinKey: 'k1', roles: ['Member'] } as unknown as AuthState);
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [LoginComponent],
        providers: [
          provideHttpClient(),
          { provide: AuthService, useValue: authService },
          { provide: Router, useValue: router },
          { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
          { provide: MessageService, useValue: messageService },
          { provide: DemoService, useValue: demo }
        ]
      });
      const demoFixture = TestBed.createComponent(LoginComponent);
      demoFixture.detectChanges();

      expect((demoFixture.nativeElement as HTMLElement).textContent).toContain('Демо-курінь');
      demoFixture.componentInstance.enterDemo('Vykhovnyk');

      expect(demo.enter).toHaveBeenCalledWith('Vykhovnyk');
      expect(router.navigate).toHaveBeenCalled();
    });
  });
});
