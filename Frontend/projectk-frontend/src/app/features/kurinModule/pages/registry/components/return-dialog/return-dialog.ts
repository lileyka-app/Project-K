import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { SelectModule } from '@openng/optimus-ui/select';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { MessageService } from '@openng/optimus-ui/api';
import {
  FormerMemberStandingDto,
  MembershipKindName,
  MembershipRequestKind,
  MembershipRequestKurinDto,
  MembershipService
} from '../../../../services/membership-service/membership.service';
import {
  MEMBERSHIP_KIND_OPTIONS,
  kurinName,
  requestErrorText,
  requestSentMessage,
  askButtonLabel,
  defaultKind,
  kindOptionsFor,
  standsInLine
} from '../../membership-asks';

/**
 * Taking a former member back. Someone who stands nowhere comes back at once; someone who has since
 * joined another kurin is asked for, and that kurin's Звʼязковий answers.
 */
@Component({
  selector: 'app-return-dialog',
  imports: [FormsModule, ButtonModule, DialogModule, SelectModule, SelectButtonModule],
  templateUrl: './return-dialog.html',
  styleUrl: './return-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReturnDialogComponent {
  private readonly memberships = inject(MembershipService);
  private readonly messages = inject(MessageService);

  readonly kurinKey = input.required<string>();
  readonly person = input<FormerMemberStandingDto | null>(null);
  readonly closed = output<void>();
  /** A request went out or the person came back; the caller reads its lists again. */
  readonly sent = output<void>();

  protected readonly kindOptions = computed(() => kindOptionsFor(this.person()?.offer));
  protected readonly sendLabel = computed(() => askButtonLabel(this.person()?.offer, 'Повернути'));
  protected readonly membershipKindOptions = MEMBERSHIP_KIND_OPTIONS;
  protected readonly standsIn = standsInLine;

  readonly kind = signal<MembershipRequestKind>('Transfer');
  readonly membershipKind = signal<MembershipKindName>('Youth');
  readonly from = signal<string | null>(null);
  readonly sending = signal(false);

  readonly asked = computed<MembershipRequestKurinDto | null>(() => {
    const askable = this.person()?.offer.askKurins ?? [];
    return askable.length === 1 ? askable[0] : askable.find(k => k.kurinKey === this.from()) ?? null;
  });
  readonly kurinOptions = computed(() =>
    (this.person()?.offer.askKurins ?? []).map(k => ({ label: kurinName(k), value: k.kurinKey })));
  readonly canSend = computed(() => {
    const offer = this.person()?.offer;
    return !!offer && (offer.joinsAtOnce || offer.personAnswers || !!this.asked());
  });

  constructor() {
    // Every person opens on the plain answer, not on what was picked for the one before.
    effect(() => {
      const person = this.person();
      if (person) {
        this.kind.set(defaultKind(person.offer));
        this.membershipKind.set('Youth');
        this.from.set(null);
      }
    });
  }

  name(person: FormerMemberStandingDto): string {
    const { lastName, firstName, middleName } = person.former;
    return [lastName, firstName, middleName].filter(Boolean).join(' ');
  }

  close(): void {
    if (!this.sending()) {
      this.closed.emit();
    }
  }

  send(): void {
    const person = this.person();
    if (!person || !this.canSend()) {
      return;
    }

    const asked = this.asked();
    this.sending.set(true);
    this.memberships.createRequest(this.kurinKey(), {
      memberKey: person.former.memberKey,
      kind: this.kind(),
      membershipKind: this.membershipKind(),
      fromKurinKey: asked?.kurinKey ?? null
    }).subscribe({
      next: result => {
        this.sending.set(false);
        this.messages.add(requestSentMessage(result, this.name(person), asked, 'Людину повернуто'));
        this.sent.emit();
        this.closed.emit();
      },
      error: (error: unknown) => {
        this.sending.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося надіслати', detail: requestErrorText(error) });
      }
    });
  }
}
