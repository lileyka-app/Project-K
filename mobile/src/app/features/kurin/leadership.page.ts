import { Component, ElementRef, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  Config,
  IonBackButton,
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
  IonRefresher,
  IonRefresherContent,
  IonSearchbar,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { settingsOutline } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { currentOffices, fullName, kurinAccess, officeTag } from './kurin.labels';
import { RoleTags } from './role-tags';
import { LeadershipDto, MemberLookupDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { officePeriod, pastOffices } from './leadership-seats';
import { LeadershipSheet } from './leadership.sheet';

/** Below this many past offices a search field is more clutter than help. */
const SEARCH_FROM = 9;

const BACK_TEXT = { kurin: 'Курінь', group: 'Гурток' } as const;

/**
 * A kurin's or a гурток's провід (web leadership-panel): who holds each office now and, as its
 * «Архів», who held them before and when — everyone reads both. The gear opens the form
 * (`LeadershipSheet`) for whoever may seat that провід (web canSetupLeadership): for the kurin the
 * Звʼязковий, admins and the курінний (who may not reseat himself), for a гурток also its
 * виховник or гуртковий.
 */
@Component({
  selector: 'app-leadership',
  imports: [
    RoleTags,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSearchbar,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonIcon,
    IonSkeletonText,
    RouterLink,
  ],
  styles: `
    ion-searchbar {
      padding-inline: 12px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button [defaultHref]="backHref" [text]="backText" /></ion-buttons>
        <ion-title>{{ title }}</ion-title>
        @if (canSetup() && leadership().state === 'ready' && members().state === 'ready') {
          <ion-buttons slot="end">
            <ion-button aria-label="Налаштувати провід" (click)="setup()" data-testid="setup-leadership">
              <ion-icon slot="icon-only" name="settings-outline" />
            </ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      <ion-list [inset]="true" data-testid="current-offices">
        <ion-list-header><ion-label>Зараз</ion-label></ion-list-header>
        <ion-item-group>
          @switch (leadership().state) {
            @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-item> }
            @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
            @default {
              @for (office of offices(); track office.leadershipHistoryKey) {
                <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', office.member.memberKey]" data-testid="office">
                  <ion-label class="ion-text-wrap">
                    <h3>{{ name(office.member) }}</h3>
                    <p>{{ period(office.startDate, null) }}</p>
                    <app-role-tags [tags]="[officeTag(office)]" />
                  </ion-label>
                </ion-item>
              } @empty {
                <ion-item><ion-label>Проводу ще немає.</ion-label></ion-item>
              }
            }
          }
        </ion-item-group>
      </ion-list>

      @if (leadership().state === 'ready') {
        @if (allPast().length >= searchFrom) {
          <ion-searchbar
            placeholder="Пошук в архіві"
            [value]="query()"
            [debounce]="150"
            (ionInput)="query.set($any($event).detail.value ?? '')"
            data-testid="archive-search"
          />
        }
        <ion-list [inset]="true" data-testid="past-offices">
          <ion-list-header><ion-label>Архів</ion-label></ion-list-header>
          <ion-item-group>
            @for (office of past(); track office.leadershipHistoryKey) {
              <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', office.member.memberKey]" data-testid="past-office">
                <ion-label class="ion-text-wrap">
                  <h3>{{ name(office.member) }}</h3>
                  <p>{{ period(office.startDate, office.endDate) }}</p>
                  <app-role-tags [tags]="[officeTag(office)]" />
                </ion-label>
              </ion-item>
            } @empty {
              <ion-item><ion-label>{{ query() ? 'Нічого не знайшлось.' : 'Архів порожній.' }}</ion-label></ion-item>
            }
          </ion-item-group>
        </ion-list>
      }
    </ion-content>
  `,
})
export class LeadershipPage implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly auth = inject(AuthService);
  private readonly modals = inject(ModalController);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  private readonly groupKey = inject(ActivatedRoute).snapshot.paramMap.get('groupKey');
  /** A гурток's when the route names one, else the scoped kurin's. */
  protected readonly kind: 'kurin' | 'group' = this.groupKey ? 'group' : 'kurin';
  private readonly entityKey = this.groupKey ?? this.auth.user()?.kurinKey ?? '';
  protected readonly title = this.kind === 'kurin' ? 'Провід куреня' : 'Провід гуртка';
  protected readonly backHref = this.groupKey ? `/tabs/kurin/group/${this.groupKey}` : '/tabs/kurin';
  protected readonly backText = inject(Config).get('mode') === 'ios' ? BACK_TEXT[this.kind] : undefined;

  protected readonly failedText = FAILED_TEXT;
  protected readonly officeTag = officeTag;
  protected readonly period = officePeriod;
  protected readonly searchFrom = SEARCH_FROM;

  protected readonly query = signal('');
  /** The провід record; null when there is none yet (the server answers with nothing). */
  protected readonly leadership = signal<Loaded<LeadershipDto | null>>({ state: 'loading' });
  /** Who the form seats from, read only for those who may open it. */
  protected readonly members = signal<Loaded<MemberLookupDto[]>>({ state: 'loading' });

  protected readonly canSetup = computed(() => {
    const access = kurinAccess(this.auth.user());
    return this.kind === 'kurin' ? access.setupKurinLeadership : access.setupGroupLeadership;
  });
  private readonly histories = computed(() => valueOf(this.leadership())?.leadershipHistories ?? []);
  protected readonly offices = computed(() => currentOffices(this.histories()));
  protected readonly allPast = computed(() => pastOffices(this.histories()));
  protected readonly past = computed(() => pastOffices(this.histories(), this.query()));

  constructor() {
    addIcons({ settingsOutline });
    // Saved from the form here or on the page below: read it again.
    effect(() => {
      if (this.data.leadershipChanged()) untracked(() => void this.loadLeadership());
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected name(member: MemberLookupDto): string {
    return fullName(member);
  }

  /** The gear (web leadership-panel onSettingsSelect): the form over this page. */
  protected async setup(): Promise<void> {
    const leadership = valueOf(this.leadership());
    const modal = await this.modals.create({
      component: LeadershipSheet,
      componentProps: {
        kind: this.kind,
        entityKey: this.entityKey,
        leadership,
        members: valueOf(this.members()) ?? [],
        lockedRoles: this.kind === 'kurin' && !kurinAccess(this.auth.user()).seatKurinHead ? ['Kurinnuy'] : [],
      },
      presentingElement: this.host.closest<HTMLElement>('.ion-page') ?? undefined,
    });
    await modal.present();
  }

  private loadLeadership(): Promise<void> {
    if (!this.entityKey) return Promise.resolve();
    return settle(this.data.leadership(this.kind, this.entityKey).then((l) => l ?? null), this.leadership);
  }

  private async load(): Promise<void> {
    if (!this.entityKey) return;
    await Promise.all([this.loadLeadership(), this.canSetup() ? this.loadMembers() : Promise.resolve()]);
  }

  private loadMembers(): Promise<void> {
    const request = this.kind === 'kurin' ? this.data.kurinMembers(this.entityKey) : this.data.groupMembers(this.entityKey);
    return settle(request, this.members);
  }
}
