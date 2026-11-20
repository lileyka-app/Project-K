import { Component, ElementRef, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ActionSheetController,
  IonBackButton,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonItemOption,
  IonItemOptions,
  IonItemSliding,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSearchbar,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ModalController,
  NavController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { alertCircle, checkmarkCircle, ellipsisHorizontal, people, personAdd, peopleOutline, settingsOutline, trophy, wallet } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import {
  activeWarning,
  birthdayWhen,
  currentOffices,
  filterMembers,
  fullName,
  kurinAccess,
  plastLevelLabel,
  memberRoleTags,
  officeTag,
  shortDate,
  sortMembers,
  upcomingBirthdays,
  warningText,
} from './kurin.labels';
import { GroupDto, LeadershipDto, LeadershipHistoryDto, MemberLookupDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { MemberAvatar } from './member-avatar';
import { RoleTags } from './role-tags';
import { GroupSilhouette } from './group-silhouette';
import { GroupEditResult, GroupEditSheet } from './group-edit.sheet';
import { LeadershipSheet } from './leadership.sheet';
import { addableToGroup, mentorChanges, placementLine } from './group-people';
import { PeoplePickSheet, PickPerson } from './people-pick.sheet';
import { ProfileEditResult, ProfileEditSheet } from './profile-edit.sheet';
import { kurinTitle } from '../../core/kurin-label';

const DESCRIPTION_LIMIT = 220;
const BIRTHDAY_DAYS = 30;
const BIRTHDAY_PREVIEW = 5;
/** Below this many people a search field is more clutter than help. */
const SEARCH_FROM = 9;

/**
 * A гурток (web group-panel): its silhouette and description, провід, birthdays in the next 30 days
 * and its people as mini cards, with rows to its score and — for those who keep its box — its dues.
 * «Редагувати» (web group-panel's menu, by the server's check-access) changes the description and
 * silhouette; the Звʼязковий also renames and deletes the гурток there.
 *
 * People, as the web's group panel, leadership form and member card place them: «Новий учасник»
 * for whoever the server lets create in this гурток; «Налаштувати провід» for whoever seats a
 * гурток's провід; and for the Звʼязковий alone (placement and виховники are his, web
 * canManageWholeKurin / canManageMentors) «Додати з куреня», moving a person to another гурток or
 * out of this one, and «Впорядники».
 */
@Component({
  selector: 'app-group',
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
    IonCard,
    IonCardContent,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonItemSliding,
    IonItemOptions,
    IonItemOption,
    IonLabel,
    IonNote,
    IonIcon,
    IonSearchbar,
    IonSkeletonText,
    IonSpinner,
    RouterLink,
    MemberAvatar,
    GroupSilhouette,
  ],
  styles: `
    .hero {
      display: flex;
      gap: 14px;
      align-items: center;
    }
    .eyebrow {
      margin: 0;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: var(--lk-faint);
    }
    .hero h2 {
      margin: 2px 0;
      font-size: 22px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .hero p {
      margin: 0;
      color: var(--lk-muted);
    }
    .description {
      margin: 14px 0 0;
      color: var(--lk-ink-soft);
      white-space: pre-line;
    }
    .description.collapsed {
      display: -webkit-box;
      -webkit-line-clamp: 4;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .verified {
      color: var(--lk-primary);
      font-size: 16px;
      vertical-align: -3px;
      margin-inline-start: 4px;
    }
    .stale {
      color: var(--lk-faint);
    }
    .dots {
      display: inline-flex;
      gap: 3px;
      margin-inline-start: 6px;
      vertical-align: 2px;
    }
    .dots span {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--lk-line);
    }
    .dots span.on {
      background: var(--lk-danger);
    }
    .today {
      color: var(--lk-danger);
      font-weight: 600;
    }
    ion-searchbar {
      padding-inline: 12px;
    }
    app-member-avatar {
      margin-inline-end: 12px;
    }
    .hint {
      margin: 0 20px;
      font-size: 13px;
      color: var(--lk-muted);
    }
    .actions {
      margin: 0;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/kurin" text="Курінь" /></ion-buttons>
        <ion-title>{{ groupValue()?.name ?? 'Гурток' }}</ion-title>
        @if (canEdit() && groupValue()) {
          <ion-buttons slot="end">
            <ion-button (click)="edit()" data-testid="edit-group">Редагувати</ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      <ion-card data-testid="group-head">
        <ion-card-content>
          @switch (group().state) {
            @case ('loading') { <ion-skeleton-text [animated]="true" style="height: 72px" /> }
            @case ('failed') { <p>{{ failedText }}</p> }
            @default {
              @if (groupValue(); as g) {
                <div class="hero">
                  <app-group-silhouette [url]="g.silhouetteUrl" [size]="72" data-testid="group-silhouette" />
                  <div>
                    <p class="eyebrow">Гурток</p>
                    <h2>{{ g.name }}</h2>
                    @if (g.kurinNumber !== undefined) { <p>{{ title(g.kurinNumber) }}</p> }
                  </div>
                </div>
                @if (description(); as text) {
                  <p class="description" [class.collapsed]="longDescription() && !expanded()">{{ text }}</p>
                  @if (longDescription()) {
                    <ion-button fill="clear" size="small" (click)="expanded.set(!expanded())">
                      {{ expanded() ? 'Згорнути' : 'Розгорнути' }}
                    </ion-button>
                  }
                }
              }
            }
          }
        </ion-card-content>
      </ion-card>

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', groupKey, 'score']">
            <ion-icon class="lk-tile" slot="start" name="trophy" aria-hidden="true" style="--lk-tile: #f2a900" />
            <ion-label>Точкування гуртка</ion-label>
          </ion-item>
          @if (keepsDues()) {
            <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', groupKey, 'dues']" data-testid="group-dues">
              <ion-icon class="lk-tile" slot="start" name="wallet" aria-hidden="true" style="--lk-tile: #30b0c7" />
              <ion-label>Вкладка гуртка</ion-label>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>

      <ion-list [inset]="true" data-testid="group-leadership">
        <ion-list-header><ion-label>Провід гуртка</ion-label></ion-list-header>
        <ion-item-group>
          @switch (leadership().state) {
            @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-item> }
            @case ('failed') { <ion-item><ion-label>Проводу ще немає.</ion-label></ion-item> }
            @default {
              @for (office of offices(); track office.leadershipHistoryKey) {
                <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', office.member.memberKey]">
                  <ion-label class="ion-text-wrap">
                    <h3>{{ name(office.member) }}</h3>
                    <app-role-tags [tags]="[officeTag(office)]" />
                  </ion-label>
                </ion-item>
              } @empty {
                <ion-item><ion-label>Проводу ще немає.</ion-label></ion-item>
              }
            }
          }
          @if (leadership().state === 'ready') {
            <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', groupKey, 'leadership']" data-testid="group-leadership-more">
              <ion-label>Архів проводу</ion-label>
            </ion-item>
          }
          <!-- Only over a провід that was read: one created blind beside an unread one would be a second. -->
          @if (canSetupLeadership() && leadership().state === 'ready' && members().state === 'ready') {
            <ion-item [button]="true" [detail]="false" (click)="setupLeadership()" data-testid="setup-leadership">
              <ion-icon slot="start" name="settings-outline" color="primary" aria-hidden="true" />
              <ion-label color="primary">Налаштувати провід</ion-label>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>

      @if (manager()) {
        <ion-list [inset]="true" data-testid="group-mentors">
          <ion-list-header><ion-label>Впорядники</ion-label></ion-list-header>
          <ion-item-group>
            @switch (mentors().state) {
              @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-item> }
              @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
              @default {
                @for (mentor of mentorList(); track mentor.memberKey) {
                  <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', mentor.memberKey]">
                    <app-member-avatar slot="start" [photo]="mentor.profilePhotoUrl" [firstName]="mentor.firstName" [lastName]="mentor.lastName" />
                    <ion-label class="ion-text-wrap"><h3>{{ name(mentor) }}</h3></ion-label>
                  </ion-item>
                } @empty {
                  <ion-item><ion-label class="ion-text-wrap">Впорядників ще не призначено.</ion-label></ion-item>
                }
                <ion-item [button]="true" [detail]="false" [disabled]="busy() !== null" (click)="chooseMentors()" data-testid="choose-mentors">
                  <ion-icon slot="start" name="people-outline" color="primary" aria-hidden="true" />
                  <ion-label color="primary">Обрати впорядників</ion-label>
                  @if (busy() === 'mentors') { <ion-spinner slot="end" name="crescent" /> }
                </ion-item>
              }
            }
          </ion-item-group>
        </ion-list>
      }

      @if (birthdays().length) {
        <ion-list [inset]="true" data-testid="birthdays">
          <ion-list-header><ion-label>Найближчі дні народження</ion-label></ion-list-header>
          <ion-item-group>
            @for (birthday of birthdays().slice(0, birthdayPreview); track birthday.member.memberKey) {
              <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', birthday.member.memberKey]">
                <app-member-avatar slot="start" [photo]="birthday.member.profilePhotoUrl" [firstName]="birthday.member.firstName" [lastName]="birthday.member.lastName" />
                <ion-label>
                  <h3>{{ birthday.member.firstName }} {{ birthday.member.lastName }}</h3>
                  <p>{{ shortDate(birthday.date) }}</p>
                </ion-label>
                <ion-note slot="end" [class.today]="birthday.daysUntil === 0">{{ birthdayWhen(birthday.daysUntil) }}</ion-note>
              </ion-item>
            }
            @if (birthdays().length > birthdayPreview) {
              <ion-item>
                <ion-label class="ion-text-wrap"><p>Ще {{ birthdays().length - birthdayPreview }} у межах 30 днів.</p></ion-label>
              </ion-item>
            }
          </ion-item-group>
        </ion-list>
      }

      @if (memberCount() >= searchFrom) {
        <ion-searchbar
          placeholder="Пошук"
          [value]="query()"
          [debounce]="150"
          (ionInput)="query.set($any($event).detail.value ?? '')"
        />
      }
      <ion-list [inset]="true" data-testid="group-members">
        <ion-list-header><ion-label>Учасники гуртка</ion-label></ion-list-header>
        <ion-item-group>
          @switch (members().state) {
            @case ('loading') {
              @for (i of [1, 2, 3]; track i) {
                <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
              }
            }
            @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
            @default {
              @for (member of shownMembers(); track member.memberKey) {
                <ion-item-sliding [disabled]="!manager() || busy() !== null" data-testid="group-member-row">
                <ion-item [button]="true" [detail]="!manager()" [routerLink]="['/tabs/kurin/member', member.memberKey]" data-testid="group-member">
                  <app-member-avatar slot="start" [size]="44" [photo]="member.profilePhotoUrl" [firstName]="member.firstName" [lastName]="member.lastName" />
                  <ion-label class="ion-text-wrap">
                    <h3>
                      {{ member.firstName }} {{ member.lastName }}
                      @if (member.profileVerificationStatus === 'VerifiedCurrent') {
                        <ion-icon class="verified" name="checkmark-circle" aria-label="Дані верифіковано" />
                      } @else if (member.profileVerificationStatus === 'VerifiedStale') {
                        <ion-icon class="verified stale" name="alert-circle" aria-label="Дані змінено після верифікації" />
                      }
                      @if (warningOf(member); as warning) {
                        <span class="dots" role="img" [attr.aria-label]="warningText(warning)">
                          @for (level of [1, 2, 3]; track level) { <span [class.on]="level <= warning.level"></span> }
                        </span>
                      }
                    </h3>
                    <p>{{ levelOf(member) }}</p>
                    <app-role-tags [tags]="roleTags(member)" />
                  </ion-label>
                  @if (manager() && !isMe(member)) {
                    <ion-button
                      slot="end"
                      fill="clear"
                      size="default"
                      class="actions"
                      [disabled]="busy() !== null"
                      [attr.aria-label]="'Дії з учасником ' + member.firstName + ' ' + member.lastName"
                      (click)="$event.stopPropagation(); $event.preventDefault(); memberActions(member)"
                      data-testid="member-actions"
                    >
                      @if (busy() === member.memberKey) {
                        <ion-spinner name="crescent" />
                      } @else {
                        <ion-icon slot="icon-only" name="ellipsis-horizontal" aria-hidden="true" />
                      }
                    </ion-button>
                  }
                </ion-item>
                @if (manager() && !isMe(member)) {
                  <ion-item-options side="end">
                    <ion-item-option color="primary" (click)="moveMember(member)">Перевести</ion-item-option>
                    <ion-item-option color="danger" (click)="removeMember(member)">Прибрати</ion-item-option>
                  </ion-item-options>
                }
                </ion-item-sliding>
              } @empty {
                <ion-item><ion-label>{{ query() ? 'Нічого не знайшлось.' : 'У гуртку ще нікого немає.' }}</ion-label></ion-item>
              }
              @if (canCreate()) {
                <ion-item [button]="true" [detail]="false" (click)="newMember()" data-testid="group-new-member">
                  <ion-icon slot="start" name="person-add" color="primary" aria-hidden="true" />
                  <ion-label color="primary">Новий учасник</ion-label>
                </ion-item>
              }
              @if (manager()) {
                <ion-item [button]="true" [detail]="false" [disabled]="busy() !== null" (click)="addFromKurin()" data-testid="add-from-kurin">
                  <ion-icon slot="start" name="people-outline" color="primary" aria-hidden="true" />
                  <ion-label color="primary">Додати з куреня</ion-label>
                  @if (busy() === 'add') { <ion-spinner slot="end" name="crescent" /> }
                </ion-item>
              }
            }
          }
        </ion-item-group>
      </ion-list>
      @if (manager() && memberCount()) {
        <p class="hint">Потягни учасника ліворуч або торкнись «⋯», щоб перевести до іншого гуртка чи прибрати з цього.</p>
      }
    </ion-content>
  `,
})
export class GroupPage implements OnInit {
  protected readonly title = kurinTitle;
  private readonly data = inject(KurinApi);
  private readonly auth = inject(AuthService);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly nav = inject(NavController);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  protected readonly groupKey = inject(ActivatedRoute).snapshot.paramMap.get('groupKey') ?? '';

  protected readonly failedText = FAILED_TEXT;
  protected readonly roleTags = memberRoleTags;
  protected readonly officeTag = officeTag;
  protected readonly shortDate = shortDate;
  protected readonly birthdayWhen = birthdayWhen;
  protected readonly warningText = warningText;
  protected readonly birthdayPreview = BIRTHDAY_PREVIEW;
  protected readonly searchFrom = SEARCH_FROM;

  protected readonly expanded = signal(false);
  protected readonly query = signal('');
  protected readonly group = signal<Loaded<GroupDto>>({ state: 'loading' });
  protected readonly leadership = signal<Loaded<LeadershipHistoryDto[]>>({ state: 'loading' });
  protected readonly members = signal<Loaded<MemberLookupDto[]>>({ state: 'loading' });
  private readonly duesGroups = signal<string[]>([]);
  /** The server's check-access answer for changing this гурток (web canEditGroupProfile). */
  protected readonly canEdit = signal(false);
  /** The server's check-access answer for a new member here (web canCreateMembers). */
  protected readonly canCreate = signal(false);
  /** The провід record itself: its key decides between creating and updating it. */
  private readonly leadershipRecord = signal<LeadershipDto | null>(null);
  protected readonly mentors = signal<Loaded<MemberLookupDto[]>>({ state: 'loading' });
  /** What is being saved: a member's key, 'add' or 'mentors'. */
  protected readonly busy = signal<string | null>(null);
  /** Placement and виховники are the Звʼязковий's (web canManageWholeKurin, canManageMentors). */
  protected readonly manager = computed(() => kurinAccess(this.auth.user()).manageWholeKurin);
  protected readonly canSetupLeadership = computed(() => kurinAccess(this.auth.user()).setupGroupLeadership);
  protected readonly mentorList = computed(() => valueOf(this.mentors()) ?? []);

  protected readonly groupValue = computed(() => valueOf(this.group()));
  protected readonly description = computed(() => this.groupValue()?.description?.trim() ?? '');
  protected readonly longDescription = computed(() => this.description().length > DESCRIPTION_LIMIT);
  protected readonly offices = computed(() => currentOffices(valueOf(this.leadership())));
  protected readonly memberCount = computed(() => valueOf(this.members())?.length ?? 0);
  protected readonly shownMembers = computed(() => sortMembers(filterMembers(valueOf(this.members()) ?? [], this.query()), 'name'));
  protected readonly birthdays = computed(() => upcomingBirthdays(valueOf(this.members()) ?? [], BIRTHDAY_DAYS));
  /** As the web's menu: the «Вкладка гуртка» entry is there for whoever keeps this гурток's box. */
  protected readonly keepsDues = computed(() => this.duesGroups().includes(this.groupKey));

  constructor() {
    addIcons({ people, peopleOutline, personAdd, settingsOutline, ellipsisHorizontal, trophy, wallet, checkmarkCircle, alertCircle });
    // Someone's card changed on a page above this one: the people here follow.
    effect(() => {
      if (this.data.membersChanged()) untracked(() => void settle(this.data.groupMembers(this.groupKey), this.members));
    });
    // Its провід seated on the провід page above this one.
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

  protected async edit(): Promise<void> {
    const group = this.groupValue();
    if (!group || !this.canEdit()) return;
    const manager = kurinAccess(this.auth.user()).manageWholeKurin;
    const modal = await this.modals.create({
      component: GroupEditSheet,
      componentProps: { group, kurinKey: group.kurinKey, canRename: manager, canDelete: manager },
      presentingElement: this.host.closest<HTMLElement>('.ion-page') ?? undefined,
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<GroupEditResult>();
    if (data === 'deleted') await this.nav.navigateBack('/tabs/kurin');
    else if (data === 'saved') await settle(this.data.group(this.groupKey), this.group);
  }

  protected name(member: MemberLookupDto): string {
    return fullName(member);
  }

  // ── People ─────────────────────────────────────────────────────────────────────────────────────

  /** «Новий учасник» straight into this гурток (web group-panel «Додати учасника»); then their card. */
  protected async newMember(): Promise<void> {
    if (!this.canCreate()) return;
    const modal = await this.modals.create({
      component: ProfileEditSheet,
      componentProps: { member: null, placement: { groupKey: this.groupKey }, kurinKey: this.groupValue()?.kurinKey ?? null },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<ProfileEditResult>();
    if (data && typeof data === 'object') await this.nav.navigateForward(['/tabs/kurin/member', data.created]);
  }

  /** People already in the kurin, put into this гурток (web member-card «Гурток», for several at once). */
  protected async addFromKurin(): Promise<void> {
    const kurinKey = this.groupValue()?.kurinKey;
    if (!kurinKey || this.busy()) return;
    this.busy.set('add');
    let people: MemberLookupDto[];
    try {
      people = addableToGroup(await this.data.kurinMembers(kurinKey), this.groupKey, valueOf(this.members()) ?? [])
        .filter((member) => !this.isMe(member));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося прочитати учасників куреня.'), 'danger');
      this.busy.set(null);
      return;
    }
    this.busy.set(null);
    const modal = await this.modals.create({
      component: PeoplePickSheet,
      componentProps: {
        title: 'Додати з куреня',
        people: people.map((member): PickPerson => ({ key: member.memberKey, member, note: placementLine(member) })),
        multiple: true,
        allowEmpty: false,
        doneLabel: 'Додати',
        emptyText: 'Усі учасники куреня вже в цьому гуртку.',
        hint: 'Той, хто вже в іншому гуртку, перейде сюди.',
      },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<string[] | null>();
    if (!data?.length) return;
    this.busy.set('add');
    let added = 0;
    for (const memberKey of data) {
      try {
        await this.data.moveToGroup(kurinKey, memberKey, this.groupKey);
        added++;
      } catch {
        // Counted below: the rest still go in.
      }
    }
    this.busy.set(null);
    await this.toasts.show(
      added === data.length ? `Додано до гуртка: ${added}.` : `Додано ${added} з ${data.length}. Решту не вдалося — спробуй ще раз.`,
      added === data.length ? undefined : 'danger',
    );
  }

  /** Nobody moves themselves between гуртки; the server refuses it too. */
  protected isMe(member: Pick<MemberLookupDto, 'memberKey'>): boolean {
    return member.memberKey === this.auth.user()?.memberKey;
  }

  protected async memberActions(member: MemberLookupDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: `${member.firstName} ${member.lastName}`,
      buttons: [
        { text: 'Перевести до іншого гуртка', data: 'move' },
        { text: 'Прибрати з гуртка', role: 'destructive', data: 'remove' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data === 'move') await this.moveMember(member);
    else if (data === 'remove') await this.removeMember(member);
  }

  /** To another гурток of the kurin (web member-card «Гурток»). */
  protected async moveMember(member: MemberLookupDto): Promise<void> {
    await this.closeSliding();
    const kurinKey = this.groupValue()?.kurinKey;
    if (!kurinKey || this.busy()) return;
    let others: GroupDto[];
    try {
      others = (await this.data.groups(kurinKey)).filter((g) => g.groupKey !== this.groupKey);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося прочитати гуртки.'), 'danger');
      return;
    }
    if (!others.length) {
      await this.toasts.show('У курені немає інших гуртків.');
      return;
    }
    const sheet = await this.sheets.create({
      header: `${member.firstName} ${member.lastName}`,
      subHeader: 'До якого гуртка перевести?',
      buttons: [...others.map((g) => ({ text: g.name, data: g.groupKey })), { text: 'Скасувати', role: 'cancel' }],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    const target = others.find((g) => g.groupKey === data);
    if (!target) return;
    await this.place(member, kurinKey, target.groupKey, `${member.firstName} ${member.lastName} тепер у гуртку «${target.name}».`);
  }

  /** Out of this гурток, still in the kurin (web «Гурток» with none chosen); leaving the kurin stays on the card. */
  protected async removeMember(member: MemberLookupDto): Promise<void> {
    await this.closeSliding();
    const kurinKey = this.groupValue()?.kurinKey;
    if (!kurinKey || this.busy()) return;
    const sheet = await this.sheets.create({
      header: `${member.firstName} ${member.lastName}`,
      subHeader: 'Прибрати з гуртка? Учасник лишається в курені.',
      buttons: [
        { text: 'Прибрати', role: 'destructive', data: 'yes' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data !== 'yes') return;
    await this.place(member, kurinKey, null, `${member.firstName} ${member.lastName} більше не в гуртку.`);
  }

  /** «Впорядники» (web group-panel's dialog): the КВ people with access to this гурток. */
  protected async chooseMentors(): Promise<void> {
    const kurinKey = this.groupValue()?.kurinKey;
    if (!kurinKey || this.busy()) return;
    this.busy.set('mentors');
    let candidates: MemberLookupDto[];
    try {
      candidates = (await this.data.mentorCandidates(kurinKey)).filter((c) => !!c.userKey);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося прочитати КВ.'), 'danger');
      this.busy.set(null);
      return;
    }
    this.busy.set(null);
    const before = this.mentorList().map((m) => m.userKey).filter((key): key is string => !!key);
    const modal = await this.modals.create({
      component: PeoplePickSheet,
      componentProps: {
        title: 'Впорядники',
        people: candidates.map((member): PickPerson => ({ key: member.userKey!, member })),
        selected: before,
        multiple: true,
        doneLabel: 'Зберегти',
        emptyText: 'У КВ ще нікого немає.',
        hint: 'Обери впорядників із КВ, які матимуть доступ до цього гуртка.',
      },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<string[] | null>();
    if (!data) return;
    const changes = mentorChanges(before, data);
    if (!changes.assign.length && !changes.revoke.length) return;
    this.busy.set('mentors');
    try {
      await this.data.saveMentors(this.groupKey, changes);
      await this.toasts.show('Впорядників збережено.');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося зберегти впорядників. Спробуй ще раз.'), 'danger');
    } finally {
      await settle(this.data.groupMentors(this.groupKey), this.mentors);
      this.busy.set(null);
    }
  }

  /** «Налаштувати провід» (web leadership-panel's gear → the leadership form for a гурток). */
  protected async setupLeadership(): Promise<void> {
    const modal = await this.modals.create({
      component: LeadershipSheet,
      componentProps: { kind: 'group', entityKey: this.groupKey, leadership: this.leadershipRecord(), members: valueOf(this.members()) ?? [] },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<boolean>();
    if (data) await this.loadLeadership();
  }

  private async place(member: MemberLookupDto, kurinKey: string, groupKey: string | null, done: string): Promise<void> {
    this.busy.set(member.memberKey);
    try {
      await this.data.moveToGroup(kurinKey, member.memberKey, groupKey);
      await this.toasts.show(done);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося перевести. Спробуй ще раз.'), 'danger');
    } finally {
      this.busy.set(null);
    }
  }

  private async closeSliding(): Promise<void> {
    const rows = this.host.querySelectorAll('ion-item-sliding');
    await Promise.all([...rows].map((row) => (row as HTMLIonItemSlidingElement).close()));
  }

  /** The page under an iOS card sheet, so it recedes behind it. */
  private presenting(): HTMLElement | undefined {
    return this.host.closest<HTMLElement>('.ion-page') ?? undefined;
  }

  /** The server answers a гурток with no провід yet with nothing, not an error. */
  private loadLeadership(): Promise<void> {
    return settle(
      this.data.leadership('group', this.groupKey).then((l) => {
        this.leadershipRecord.set(l ?? null);
        return l?.leadershipHistories ?? [];
      }),
      this.leadership,
    );
  }

  protected levelOf(member: MemberLookupDto): string {
    return plastLevelLabel(member) ?? 'Ступінь не вказано';
  }

  protected warningOf(member: MemberLookupDto): { level: number; daysLeft: number } | null {
    return activeWarning(member.warnings);
  }

  private async load(): Promise<void> {
    const groupKey = this.groupKey;
    const kurinKey = this.auth.user()?.kurinKey;
    await Promise.all([
      settle(this.data.group(groupKey), this.group),
      settle(this.data.groupMembers(groupKey), this.members),
      this.loadLeadership(),
      this.data.canUpdateGroup(groupKey).then(
        (allowed) => this.canEdit.set(allowed),
        () => this.canEdit.set(false),
      ),
      this.data.canCreateInGroup(groupKey).then(
        (allowed) => this.canCreate.set(allowed),
        () => this.canCreate.set(false),
      ),
      this.manager() ? settle(this.data.groupMentors(groupKey), this.mentors) : Promise.resolve(),
      kurinKey && kurinAccess(this.auth.user()).seeGroupDues
        ? this.data.duesGroups(kurinKey).then(
            (groups) => this.duesGroups.set(groups.map((g) => g.groupKey)),
            () => this.duesGroups.set([]),
          )
        : Promise.resolve(),
    ]);
  }
}
