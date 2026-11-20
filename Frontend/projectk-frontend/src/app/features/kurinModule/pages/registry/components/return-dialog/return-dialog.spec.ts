import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from '@openng/optimus-ui/api';
import { of, throwError } from 'rxjs';
import { FormerMemberStandingDto, MembershipRequestKurinDto, MembershipService } from '../../../../services/membership-service/membership.service';
import { KurinBranch } from '../../../../models/enums/kurin-branch.enum';
import { ReturnDialogComponent } from './return-dialog';

describe('ReturnDialogComponent', () => {
  let memberships: MockedObject<MembershipService>;
  let messages: MessageService;

  const kurin = (number: number): MembershipRequestKurinDto => ({ kurinKey: `k${number}`, number, namedAfter: null, branches: [{ branch: KurinBranch.UPYu, number }] });
  const formerPerson = (memberKey: string, standsIn: MembershipRequestKurinDto[]): FormerMemberStandingDto => ({
    former: { memberKey, firstName: 'Віталій', lastName: 'Бойко', groupName: 'Соколи', joinedAtUtc: '2023-09-01', leftAtUtc: '2026-10-01' },
    standsIn,
    offer: { seniorKurin: false, joinsAtOnce: standsIn.length === 0, personAnswers: false, transferAllowed: true, askKurins: standsIn }
  });

  function create(person: FormerMemberStandingDto) {
    memberships = { createRequest: vi.fn() } as unknown as MockedObject<MembershipService>;
    memberships.createRequest.mockReturnValue(of({ membershipRequestKey: 'r9', joined: false }));
    TestBed.configureTestingModule({
      imports: [ReturnDialogComponent],
      providers: [provideNoopAnimations(), MessageService, { provide: MembershipService, useValue: memberships }]
    });
    const fixture = TestBed.createComponent(ReturnDialogComponent);
    fixture.componentRef.setInput('kurinKey', 'k15');
    fixture.componentRef.setInput('person', person);
    messages = TestBed.inject(MessageService);
    fixture.detectChanges();
    return fixture;
  }

  it('takes back at once a former member who stands nowhere', () => {
    const fixture = create(formerPerson('nowhere', []));
    const component = fixture.componentInstance;
    memberships.createRequest.mockReturnValue(of({ membershipRequestKey: null, joined: true }));
    const sent = vi.fn();
    component.sent.subscribe(sent);
    const toast = vi.spyOn(messages, 'add');

    component.membershipKind.set('Staff');
    component.send();

    expect(memberships.createRequest).toHaveBeenCalledWith('k15', {
      memberKey: 'nowhere', kind: 'Transfer', membershipKind: 'Staff', fromKurinKey: null
    });
    expect(sent).toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ summary: 'Людину повернуто' }));
  });

  it('asks the kurin a former member stands in, «ще й до нас» when chosen', () => {
    const fixture = create(formerPerson('elsewhere', [kurin(1), kurin(7)]));
    const component = fixture.componentInstance;

    component.kind.set('AlsoJoin');
    expect(component.canSend()).toBe(false);
    component.from.set('k7');
    component.send();

    expect(memberships.createRequest).toHaveBeenCalledWith('k15', {
      memberKey: 'elsewhere', kind: 'AlsoJoin', membershipKind: 'Youth', fromKurinKey: 'k7'
    });
  });

  it('stays open and says why when the server refuses', () => {
    const fixture = create(formerPerson('asked', [kurin(1)]));
    const component = fixture.componentInstance;
    memberships.createRequest.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'RequestPending' } })));
    const closed = vi.fn();
    component.closed.subscribe(closed);
    const toast = vi.spyOn(messages, 'add');

    component.send();

    expect(closed).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ detail: 'Запит щодо цієї людини вже чекає відповіді.' }));
  });
});
