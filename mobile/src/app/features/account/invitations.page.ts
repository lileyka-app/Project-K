import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  IonBackButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { kurinInText } from '../../core/kurin-label';
import { Toasts } from '../../core/toast';
import { MembershipInvitationDto } from '../kurin/kurin.models';
import { KurinApi } from '../kurin/kurin.service';

/**
 * «Запрошення» (web /invitations): kurins that invite the person themself, across the youth and
 * senior lines. Accepting adds a membership; nothing the person holds elsewhere closes.
 */
@Component({
  selector: 'app-invitations',
  imports: [
    DatePipe,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonList,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonSkeletonText,
  ],
  styles: `
    .note {
      padding: 16px 20px;
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/more" text="" /></ion-buttons>
        <ion-title>Запрошення</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      @if (loading()) {
        <ion-list [inset]="true"><ion-item><ion-skeleton-text [animated]="true" style="width: 60%" /></ion-item></ion-list>
      } @else {
        @for (invitation of invitations(); track invitation.membershipRequestKey) {
          <ion-list [inset]="true" data-testid="invitation">
            <ion-item-group>
              <ion-item>
                <ion-label class="ion-text-wrap">
                  <h2>{{ kurin(invitation) }}</h2>
                  <p>до {{ invitation.expiresAtUtc | date: 'dd.MM.yyyy' }}</p>
                </ion-label>
              </ion-item>
              <ion-item [button]="true" [detail]="false" [disabled]="busy()" (click)="answer(invitation, true)" data-testid="accept">
                <ion-label color="primary">Прийняти</ion-label>
              </ion-item>
              <ion-item [button]="true" [detail]="false" [disabled]="busy()" (click)="answer(invitation, false)" data-testid="decline">
                <ion-label color="medium">Відхилити</ion-label>
              </ion-item>
            </ion-item-group>
          </ion-list>
        } @empty {
          <p class="note">Запрошень немає</p>
        }
      }
    </ion-content>
  `,
})
export class InvitationsPage implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly toasts = inject(Toasts);

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly invitations = signal<MembershipInvitationDto[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  protected kurin(invitation: MembershipInvitationDto): string {
    const label = kurinInText(invitation.kurin.branches, invitation.kurin.number);
    return invitation.kurin.namedAfter ? `${label} · ${invitation.kurin.namedAfter}` : label;
  }

  protected async answer(invitation: MembershipInvitationDto, accept: boolean): Promise<void> {
    this.busy.set(true);
    try {
      await this.data.answerInvitation(invitation.membershipRequestKey, accept);
      await this.toasts.show(accept ? 'Запрошення прийнято.' : 'Запрошення відхилено.');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося відповісти. Спробуй ще раз.', { PersonStillYouth: 'Ти ще в УПЮ.' }), 'danger');
    } finally {
      this.busy.set(false);
      await this.load();
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.invitations.set(await this.data.myInvitations());
    } catch {
      this.invitations.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
