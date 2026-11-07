import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import { MembershipCandidateDto, MembershipRequestDto, MembershipRequestKurinDto, MembershipService } from '../../../../services/membership-service/membership.service';
import { KurinBranch } from '../../../../models/enums/kurin-branch.enum';
import { TransfersComponent } from './transfers';

describe('TransfersComponent', () => {
  let memberships: MockedObject<MembershipService>;
  let messages: MessageService;

  const kurin = (number: number): MembershipRequestKurinDto => ({ kurinKey: `k${number}`, number, namedAfter: null, branches: [{ branch: KurinBranch.UPYu, number }] });
  const seniorKurin = (number: number): MembershipRequestKurinDto => ({ kurinKey: `s${number}`, number, namedAfter: null, branches: [{ branch: KurinBranch.USP, number }] });
  const request = (over: Partial<MembershipRequestDto>): MembershipRequestDto => ({
    membershipRequestKey: 'r1',
    kind: 'Transfer',
    status: 'Pending',
    memberKey: 'm1',
    memberName: 'Назар Коваль',
    fromKurin: kurin(1),
    toKurin: kurin(15),
    membershipKind: 'Youth',
    createdAtUtc: '2026-10-30T10:00:00Z',
    expiresAtUtc: '2026-11-13T10:00:00Z',
    decidedAtUtc: null,
    owedToLeave: 0,
    ...over
  });
  const candidate = (currentKurins: ReturnType<typeof kurin>[]): MembershipCandidateDto => ({
    member: { memberKey: 'm1', publicId: 'PL-4F7A2-9D4A1', firstName: 'Назар', lastName: 'Коваль', fullName: 'Назар Коваль' },
    alreadyInThisKurin: false,
    currentKurins
  });

  function create(incoming: MembershipRequestDto[] = [], outgoing: MembershipRequestDto[] = []) {
    memberships = {
      requests: vi.fn(),
      candidate: vi.fn(),
      createRequest: vi.fn(),
      approveRequest: vi.fn(),
      declineRequest: vi.fn(),
      cancelRequest: vi.fn()
    } as MockedObject<MembershipService>;
    memberships.requests.mockReturnValue(of({ incoming, outgoing }));
    memberships.createRequest.mockReturnValue(of({ membershipRequestKey: 'r9', joined: false }));

    TestBed.configureTestingModule({
      imports: [TransfersComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        MessageService,
        { provide: MembershipService, useValue: memberships },
        { provide: AuthService, useValue: { getAuthState: () => of({ kurinKey: 'k15' }) } }
      ]
    });
    const fixture = TestBed.createComponent(TransfersComponent);
    messages = TestBed.inject(MessageService);
    fixture.detectChanges();
    return fixture;
  }

  const text = (fixture: ReturnType<typeof create>) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('lists the requests to answer and those sent, pending first', () => {
    const fixture = create(
      [request({ membershipRequestKey: 'old', status: 'Declined' }), request({})],
      [request({ membershipRequestKey: 'out', fromKurin: kurin(7), toKurin: kurin(15) })]);

    expect(fixture.componentInstance.incoming()[0].status).toBe('Pending');
    expect(fixture.componentInstance.pendingIncoming()).toBe(1);
    expect(text(fixture)).toContain('к. ч. 15 просить перевести до себе');
    expect(text(fixture)).toContain('Перевести з к. ч. 7');
  });

  it('names a senior kurin by its гілка, not as the УПЮ kurin of the same number', () => {
    const fixture = create([request({ fromKurin: kurin(5), toKurin: seniorKurin(5) })]);

    expect(text(fixture)).toContain('5-й курінь УСП просить перевести до себе');
    expect(text(fixture)).not.toContain('к. ч. 5 просить');
  });

  it('will not offer to approve a transfer while the person owes', () => {
    const fixture = create([request({ owedToLeave: 460 })]);

    expect(text(fixture)).toContain('Борг за минулі квартали');
    const approve = (fixture.nativeElement as HTMLElement).querySelector('.transfers__actions button') as HTMLButtonElement;
    expect(approve.disabled).toBe(true);
  });

  it('finds a person by code and asks the kurin they stand in', () => {
    const fixture = create();
    const component = fixture.componentInstance;
    memberships.candidate.mockReturnValue(of(candidate([kurin(1)])));

    component.code.set('pl-4f7a2-9d4a1');
    component.find();
    component.kind.set('AlsoJoin');
    component.send();

    expect(memberships.candidate).toHaveBeenCalledWith('k15', 'PL-4F7A2-9D4A1');
    expect(memberships.createRequest).toHaveBeenCalledWith('k15', {
      publicId: 'PL-4F7A2-9D4A1', kind: 'AlsoJoin', membershipKind: 'Youth', fromKurinKey: 'k1'
    });
  });

  it('takes in at once someone who stands nowhere', () => {
    const fixture = create();
    const component = fixture.componentInstance;
    memberships.candidate.mockReturnValue(of(candidate([])));
    memberships.createRequest.mockReturnValue(of({ membershipRequestKey: null, joined: true }));
    const toast = vi.spyOn(messages, 'add');

    component.code.set('PL-4F7A2-9D4A1');
    component.find();
    fixture.detectChanges();

    expect(text(fixture)).toContain('Прийняти до куреня');
    component.send();
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ summary: 'Людину прийнято' }));
  });

  it('needs the kurin to ask when the person stands in several', () => {
    const fixture = create();
    const component = fixture.componentInstance;
    memberships.candidate.mockReturnValue(of(candidate([kurin(1), kurin(7)])));

    component.code.set('PL-4F7A2-9D4A1');
    component.find();
    expect(component.canSend()).toBe(false);

    component.fromKurinKey.set('k7');
    expect(component.canSend()).toBe(true);
  });

  it('says so when the code is nobody\'s', () => {
    const fixture = create();
    const component = fixture.componentInstance;
    memberships.candidate.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    component.code.set('PL-00000-00000');
    component.find();

    expect(component.lookupError()).toBe('Такого коду немає. Перевір його з людиною.');
  });

  it('approves after the dialog, with a red button for «перевести»', () => {
    const fixture = create([request({})]);
    const confirmation = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = vi.spyOn(confirmation, 'confirm').mockImplementation(options => {
      options.accept?.();
      return confirmation;
    });
    memberships.approveRequest.mockReturnValue(of(undefined));

    fixture.componentInstance.confirmApprove(request({}));

    expect(confirm.mock.calls[0][0].acceptButtonProps).toEqual({ label: 'Перевести', severity: 'danger' });
    expect(memberships.approveRequest).toHaveBeenCalledWith('k15', 'r1');
  });

  it('has no former members of its own: they are in the archive', () => {
    const fixture = create();

    expect(text(fixture)).not.toContain('Колишні');
  });
});
