import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LogoutComponent } from './logout';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from '../../services/auth-service/auth.service';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

describe('LogoutComponent', () => {
  let component: LogoutComponent;
  let fixture: ComponentFixture<LogoutComponent>;
  let mockAuthService: MockedObject<AuthService>;
  let mockRouter: MockedObject<Router>;

  beforeEach(async () => {
    mockAuthService = {
      logout: vi.fn()
    } as MockedObject<AuthService>;
    mockRouter = {
      navigate: vi.fn()
    } as MockedObject<Router>;

    await TestBed.configureTestingModule({
      imports: [LogoutComponent],
      providers: [
        provideHttpClient(),
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter }
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(LogoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('logout', () => {
    it('should call authService.logout', () => {
      mockAuthService.logout.mockReturnValue(of('Logged out successfully'));

      component.logout();

      expect(mockAuthService.logout).toHaveBeenCalled();
    });

    it('should navigate to /login on successful logout', () => {
      mockAuthService.logout.mockReturnValue(of('Logged out successfully'));

      component.logout();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should handle logout error', () => {
      const error = { status: 500, message: 'Server error' };
      mockAuthService.logout.mockReturnValue(throwError(() => error));
      vi.spyOn(console, 'error').mockReturnValue(undefined);

      component.logout();

      expect(console.error).toHaveBeenCalledWith(expect.stringContaining('Logout failed'));
    });

    it('should navigate to /login on logout error', () => {
      const error = { status: 500, message: 'Server error' };
      mockAuthService.logout.mockReturnValue(throwError(() => error));
      vi.spyOn(console, 'error').mockReturnValue(undefined);

      component.logout();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('Integration scenarios', () => {
    it('should complete full logout flow', () => {
      const message = 'Successfully logged out';
      mockAuthService.logout.mockReturnValue(of(message));

      component.logout();

      expect(mockAuthService.logout).toHaveBeenCalled();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should handle network error gracefully and navigate', () => {
      const networkError = new Error('Network error');
      mockAuthService.logout.mockReturnValue(throwError(() => networkError));
      vi.spyOn(console, 'error').mockReturnValue(undefined);

      component.logout();

      expect(mockAuthService.logout).toHaveBeenCalled();
      expect(console.error).toHaveBeenCalledWith(expect.stringContaining('Logout failed'));
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should handle 401 unauthorized error and navigate', () => {
      const error = { status: 401, message: 'Unauthorized' };
      mockAuthService.logout.mockReturnValue(throwError(() => error));
      vi.spyOn(console, 'error').mockReturnValue(undefined);

      component.logout();

      expect(console.error).toHaveBeenCalled();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });
  });
});