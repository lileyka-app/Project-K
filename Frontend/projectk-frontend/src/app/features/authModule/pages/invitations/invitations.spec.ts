import { TestBed } from '@angular/core/testing';
import { MessageService } from '@openng/optimus-ui/api';
import { of } from 'rxjs';
import { MockedObject, vi } from 'vitest';
import { KurinBranch } from '../../../kurinModule/models/enums/kurin-branch.enum';
import {
  MembershipInvitationDto,
  MembershipService
} from '../../../kurinModule/services/membership-service/membership.service';
import { InvitationsComponent } from './invitations';

describe('InvitationsComponent', () => {
  let memberships: MockedObject<MembershipService>;

  const invitation: MembershipInvitationDto = {
    membershipRequestKey: 'r1',
    kurin: { kurinKey: 'k5', number: 5, namedAfter: null, branches: [{ branch: KurinBranch.USP, number: 5 }] },
    membershipKind: 'Youth',
    createdAtUtc: '2026-11-07T10:00:00Z',
    expiresAtUtc: '2026-11-21T10:00:00Z'
  };

  function create(list: MembershipInvitationDto[]) {
    memberships = { myInvitations: vi.fn(), answerInvitation: vi.fn() } as unknown as MockedObject<MembershipService>;
    memberships.myInvitations.mockReturnValue(of(list));
    memberships.answerInvitation.mockReturnValue(of({}));
    TestBed.configureTestingModule({
      imports: [InvitationsComponent],
      providers: [MessageService, { provide: MembershipService, useValue: memberships }]
    });
    const fixture = TestBed.createComponent(InvitationsComponent);
    fixture.detectChanges();
    return fixture;
  }

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

  it('lists the kurins that invite the person', () => {
    const fixture = create([invitation]);

    expect(fixture.nativeElement.querySelectorAll('[data-testid="invitation"]').length).toBe(1);
    expect(text(fixture)).toContain('УСП');
  });

  it('accepts an invitation and reads the list again', () => {
    const fixture = create([invitation]);

    fixture.componentInstance.answer(invitation, true);

    expect(memberships.answerInvitation).toHaveBeenCalledWith('r1', true);
    expect(memberships.myInvitations).toHaveBeenCalledTimes(2);
  });

  it('says so when nobody invites', () => {
    const fixture = create([]);

    expect(text(fixture)).toContain('Запрошень немає');
  });
});
