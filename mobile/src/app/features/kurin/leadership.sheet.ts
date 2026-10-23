import { Component, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { fullName, roleName, toDateOnly } from './kurin.labels';
import { LeadershipDto, MemberLookupDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import {
  LEADERSHIP_ROLES,
  Seats,
  holdersLine,
  holdsSeveral,
  leadershipRequest,
  officePeriod,
  pastOffices,
  sameSeats,
  seatsOf,
} from './leadership-seats';
import { PeoplePickSheet, PickPerson } from './people-pick.sheet';

const TITLES = { kurin: 'Провід куреня', group: 'Провід гуртка' } as const;
const FROM = { kurin: 'Обирай з учасників куреня.', group: 'Обирай з учасників гуртка.' } as const;

/**
 * «Налаштувати провід» for a kurin or a гурток (web leadership form): the people in each office,
 * picked from the kurin or the гурток, or nobody; several where the office takes several. Saving
 * closes the offices whose holder changed and opens the new ones; «Архів» shows who held them
 * before. Dismisses with true once saved.
 */
@Component({
  selector: 'app-leadership-sheet',
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
    IonSpinner,
    IonToggle,
  ],
  styles: `
    ion-note {
      max-width: 60%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
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
        <ion-title>{{ title() }}</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="!canSave() || busy()" (click)="save()" data-testid="save-leadership">
            @if (busy()) { <ion-spinner name="crescent" /> } @else { Зберегти }
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true" data-testid="leadership-seats">
        <ion-list-header><ion-label>Склад проводу</ion-label></ion-list-header>
        <ion-item-group>
          @for (role of roles(); track role) {
            @if (isLocked(role)) {
              <ion-item [attr.data-testid]="'seat-' + role">
                <ion-label class="ion-text-wrap">
                  {{ roleName(role) }}
                  <p>{{ lockedHint }}</p>
                </ion-label>
                <ion-note slot="end" [color]="seats()[role]?.length ? undefined : 'medium'">{{ holder(role) }}</ion-note>
              </ion-item>
            } @else {
              <ion-item [button]="true" [detail]="true" [disabled]="busy()" (click)="choose(role)" [attr.data-testid]="'seat-' + role">
                <ion-label>{{ roleName(role) }}</ion-label>
                <ion-note slot="end" [color]="seats()[role]?.length ? undefined : 'medium'">{{ holder(role) }}</ion-note>
              </ion-item>
            }
          }
        </ion-item-group>
      </ion-list>
      <p class="hint">{{ fromHint() }} Попередні ролі лишаються в архіві проводу.</p>

      <ion-list [inset]="true" data-testid="leadership-archive">
        <ion-item-group>
          <ion-item>
            <ion-toggle [checked]="showArchive()" (ionChange)="showArchive.set($any($event).detail.checked)" justify="space-between" data-testid="show-archive">
              Архів
            </ion-toggle>
          </ion-item>
          @if (showArchive()) {
            @for (office of archive(); track office.leadershipHistoryKey) {
              <ion-item data-testid="archived-office">
                <ion-label class="ion-text-wrap">
                  <h3>{{ name(office.member) }}</h3>
                  <p>{{ roleName(office.role) }} · {{ period(office.startDate, office.endDate) }}</p>
                </ion-label>
              </ion-item>
            } @empty {
              <ion-item><ion-label>Архів порожній.</ion-label></ion-item>
            }
          }
        </ion-item-group>
      </ion-list>
    </ion-content>
  `,
})
export class LeadershipSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly modals = inject(ModalController);
  private readonly toasts = inject(Toasts);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  // Set by ModalController's componentProps.
  kind: 'kurin' | 'group' = 'group';
  /** The kurin's or the гурток's key. */
  entityKey = '';
  /** The провід as read, or null when there is none yet (it is created on save). */
  leadership: LeadershipDto | null = null;
  /** Who can be seated: the kurin's or the гурток's people (web loads the same). */
  members: MemberLookupDto[] = [];
  /** Offices this person may not change, shown with who holds them (the курінний's own). */
  lockedRoles: string[] = [];
  lockedHint = 'Курінного призначає Звʼязковий.';

  protected readonly roleName = roleName;
  protected readonly period = officePeriod;
  private readonly kindRead = signal<'kurin' | 'group'>('group');
  protected readonly roles = computed(() => LEADERSHIP_ROLES[this.kindRead()]);
  protected readonly title = computed(() => TITLES[this.kindRead()]);
  protected readonly fromHint = computed(() => FROM[this.kindRead()]);
  private initial: Seats = {};
  protected readonly seats = signal<Seats>({});
  protected readonly busy = signal(false);
  protected readonly showArchive = signal(false);
  protected readonly archive = signal<ReturnType<typeof pastOffices>>([]);
  /** As the web: a new провід needs somebody in it; an existing one may be emptied. */
  protected readonly canSave = computed(() => {
    const seats = this.seats();
    if (sameSeats(seats, this.initial)) return false;
    return !!this.leadership?.leadershipKey || Object.values(seats).some((holders) => holders.length > 0);
  });

  ngOnInit(): void {
    this.kindRead.set(this.kind);
    this.initial = seatsOf(this.kind, this.leadership?.leadershipHistories);
    this.seats.set({ ...this.initial });
    this.archive.set(pastOffices(this.leadership?.leadershipHistories));
  }

  protected isLocked(role: string): boolean {
    return this.lockedRoles.includes(role);
  }

  protected holder(role: string): string {
    return holdersLine(this.seats()[role]);
  }

  protected name(member: MemberLookupDto): string {
    return fullName(member);
  }

  protected async choose(role: string): Promise<void> {
    const current = this.seats()[role] ?? [];
    // Someone seated before who has since left still shows, so keeping them is a no-op.
    const gone = current.filter((c) => !this.members.some((m) => m.memberKey === c.memberKey));
    const people = [...gone, ...this.members];
    const several = holdsSeveral(role);
    const modal = await this.modals.create({
      component: PeoplePickSheet,
      componentProps: {
        title: roleName(role),
        people: people.map((member): PickPerson => ({ key: member.memberKey, member })),
        selected: current.map((m) => m.memberKey),
        multiple: several,
        noneLabel: several ? null : 'Не обсаджено',
        emptyText: this.kind === 'kurin' ? 'У курені ще нікого немає.' : 'У гуртку ще нікого немає.',
      },
      presentingElement: this.host.closest('ion-modal') ?? undefined,
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<string[] | null>();
    if (!data) return;
    const picked = data.map((key) => people.find((m) => m.memberKey === key)).filter((m): m is MemberLookupDto => !!m);
    this.seats.update((seats) => ({ ...seats, [role]: picked }));
  }

  protected async save(): Promise<void> {
    if (!this.canSave() || this.busy()) return;
    this.busy.set(true);
    try {
      const request = leadershipRequest(this.kind, this.entityKey, this.seats(), toDateOnly(new Date()));
      await this.data.saveLeadership(this.leadership?.leadershipKey ?? null, request);
      await this.toasts.show(`${this.title()} збережено.`);
      this.close(true);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти провід. Спробуй ще раз.'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  protected close(saved: boolean): void {
    const own = this.host.closest('ion-modal');
    if (own) void own.dismiss(saved);
    else void this.modals.dismiss(saved);
  }
}
