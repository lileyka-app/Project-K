import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import {
  ActionSheetController,
  IonButton,
  IonButtons,
  IonCheckbox,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { CountryCode } from 'libphonenumber-js';
import { AuthService } from '../../auth/auth.service';
import { apiErrorText } from '../../core/api';
import { DEFAULT_PHONE_COUNTRY, formatPhone, isPossiblePhone, toStoredPhone } from '../../core/phone';
import { Toasts } from '../../core/toast';
import { IMAGE_TOO_LARGE } from './image-crop';
import { CroppedImage, ImageCropSheet } from './image-crop.sheet';
import { kurinAccess, shortDate, toDateOnly, utcDate } from './kurin.labels';
import { CardFields, MemberDataGroup, MemberDto, ProfileVerificationStatus, WarningLevel } from './kurin.models';
import { KurinApi } from './kurin.service';
import { MemberAvatar } from './member-avatar';
import {
  CardExtras,
  DEFAULT_CARD_FIELDS,
  MIN_AGE,
  MemberForm,
  NO_WARNING_CHANGES,
  NewMemberPlacement,
  RANK_BRANCHES,
  RANK_FIELDS,
  RankBranch,
  RankDates,
  WARNING_LEVELS,
  WarningDraft,
  canClearWarning,
  canEditEmail,
  cardExtraValid,
  cardExtrasDraft,
  cardFieldError,
  changedCardExtras,
  fieldMode,
  hiddenGroupOf,
  canSelectWarning,
  closeBranch,
  isPendingAccount,
  looksLikeEmail,
  memberFormData,
  newMemberFormData,
  oldEnough,
  openBranches,
  rankDates,
  rankHistories,
  toggleWarning,
  warningCalls,
  warningChecked,
  warningInForce,
} from './member-edit';
import { MemberAbilities, NO_ABILITIES, mayCancelWarning, mayGiveWarning } from './abilities';

const SAVE_ERRORS = {
  ContactInfoLinked: 'Телефон цього акаунта змінюється в налаштуваннях акаунта.',
  EmailTaken: 'Ця адреса вже належить іншому акаунту або заявці. Перевір її.',
  ImageTooLarge: IMAGE_TOO_LARGE,
  UnsupportedImageType: 'Підтримуються лише PNG, JPEG або WebP.',
  InvalidImage: 'Це не схоже на зображення. Обери інше фото.',
};

type Field = keyof MemberForm;

interface FormField {
  key: Field;
  label: string;
  type: 'text' | 'tel' | 'date' | 'email';
  autocomplete: string;
}

/** An input of «Основне»: a field of the form, or one the край adds (`extra`). */
interface Row extends Omit<FormField, 'key'> {
  key: string;
  extra: boolean;
  testId: string;
  /** Only the birth date stops at today; a край's date may be ahead (a visa's end). */
  max?: string;
}

const FIELDS: FormField[] = [
  { key: 'firstName', label: 'Імʼя', type: 'text', autocomplete: 'given-name' },
  { key: 'middleName', label: 'По батькові', type: 'text', autocomplete: 'additional-name' },
  { key: 'lastName', label: 'Прізвище', type: 'text', autocomplete: 'family-name' },
  { key: 'email', label: 'Email', type: 'email', autocomplete: 'email' },
  { key: 'phoneNumber', label: 'Номер телефону', type: 'tel', autocomplete: 'tel' },
  { key: 'dateOfBirth', label: 'Дата народження', type: 'date', autocomplete: 'bday' },
  { key: 'address', label: 'Адреса', type: 'text', autocomplete: 'street-address' },
  { key: 'school', label: 'Школа', type: 'text', autocomplete: 'off' },
  { key: 'motherFirstName', label: 'Імʼя мами', type: 'text', autocomplete: 'off' },
  { key: 'motherLastName', label: 'Прізвище мами', type: 'text', autocomplete: 'off' },
  { key: 'motherPhone', label: 'Телефон мами', type: 'tel', autocomplete: 'off' },
  { key: 'fatherFirstName', label: 'Імʼя тата', type: 'text', autocomplete: 'off' },
  { key: 'fatherLastName', label: 'Прізвище тата', type: 'text', autocomplete: 'off' },
  { key: 'fatherPhone', label: 'Телефон тата', type: 'tel', autocomplete: 'off' },
];

const PARENT_PHONES = ['motherPhone', 'fatherPhone'] as const;

/** A field the край requires comes back in the server's own words, which name it. */
function saveErrorText(error: unknown, fallback: string): string {
  return cardFieldError(error) ?? apiErrorText(error, fallback, SAVE_ERRORS);
}

function resendErrorText(error: unknown): string {
  const status = error instanceof HttpErrorResponse ? error.status : 0;
  if (status === 409) return 'Акаунт уже активовано — запрошення більше не потрібне. Онови сторінку.';
  if (status === 500) return 'Поштовий сервіс не прийняв лист. Спробуй за хвилину; попереднє посилання ще діє.';
  return apiErrorText(error, 'Лист не надіслано. Спробуй ще раз.');
}

/**
 * What the card does once the sheet is gone: nothing, read itself again, or leave (the profile is
 * deleted). A new member comes back as their key, for the caller to open their card.
 */
export type ProfileEditResult = 'saved' | 'changed' | 'deleted' | { created: string } | null;

/**
 * The member edit (web upsert-member): photo with a square crop, name, phone and birth date on any
 * card the server lets this person change. The rest is shown as the web shows it: the email for a
 * record without a claimed account (or to an admin), the ступені to the провід who moderate
 * (Виховник, Звʼязковий, admin) and to the member on their own card, перестороги to that провід, the
 * profile check to such a reviewer on somebody else's card when the kurin uses it, and «Видалити
 * профіль» to the Звʼязковий and admins.
 * The middle name and phone are asked, left optional or left out as the kurin's край says, and
 * its own fields (`cardExtras`) follow them.
 *
 * Without a `member` and with a `placement` it is the web's «Новий учасник»: the same form with the
 * email required, as the address the account's invitation goes to, and the ступені for the провід
 * who start the record.
 */
@Component({
  selector: 'app-profile-edit-sheet',
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
    IonInput,
    IonToggle,
    IonCheckbox,
    IonSpinner,
    MemberAvatar,
  ],
  styles: `
    ion-item > div {
      width: 100%;
      padding: 10px 0;
    }
    .photo {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 16px 16px 0;
    }
    .photo-actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
    }
    .photo input {
      display: none;
    }
    .hint {
      margin: 0 20px;
      font-size: 13px;
      color: var(--lk-muted);
    }
    .hint.error {
      color: var(--lk-danger);
    }
    .hint ion-button {
      margin: 4px 0 0;
    }
    .warning-label {
      display: block;
      white-space: normal;
    }
    .warning-hint {
      display: block;
      margin-top: 2px;
      font-size: 13px;
      white-space: normal;
      color: var(--lk-muted);
    }
    .actions {
      padding: 4px 16px 24px;
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="close(changed ? 'changed' : null)">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ creating ? 'Новий учасник' : own ? 'Мій профіль' : 'Профіль учасника' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button [strong]="true" [disabled]="!valid() || busy()" (click)="save()" data-testid="save-profile">Зберегти</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <div class="photo" data-testid="profile-photo">
        <app-member-avatar [size]="96" [photo]="shownPhoto()" [firstName]="form().firstName" [lastName]="form().lastName" />
        <div class="photo-actions">
          <ion-button fill="clear" size="small" [disabled]="busy()" (click)="pickPhoto()" data-testid="pick-photo">
            {{ shownPhoto() ? 'Змінити фото' : 'Завантажити фото' }}
          </ion-button>
          @if (shownPhoto()) {
            <ion-button fill="clear" size="small" color="danger" [disabled]="busy()" (click)="clearPhoto()" data-testid="clear-photo">Очистити фото</ion-button>
          }
        </div>
        <input #photoInput type="file" accept="image/*" (change)="photoPicked($event)" data-testid="photo-input" />
      </div>

      <ion-list [inset]="true">
        <ion-list-header><ion-label>Основне</ion-label></ion-list-header>
        <ion-item-group>
          @for (row of rows(); track row.testId) {
            <ion-item>
              <div class="lk-field">
                <span class="lk-field__label">{{ row.label }}</span>
                <div class="lk-input-box" [class.lk-invalid]="touched() && !rowValid(row)">
                  <ion-input
                    [type]="row.type"
                    [attr.data-testid]="row.testId"
                    [attr.aria-label]="row.label"
                    [autocomplete]="$any(row.autocomplete)"
                    [max]="row.max"
                    [value]="rowValue(row)"
                    (ionInput)="setRow(row, $any($event).detail.value)"
                  />
                </div>
              </div>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>
      @if (tooYoung()) {
        <p class="hint error" data-testid="too-young">Учасникові має бути щонайменше {{ minAge }} років.</p>
      }
      @if (creating) {
        <p class="hint" data-testid="invite-hint">
          На цю адресу прийде лист із посиланням для активації акаунта. Попроси перевірити теку «Спам», якщо лист не видно.
        </p>
      } @else if (pending()) {
        <p class="hint" data-testid="pending-account">
          @if (emailChanged()) {
            Після збереження лист із новим посиланням піде на цю адресу, а посилання з попереднього листа перестане діяти.
          } @else {
            Акаунт ще не активовано. Якщо в адресі помилка — виправ її й збережи, лист піде заново.
            <br />
            <ion-button fill="clear" size="small" [disabled]="sending()" (click)="resend()" data-testid="resend-invitation">
              @if (sending()) { <ion-spinner name="crescent" /> } @else { Надіслати лист ще раз }
            </ion-button>
          }
        </p>
      } @else if (!showEmail()) {
        <p class="hint">
          {{ own ? 'Email змінюється в налаштуваннях акаунта.' : 'Email належить акаунту учасника: змінює його сам учасник або адмін.' }}
        </p>
      }

      @if (canRanks()) {
        <ion-list [inset]="true" data-testid="profile-ranks">
          <ion-list-header><ion-label>Пластові ступені</ion-label></ion-list-header>
          <ion-item-group>
            @for (branch of branches; track branch.branch) {
              <ion-item>
                <ion-toggle
                  justify="space-between"
                  [checked]="open()[branch.branch]"
                  (ionChange)="toggleBranch(branch.branch, $any($event).detail.checked)"
                  [attr.data-testid]="'rank-branch-' + branch.branch"
                >{{ branch.label }}</ion-toggle>
              </ion-item>
            }
          </ion-item-group>
          <ion-item-group>
            @for (rank of shownRanks(); track rank.level) {
              <ion-item>
                <div class="lk-field">
                  <span class="lk-field__label">{{ rank.label }}</span>
                  <div class="lk-input-box">
                    <ion-input
                      type="date"
                      [attr.aria-label]="rank.label"
                      [attr.data-testid]="'rank-' + rank.level"
                      [max]="today"
                      [value]="dates()[rank.level]"
                      (ionInput)="setRank(rank.level, $any($event).detail.value)"
                    />
                  </div>
                </div>
              </ion-item>
            }
          </ion-item-group>
        </ion-list>
        @if (own && verification().status === 'VerifiedCurrent') {
          <p class="hint" data-testid="ranks-stale-note">Зміна ступенів зніме верифікацію профілю, доки провід не перевірить його знову.</p>
        }
      }

      @if (canVerify()) {
        <ion-list [inset]="true" data-testid="profile-verification">
          <ion-list-header><ion-label>Верифікація профілю</ion-label></ion-list-header>
          <ion-item-group>
            <ion-item>
              <ion-label class="ion-text-wrap">{{ verificationLine() }}</ion-label>
            </ion-item>
            <ion-item [button]="true" [detail]="false" [disabled]="verifying()" (click)="verificationAction()" data-testid="verification-action">
              <ion-label [color]="verification().status === 'VerifiedCurrent' ? 'danger' : 'primary'">
                {{ verification().status === 'VerifiedCurrent' ? 'Зняти верифікацію профілю' : 'Верифікувати профіль' }}
              </ion-label>
              @if (verifying()) { <ion-spinner slot="end" name="crescent" /> }
            </ion-item>
          </ion-item-group>
        </ion-list>
      }

      @if (canWarnings()) {
        <ion-list [inset]="true" data-testid="profile-warnings">
          <ion-list-header><ion-label>Перестороги</ion-label></ion-list-header>
          <ion-item-group>
            @for (warning of warningLevels; track warning.level) {
              <ion-item>
                <ion-checkbox
                  justify="space-between"
                  [checked]="isChecked(warning.level)"
                  [disabled]="busy() || !canToggle(warning.level)"
                  (ionChange)="toggle(warning.level, $any($event).detail.checked)"
                  [attr.data-testid]="'warning-' + warning.level"
                >
                  <span class="warning-label">{{ warning.label }}</span>
                  @if (warningHint(warning.level); as hint) { <span class="warning-hint">{{ hint }}</span> }
                </ion-checkbox>
              </ion-item>
            }
          </ion-item-group>
        </ion-list>
        <p class="hint">Перестороги записуються разом із профілем.</p>
      }

      @if (canDelete()) {
        <div class="actions">
          <ion-button expand="block" fill="clear" color="danger" class="lk-danger" [disabled]="busy()" (click)="remove()" data-testid="delete-member">
            Видалити профіль
          </ion-button>
        </div>
      }
    </ion-content>
  `,
})
export class ProfileEditSheet implements OnInit, OnDestroy {
  private readonly data = inject(KurinApi);
  private readonly auth = inject(AuthService);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly photoInput = viewChild<ElementRef<HTMLInputElement>>('photoInput');

