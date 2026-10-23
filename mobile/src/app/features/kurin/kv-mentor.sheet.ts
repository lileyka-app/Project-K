import { Component, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { checkmarkCircle, ellipseOutline } from 'ionicons/icons';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { mentorChanges } from './group-people';
import { KvRow, fullName, toDateOnly } from './kurin.labels';
import { GroupDto, MemberLookupDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { kvSeatRequest } from './leadership-seats';
import { PeoplePickSheet, PickPerson } from './people-pick.sheet';

/**
 * «Учасник КВ» (web kv-panel's dialog): a member of the kurin with an account and the гуртки they
 * are виховник of. Opened empty it adds someone to the КВ; from a row it changes their гуртки.
 * Saving assigns the newly ticked гуртки, revokes the unticked, and keeps the КВ seat in step:
 * without a гурток the person sits as інструктор. Dismisses with true once saved.
 */
@Component({
  selector: 'app-kv-mentor-sheet',
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonNote,
    IonIcon,
    IonSpinner,
  ],
  styles: `
    ion-note {
      max-width: 60%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .mark {
      font-size: 24px;
      color: var(--lk-faint);
    }
    .mark.on {
      color: var(--lk-primary);
    }
    .hint {
      margin: 0 20px;
      font-size: 13px;
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="close(false)">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>Учасник КВ</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="!canSave() || busy()" (click)="save()" data-testid="save-mentor">
            @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item [button]="true" [detail]="true" [disabled]="busy()" (click)="chooseMentor()" data-testid="mentor">
            <ion-label>Учасник</ion-label>
            <ion-note slot="end" [color]="mentor() ? undefined : 'medium'">{{ mentorName() }}</ion-note>
          </ion-item>
        </ion-item-group>
      </ion-list>

      <ion-list [inset]="true" data-testid="mentor-groups">
        <ion-list-header><ion-label>Гуртки</ion-label></ion-list-header>
        <ion-item-group>
          @for (group of groups; track group.groupKey) {
            <ion-item
              [button]="true"
              [detail]="false"
              [disabled]="busy() || !mentor()"
              (click)="toggle(group.groupKey)"
              [attr.aria-pressed]="isPicked(group.groupKey)"
              data-testid="mentor-group"
            >
              <ion-label>{{ group.name }}</ion-label>
              <ion-icon
                slot="end"
                class="mark"
                [class.on]="isPicked(group.groupKey)"
                [name]="isPicked(group.groupKey) ? 'checkmark-circle' : 'ellipse-outline'"
                aria-hidden="true"
              />
            </ion-item>
          } @empty {
            <ion-item><ion-label>Гуртків поки немає.</ion-label></ion-item>
          }
        </ion-item-group>
      </ion-list>
      <p class="hint">З гуртком людина — впорядник із доступом до обраних гуртків, без гуртка — інструктор КВ.</p>
    </ion-content>
  `,
})
export class KvMentorSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly toasts = inject(Toasts);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  // Set by ModalController's componentProps.
  /** Who may be a впорядник: the kurin's people with an account (`mentor-candidates`). */
  candidates: MemberLookupDto[] = [];
  groups: GroupDto[] = [];
  /** The КВ as shown, to know whose гуртки are whose. */
  rows: KvRow[] = [];
  /** The row this was opened from, or null to add someone to the КВ. */
  row: KvRow | null = null;
  kurinKey = '';

  protected readonly mentor = signal<MemberLookupDto | null>(null);
  protected readonly picked = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly mentorName = computed(() => {
    const mentor = this.mentor();
    return mentor ? fullName(mentor) : 'Оберіть учасника';
  });
  private readonly before = computed(() => this.groupsOf(this.mentor()));
  protected readonly canSave = computed(() => {
    const mentor = this.mentor();
    if (!mentor?.userKey) return false;
    const changes = mentorChanges(this.before(), this.picked());
    // Someone new saved without a гурток still joins the КВ, as інструктор.
    return changes.assign.length + changes.revoke.length > 0 || !this.rowOf(mentor);
  });

  constructor() {
    addIcons({ checkmarkCircle, ellipseOutline });
  }

  ngOnInit(): void {
    const member = this.row?.member ?? null;
    this.mentor.set(member);
    this.picked.set(this.groupsOf(member));
  }

  protected isPicked(groupKey: string): boolean {
    return this.picked().includes(groupKey);
  }

  protected toggle(groupKey: string): void {
    this.picked.update((keys) => (keys.includes(groupKey) ? keys.filter((k) => k !== groupKey) : [...keys, groupKey]));
  }

  protected async chooseMentor(): Promise<void> {
    const people = this.candidates.filter((c) => !!c.userKey);
    const modal = await this.modals.create({
      component: PeoplePickSheet,
      componentProps: {
        title: 'Учасник КВ',
        people: people.map((member): PickPerson => ({ key: member.userKey!, member })),
        selected: this.mentor()?.userKey ? [this.mentor()!.userKey!] : [],
        emptyText: 'У курені ще нікого з акаунтом немає.',
      },
      presentingElement: this.host.closest('ion-modal') ?? undefined,
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<string[] | null>();
    const picked = people.find((p) => p.userKey === data?.[0]);
    if (!picked) return;
    // Someone else: start from the гуртки they already have.
    this.mentor.set(picked);
    this.picked.set(this.groupsOf(picked));
  }

  protected async save(): Promise<void> {
    const member = this.mentor();
    const userKey = member?.userKey;
    if (!member || !userKey || !this.canSave() || this.busy()) return;
    this.busy.set(true);
    try {
      const changes = mentorChanges(this.before(), this.picked());
      if (changes.assign.length + changes.revoke.length > 0) {
        await this.data.saveMentorGroups(userKey, changes);
      }
      if (!this.rowOf(member)?.isManager) {
        // A kurin whose КВ провід was never written answers 404: the first seat creates it.
        const kv = (await this.data.leadership('kv', this.kurinKey).catch(() => null)) ?? null;
        const request = kvSeatRequest(kv, this.kurinKey, member, this.picked().length > 0, toDateOnly(new Date()));
        if (request) await this.data.saveLeadership(kv?.leadershipKey ?? null, request);
      }
      await this.toasts.show('Збережено.');
      this.close(true);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти гуртки. Спробуй ще раз.'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  protected close(saved: boolean): void {
    const own = this.host.closest('ion-modal');
    if (own) void own.dismiss(saved);
    else void this.modals.dismiss(saved);
  }

  private rowOf(member: MemberLookupDto): KvRow | undefined {
    const same = (r: KvRow) => (r.member.userKey && member.userKey ? r.member.userKey === member.userKey : r.member.memberKey === member.memberKey);
    return this.rows.find(same);
  }

  private groupsOf(member: MemberLookupDto | null): string[] {
    return member ? [...(this.rowOf(member)?.groupKeys ?? [])] : [];
  }
}
