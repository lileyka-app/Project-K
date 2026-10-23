import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { MessageService } from '@openng/optimus-ui/api';
import { of, throwError } from 'rxjs';
import { PhoneAppInstallCode, PhoneAppService } from '../../services/phone-app-service/phone-app.service';
import { InstallOnPhoneDialogComponent } from './install-on-phone-dialog';

describe('InstallOnPhoneDialogComponent', () => {
  const installCode: PhoneAppInstallCode = { url: 'https://lileyka.example/m/install', qrCode: 'data:image/png;base64,AAAA' };
  let messages: MockedObject<MessageService>;
  let phoneApp: { installCode$: unknown };

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [InstallOnPhoneDialogComponent],
      providers: [
        { provide: MessageService, useValue: messages },
        { provide: PhoneAppService, useValue: phoneApp }
      ]
    });
  }

  beforeEach(() => {
    messages = {
      add: vi.fn()
    } as MockedObject<MessageService>;
    phoneApp = { installCode$: of(installCode) };
  });

  it('asks the server for the code only once the dialog opens', () => {
    setup();
    const fixture = TestBed.createComponent(InstallOnPhoneDialogComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.code()).toBeNull();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.code()).toEqual(installCode);
  });

  it('says so when the code cannot be read', () => {
    phoneApp = { installCode$: throwError(() => new Error('offline')) };
    setup();
    const fixture = TestBed.createComponent(InstallOnPhoneDialogComponent);
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.failed()).toBe(true);
  });

  it('shows the address itself when the clipboard refuses', async () => {
    setup();
    // jsdom has no clipboard at all.
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } });
    const component = TestBed.createComponent(InstallOnPhoneDialogComponent).componentInstance;

    await component.copyLink(installCode.url);

    expect(messages.add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'warn', detail: installCode.url }));
  });
});