  // Set by ModalController's componentProps.
  member: MemberDto | null = null;
  /** One's own card, or somebody else's opened by провід or an admin. */
  own = true;
  /** Where a new member goes; with no `member`, the sheet creates one there. */
  placement: NewMemberPlacement | null = null;
  /** The kurin checks profiles (KurinDto.profileVerificationEnabled). */
  verificationEnabled = false;
  /** What the край asks on the card, when the caller has it; otherwise the sheet reads it for `kurinKey`. */
  cardFields: CardFields | null = null;
  /** The kurin the member is in or goes to, when neither the card nor the placement says it. */
  kurinKey: string | null = null;
  /** What the kurin's policy lets this person do to the member: which перестороги to give and lift. */
  abilities: MemberAbilities = NO_ABILITIES;

  /** The profile check is done at once, as on the web; the card reads itself again after. */
  protected changed = false;
  protected creating = false;
  protected readonly minAge = MIN_AGE;
  protected readonly today = toDateOnly(new Date());
  protected readonly branches = RANK_BRANCHES;
  protected readonly warningLevels = WARNING_LEVELS;

  private readonly user = this.auth.user();
  private readonly access = kurinAccess(this.user);
  /** Web isReviewer: Виховник and up. */
  private readonly reviewer = this.access.leadGroups;

