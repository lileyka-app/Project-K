import { Component, ElementRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ActionSheetController,
  IonBackButton,
  IonBadge,
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
  IonProgressBar,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
  ModalController,
  NavController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { add, alertCircle, checkmarkCircle, gitNetwork, lockClosed, logOut, medal, ribbon } from 'ionicons/icons';
import { AuthService } from '../../auth/auth.service';
import { formatPhone } from '../../core/phone';
import { apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { dateLabel } from '../../me/labels';
import { AwardSheet } from './award.sheet';
import {
  AwardGroup,
  BRANCH_LABELS,
  DUES_KIND_LABELS,
  MEMBERSHIP_KIND_LABELS,
  ProbeRow,
  SkillView,
  activeWarning,
  awardGroups,
  awardTitle,
  awardWeight,
  currentOffices,
  dateOnly,
  duesAmountLabel,
  duesMoney,
  emailHref,
  hasYouthProgram,
  kurinAccess,
  memberBranch,
  membershipPeriod,
  phoneHref,
  plastLevelLabel,
  probeMeta,
  probeRows,
  progressStatus,
  quarterLabel,
  officeTag,
  roleTone,
  type RoleTag,
  shortDate,
  skillsSummary,
  utcDate,
  warningText,
} from './kurin.labels';
import {
  BadgeCatalogItemDto,
  BadgeProgressDto,
  CardFields,
  GroupDto,
  MemberAwardDto,
  MemberDto,
  MemberDuesDto,
  MembershipDto,
} from './kurin.models';
import { KurinApi, ProtectedImages } from './kurin.service';
import { MemberAvatar } from './member-avatar';
import { DEFAULT_CARD_FIELDS, cardExtraRows } from './member-edit';
import { RoleTags } from './role-tags';
import { ProfileEditResult, ProfileEditSheet } from './profile-edit.sheet';
import { SkillsSheet } from './skills.sheet';
import { inKurin, kurinLabel } from '../../core/kurin-label';

interface Skills {
  catalog: BadgeCatalogItemDto[];
  /** What the kurin's край offers for a new start; the whole catalogue stays for what the member has. */
  offered: BadgeCatalogItemDto[];
  progress: BadgeProgressDto[];
}

const STANDING: Record<string, string | null> = { Current: null, Moved: 'переведений', Left: 'вибув' };

/**
 * A member card (web member-card): profile and contacts, skills, probes and awards (youth only),
 * offices in the КВ, their вкладка when the server shows it to this viewer, and memberships.
 * Changing the card is offered where the server's check-access allows it: one's own fields,
 * skills and awards; reviewers confirm skills and awards. The Звʼязковий changes the гурток of a
 * membership in their kurin and closes it («Вивести з куреня»). Also «Профіль» in «Ще» (`own`).
 */
@Component({
  selector: 'app-member',
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
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonNote,
    IonIcon,
    IonBadge,
    IonProgressBar,
    IonSkeletonText,
    RouterLink,
    MemberAvatar,
  ],
  host: { '[class.lk-embedded]': 'own()' },
  styles: `
    app-role-tags.inline {
      margin-top: 0;
    }
    /* Inside «Профіль» the profile page is the Ionic page; the card lays out as its content. */
    :host(.lk-embedded) {
      display: contents;
    }
    .head {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 20px 16px 4px;
      text-align: center;
    }
    .head h1 {
      margin: 12px 0 2px;
      font-size: 24px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .head p {
      margin: 2px 0 0;
      color: var(--lk-muted);
    }
    .head ion-badge {
      margin-top: 6px;
    }
    .verified {
      color: var(--lk-primary);
      font-size: 20px;
      vertical-align: -3px;
    }
    .stale {
      color: var(--lk-faint);
    }
    .warning {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      margin-top: 8px;
      color: var(--lk-danger);
      font-size: 14px;
    }
    .dots {
      display: inline-flex;
      gap: 3px;
    }
    .dots span {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--lk-line);
    }
    .dots span.on {
      background: var(--lk-danger);
    }
    .thumb {
      width: 36px;
      height: 36px;
      object-fit: contain;
      margin-inline-end: 12px;
    }
    ion-icon.thumb {
      border-radius: 50%;
      box-sizing: border-box;
      padding: 8px;
      background: var(--lk-primary-50);
      color: var(--lk-primary);
    }
    ion-icon.thumb.locked {
      background: var(--lk-surface);
      color: var(--lk-faint);
    }
    ion-progress-bar {
      margin: 8px 0 2px;
      height: 6px;
      border-radius: 3px;
    }
    .balance {
      font-size: 26px;
      font-weight: 700;
    }
    .ok {
      color: var(--lk-primary);
    }
    .debt {
      color: var(--lk-danger);
    }
    .empty {
      padding: 0 20px;
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start">
          @if (own()) {
            <ion-back-button defaultHref="/tabs/more" text="Меню" />
          } @else {
            <ion-back-button defaultHref="/tabs/kurin" text="Назад" />
          }
        </ion-buttons>
        <ion-title>{{ own() ? 'Профіль' : 'Учасник' }}</ion-title>
        @if (canEditProfile()) {
          <ion-buttons slot="end">
            <ion-button (click)="editProfile()" data-testid="edit-profile">Редагувати</ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @switch (member().state) {
        @case ('loading') {
          <div class="head">
            <ion-skeleton-text [animated]="true" style="width: 96px; height: 96px; border-radius: 50%" />
            <ion-skeleton-text [animated]="true" style="width: 180px; height: 24px; margin-top: 12px" />
          </div>
          <ion-list [inset]="true">
            <ion-item-group>
              <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
              <ion-item><ion-skeleton-text [animated]="true" style="height: 40px" /></ion-item>
            </ion-item-group>
          </ion-list>
        }
        @case ('failed') {
          <ion-list [inset]="true">
            <ion-item-group>
              <ion-item><ion-label class="ion-text-wrap">{{ noCard() || failedText }}</ion-label></ion-item>
            </ion-item-group>
          </ion-list>
        }
        @default {
          @if (memberValue(); as m) {
            <div class="head" data-testid="member-head">
              <app-member-avatar [size]="96" [photo]="m.profilePhotoUrl" [firstName]="m.firstName" [lastName]="m.lastName" />
              <h1>
                {{ m.firstName }} {{ m.lastName }}
                @if (verificationEnabled() && m.profileVerificationStatus === 'VerifiedCurrent') {
                  <ion-icon class="verified" name="checkmark-circle" aria-label="Дані верифіковано" />
                } @else if (verificationEnabled() && m.profileVerificationStatus === 'VerifiedStale') {
                  <ion-icon class="verified stale" name="alert-circle" aria-label="Дані змінено після верифікації" />
                }
              </h1>
              @if (m.middleName) { <p>{{ m.middleName }}</p> }
              <ion-badge class="lk-tag--info">{{ level() }}</ion-badge>
              @if (warning(); as w) {
                <span class="warning" data-testid="warning">
                  <span class="dots" aria-hidden="true">
                    @for (n of [1, 2, 3]; track n) { <span [class.on]="n <= w.level"></span> }
                  </span>
                  {{ warningText(w) }}
                </span>
              }
            </div>

            <ion-list [inset]="true" data-testid="contacts">
              <ion-item-group>
                <ion-item [href]="emailHref(m.email)" [detail]="false">
                  <ion-label><h3>Email</h3><p>{{ m.email || 'Не вказано' }}</p></ion-label>
                </ion-item>
                <ion-item [href]="phoneHref(m.phoneNumber)" [detail]="false">
                  <ion-label><h3>Телефон</h3><p>{{ formatPhone(m.phoneNumber) || 'Не вказано' }}</p></ion-label>
                </ion-item>
                <ion-item>
                  <ion-label><h3>Дата народження</h3><p>{{ birthDate() }}</p></ion-label>
                </ion-item>
                @for (row of privateRows(); track row.key) {
                  <ion-item [attr.data-testid]="'card-' + row.key">
                    <ion-label class="ion-text-wrap"><h3>{{ row.label }}</h3><p>{{ row.value }}</p></ion-label>
                  </ion-item>
                }
                @for (row of extraRows(); track row.key) {
                  <ion-item [attr.data-testid]="'card-extra-' + row.key">
                    <ion-label class="ion-text-wrap"><h3>{{ row.label }}</h3><p>{{ row.value }}</p></ion-label>
                  </ion-item>
                }
                @if (verificationEnabled() && m.profileVerifiedAtUtc) {
                  <ion-item><ion-label><h3>Верифіковано</h3><p>{{ when(m.profileVerifiedAtUtc) }}</p></ion-label></ion-item>
                }
              </ion-item-group>
            </ion-list>

            @if (youth()) {
              <ion-list [inset]="true" data-testid="skills">
                <ion-list-header><ion-label>Здобуті вмілості</ion-label></ion-list-header>
                <ion-item-group>
                  @switch (skills().state) {
                    @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 36px" /></ion-item> }
                    @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
                    @default {
                      @for (skill of skillPreview(); track skill.badgeId) {
                        <ion-item>
                          @if (badgeImage(skill); as src) { <img class="thumb" slot="start" [src]="src" alt="" /> }
                          @else { <ion-icon class="thumb" slot="start" name="ribbon" aria-hidden="true" /> }
                          <ion-label class="ion-text-wrap">{{ skill.title }}</ion-label>
                          @if (skill.status === 'Submitted') { <ion-note slot="end">очікує</ion-note> }
                        </ion-item>
                      } @empty {
                        <ion-item><ion-label class="ion-text-wrap">Вмілості ще не додані.</ion-label></ion-item>
                      }
                      <ion-item [button]="true" [detail]="true" (click)="openSkills()" data-testid="all-skills">
                        <ion-label>Усі вмілості</ion-label>
                        <ion-note slot="end">{{ skillCounts() }}</ion-note>
                      </ion-item>
                      @if (canModerate()) {
                        <ion-item [button]="true" [detail]="true" routerLink="/tabs/kurin/review/skills">
                          <ion-label>До модерації</ion-label>
                        </ion-item>
                      }
                    }
                  }
                </ion-item-group>
              </ion-list>

              <ion-list [inset]="true" data-testid="probes">
                <ion-list-header><ion-label>Проба</ion-label></ion-list-header>
                <ion-item-group>
                  @switch (probes().state) {
                    @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 48px" /></ion-item> }
                    @case ('failed') { <ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item> }
                    @default {
                      @for (row of probeList(); track row.probeId) {
                        @if (row.canOpen && !row.isDisabled) {
                          <ion-item
                            [button]="true"
                            [detail]="true"
                            [routerLink]="['/tabs/kurin/member', key(), 'probe']"
                            [queryParams]="{ id: row.probeId }"
                            data-testid="probe"
                          >
                            <ion-label class="ion-text-wrap">
                              <h3>{{ row.label }}</h3>
                              <p>{{ probeMeta(row) }}</p>
                              @if (row.percent !== null) {
                                <ion-progress-bar [value]="row.percent / 100" />
                                <p>Підписано {{ row.signedPoints }} з {{ row.pointsCount }} · {{ row.percent }}%</p>
                              }
                            </ion-label>
                          </ion-item>
                        } @else {
                          <ion-item>
                            <ion-icon class="thumb locked" slot="start" name="lock-closed" aria-hidden="true" />
                            <ion-label class="ion-text-wrap">
                              <h3>{{ row.label }}</h3>
                              <p>{{ probeMeta(row) }}</p>
                            </ion-label>
                          </ion-item>
                        }
                      }
                    }
                  }
                </ion-item-group>
              </ion-list>

              <ion-list [inset]="true" data-testid="awards">
                <ion-list-header><ion-label>Відзначення УПЮ</ion-label></ion-list-header>
                <ion-item-group>
                  @for (group of awards(); track group.level) {
                    <ion-item [button]="canEdit()" [detail]="canEdit()" (click)="openAward(group.latest)" data-testid="award">
                      @if (awardImage(group.latest); as src) { <img class="thumb" slot="start" [src]="src" alt="" /> }
                      @else { <ion-icon class="thumb" slot="start" name="medal" aria-hidden="true" /> }
                      <ion-label class="ion-text-wrap">
                        <h3>{{ awardTitle(group.level) }}{{ group.count > 1 ? ' ×' + group.count : '' }}</h3>
                        <p>{{ shortDate(dateOnly(group.latest.dateAcquired)) }}</p>
                      </ion-label>
                      @if (isPending(group.latest)) { <ion-note slot="end">очікує</ion-note> }
                    </ion-item>
                  } @empty {
                    <ion-item><ion-label>Немає здобутих відзначень.</ion-label></ion-item>
                  }
                  @if (canEdit()) {
                    <ion-item [button]="true" [detail]="false" (click)="openAward(null)" data-testid="add-award">
                      <ion-icon slot="start" name="add" color="primary" aria-hidden="true" />
                      <ion-label color="primary">Додати відзначення</ion-label>
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
            }

            @if (staffOffices().length || mentoredGroups().length) {
              <ion-list [inset]="true" data-testid="staff">
                <ion-list-header><ion-label>Впорядництво</ion-label></ion-list-header>
                <ion-item-group>
                  @for (office of staffOffices(); track office.leadershipHistoryKey) {
                    <ion-item>
                      <ion-label><app-role-tags class="inline" [tags]="[officeTag(office)]" /></ion-label>
                      <ion-note slot="end">з {{ office.startDate.slice(0, 4) }}</ion-note>
                    </ion-item>
                  }
                  @for (group of mentoredGroups(); track group.name) {
                    @if (group.groupKey) {
                      <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', group.groupKey]">
                        <ion-label class="ion-text-wrap"><h3>{{ group.name }}</h3><app-role-tags [tags]="mentorTag" /></ion-label>
                      </ion-item>
                    } @else {
                      <ion-item>
                        <ion-label class="ion-text-wrap"><h3>{{ group.name }}</h3><app-role-tags [tags]="mentorTag" /></ion-label>
                      </ion-item>
                    }
                  }
                </ion-item-group>
              </ion-list>
            }

            @if (dues(); as d) {
              <ion-list [inset]="true" data-testid="dues">
                <ion-list-header><ion-label>Вкладка</ion-label></ion-list-header>
                <ion-item-group>
                  <ion-item>
                    <ion-label class="ion-text-wrap">
                      <span class="balance" [class.ok]="d.balance >= 0" [class.debt]="d.balance < 0">{{ balanceTitle(d) }}</span>
                      <p>{{ balanceHint(d) }}</p>
                      <p>{{ duesLine(d) }}</p>
                    </ion-label>
                    @if (d.isConcessionNow) { <ion-badge slot="end" class="lk-tag--info">пільгова</ion-badge> }
                  </ion-item>
                  @if (d.accounts.length > 1) {
                    @for (account of d.accounts; track account.groupKey) {
                      <ion-item>
                        <ion-label>{{ account.groupName }}{{ standing(account.standing) }}</ion-label>
                        <ion-note slot="end" [class.debt]="account.balance < 0">{{ account.balance === 0 ? 'сплачено' : money(account.balance) }}</ion-note>
                      </ion-item>
                    }
                  }
                  @for (entry of d.entries.slice(0, 3); track entry.duesEntryKey) {
                    <ion-item>
                      <ion-label><h3>{{ kindLabel(entry.kind) }}</h3><p>{{ shortDate(dateOnly(entry.occurredOn)) }}</p></ion-label>
                      <ion-note slot="end">{{ amountLabel(entry) }}</ion-note>
                    </ion-item>
                  } @empty {
                    <ion-item><ion-label>Внесків ще не записано.</ion-label></ion-item>
                  }
                  @if (d.canOpenGroupDues && d.currentGroupKey) {
                    <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', d.currentGroupKey, 'dues']">
                      <ion-label>Каса гуртка</ion-label>
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
            }

            <ion-list [inset]="true" data-testid="memberships">
              <ion-list-header><ion-label>Членства</ion-label></ion-list-header>
              <ion-item-group>
                @switch (memberships().state) {
                  @case ('loading') { <ion-item><ion-skeleton-text [animated]="true" style="height: 36px" /></ion-item> }
                  @case ('failed') { <ion-item><ion-label class="ion-text-wrap">Не вдалося прочитати членства.</ion-label></ion-item> }
                  @default {
                    @for (item of currentMemberships(); track item.membershipKey) {
                      @if (item.groupKey && item.kurinKey === scopedKurin) {
                        <ion-item [button]="true" [detail]="true" [routerLink]="['/tabs/kurin/group', item.groupKey]">
                          <ion-label class="ion-text-wrap"><h3>{{ kurinTitle(item) }}</h3><p>{{ membershipLine(item) }}</p></ion-label>
                        </ion-item>
                      } @else {
                        <ion-item>
                          <ion-label class="ion-text-wrap"><h3>{{ kurinTitle(item) }}</h3><p>{{ membershipLine(item) }}</p></ion-label>
                        </ion-item>
                      }
                      @if (canPlace(item)) {
                        <ion-item [button]="true" [detail]="false" [disabled]="placing()" (click)="chooseGroup(item)" data-testid="membership-group">
                          <ion-icon slot="start" name="git-network" color="primary" aria-hidden="true" />
                          <ion-label color="primary">Змінити гурток</ion-label>
                        </ion-item>
                        <ion-item [button]="true" [detail]="false" [disabled]="placing()" (click)="confirmLeave(item)" data-testid="membership-leave">
                          <ion-icon slot="start" name="log-out" color="danger" aria-hidden="true" />
                          <ion-label color="danger">Вивести з куреня</ion-label>
                        </ion-item>
                      }
                    } @empty {
                      <ion-item><ion-label class="ion-text-wrap">Поки що не належить до жодного куреня.</ion-label></ion-item>
                    }
                  }
                }
              </ion-item-group>
            </ion-list>
            @if (pastMemberships().length) {
              <ion-list [inset]="true" data-testid="member-memberships-past">
                <ion-list-header><ion-label>Раніше</ion-label></ion-list-header>
                <ion-item-group>
                  @for (item of pastMemberships(); track item.membershipKey) {
                    <ion-item>
                      <ion-label class="ion-text-wrap"><h3>{{ kurinTitle(item) }}</h3><p>{{ branchLabel(item) }} · {{ period(item) }}</p></ion-label>
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
            }
          }
        }
      }
    </ion-content>
  `,
})
export class MemberPage implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly images = inject(ProtectedImages);
  private readonly auth = inject(AuthService);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly nav = inject(NavController);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  /** The person's own card inside «Профіль»: no :memberKey, back to «Ще». */
  readonly own = input(false);

  protected readonly failedText = FAILED_TEXT;
  protected readonly scopedKurin = this.auth.user()?.kurinKey ?? null;
  protected readonly officeTag = officeTag;
  protected readonly mentorTag: RoleTag[] = [{ label: 'Впорядник гуртка', tone: roleTone('Vykhovnyk') }];
  protected readonly awardTitle = awardTitle;
  protected readonly probeMeta = probeMeta;
  protected readonly warningText = warningText;
  protected readonly shortDate = shortDate;
  protected readonly dateOnly = dateOnly;
  protected readonly emailHref = emailHref;
  protected readonly phoneHref = phoneHref;
  protected readonly formatPhone = formatPhone;
  protected readonly money = (amount: number): string => duesMoney(amount, this.dues()?.currency, this.dues()?.numberLocale);
  protected readonly period = membershipPeriod;

  protected readonly key = signal<string | null>(null);
  protected readonly noCard = signal<string | null>(null);
  protected readonly member = signal<Loaded<MemberDto>>({ state: 'loading' });
  protected readonly memberships = signal<Loaded<MembershipDto[]>>({ state: 'loading' });
  protected readonly skills = signal<Loaded<Skills>>({ state: 'loading' });
  protected readonly probes = signal<Loaded<ProbeRow[]>>({ state: 'loading' });
  protected readonly dues = signal<MemberDuesDto | null>(null);
  protected readonly canUpdate = signal(false);
  protected readonly verificationEnabled = signal(false);
  /** What the kurin's край asks on the card; null until read (the edit sheet then reads it itself). */
  private readonly cardFields = signal<CardFields | null>(null);
  protected readonly placing = signal(false);

  protected readonly memberValue = computed(() => valueOf(this.member()));
  private readonly membershipList = computed(() => valueOf(this.memberships()) ?? []);
  protected readonly isOwnCard = computed(() => !!this.key() && this.key() === this.auth.user()?.memberKey);
  /** «Редагувати» (web upsert-member): on any card the server lets this user change (own, or as провід/адмін). */
  protected readonly canEditProfile = computed(() => this.canUpdate() && !!this.memberValue());
  /** Skills and awards: whoever the server lets change the card (web canEditMember). */
  protected readonly canEdit = computed(() => this.canUpdate());
  /** Confirming skills and awards: a reviewer who may change this card (web canInlineModerateSkills). */
  protected readonly canModerate = computed(() => kurinAccess(this.auth.user()).reviewSkills && this.canUpdate());
  /** Probes, skills and awards are the youth's; known once the card and memberships are read. */
  protected readonly youth = computed(() => {
    const m = this.memberValue();
    return !!m && this.memberships().state !== 'loading' && hasYouthProgram(memberBranch(m, this.membershipList()));
  });
  protected readonly level = computed(() => {
    const m = this.memberValue();
    return (m && plastLevelLabel(m)) || 'Ступінь не вказано';
  });
  protected readonly warning = computed(() => activeWarning(this.memberValue()?.warnings));
  /** Address and school when known and the край keeps them on the card. */
  protected readonly privateRows = computed(() => {
    const card = this.cardFields() ?? DEFAULT_CARD_FIELDS;
    const m = this.memberValue();
    const rows = ([['address', 'Адреса'], ['school', 'Школа']] as const)
      .filter(([key]) => card.fields[key] !== 'hidden' && !!m?.[key])
      .map(([key, label]) => ({ key: key as string, label, value: m![key]! }));
    if (!m || card.fields.parents === 'hidden') return rows;
    const parents = [
      { key: 'mother', label: 'Мама', first: m.motherFirstName, last: m.motherLastName, phone: m.motherPhone },
      { key: 'father', label: 'Тато', first: m.fatherFirstName, last: m.fatherLastName, phone: m.fatherPhone },
    ]
      .map((p) => ({ key: p.key, label: p.label, value: [[p.first, p.last].filter(Boolean).join(' '), formatPhone(p.phone)].filter(Boolean).join(', ') }))
      .filter((p) => !!p.value);
    return [...rows, ...parents];
  });
  /** The край's own fields that have a value on this card. */
  protected readonly extraRows = computed(() => cardExtraRows(this.cardFields() ?? DEFAULT_CARD_FIELDS, this.memberValue()?.cardExtras));
  protected readonly birthDate = computed(() => {
    const born = dateOnly(this.memberValue()?.dateOfBirth);
    return born ? dateLabel(born) : 'Не вказано';
  });
  private readonly skillViews = computed(() => {
    const s = valueOf(this.skills());
    return s ? skillsSummary(s.progress, s.catalog) : { confirmed: [], pending: [] };
  });
  protected readonly skillPreview = computed(() => [...this.skillViews().confirmed.slice(0, 3), ...this.skillViews().pending].slice(0, 4));
  protected readonly skillCounts = computed(() => `${this.skillViews().confirmed.length} · очікують ${this.skillViews().pending.length}`);
  protected readonly probeList = computed(() => valueOf(this.probes()) ?? []);
  protected readonly awards = computed<AwardGroup[]>(() => awardGroups(this.memberValue()?.awards));
  protected readonly staffOffices = computed(() =>
    currentOffices(this.memberValue()?.leadershipHistories).filter((h) => (h.leadershipType ?? '').toLowerCase() === 'kv'),
  );
  /** A гурток opens only from the kurin the viewer is in (another kurin's page would refuse them). */
  protected readonly mentoredGroups = computed(() => {
    const m = this.memberValue();
    const linkable = !!m?.kurinKey && m.kurinKey === this.scopedKurin;
    if (m?.mentoredGroups?.length) return m.mentoredGroups.map((g) => ({ groupKey: linkable ? g.groupKey : null, name: g.name }));
    return (m?.mentoredGroupNames ?? []).map((name) => ({ groupKey: null as string | null, name }));
  });
  /** Placing people (a гурток, leaving the kurin) is the Звʼязковий's (web canManagePlacement). */
  private readonly canManagePlacement = computed(() => kurinAccess(this.auth.user()).manageWholeKurin);
  protected readonly currentMemberships = computed(() => this.membershipList().filter((m) => m.isCurrent));
  protected readonly pastMemberships = computed(() => this.membershipList().filter((m) => !m.isCurrent));

  constructor() {
    addIcons({ add, alertCircle, checkmarkCircle, gitNetwork, lockClosed, logOut, medal, ribbon });
  }

  ngOnInit(): void {
    const key = this.own() ? (this.auth.user()?.memberKey ?? null) : this.route.snapshot.paramMap.get('memberKey');
    this.key.set(key);
    if (!key) {
      this.noCard.set('Цей акаунт не привʼязаний до картки учасника.');
      this.member.set({ state: 'failed' });
      return;
    }
    void this.load(key);
  }

  protected async refresh(event: Event): Promise<void> {
    const key = this.key();
    if (key) await this.load(key);
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected when(value: string): string {
    return shortDate(utcDate(value));
  }

  protected badgeImage(skill: SkillView): string | null {
    return this.images.badge(skill.imagePath);
  }

  protected awardImage(award: MemberAwardDto): string | null {
    return this.images.award(award, awardWeight(award.level), progressStatus(award.status) === 'Confirmed');
  }

  protected isPending(award: MemberAwardDto): boolean {
    return progressStatus(award.status) === 'Submitted';
  }

  protected balanceTitle(d: MemberDuesDto): string {
    if (d.balance === 0) return 'Сплачено';
    return d.balance < 0 ? this.money(d.balance) : `+${this.money(d.balance)}`;
  }

  protected balanceHint(d: MemberDuesDto): string {
    if (d.balance === 0) return 'боргу немає';
    return d.balance < 0 ? 'борг — віддай скарбникові гуртка' : 'надлишок — піде на наступний квартал';
  }

  protected duesLine(d: MemberDuesDto): string {
    return [quarterLabel(d.currentQuarter), d.quarterRate ? `${this.money(d.quarterRate.total)} за квартал` : null, d.currentGroupName]
      .filter(Boolean)
      .join(' · ');
  }

  protected standing(value: string): string {
    const label = STANDING[value];
    return label ? ` · ${label}` : '';
  }

  protected kindLabel(kind: string): string {
    return DUES_KIND_LABELS[kind] ?? kind;
  }

  protected amountLabel(entry: { kind: string; amount: number }): string {
    return duesAmountLabel(entry, this.dues()?.currency, this.dues()?.numberLocale);
  }

  protected kurinTitle(m: MembershipDto): string {
    return m.kurinNamedAfter ? `${kurinLabel(m.kurinNumber)} · ім. ${m.kurinNamedAfter}` : kurinLabel(m.kurinNumber);
  }

  protected branchLabel(m: MembershipDto): string {
    return BRANCH_LABELS[m.branch] ?? m.branch;
  }

  protected membershipLine(m: MembershipDto): string {
    return [this.branchLabel(m), MEMBERSHIP_KIND_LABELS[m.kind] ?? m.kind, m.groupName, membershipPeriod(m)].filter(Boolean).join(' · ');
  }

  /**
   * «Гурток» and «Вивести» (web member-memberships-tile canAct): a current membership in the kurin
   * the viewer acts in; another kurin's memberships are its own провід's.
   */
  protected canPlace(m: MembershipDto): boolean {
    return this.canManagePlacement() && m.isCurrent && m.kurinKey === this.scopedKurin;
  }

  /** Another гурток of the same kurin, or none (web «Гурток» dialog with «Поза гуртком»). */
  protected async chooseGroup(m: MembershipDto): Promise<void> {
    const key = this.key();
    if (!key || this.placing()) return;
    let groups: GroupDto[];
    try {
      groups = await this.data.groups(m.kurinKey);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося прочитати гуртки.'), 'danger');
      return;
    }
    const options = groups.filter((g) => g.groupKey !== m.groupKey);
    if (!options.length && !m.groupKey) {
      await this.toasts.show('У курені ще немає гуртків.');
      return;
    }
    const sheet = await this.sheets.create({
      header: m.groupName ? `Зараз: ${m.groupName}` : 'Зараз поза гуртком',
      subHeader: 'Курінь лишається той самий — міняється лише гурток усередині нього.',
      buttons: [
        ...options.map((g) => ({ text: g.name, data: g.groupKey })),
        ...(m.groupKey ? [{ text: 'Поза гуртком', data: '' }] : []),
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss<string>();
    if (typeof data !== 'string') return;
    const target = options.find((g) => g.groupKey === data) ?? null;
    if (data && !target) return;
    this.placing.set(true);
    try {
      await this.data.moveToGroup(m.kurinKey, key, target?.groupKey ?? null);
      await this.toasts.show(target ? `Тепер у гуртку «${target.name}».` : 'Тепер поза гуртком.');
      await Promise.all([settle(this.data.member(key), this.member), settle(this.data.memberships(key), this.memberships)]);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося перевести. Спробуй ще раз.'), 'danger');
    } finally {
      this.placing.set(false);
    }
  }

  /**
   * «Вивести з куреня», confirmed as on the web. Not a deletion, and the sheet says so: the fear of
   * losing the history is what kept people who had long left in their kurin.
   */
  protected async confirmLeave(m: MembershipDto): Promise<void> {
    const key = this.key();
    if (!key || this.placing()) return;
    const sheet = await this.sheets.create({
      header: 'Вивести з куреня',
      subHeader:
        `Членство в ${inKurin(m.kurinNumber)} буде закрите. Людина, її проби, вмілості й відзначення ` +
        'лишаються — вони належать їй, а не куреню.',
      buttons: [
        { text: 'Вивести', role: 'destructive', data: 'leave' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data !== 'leave') return;
    this.placing.set(true);
    try {
      await this.data.leaveKurin(m.kurinKey, key);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося вивести. Спробуй ще раз.'), 'danger');
      this.placing.set(false);
      return;
    }
    await this.toasts.show(`Членство в ${inKurin(m.kurinNumber)} закрито.`);
    try {
      // The card as it is now: the membership moves to «Раніше».
      const [member, memberships] = await Promise.all([this.data.member(key), this.data.memberships(key)]);
      this.member.set({ state: 'ready', value: member });
      this.memberships.set({ state: 'ready', value: memberships });
    } catch {
      // The kurin no longer shows this person to its провід: back to its list.
      await this.nav.navigateBack('/tabs/kurin');
    } finally {
      this.placing.set(false);
    }
  }

  protected async openSkills(): Promise<void> {
    const key = this.key();
    const s = valueOf(this.skills());
    if (!key || !s) return;
    const modal = await this.modals.create({
      component: SkillsSheet,
      componentProps: { memberKey: key, canEdit: this.canEdit(), canReview: this.canModerate(), catalog: s.catalog, offered: s.offered, progress: s.progress },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<boolean>();
    if (data) await this.loadSkills(key);
  }

  protected async openAward(award: MemberAwardDto | null): Promise<void> {
    const key = this.key();
    if (!key || !this.canEdit()) return;
    const modal = await this.modals.create({
      component: AwardSheet,
      componentProps: { memberKey: key, award, canEdit: this.canEdit(), canReview: this.canModerate() },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<boolean>();
    if (data) await settle(this.data.member(key), this.member);
  }

  protected async editProfile(): Promise<void> {
    const key = this.key();
    const member = this.memberValue();
    if (!key || !member) return;
    const modal = await this.modals.create({
      component: ProfileEditSheet,
      componentProps: { member, own: this.isOwnCard(), verificationEnabled: this.verificationEnabled(), cardFields: this.cardFields() },
      presentingElement: this.presenting(),
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<ProfileEditResult>();
    if (data === 'deleted') await this.nav.navigateBack('/tabs/kurin');
    else if (data) await settle(this.data.member(key), this.member);
  }

  /** The page under an iOS card sheet, so it recedes behind it. */
  private presenting(): HTMLElement | undefined {
    return this.host.closest<HTMLElement>('.ion-page') ?? undefined;
  }

  private async load(key: string): Promise<void> {
    const member = settle(this.data.member(key), this.member);
    const memberships = settle(this.data.memberships(key), this.memberships);
    await Promise.all([
      member,
      memberships,
      this.data.canUpdateMember(key).then(
        (allowed) => this.canUpdate.set(allowed),
        () => this.canUpdate.set(false),
      ),
      // A youth may read only their own вкладка: for anyone else the server refuses and the tile stays away.
      this.data.dues(key).then(
        (dues) => this.dues.set(dues?.hasAccount ? dues : null),
        () => this.dues.set(null),
      ),
      member.then(() => Promise.all([this.loadVerificationToggle(), this.loadCardFields()])),
      // Skills and probes wait for both: the branch comes from the ступінь and the kurin.
      Promise.all([member, memberships]).then(() => (this.youth() ? Promise.all([this.loadSkills(key), this.loadProbes(key)]) : null)),
    ]);
  }

  private async loadVerificationToggle(): Promise<void> {
    const kurinKey = this.memberValue()?.kurinKey;
    if (!kurinKey) return;
    try {
      this.verificationEnabled.set((await this.data.kurin(kurinKey)).profileVerificationEnabled ?? false);
    } catch {
      this.verificationEnabled.set(false);
    }
  }

  private async loadCardFields(): Promise<void> {
    const kurinKey = this.memberValue()?.kurinKey;
    if (!kurinKey) return;
    try {
      this.cardFields.set(await this.data.cardFields(kurinKey));
    } catch {
      this.cardFields.set(null);
    }
  }

  private loadSkills(key: string): Promise<void> {
    const kurinKey = this.memberValue()?.kurinKey;
    return settle(
      Promise.all([
        this.data.badgeCatalog().catch(() => [] as BadgeCatalogItemDto[]),
        kurinKey ? this.data.kurinBadgeCatalog(kurinKey).catch(() => [] as BadgeCatalogItemDto[]) : Promise.resolve([] as BadgeCatalogItemDto[]),
        this.data.badgeProgress(key),
      ]).then(([catalog, offered, progress]) => ({ catalog, offered, progress })),
      this.skills,
    );
  }

  private loadProbes(key: string): Promise<void> {
    return settle(
      this.data.probes().then(async (probes) => {
        const progresses = await Promise.all(probes.map((p) => this.data.probeProgress(key, p.id).catch(() => null)));
        return probeRows(probes, progresses.filter((p) => p !== null));
      }),
      this.probes,
    );
  }
}
