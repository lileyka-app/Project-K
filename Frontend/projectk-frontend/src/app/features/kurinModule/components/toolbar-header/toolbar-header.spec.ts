import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ToolbarHeaderComponent } from './toolbar-header';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { throwError } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { MFA_REQUIRED_MESSAGE } from '../../../../shared/functions/failure-detail.function';

describe('ToolbarHeaderComponent', () => {
  let component: ToolbarHeaderComponent;
  let fixture: ComponentFixture<ToolbarHeaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToolbarHeaderComponent],
      providers: [provideHttpClient(), MessageService],
    })
    .compileComponents();

    fixture = TestBed.createComponent(ToolbarHeaderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // STAB-06: the privileged-MFA gate is a standing condition, not a transient failure. Asking to
  // try again there is a lie; the toast has to say what to turn on.
  it('names the missing second factor instead of asking to retry when the MFA gate refuses', () => {
    const authService = TestBed.inject(AuthService);
    const messageService = TestBed.inject(MessageService);
    vi.spyOn(authService, 'setKurinScope').mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403, error: { message: MFA_REQUIRED_MESSAGE } })));
    const add = vi.spyOn(messageService, 'add').mockReturnValue(undefined);

    component.backToKurinPanel();

    const toast = vi.mocked(add).mock.lastCall![0];
    expect(toast.detail).toContain('двофакторн');
    expect(toast.detail).not.toContain('Спробуй ще раз');
  });

  it('still asks to retry when leaving the kurin failed for a passing reason', () => {
    const authService = TestBed.inject(AuthService);
    const messageService = TestBed.inject(MessageService);
    vi.spyOn(authService, 'setKurinScope').mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    const add = vi.spyOn(messageService, 'add').mockReturnValue(undefined);

    component.backToKurinPanel();

    expect(vi.mocked(add).mock.lastCall![0].detail).toBe('Спробуй ще раз.');
  });
});