  protected readonly form = signal<MemberForm>({ firstName: '', middleName: '', lastName: '', email: '', phoneNumber: '', dateOfBirth: '', address: '', school: '',
    motherFirstName: '', motherLastName: '', motherPhone: '', fatherFirstName: '', fatherLastName: '', fatherPhone: '' });
  protected readonly card = signal<CardFields>(DEFAULT_CARD_FIELDS);
  /** Where a number typed without «+» is read: the kurin's край. */
  private readonly phoneCountry = signal<CountryCode>(DEFAULT_PHONE_COUNTRY);
  /** Saving waits for the край's fields, so none of them goes out unasked. */
  private readonly cardReady = signal(false);
  protected readonly extras = signal<CardExtras>({});
  /** The extra values as read, to send only what changed. */
  private loadedExtras: CardExtras = {};
  protected readonly dates = signal<RankDates>(rankDates([]));
  protected readonly open = signal({ UPYu: false, USP: false, UPS: false });
  protected readonly draft = signal<WarningDraft>(NO_WARNING_CHANGES);
  protected readonly verification = signal<{ status: ProfileVerificationStatus; at: string | null }>({ status: 'Unverified', at: null });
  protected readonly photo = signal<CroppedImage | null>(null);
  protected readonly photoRemoved = signal(false);
  private readonly preview = signal<string | null>(null);
  protected readonly touched = signal(false);
  protected readonly busy = signal(false);
  protected readonly verifying = signal(false);
  protected readonly sending = signal(false);

