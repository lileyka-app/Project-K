import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageService } from '@openng/optimus-ui/api';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { kurinName } from '../../../kurinModule/pages/registry/membership-asks';
import {
  MembershipInvitationDto,
  MembershipService
} from '../../../kurinModule/services/membership-service/membership.service';

function stillYouth(error: unknown): boolean {
  return error instanceof HttpErrorResponse && (error.error as { error?: string } | null)?.error === 'PersonStillYouth';
}

/**
 * Kurins that invite the person themself: across the youth and senior lines, to someone who is no
 * longer a юнак. Accepting adds a membership; nothing the person holds elsewhere closes.
 */
@Component({
  selector: 'app-invitations',
  standalone: true,
  imports: [DatePipe, ButtonModule, EmptyStateComponent],
  templateUrl: './invitations.html',
  styleUrl: './invitations.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InvitationsComponent implements OnInit {
  private readonly memberships = inject(MembershipService);
  private readonly messages = inject(MessageService);

  protected readonly kurinName = kurinName;
  readonly loading = signal(true);
  readonly invitations = signal<MembershipInvitationDto[]>([]);
  readonly answering = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  answer(invitation: MembershipInvitationDto, accept: boolean): void {
    this.answering.set(invitation.membershipRequestKey);
    this.memberships.answerInvitation(invitation.membershipRequestKey, accept).subscribe({
      next: () => {
        this.answering.set(null);
        this.messages.add(accept
          ? { severity: 'success', summary: 'Запрошення прийнято' }
          : { severity: 'info', summary: 'Запрошення відхилено' });
        this.load();
      },
      error: (error: unknown) => {
        this.answering.set(null);
        this.messages.add({ severity: 'error', summary: 'Не вдалося відповісти', detail: stillYouth(error) ? 'Ти ще в УПЮ.' : failureDetail(error) });
        this.load();
      }
    });
  }

  private load(): void {
    this.loading.set(true);
    this.memberships.myInvitations().subscribe({
      next: invitations => {
        this.invitations.set(invitations);
        this.loading.set(false);
      },
      error: () => {
        this.invitations.set([]);
        this.loading.set(false);
      }
    });
  }
}
