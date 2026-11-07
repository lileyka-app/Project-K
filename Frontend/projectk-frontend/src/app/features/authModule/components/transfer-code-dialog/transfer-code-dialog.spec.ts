import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { MessageService } from '@openng/optimus-ui/api';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../services/auth-service/auth.service';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import { MemberDto } from '../../../kurinModule/models/member.dto';
import { TransferCodeDialogComponent } from './transfer-code-dialog';
import { CardService } from '../../services/card-service/card.service';

describe('TransferCodeDialogComponent', () => {
  let members: MockedObject<MemberService>;
  let memberKey: string | null;

  function create() {
    TestBed.configureTestingModule({
      imports: [TransferCodeDialogComponent],
      providers: [
        { provide: MessageService, useValue: { add: vi.fn() } },
        { provide: MemberService, useValue: members },
        { provide: CardService, useValue: { transferCode: () => of({ url: 'u', qrCode: 'data:image/png;base64,AA', expiresAtUtc: null }) } },
        { provide: AuthService, useValue: { getAuthStateValue: () => (memberKey ? { memberKey } : null) } }
      ]
    });
    const fixture = TestBed.createComponent(TransferCodeDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    memberKey = 'm1';
    members = { getByKey: vi.fn() } as MockedObject<MemberService>;
    members.getByKey.mockReturnValue(of({ memberKey: 'm1', publicId: 'PL-4F7A2-9D4A1' } as MemberDto));
  });

  it('reads the person\'s own code only once the dialog opens', () => {
    const fixture = create();
    expect(members.getByKey).not.toHaveBeenCalled();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(members.getByKey).toHaveBeenCalledWith('m1');
    expect(fixture.componentInstance.code()).toBe('PL-4F7A2-9D4A1');
  });

  it('says the code did not load when the card comes without it', () => {
    members.getByKey.mockReturnValue(of({ memberKey: 'm1' } as MemberDto));
    const fixture = create();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.code()).toBeNull();
    expect(fixture.componentInstance.failed()).toBe(true);
  });

  it('says the code did not load when the read fails', () => {
    members.getByKey.mockReturnValue(throwError(() => new Error('403')));
    const fixture = create();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.failed()).toBe(true);
  });

  it('asks for nothing without a member behind the account', () => {
    memberKey = null;
    const fixture = create();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(members.getByKey).not.toHaveBeenCalled();
    expect(fixture.componentInstance.failed()).toBe(true);
  });
});