  protected readonly showEmail = signal(false);
  protected readonly pending = signal(false);
  protected readonly canRanks = signal(false);
  protected readonly canVerify = signal(false);
  protected readonly canWarnings = signal(false);
  protected readonly canDelete = signal(false);

  /** The form's fields this sheet asks, then the край's own; a group the kurin hides from the editor is not shown at all. */
  protected readonly rows = computed<Row[]>(() => [
    ...FIELDS.filter((f) => (f.key !== 'email' || this.showEmail()) && fieldMode(this.card(), f.key) !== 'hidden' && !this.hides(hiddenGroupOf(f.key))).map((f) => ({
      ...f,
      extra: false,
      testId: `profile-${f.key}`,
      max: f.type === 'date' ? this.today : undefined,
    })),
    ...(this.hides('extras') ? [] : this.card().extra).map((f) => ({
      key: f.key,
      label: f.label,
      type: f.type,
      autocomplete: 'off',
      extra: true,
      testId: `profile-extra-${f.key}`,
    })),
  ]);
  protected readonly shownRanks = computed(() => RANK_FIELDS.filter((f) => f.branch === 'base' || this.open()[f.branch]));
  protected readonly shownPhoto = computed(() => this.preview() ?? (this.photoRemoved() ? null : (this.member?.profilePhotoUrl ?? null)));
  protected readonly emailChanged = computed(() => this.form().email.trim().toLowerCase() !== (this.member?.email ?? '').trim().toLowerCase());
  protected readonly valid = computed(() => {
    const ranksOk = Object.values(this.dates()).every((d) => !d || /^\d{4}-\d{2}-\d{2}$/.test(d));
    return this.cardReady() && this.rows().every((row) => this.rowValid(row)) && ranksOk;
  });
  protected readonly tooYoung = computed(() => {
    const birth = this.form().dateOfBirth;
    return this.touched() && /^\d{4}-\d{2}-\d{2}$/.test(birth) && birth <= this.today && !oldEnough(birth, this.today);
  });
  protected readonly verificationLine = computed(() => {
    const { status, at } = this.verification();
    if (status === 'VerifiedCurrent') return at ? `Верифіковано ${shortDate(utcDate(at))}` : 'Верифіковано';
    if (status === 'VerifiedStale') return 'Дані змінено після верифікації';
    return 'Ще не верифіковано';
  });

