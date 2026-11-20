import { Component, ElementRef, OnDestroy, OnInit, afterRenderEffect, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ActionSheetController,
  IonBadge,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
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
  IonSegment,
  IonSegmentButton,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
  IonMenuButton,
  ModalController,
  NavController,
} from '@ionic/angular';
import { HeaderActions } from '../../nav/header-actions';
import { addIcons } from 'ionicons';
import { add, alertCircle, checkmarkCircle, flagOutline, people, personAdd, ribbon, settingsOutline, swapHorizontal, swapVertical, trophy } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { canShareFile, saveFile, shareFile } from '../../core/file-share';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { GlassEffects } from '../../ui/glass';
import {
  KvRow,
  MemberSort,
  currentOffices,
  filterMembers,
  fullName,
  hasYouthProgram,
  kurinAccess,
  kvRowTags,
  kvRows,
  memberRoleTags,
  officeTag,
  plastLevelLabel,
  sortMembers,
} from './kurin.labels';
import { GroupDto, KurinDto, LeadershipDto, MemberLookupDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { MemberAvatar } from './member-avatar';
import { RoleTags } from './role-tags';
import { GroupSilhouette } from './group-silhouette';
import { KurinPlaque } from './kurin-plaque';
import { GroupEditResult, GroupEditSheet } from './group-edit.sheet';
import { ProfileEditResult, ProfileEditSheet } from './profile-edit.sheet';
import { KurinEditSheet } from './kurin-edit.sheet';
import { reportFileName } from './kurin-profile';
import { LeadershipSheet } from './leadership.sheet';
import { isSeniorBranch, kurinBranchTags, kurinBranchesName, kurinTitle } from '../../core/kurin-label';

type Section = 'overview' | 'members';

const DESCRIPTION_LIMIT = 360;

/**
 * The kurin tab (web kurin-panel): who the kurin is, its гуртки, КВ and провід on «Огляд», and
 * everyone in it on «Учасники». The Звʼязковий starts a гурток here («Новий гурток», web
 * manage-panel), adds a member to the kurin («Новий учасник», web «Додати учасника куреня»); a гурток is changed
 * and deleted from its own page. «Редагувати» on the kurin's card is the web's menu of the same
 * name: the kurin's record and its PDF report. The КВ and the провід open their own pages with the
 * archive (web kv-panel, leadership-panel); «Налаштувати провід» opens the провід form here for
 * whoever may seat it.
 */
@Component({
  selector: 'app-kurin',
  imports: [
    RoleTags,
    IonMenuButton,
    HeaderActions,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSegment,
    IonSegmentButton,
    IonLabel,
    IonCard,
    IonCardContent,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonIcon,
    IonBadge,
    IonSearchbar,
    IonSkeletonText,
    IonSpinner,
    RouterLink,
    MemberAvatar,
    GroupSilhouette,
    KurinPlaque,
  ],
  styles: `
    app-group-silhouette.mini {
      margin-inline-end: 16px;
    }
    .segment {
      padding: 4px 16px 8px;
    }
    .head {
      display: flex;
      gap: 14px;
      align-items: center;
    }
    .emblem {
      width: 48px;
      height: 48px;
      flex: none;
      object-fit: contain;
    }
    .emblem--empty {
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px dashed var(--lk-line);
      border-radius: 12px;
      color: var(--ion-color-medium);
      font-size: 20px;
    }
    .head .edit {
      margin: -6px -8px 0 auto;
      align-self: flex-start;
      flex-shrink: 0;
    }
    .head .edit ion-spinner {
      width: 18px;
      height: 18px;
    }
    .head h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .head p {
      margin: 2px 0 0;
      color: var(--lk-muted);
    }
    .facts {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
      margin: 14px 0 0;
    }
    .facts dt {
      font-size: 13px;
      font-weight: 600;
      color: var(--lk-faint);
    }
    .facts dd {
      margin: 2px 0 0;
      color: var(--lk-ink);
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
    ion-badge {
      margin-top: 6px;
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
    ion-searchbar {
      padding-inline: 12px;
    }
    .empty {
      padding: 4px 20px;
      color: var(--lk-muted);
    }
    app-member-avatar {
      margin-inline-end: 12px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-menu-button /></ion-buttons>
        <ion-title>Курінь</ion-title>
        <ion-buttons slot="end">
          @if (section() === 'members') {
            <ion-button aria-label="Сортування" (click)="chooseSort()">
              <ion-icon slot="icon-only" name="swap-vertical" />
            </ion-button>
          }
          <app-header-actions />
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>
      <ion-header collapse="condense">
        <ion-toolbar><ion-title size="large">Курінь</ion-title></ion-toolbar>
      </ion-header>

      @if (!kurinKey) {
        <p class="empty">Ти ще не в курені.</p>
      } @else {
        <div class="segment">
          <ion-segment [value]="section()" (ionChange)="pick($event)">
            <ion-segment-button value="overview"><ion-label>Огляд</ion-label></ion-segment-button>
            <ion-segment-button value="members"><ion-label>Учасники</ion-label></ion-segment-button>
          </ion-segment>
        </div>

        @if (section() === 'overview') {
          <ion-card data-testid="kurin-head">
            <ion-card-content>
              @switch (kurin().state) {
                @case ('loading') { <ion-skeleton-text [animated]="true" style="height: 56px" /> }
                @case ('failed') { <p>{{ failedText }}</p> }
                @default {
                  @if (kurinValue(); as k) {
                    <div class="head">
                      @if (isSenior(k)) {
                        @if (k.emblemUrl) {
                          <img class="emblem" [src]="k.emblemUrl" alt="Емблема куреня" width="48" height="48" data-testid="kurin-emblem" />
                        } @else {
                          <span class="emblem emblem--empty" role="img" aria-label="Емблему ще не завантажено"><ion-icon name="flag-outline" aria-hidden="true" /></span>
                        }
                      } @else {
                        <app-kurin-plaque [number]="k.number" [size]="48" data-testid="kurin-plaque" />
                      }
                      <div>
                        <h2>{{ heading(k) }}</h2>
                        @if (isSenior(k)) {
                          @if (k.namedAfter) { <p>{{ branchesName(k) }}</p> }
                        } @else if (k.namedAfter) {
                          <p>ім. {{ k.namedAfter }}</p>
                        }
                        <ion-badge class="lk-tag--secondary">{{ branchLabel(k) }}</ion-badge>
                      </div>
                      @if (canManageGroups()) {
                        <ion-button
                          class="edit"
                          fill="clear"
                          size="small"
                          [disabled]="reportBusy()"
                          (click)="kurinActions(k)"
                          data-testid="edit-kurin"
                          [attr.aria-label]="reportBusy() ? 'Готуємо звіт куреня' : null"
                        >
                          @if (reportBusy()) { <ion-spinner name="crescent" /> } @else { Редагувати }
                        </ion-button>
                      }
                    </div>
                    <dl class="facts">
                      <div><dt>Станиця</dt><dd>{{ k.stanytsia || '—' }}</dd></div>
                      <div><dt>Край</dt><dd>{{ k.regionOrCountry || '—' }}</dd></div>
                    </dl>
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

          @if ((kurinValue()?.scoreEnabled ?? true) || canManageGroups() || canReviewSkills()) {
          <ion-list [inset]="true">
            <ion-item-group>
              @if (kurinValue()?.scoreEnabled ?? true) {
                <ion-item [button]="true" [detail]="true" routerLink="/tabs/kurin/score">
                  <ion-icon class="lk-tile" slot="start" name="trophy" aria-hidden="true" style="--lk-tile: #f2a900" />
                  <ion-label>Точкування</ion-label>
                </ion-item>
              }
              @if (canManageGroups()) {
                <ion-item [button]="true" [detail]="true" routerLink="/tabs/kurin/transfers" data-testid="transfers">
                  <ion-icon class="lk-tile" slot="start" name="swap-horizontal" aria-hidden="true" style="--lk-tile: #0e6e4e" />
                  <ion-label>Переходи</ion-label>
                </ion-item>
              }
              @if (canReviewSkills()) {
                <ion-item [button]="true" [detail]="true" routerLink="/tabs/kurin/review/skills">
                  <ion-icon class="lk-tile" slot="start" name="ribbon" aria-hidden="true" style="--lk-tile: #5e5ce6" />
                  <ion-label>Вмілості на перевірку</ion-label>
                </ion-item>
              }
            </ion-item-group>
          </ion-list>
          }

          @if (!kurinIsSenior()) {
          <ion-list [inset]="true" data-testid="groups">
            <ion-list-header><ion-label>Гуртки</ion-label></ion-list-header>
            <ion-item-group>
              @switch (groups().state) {
                @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-item> }
                @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
                @default {
                  @for (group of groupList(); track group.groupKey) {
                    <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', group.groupKey]">
                      <app-group-silhouette slot="start" class="mini" [url]="group.silhouetteUrl" [size]="32" />
                      <ion-label>{{ group.name }}</ion-label>
                    </ion-item>
                  } @empty {
                    <ion-item><ion-label>Гуртків поки немає.</ion-label></ion-item>
                  }
                  @if (canManageGroups()) {
                    <ion-item [button]="true" [detail]="false" (click)="newGroup()" data-testid="new-group">
                      <ion-icon slot="start" name="add" color="primary" aria-hidden="true" />
                      <ion-label color="primary">Новий гурток</ion-label>
                    </ion-item>
                  }
                }
              }
            </ion-item-group>
          </ion-list>
          }

          <ion-list [inset]="true" data-testid="kv">
            <ion-list-header><ion-label>КВ</ion-label></ion-list-header>
            <ion-item-group>
              @switch (kv().state) {
                @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-item> }
                @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
                @default {
                  @for (row of kvList(); track row.member.memberKey) {
                    <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', row.member.memberKey]">
                      <app-member-avatar slot="start" [photo]="row.member.profilePhotoUrl" [firstName]="row.member.firstName" [lastName]="row.member.lastName" />
                      <ion-label class="ion-text-wrap">
                        <h3>{{ name(row.member) }}</h3>
                        <app-role-tags [tags]="kvTags(row)" />
                      </ion-label>
                    </ion-item>
                  } @empty {
                    <ion-item><ion-label>Впорядників ще не призначено.</ion-label></ion-item>
                  }
                  <ion-item [button]="true" [detail]="true" routerLink="/tabs/kurin/kv" data-testid="kv-more">
                    <ion-label>{{ canManageGroups() ? 'Впорядники й архів' : 'Архів впорядників' }}</ion-label>
                  </ion-item>
                }
              }
            </ion-item-group>
          </ion-list>

          <ion-list [inset]="true" data-testid="leadership">
            <ion-list-header><ion-label>Провід куреня</ion-label></ion-list-header>
            <ion-item-group>
              @switch (leadership().state) {
                @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 24px" /></ion-item> }
                @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
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
                  <ion-item [button]="true" [detail]="true" routerLink="/tabs/kurin/leadership" data-testid="leadership-more">
                    <ion-label>Архів проводу</ion-label>
                  </ion-item>
                  <!-- Only over a провід that was read: one created blind beside an unread one would be a second. -->
                  @if (canSetupLeadership() && members().state === 'ready') {
                    <ion-item [button]="true" [detail]="false" (click)="setupLeadership()" data-testid="setup-leadership">
                      <ion-icon slot="start" name="settings-outline" color="primary" aria-hidden="true" />
                      <ion-label color="primary">Налаштувати провід</ion-label>
                    </ion-item>
                  }
                }
              }
            </ion-item-group>
          </ion-list>
        } @else {
          <ion-searchbar
            placeholder="Пошук"
            [value]="query()"
            [debounce]="150"
            (ionInput)="query.set($any($event).detail.value ?? '')"
            data-testid="member-search"
          />
          <ion-list [inset]="true" data-testid="members">
            <ion-list-header><ion-label>{{ membersHeader() }}</ion-label></ion-list-header>
            <ion-item-group>
              @switch (members().state) {
                @case ('loading') {
                  @for (i of [1, 2, 3, 4]; track i) {
                    <ion-item><ion-skeleton-text [animated]="true" style="height: 32px" /></ion-item>
                  }
                }
                @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
                @default {
                  @if (canManageGroups()) {
                    <ion-item [button]="true" [detail]="false" (click)="newMember()" data-testid="new-member">
                      <ion-icon slot="start" name="person-add" color="primary" aria-hidden="true" />
                      <ion-label color="primary">Новий учасник</ion-label>
                    </ion-item>
                  }
                  @for (member of shownMembers(); track member.memberKey) {
                    <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/member', member.memberKey]" data-testid="member">
                      <app-member-avatar slot="start" [photo]="member.profilePhotoUrl" [firstName]="member.firstName" [lastName]="member.lastName" />
                      <ion-label class="ion-text-wrap">
                        <h3>
                          {{ name(member) }}
                          @if (member.profileVerificationStatus === 'VerifiedCurrent') {
                            <ion-icon class="verified" name="checkmark-circle" aria-label="Дані верифіковано" />
                          } @else if (member.profileVerificationStatus === 'VerifiedStale') {
                            <ion-icon class="verified stale" name="alert-circle" aria-label="Дані змінено після верифікації" />
                          }
                        </h3>
                        @if (levelLine(member); as level) {
                          <p>{{ level }}</p>
                        }
                        <app-role-tags [tags]="roleTags(member)" />
                      </ion-label>
                    </ion-item>
                  } @empty {
                    <ion-item><ion-label>{{ query() ? 'Нічого не знайшлось.' : 'Тут ще нікого немає.' }}</ion-label></ion-item>
                  }
                }
              }
            </ion-item-group>
          </ion-list>
        }
      }
    </ion-content>
  `,
})
export class KurinPage implements OnInit, OnDestroy {

  private readonly data = inject(KurinApi);
  private readonly auth = inject(AuthService);
  private readonly sheets = inject(ActionSheetController);
  private readonly modals = inject(ModalController);
  private readonly nav = inject(NavController);
  private readonly toasts = inject(Toasts);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly glass = new GlassEffects(this.host);

  protected readonly kurinKey = this.auth.user()?.kurinKey ?? null;
  protected readonly failedText = FAILED_TEXT;
  protected readonly roleTags = memberRoleTags;
  protected readonly officeTag = officeTag;
  protected readonly kvTags = kvRowTags;

  protected readonly section = signal<Section>('overview');
  protected readonly expanded = signal(false);
  protected readonly query = signal('');
  protected readonly sort = signal<MemberSort>('name');

  protected readonly kurin = signal<Loaded<KurinDto>>({ state: 'loading' });
  protected readonly groups = signal<Loaded<GroupDto[]>>({ state: 'loading' });
  protected readonly kv = signal<Loaded<KvRow[]>>({ state: 'loading' });
  /** The kurin's провід; null when it has none yet (the server answers with nothing). */
  protected readonly leadership = signal<Loaded<LeadershipDto | null>>({ state: 'loading' });
  protected readonly members = signal<Loaded<MemberLookupDto[]>>({ state: 'loading' });
  protected readonly reportBusy = signal(false);

  protected readonly kurinValue = computed(() => valueOf(this.kurin()));
  protected readonly groupList = computed(() => valueOf(this.groups()) ?? []);
  /** A senior kurin has no гуртки: the section is not there at all. */
  protected readonly kurinIsSenior = computed(() => isSeniorBranch(this.kurinValue()?.branch));
  protected readonly kvList = computed(() => valueOf(this.kv()) ?? []);
  protected readonly offices = computed(() => currentOffices(valueOf(this.leadership())?.leadershipHistories));
  protected readonly description = computed(() => this.kurinValue()?.description?.trim() ?? '');
  protected readonly longDescription = computed(() => this.description().length > DESCRIPTION_LIMIT);
  /** As the web's menu: reviewers of a youth kurin. */
  protected readonly canReviewSkills = computed(
    () => kurinAccess(this.auth.user()).reviewSkills && hasYouthProgram(this.kurinValue()?.branch),
  );
  /**
   * Web canManageGroups: the Звʼязковий and admins create, rename and delete гуртки, and (as the
   * web's canEditKurinProfile) change the kurin's record and export its report.
   */
  protected readonly canManageGroups = computed(() => kurinAccess(this.auth.user()).manageWholeKurin);
  /** Web canSetupLeadership('kurin'): the Звʼязковий, admins and the курінний. */
  protected readonly canSetupLeadership = computed(() => kurinAccess(this.auth.user()).setupKurinLeadership);
  protected readonly shownMembers = computed(() =>
    sortMembers(filterMembers(valueOf(this.members()) ?? [], this.query()), this.sort()),
  );
  protected readonly membersHeader = computed(() => {
    const all = valueOf(this.members());
    const by = this.sort() === 'name' ? 'за прізвищем' : 'за роллю';
    return all ? `${all.length} ${plural(all.length)} · ${by}` : 'Учасники';
  });

  constructor() {
    addIcons({ add, personAdd, trophy, ribbon, people, swapHorizontal, swapVertical, checkmarkCircle, alertCircle, settingsOutline, flagOutline });
    afterRenderEffect(() => {
      this.section();
      this.glass.sync();
    });
    // A гурток or a card changed on a page above this one: the lists here follow.
    effect(() => {
      if (this.data.groupsChanged() && this.kurinKey) untracked(() => void this.reloadGroups(this.kurinKey!));
    });
    effect(() => {
      if (this.data.membersChanged() && this.kurinKey) untracked(() => void settle(this.data.kurinMembers(this.kurinKey!), this.members));
    });
    effect(() => {
      if (this.data.leadershipChanged() && this.kurinKey) untracked(() => void this.loadLeadership(this.kurinKey!));
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    this.glass.destroy();
  }

  protected pick(event: Event): void {
    const value = (event as CustomEvent<{ value?: string }>).detail.value;
    if (value === 'overview' || value === 'members') this.section.set(value);
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected name(member: MemberLookupDto): string {
    return fullName(member);
  }

  protected branchLabel(kurin: KurinDto): string {
    return kurinBranchTags(kurin.branches, kurin.branch ?? 'UPYu');
  }

  protected isSenior(kurin: KurinDto): boolean {
    return isSeniorBranch(kurin.branch);
  }

  /** «47 курінь»; a senior kurin by its own name, its гілки under it. */
  protected heading(kurin: KurinDto): string {
    return this.isSenior(kurin) ? kurin.namedAfter || this.branchesName(kurin) : kurinTitle(kurin.number);
  }

  /** «5-й курінь УСП · 20-й курінь УПС». */
  protected branchesName(kurin: KurinDto): string {
    return kurinBranchesName(kurin.branches);
  }

  /** The ступінь above the office tags; «Учасник» only for someone with neither (web «Статус»). */
  protected levelLine(member: MemberLookupDto): string | null {
    return plastLevelLabel(member) ?? (memberRoleTags(member).length ? null : 'Учасник');
  }

  protected async chooseSort(): Promise<void> {
    const sheet = await this.sheets.create({
      header: 'Сортування',
      buttons: [
        { text: 'За прізвищем', data: 'name' },
        { text: 'За роллю', data: 'role' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<MemberSort>();
    if (data === 'name' || data === 'role') this.sort.set(data);
  }

  protected async newGroup(): Promise<void> {
    if (!this.kurinKey) return;
    const modal = await this.modals.create({
      component: GroupEditSheet,
      componentProps: { group: null, kurinKey: this.kurinKey, canRename: true, canDelete: false },
      presentingElement: this.host.closest<HTMLElement>('.ion-page') ?? undefined,
    });
    await modal.present();
    await modal.onWillDismiss<GroupEditResult>();
  }

  /** «Новий учасник» in the kurin, with no гурток yet (web «Додати учасника куреня»); then their card. */
  protected async newMember(): Promise<void> {
    if (!this.kurinKey) return;
    const modal = await this.modals.create({
      component: ProfileEditSheet,
      componentProps: { member: null, placement: { kurinKey: this.kurinKey } },
      presentingElement: this.host.closest<HTMLElement>('.ion-page') ?? undefined,
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<ProfileEditResult>();
    if (data && typeof data === 'object') await this.nav.navigateForward(['/tabs/kurin/member', data.created]);
  }

  /** «Налаштувати провід» (web leadership-panel's gear → the leadership form for the kurin). */
  protected async setupLeadership(): Promise<void> {
    if (!this.kurinKey || this.leadership().state !== 'ready') return;
    const modal = await this.modals.create({
      component: LeadershipSheet,
      componentProps: {
        kind: 'kurin',
        entityKey: this.kurinKey,
        leadership: valueOf(this.leadership()),
        members: valueOf(this.members()) ?? [],
        // The курінний seats everyone but himself (the server's AssignableOffices).
        lockedRoles: kurinAccess(this.auth.user()).seatKurinHead ? [] : ['Kurinnuy'],
      },
      presentingElement: this.host.closest<HTMLElement>('.ion-page') ?? undefined,
    });
    await modal.present();
  }

  /** The web's «Редагувати» menu on the kurin's header. */
  protected async kurinActions(kurin: KurinDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: this.heading(kurin),
      buttons: [
        { text: 'Редагування даних куреня', data: 'edit' },
        { text: 'Експорт звіту куреня', data: 'report' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (data === 'edit') await this.editKurin(kurin);
    if (data === 'report') await this.exportReport(kurin);
  }

  private async editKurin(kurin: KurinDto): Promise<void> {
    const modal = await this.modals.create({
      component: KurinEditSheet,
      componentProps: { kurin },
      presentingElement: this.host.closest<HTMLElement>('.ion-page') ?? undefined,
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<KurinDto | null>();
    if (data) this.kurin.set({ state: 'ready', value: data });
  }

  /**
   * The PDF takes the server a while. A phone that shares files asks once more when it is ready:
   * its share sheet opens only from a fresh tap. Elsewhere it downloads, as on the web.
   */
  private async exportReport(kurin: KurinDto): Promise<void> {
    if (this.reportBusy()) return;
    this.reportBusy.set(true);
    let file: File;
    try {
      const pdf = await this.data.reportPdf(kurin.kurinKey);
      file = new File([pdf], reportFileName(kurin.number, new Date()), { type: 'application/pdf' });
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося підготувати звіт. Спробуй ще раз.'), 'danger');
      return;
    } finally {
      this.reportBusy.set(false);
    }
    if (!canShareFile(file)) {
      saveFile(file);
      return;
    }
    const ready = await this.sheets.create({
      header: 'Звіт куреня готовий',
      subHeader: file.name,
      buttons: [
        // Handlers run inside the tap, which the share sheet needs.
        { text: 'Поділитися…', handler: () => void shareFile(file) },
        { text: 'Зберегти файл', handler: () => saveFile(file) },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await ready.present();
  }

  private async reloadGroups(kurinKey: string): Promise<void> {
    const groups = this.data.groups(kurinKey);
    await Promise.all([
      settle(groups, this.groups),
      settle(
        Promise.all([groups, this.data.kvMembers(kurinKey), this.data.mentorAssignments(kurinKey)]).then(
          ([all, kv, assignments]) => kvRows(kv, assignments, all),
        ),
        this.kv,
      ),
    ]);
  }

  private loadLeadership(kurinKey: string): Promise<void> {
    return settle(this.data.leadership('kurin', kurinKey).then((l) => l ?? null), this.leadership);
  }

  private async load(): Promise<void> {
    const kurinKey = this.kurinKey;
    if (!kurinKey) return;
    const groups = this.data.groups(kurinKey);
    await Promise.all([
      settle(this.data.kurin(kurinKey), this.kurin),
      settle(groups, this.groups),
      settle(this.data.kurinMembers(kurinKey), this.members),
      this.loadLeadership(kurinKey),
      settle(
        Promise.all([groups, this.data.kvMembers(kurinKey), this.data.mentorAssignments(kurinKey)]).then(
          ([all, kv, assignments]) => kvRows(kv, assignments, all),
        ),
        this.kv,
      ),
    ]);
  }
}

function plural(count: number): string {
  const tens = count % 100;
  const ones = count % 10;
  if (tens >= 11 && tens <= 14) return 'учасників';
  if (ones === 1) return 'учасник';
  if (ones >= 2 && ones <= 4) return 'учасники';
  return 'учасників';
}
