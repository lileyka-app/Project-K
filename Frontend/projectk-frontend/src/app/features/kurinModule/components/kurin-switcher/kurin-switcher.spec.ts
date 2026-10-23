import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { Router } from '@angular/router';

import { KurinSwitcherComponent } from './kurin-switcher';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinScopeOption } from '../../models/kurin-scope-option.model';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { KurinBranch } from '../../models/enums/kurin-branch.enum';
import { MembershipKind } from '../../models/enums/membership-kind.enum';

describe('KurinSwitcherComponent', () => {
  let fixture: ComponentFixture<KurinSwitcherComponent>;
  let component: KurinSwitcherComponent;
  let authService: MockedObject<AuthService>;
  let router: MockedObject<Router>;

  const upyu: KurinScopeOption = {
    kurinKey: 'kurin-a',
    kurinNumber: 7,
    branch: KurinBranch.UPYu,
    namedAfter: null,
    kind: MembershipKind.Youth
  };
  const usp: KurinScopeOption = {
    kurinKey: 'kurin-b',
    kurinNumber: 42,
    branch: KurinBranch.USP,
    namedAfter: 'Сірого Лева',
    kind: MembershipKind.Staff
  };

  const popover = { hide: () => undefined };

  beforeEach(async () => {
    authService = {
      getKurinScopeOptions: vi.fn(),
      setKurinScope: vi.fn(),
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    router = {
      navigate: vi.fn()
    } as MockedObject<Router>;

    authService.getKurinScopeOptions.mockReturnValue(of([upyu, usp]));
    authService.getAuthStateValue.mockReturnValue({ kurinKey: 'kurin-a' } as unknown as AuthState);
    authService.setKurinScope.mockReturnValue(of({} as unknown as AuthState));

    await TestBed.configureTestingModule({
      imports: [KurinSwitcherComponent],
      providers: [
        provideHttpClient(),
        MessageService,
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KurinSwitcherComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('пропонує вибір лише тому, хто належить не до одного куреня', () => {
    expect(component.isVisible).toBe(true);

    component.options.set([upyu]);
    expect(component.isVisible).toBe(false);
  });

  it('показує курінь, у якому людина стоїть зараз', () => {
    expect(component.currentLabel).toBe('к. ч. 7');
    expect(component.isCurrent(upyu)).toBe(true);
    expect(component.isCurrent(usp)).toBe(false);
  });

  it('перемикання просить сервер видати новий токен і веде в новий курінь', () => {
    component.switchTo(usp, popover);

    expect(authService.setKurinScope).toHaveBeenCalledWith('kurin-b');
    expect(router.navigate).toHaveBeenCalledWith(['/kurin']);
  });

  it('на курінь, де ми вже стоїмо, нічого не перевидає', () => {
    component.switchTo(upyu, popover);

    expect(authService.setKurinScope).not.toHaveBeenCalled();
  });

  it('якщо сервер відмовив — лишаємось на місці', () => {
    authService.setKurinScope.mockReturnValue(throwError(() => new Error('403')));

    component.switchTo(usp, popover);

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.switchingTo()).toBeNull();
  });
});