  ngOnInit(): void {
    void this.loadCard();
    const m = this.member;
    if (!m) {
      // «Новий учасник»: the email is the invitation's address, the ступені are the провід's.
      this.creating = !!this.placement;
      this.own = false;
      this.showEmail.set(this.creating);
      this.canRanks.set(this.creating && this.reviewer);
      return;
    }
    const isAdmin = this.user?.isAdmin ?? false;
    this.form.set({
      firstName: m.firstName ?? '',
      middleName: m.middleName ?? '',
      lastName: m.lastName ?? '',
      email: m.email ?? '',
      phoneNumber: formatPhone(m.phoneNumber),
      dateOfBirth: (m.dateOfBirth ?? '').slice(0, 10),
      address: m.address ?? '',
      school: m.school ?? '',
      motherFirstName: m.motherFirstName ?? '',
      motherLastName: m.motherLastName ?? '',
      motherPhone: formatPhone(m.motherPhone),
      fatherFirstName: m.fatherFirstName ?? '',
      fatherLastName: m.fatherLastName ?? '',
      fatherPhone: formatPhone(m.fatherPhone),
    });
    const dates = rankDates(m.plastLevelHistories);
    this.dates.set(dates);
    this.open.set(openBranches(dates));
    this.verification.set({ status: m.profileVerificationStatus ?? 'Unverified', at: m.profileVerifiedAtUtc ?? null });
    this.showEmail.set(canEditEmail(m, isAdmin));
    this.pending.set(isPendingAccount(m));
    // The провід, and a member on their own card (a change makes a verified profile stale).
    this.canRanks.set(this.reviewer || this.own);
    this.canVerify.set(this.verificationEnabled && !this.own && this.reviewer);
    this.canWarnings.set(this.abilities.warnings.give.length > 0 || this.abilities.warnings.cancel !== 'None');
    this.canDelete.set(this.access.manageWholeKurin);
  }

  ngOnDestroy(): void {
    const url = this.preview();
    if (url) URL.revokeObjectURL(url);
  }

  /** Whether the kurin hides this group of the member's data from whoever edits. */
  private hides(group: MemberDataGroup | null): boolean {
    return !this.creating && !!group && (this.member?.hiddenData?.includes(group) ?? false);
  }

  protected rowValue(row: Row): string {
    return row.extra ? (this.extras()[row.key] ?? '') : this.form()[row.key as Field];
  }

  protected rowValid(row: Row): boolean {
    if (!row.extra) return this.fieldValid(row.key as Field);
    const field = this.card().extra.find((f) => f.key === row.key);
    return !field || cardExtraValid(field, this.extras()[row.key]);
  }

  protected setRow(row: Row, value: string | null | undefined): void {
    if (!row.extra) return this.set(row.key as Field, value);
    this.touched.set(true);
    this.extras.update((extras) => ({ ...extras, [row.key]: value ?? '' }));
  }

  private fieldValid(key: Field): boolean {
    const value = this.form()[key].trim();
    if (!value) return fieldMode(this.card(), key) === 'optional';
    if (key === 'email') return looksLikeEmail(value);
    if (key === 'phoneNumber' || key === 'motherPhone' || key === 'fatherPhone') {
      return value === formatPhone(this.member?.[key]) || isPossiblePhone(value, this.phoneCountry());
    }
    if (key === 'dateOfBirth') return /^\d{4}-\d{2}-\d{2}$/.test(value) && value <= this.today && oldEnough(value, this.today);
    return true;
  }

  protected set(key: Field, value: string | null | undefined): void {
    this.touched.set(true);
    this.form.update((form) => ({ ...form, [key]: value ?? '' }));
  }

  protected setRank(level: string, value: string | null | undefined): void {
    this.dates.update((dates) => ({ ...dates, [level]: value ?? '' }));
  }

  protected toggleBranch(branch: Exclude<RankBranch, 'base'>, on: boolean): void {
    this.open.update((open) => ({ ...open, [branch]: on }));
    if (!on) this.dates.update((dates) => closeBranch(dates, branch));
  }

  protected isChecked(level: WarningLevel): boolean {
    return warningChecked(this.member?.warnings, this.draft(), level);
  }

  /**
   * As the web's dialog: tick a level the kurin lets this person give once the one before is ticked;
   * untick one's own while one may still give it, or anyone's with the right to.
   */
  protected canToggle(level: WarningLevel): boolean {
    const warnings = this.member?.warnings;
    if (!this.isChecked(level)) {
      return mayGiveWarning(this.abilities, level) && canSelectWarning(warnings, this.draft(), level);
    }
    if (this.draft().assign.includes(level)) return true;
    const current = warningInForce(warnings, level);
    return !!current && mayCancelWarning(this.abilities, level, current.issuedByUserKey, this.user?.userKey)
      && canClearWarning(warnings, this.draft(), level, this.user?.userKey, Date.now(), this.cancelsAnyone());
  }

  protected warningHint(level: WarningLevel): string | null {
    if (this.canToggle(level)) return null;
    if (this.isChecked(level)) return 'Скасувати може автор перестороги або той, кому курінь це дозволив.';
    return canSelectWarning(this.member?.warnings, this.draft(), level) ? 'Цю дає хтось інший.' : 'Доступно після попереднього рівня.';
  }

  protected toggle(level: WarningLevel, on: boolean): void {
    if (on === this.isChecked(level) || !this.canToggle(level)) return;
    this.draft.set(toggleWarning(this.member?.warnings, this.draft(), level, on, this.user?.userKey, Date.now(), this.cancelsAnyone()));
  }

  private cancelsAnyone(): boolean {
    return this.abilities.warnings.cancel === 'Any';
  }

  protected close(result: ProfileEditResult): void {
    void this.modals.dismiss(result);
  }

  protected pickPhoto(): void {
    this.photoInput()?.nativeElement.click();
  }

  protected async photoPicked(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    if (!file) return;
    const modal = await this.modals.create({ component: ImageCropSheet, componentProps: { file } });
    await modal.present();
    const { data } = await modal.onWillDismiss<CroppedImage | null>();
    if (!data) return;
    this.setPreview(URL.createObjectURL(data.blob));
    this.photo.set(data);
    this.photoRemoved.set(false);
  }

  protected clearPhoto(): void {
    this.setPreview(null);
    this.photo.set(null);
    this.photoRemoved.set(true);
  }

  protected async verificationAction(): Promise<void> {
    const member = this.member;
    if (!member || this.verifying()) return;
    const current = this.verification().status === 'VerifiedCurrent';
    if (current && !(await this.confirm('Зняти верифікацію з профілю цього учасника?', 'Зняти'))) return;
    this.verifying.set(true);
    try {
      const saved = current ? await this.data.resetProfileVerification(member.memberKey) : await this.data.verifyProfile(member.memberKey);
      this.verification.set({
        status: saved?.profileVerificationStatus ?? (current ? 'Unverified' : 'VerifiedCurrent'),
        at: saved?.profileVerifiedAtUtc ?? (current ? null : new Date().toISOString()),
      });
      this.changed = true;
      await this.toasts.show(current ? 'Верифікацію профілю знято.' : 'Профіль верифіковано.');
    } catch (error) {
      const off = error instanceof HttpErrorResponse && error.status === 400;
      const fallback = current ? 'Не вдалося зняти верифікацію. Спробуй ще раз.' : 'Не вдалося верифікувати профіль. Спробуй ще раз.';
      await this.toasts.show(off ? 'Верифікацію профілів вимкнено для цього куреня.' : apiErrorText(error, fallback), 'danger');
    } finally {
      this.verifying.set(false);
    }
  }

  protected async resend(): Promise<void> {
    const member = this.member;
    if (!member || this.sending()) return;
    this.sending.set(true);
    try {
      await this.data.resendInvitation(member.memberKey);
      await this.toasts.show(`Нове посилання пішло на ${member.email}. Попереднє більше не діє.`);
    } catch (error) {
      await this.toasts.show(resendErrorText(error), 'danger');
    } finally {
      this.sending.set(false);
    }
  }

  protected async remove(): Promise<void> {
    const member = this.member;
    if (!member || this.busy()) return;
    if (!(await this.confirm('Видалити профіль учасника? Цю дію неможливо скасувати.', 'Видалити'))) return;
    this.busy.set(true);
    try {
      await this.data.deleteMember(member.memberKey);
      await this.toasts.show('Профіль видалено.');
      this.close('deleted');
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося видалити профіль. Спробуй ще раз.'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(): Promise<void> {
    if (this.creating) return this.create();
    const member = this.member;
    if (!member || !this.valid() || this.busy()) return;
    this.busy.set(true);
    const form = { ...this.storedForm(), email: this.showEmail() ? this.form().email : (member.email ?? '') };
    // Someone who may not change the ступені sends them back as they were.
    const histories = this.canRanks()
      ? rankHistories(member.plastLevelHistories, this.dates())
      : (member.plastLevelHistories ?? []).filter((history) => history.dateAchieved);
    const photo = this.photo();
    let saved: MemberDto;
    try {
      saved = await this.data.updateMember(
        member.memberKey,
        memberFormData(
          member,
          form,
          histories,
          { blob: photo?.blob ?? null, name: photo?.name ?? 'profile.jpg', remove: this.photoRemoved() },
          changedCardExtras(this.loadedExtras, this.extras()),
        ),
      );
    } catch (error) {
      await this.toasts.show(saveErrorText(error, 'Не вдалося зберегти профіль. Спробуй ще раз.'), 'danger');
      this.busy.set(false);
      return;
    }
    const notes = ['Профіль збережено.'];
    if (!(await this.saveWarnings(member.memberKey))) notes.push('Не всі перестороги вдалося записати.');
    if (saved?.invitationSent === false) notes.push('Лист із запрошенням не пішов — натисни «Надіслати лист ще раз».');
    await this.toasts.show(notes.join(' '), notes.length > 1 ? 'danger' : undefined);
    this.busy.set(false);
    this.close('saved');
  }

  /** «Новий учасник»: the record, its account and the invitation, then the new card (web upsert-member create). */
  private async create(): Promise<void> {
    const placement = this.placement;
    if (!placement || !this.valid() || this.busy()) return;
    this.busy.set(true);
    const photo = this.photo();
    const histories = this.canRanks() ? rankHistories([], this.dates()) : [];
    let created: MemberDto;
    try {
      created = await this.data.createMember(
        placement,
        newMemberFormData(
          placement,
          this.storedForm(),
          histories,
          { blob: photo?.blob ?? null, name: photo?.name ?? 'profile.jpg' },
          changedCardExtras(this.loadedExtras, this.extras()),
        ),
      );
    } catch (error) {
      // A bare 409 on create is the server's «this address is already an account or an application».
      const taken = error instanceof HttpErrorResponse && error.status === 409;
      await this.toasts.show(taken ? SAVE_ERRORS.EmailTaken : saveErrorText(error, 'Не вдалося створити учасника. Спробуй ще раз.'), 'danger');
      this.busy.set(false);
      return;
    }
    await this.toasts.show(
      created?.invitationSent === false
        ? 'Запис збережено, але лист із запрошенням не пішов. Відкрий редагування й натисни «Надіслати лист ще раз».'
        : `Учасника додано. Запрошення пішло на ${this.form().email.trim()}.`,
      created?.invitationSent === false ? 'danger' : undefined,
    );
    this.busy.set(false);
    this.close({ created: created.memberKey });
  }

  /**
   * The край's card fields: from the caller, or read for the kurin. One that cannot be read leaves
   * the default; the server still holds the край's rules and says what is missing.
   */
  private async loadCard(): Promise<void> {
    const placed = this.placement && 'kurinKey' in this.placement ? this.placement.kurinKey : null;
    const kurinKey = this.kurinKey ?? this.member?.kurinKey ?? placed;
    let card = this.cardFields ?? DEFAULT_CARD_FIELDS;
    if (!this.cardFields && kurinKey) card = await this.data.cardFields(kurinKey).catch(() => DEFAULT_CARD_FIELDS);
    if (kurinKey) this.phoneCountry.set(await this.data.phoneCountry(kurinKey).catch(() => DEFAULT_PHONE_COUNTRY));
    this.card.set(card);
    this.loadedExtras = cardExtrasDraft(card, this.member?.cardExtras);
    this.extras.set(this.loadedExtras);
    this.cardReady.set(true);
  }

  /** The form as it is sent: the phone as +420777123456. */
  private storedForm(): MemberForm {
    const form = this.form();
    // A number left as it was goes back as it was, even one older than the rule.
    const stored = (key: 'phoneNumber' | (typeof PARENT_PHONES)[number]) =>
      this.member && form[key] === formatPhone(this.member[key]) ? (this.member[key] ?? '') : toStoredPhone(form[key], this.phoneCountry());
    return {
      ...form,
      phoneNumber: stored('phoneNumber'),
      ...Object.fromEntries(PARENT_PHONES.map((key) => [key, stored(key)])),
    };
  }

  /** The перестороги after the profile, one by one as the web does; false when any call failed. */
  private async saveWarnings(memberKey: string): Promise<boolean> {
    let allDone = true;
    for (const call of warningCalls(this.draft())) {
      try {
        if (call.kind === 'cancel') await this.data.cancelWarning(memberKey, call.key);
        else await this.data.assignWarning(memberKey, call.level);
      } catch {
        allDone = false;
      }
    }
    return allDone;
  }

  private setPreview(url: string | null): void {
    const before = this.preview();
    if (before) URL.revokeObjectURL(before);
    this.preview.set(url);
  }

  private async confirm(header: string, action: string): Promise<boolean> {
    const sheet = await this.sheets.create({
      header,
      buttons: [
        { text: action, role: 'destructive', data: 'yes' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    return data === 'yes';
  }
}
