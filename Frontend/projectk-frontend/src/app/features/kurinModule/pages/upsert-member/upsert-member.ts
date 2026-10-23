import { Component, inject, OnInit, ChangeDetectionStrategy, viewChild, HostListener } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormGroup, FormsModule, NgForm } from '@angular/forms';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { MemberService } from '../../services/member-service/member.service';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MemberDataGroup, MemberDto } from '../../models/member.dto';
import { UpsertMemberDto } from '../../models/requests/member/upsert-member.dto';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { MinAgeValidatorDirective } from "../../directives/min-age-validator/min-age.validator";
import { ImageCropperComponent, ImageCroppedEvent } from 'ngx-image-cropper';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { base64ToBlob } from '../../functions/base64-to-blob.function';
import { Location } from '@angular/common';
import { PlastLevelHistoryDto } from '../../models/plast-level-history.dto';
import { PlastLevel } from '../../models/enums/plast-level.enum';
import { parseDateOnlyString, toDateOnlyString } from '../../functions/to-date-only-string.function';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { finalize } from 'rxjs';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { AccountStatus } from '../../models/enums/account-status.enum';
import { HttpErrorResponse } from '@angular/common/http';
import { TagModule } from '@openng/optimus-ui/tag';
import { ProfileVerificationBadgeComponent } from '../../components/profile-verification-badge/profile-verification-badge';
import { isUsableKey } from '../../../../shared/functions/is-usable-key.function';
import { initials } from '../../../dashboardModule/functions/greeting.function';
import { CardExtraFieldDto, cardFieldMode, CardFieldMode, CardFieldsDto, DEFAULT_CARD_FIELDS, StandardCardField } from '../../models/card-fields.dto';
import { CountryCode } from 'libphonenumber-js';
import { DEFAULT_PHONE_COUNTRY } from '../../../../shared/functions/phone.function';
import { PhoneInputComponent } from '../../../../shared/phone-input/phone-input';
import { LeavesWithUnsavedChanges } from '../../../../shared/guards/unsaved-changes.guard';

@Component({
  selector: 'app-upsert-member',
  imports: [FormsModule, InputTextModule, PhoneInputComponent, DatePickerModule, ButtonModule, ConfirmDialogModule, MinAgeValidatorDirective, ImageCropperComponent, DialogModule, TagModule, ProfileVerificationBadgeComponent],
  providers: [ConfirmationService],
  templateUrl: './upsert-member.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './upsert-member.css'
})
export class UpsertMemberComponent implements OnInit, LeavesWithUnsavedChanges {
  readonly form = viewChild.required<NgForm>('form');

  member: MemberDto = {
    memberKey: '',
    groupKey: '',
    kurinKey: '',
    firstName: '',
    middleName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    dateOfBirth: null,
    plastLevelHistories: [],
    leadershipHistories: [],
    profilePhotoUrl: null,
  };

  memberKey = '';
  groupKey = '';
  kurinKey = '';
  // Every new member gets an account: the letter is how they get in, and the box that used
  // to make it optional was mostly left unticked by accident.
  createUserAccount = true;
  /** The country a new number starts in: the kurin's край's. */
  phoneCountry: CountryCode = DEFAULT_PHONE_COUNTRY;

  route = inject(ActivatedRoute);
  router = inject(Router);
  location = inject(Location);
  memberService = inject(MemberService);
  kurinService = inject(KurinService);
  authService = inject(AuthService);
  permissionService = inject(PermissionService);
  confirmationService = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  private cameFromMember = false;
  /** Saved or deleted: the page goes away on purpose, nothing to ask about. */
  private done = false;

  isCreate = false;
  // Адреса, на яку пішов лист. Поки поле з нею не збігається, «надіслати ще раз» пошле на стару.
  private savedEmail = '';
  invitationSending = false;
  profileVerificationEnabled = false;

  plastLevelMap: Record<PlastLevel, PlastLevelHistoryDto> = {} as Record<PlastLevel, PlastLevelHistoryDto>;
  readonly PlastLevel = PlastLevel;
  /** The ступені by улад, in the order they are earned. */
  readonly levelGroups: LevelGroup[] = [
    { key: 'base', title: 'Пласт', levels: [
      { level: PlastLevel.Entry, title: 'Вступ до Пласту' },
      { level: PlastLevel.Prykhylnyk, title: 'Прихильник' },
      { level: PlastLevel.Uchasnyk, title: 'Учасник (заприсяження)' },
    ] },
    { key: 'upu', title: 'УПЮ', levels: [
      { level: PlastLevel.Rozviduvach, title: 'Розвідувач' },
      { level: PlastLevel.Skob, title: 'Скоб / вірлиця' },
      { level: PlastLevel.HetmanskiySkob, title: 'Гетьманський скоб / вірлиця' },
    ] },
    { key: 'usp', title: 'УСП', levels: [
      { level: PlastLevel.Starshoplastun, title: 'Перехід в УСП' },
    ] },
    { key: 'ups', title: 'УПС', levels: [
      { level: PlastLevel.Senior, title: 'Перехід в УПС' },
      { level: PlastLevel.SeniorPratsi, title: 'Пластун сеньйор праці' },
      { level: PlastLevel.SeniorDovirja, title: 'Пластун сеньйор довірʼя' },
      { level: PlastLevel.SeniorKerivnytstva, title: 'Пластун сеньйор керівництва' },
    ] },
  ];
  /** Groups on screen: Пласт and УПЮ always, the rest once they have a date or are asked for. */
  private readonly shownLevelGroups = new Set<LevelGroupKey>(['base', 'upu']);

  imageFile?: File;
  croppedImage = '';
  croppedFile: File | null = null;
  displayCropper = false;
  fileToUpload: Blob | null = null;
  removeProfilePhoto = false;

  private objectUrlToRevoke: string | null = null;

  defaultDateFormat = 'dd.mm.yy';
  /** Ступені can't be dated ahead of today. */
  readonly today = new Date();

  /** What the member's край asks of the card; the card as it always was until it loads. */
  cardFields: CardFieldsDto = DEFAULT_CARD_FIELDS;
  /** The край's own fields as the form edits them: text, or a date for a date field. */
  cardExtras: Record<string, string | Date | null> = {};

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.memberKey = params.get('memberKey') ?? '';
      // Guid.Empty reaches the URL whenever a groupless member is linked to; taking it as
      // a real key would send it back to the API as the member's group on save.
      this.groupKey = this.usableParam(params.get('groupKey'));
      this.kurinKey = this.usableParam(params.get('kurinKey'));
    });
    if (this.memberKey) {
      this.loadData();
      this.isCreate = false;
    } else {
      this.ensurePlastLevelMap();
      this.showLevelGroupsWithDates();
      this.isCreate = true;
      this.loadCardFields(this.kurinKey);
    }
    const currentNavigation = this.router.currentNavigation();
    const navState = (currentNavigation?.extras.state as { fromMember?: boolean } | undefined) ?? history.state;
    this.cameFromMember = navState?.fromMember === true;
  }

  private loadData(): void {
    this.memberService.getByKey(this.memberKey).subscribe({
      next: (member) => {
        const plastLevelHistories = (member.plastLevelHistories ?? []).map(history => ({
          ...history,
          dateAchieved: parseDateOnlyString(history.dateAchieved)
        }));

        const memberForEdit: MemberDto = {
          ...member,
          dateOfBirth: parseDateOnlyString(member.dateOfBirth),
          plastLevelHistories
        };

        this.member = memberForEdit;
        this.savedEmail = memberForEdit.email;
        this.ensurePlastLevelMap();
        this.showLevelGroupsWithDates();
        this.loadProfileVerificationToggle(memberForEdit.kurinKey);
        this.loadCardFields(memberForEdit.kurinKey);
      },
      error: (error) => {
        console.error('Error fetching member:', error);
        this.router.navigate(this.scopeRoute(), { replaceUrl: true });
      }
    });
  }

  fieldMode(field: StandardCardField): CardFieldMode {
    return cardFieldMode(this.cardFields, field);
  }

  // A new member placed in a гурток has no kurin in the route; they join the kurin being worked in.
  private loadCardFields(kurinKey: string | null | undefined): void {
    const key = kurinKey || this.authService.getAuthStateValue?.()?.kurinKey;
    if (!key) {
      return;
    }

    this.kurinService.getPhoneCountry(key).subscribe(country => this.phoneCountry = country);
    this.kurinService.getCardFields(key).subscribe(card => {
      this.cardFields = card;
      this.cardExtras = Object.fromEntries(card.extra.map(field => [field.key, this.editableExtra(field)]));
    });
  }

  private editableExtra(field: CardExtraFieldDto): string | Date | null {
    const value = this.member.cardExtras?.[field.key] ?? '';
    return field.type === 'date' ? parseDateOnlyString(value) : value;
  }

  // Only what was changed goes back, an emptied field as '' to clear it. A card loaded without
  // the private details shows these blank, and sending the blanks would wipe what is stored.
  private cardExtrasPayload(): Record<string, string> {
    return Object.fromEntries(this.cardFields.extra
      .map(field => {
        const value = this.cardExtras[field.key];
        return [field.key, value instanceof Date ? toDateOnlyString(value) ?? '' : (value ?? '').trim()];
      })
      .filter(([key, value]) => value !== (this.member.cardExtras?.[key] ?? '')));
  }

  private usableParam(value: string | null): string {
    return isUsableKey(value) ? value!.trim() : '';
  }

  // Where to land when the member page itself is gone: their group if we know it,
  // otherwise the kurin.
  private scopeRoute(): unknown[] {
    return this.groupKey ? ['/group', this.groupKey] : ['/kurin'];
  }

  // A group with a date stays on screen: hiding it would hide a ступінь that is saved.
  private showLevelGroupsWithDates() {
    for (const group of this.levelGroups) {
      if (group.levels.some(({ level }) => this.plastLevelMap[level]?.dateAchieved != null)) {
        this.shownLevelGroups.add(group.key);
      }
    }
  }

  get visibleLevelGroups(): LevelGroup[] {
    return this.levelGroups.filter(group => this.shownLevelGroups.has(group.key));
  }

  get hiddenLevelGroups(): LevelGroup[] {
    return this.levelGroups.filter(group => !this.shownLevelGroups.has(group.key));
  }

  showLevelGroup(key: LevelGroupKey): void {
    this.shownLevelGroups.add(key);
  }

  get photoInitials(): string {
    return initials(this.member.firstName, this.member.lastName);
  }

  /** The «Батьки» block: a row of name, surname and phone for the mother, then the father. */
  readonly parentRows = [
    { of: 'мами', firstName: 'motherFirstName', lastName: 'motherLastName', phone: 'motherPhone' },
    { of: 'тата', firstName: 'fatherFirstName', lastName: 'fatherLastName', phone: 'fatherPhone' },
  ] as const;

  /**
   * Whether the kurin hides this group of the person's data from whoever edits. Its fields stay
   * disabled and are not sent: the server keeps what it has, and blanks would not wipe it anyway.
   */
  hides(group: MemberDataGroup): boolean {
    return !this.isCreate && (this.member.hiddenData?.includes(group) ?? false);
  }

  private privateFieldValue(field: PrivateField): string | undefined {
    if (this.hides(PARENT_FIELDS.includes(field) ? 'parents' : 'address')) {
      return undefined;
    }

    const changed = this.isCreate || !!this.form()?.controls[field]?.dirty;
    const mode = this.fieldMode(PARENT_FIELDS.includes(field) ? 'parents' : field as 'address' | 'school');
    return changed && mode !== 'hidden' ? (this.member[field] ?? '').trim() : undefined;
  }

  /** The required fields still empty or wrong, named as the form names them. */
  get missingFields(): string[] {
    const controls = this.form()?.controls ?? {};
    const order = ['firstName', 'middleName', 'lastName', 'dateOfBirth', 'school', 'email', 'phoneNumber', 'address', 'motherPhone', 'fatherPhone'];
    const rank = (name: string) => order.includes(name) ? order.indexOf(name) : order.length;
    return Object.entries(controls)
      .filter(([, control]) => control.invalid)
      .map(([name]) => name)
      .sort((left, right) => rank(left) - rank(right))
      .map(name => this.fieldLabel(name))
      .filter((label): label is string => !!label);
  }

  private fieldLabel(name: string): string | null {
    const labels: Record<string, string> = {
      firstName: 'імʼя',
      middleName: 'по батькові',
      lastName: 'прізвище',
      dateOfBirth: 'дату народження',
      email: 'email',
      phoneNumber: 'телефон',
      address: 'адресу',
      school: 'школу',
      motherPhone: 'телефон мами',
      fatherPhone: 'телефон тата',
    };
    if (labels[name]) {
      return labels[name];
    }
    const extra = this.cardFields.extra.find(field => `extra-${field.key}` === name);
    return extra ? extra.label.toLowerCase() : null;
  }

  cancel(): void {
    if (this.cameFromMember) {
      this.location.back();
    } else if (this.isCreate) {
      this.router.navigate(this.scopeRoute());
    } else {
      this.router.navigate(['/member', this.memberKey]);
    }
  }

  /** Edits on screen that nobody saved. */
  get hasUnsavedChanges(): boolean {
    return !this.done && !!this.form()?.dirty;
  }

  confirmLeave(): boolean | Promise<boolean> {
    if (!this.hasUnsavedChanges) {
      return true;
    }

    return new Promise(resolve => this.confirmationService.confirm({
      header: 'Вийти без збереження?',
      message: 'Зміни в цій формі пропадуть.',
      icon: 'pi pi-exclamation-triangle',
      rejectButtonProps: { label: 'Лишитися', severity: 'secondary', variant: 'text' },
      acceptButtonProps: { label: 'Вийти', severity: 'danger' },
      accept: () => resolve(true),
      reject: () => resolve(false)
    }));
  }

  /** Closing or reloading the tab: the browser asks with its own words. */
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges) {
      event.preventDefault();
    }
  }

  /** What is wrong with a ступінь's date, or null: not before birth, not ahead of today, not before the one it follows in its улад. */
  levelDateIssue(level: PlastLevel): string | null {
    const date = toDateOnlyString(this.plastLevelMap[level]?.dateAchieved);
    if (!date) {
      return null;
    }

    const birth = toDateOnlyString(this.member.dateOfBirth);
    if (birth && date < birth) {
      return 'Раніше за дату народження.';
    }
    if (date > toDateOnlyString(new Date())!) {
      return 'Ця дата ще не настала.';
    }

    const levels = this.levelGroups.find(group => group.levels.some(item => item.level === level))?.levels ?? [];
    const before = levels.slice(0, levels.findIndex(item => item.level === level))
      .filter(item => toDateOnlyString(this.plastLevelMap[item.level]?.dateAchieved))
      .at(-1);
    const beforeDate = before && toDateOnlyString(this.plastLevelMap[before.level].dateAchieved);
    if (before && beforeDate && date < beforeDate) {
      return `Раніше, ніж «${before.title}».`;
    }

    return null;
  }

  get hasLevelDateIssues(): boolean {
    return this.levelGroups.some(group => group.levels.some(({ level }) => !!this.levelDateIssue(level)));
  }

  markFormDirty() {
    this.form()?.form.markAsDirty();
  }

  private loadProfileVerificationToggle(kurinKey: string | null | undefined): void {
    this.profileVerificationEnabled = false;
    if (!kurinKey) {
      return;
    }

    this.kurinService.getByKey(kurinKey).subscribe({
      next: (kurin) => {
        this.profileVerificationEnabled = kurin.profileVerificationEnabled ?? false;
      },
      error: () => {
        this.profileVerificationEnabled = false;
      }
    });
  }

  /** Акаунт є, але запрошення ще ніхто не прийняв — пошту можна виправити, а лист переслати. */
  get isPendingAccount(): boolean {
    return !this.isCreate
      && !!this.member.userKey
      && this.member.accountStatus === AccountStatus.PendingActivation;
  }

  get emailChangedSinceSave(): boolean {
    return this.member.email.trim().toLowerCase() !== this.savedEmail.trim().toLowerCase();
  }

  canEditEmail(): boolean {
    return this.isCreate || !this.member.userKey || this.isPendingAccount || this.permissionService.isAdmin();
  }

  resendInvitation(): void {
    if (!this.isPendingAccount || this.emailChangedSinceSave || this.invitationSending) {
      return;
    }

    this.invitationSending = true;
    this.memberService.resendInvitation(this.memberKey).pipe(
      finalize(() => this.invitationSending = false)
    ).subscribe({
      next: () => this.messages.add({
        severity: 'success',
        summary: 'Лист надіслано',
        detail: `Нове посилання пішло на ${this.savedEmail}. Попереднє більше не діє.`
      }),
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.messages.add({
            severity: 'warn',
            summary: 'Лист не надіслано',
            detail: 'Акаунт уже активовано — запрошення більше не потрібне. Онови сторінку.'
          });
        } else if (error instanceof HttpErrorResponse && error.status === 500) {
          this.messages.add({
            severity: 'error',
            summary: 'Лист не надіслано',
            detail: 'Поштовий сервіс не прийняв лист. Спробуй за хвилину; попереднє посилання ще діє.'
          });
        }
      }
    });
  }

  canDeleteMember(): boolean {
    return !this.isCreate && (this.permissionService.isAdmin() || this.permissionService.canManageWholeKurin());
  }

  private ensurePlastLevelMap() {
    this.levelGroups.flatMap(group => group.levels).forEach(config => {
      let rec = this.member.plastLevelHistories.find(x => x.plastLevel === config.level);
      if (!rec) {
        rec = { plastLevel: config.level, dateAchieved: null };
        this.member.plastLevelHistories.push(rec);
      }
      this.plastLevelMap[config.level] = rec;
    });
  }

  private buildPlastLevelsPayload() {
    const history = this.member?.plastLevelHistories ?? [];
    const filteredHistory = history.filter(x => x.dateAchieved != null);
    filteredHistory.forEach(x => x.dateAchieved = toDateOnlyString(x.dateAchieved));
    return filteredHistory;
  }

  submit(): void {
    const baseDto: UpsertMemberDto = {
      firstName: this.member.firstName,
      middleName: this.member.middleName,
      lastName: this.member.lastName,
      email: this.hides('contacts') ? undefined : this.member.email,
      phoneNumber: this.hides('contacts') ? undefined : this.member.phoneNumber,
      dateOfBirth: this.hides('birthDate') ? undefined : toDateOnlyString(this.member.dateOfBirth)!,
      plastLevelHistories: this.buildPlastLevelsPayload(),
      // Private: someone who could not see them gets them blank, so they go only when touched here.
      // Left out, the server keeps what it has; sent empty, it clears them.
      address: this.privateFieldValue('address'),
      school: this.privateFieldValue('school'),
      ...Object.fromEntries(PARENT_FIELDS.map(field => [field, this.privateFieldValue(field)])),
      cardExtras: this.hides('extras') ? undefined : this.cardExtrasPayload(),
    };

    // Куди людину поставити — сказано лише при заведенні. Редагування профілю нікого не переносить:
    // належність живе в членстві, і міняється діями «перевести в гурток» та «вивести з куреня».
    if (this.isCreate) {
      if (this.groupKey) {
        baseDto.groupKey = this.groupKey;
      } else if (this.kurinKey) {
        baseDto.kurinKey = this.kurinKey;
      }
    }

    const saveObs = this.isCreate 
      ? this.memberService.create({ ...baseDto, createUserAccount: this.createUserAccount }, this.fileToUpload)
      : this.memberService.update(this.memberKey, { ...baseDto, removeProfilePhoto: this.removeProfilePhoto }, this.fileToUpload);

    saveObs.subscribe({
      next: (savedMember) => {
        this.done = true;
        if (savedMember.invitationSent === false) {
          this.messages.add({
            severity: 'warn',
            summary: 'Лист не надіслано',
            detail: 'Запис збережено, але лист із запрошенням не пішов. Відкрий редагування й натисни «Надіслати лист ще раз».',
            life: 10000
          });
        }
        if (this.cameFromMember) {
          this.location.back();
        } else {
          this.router.navigate(['/member', savedMember.memberKey], { replaceUrl: true });
        }
      },
      error: (error) => {
        if (error instanceof HttpErrorResponse && error.error?.error === 'EmailTaken') {
          this.messages.add({
            severity: 'warn',
            summary: 'Пошта зайнята',
            detail: 'Ця адреса вже належить іншому акаунту або заявці. Перевір її.'
          });
          return;
        }

        if (error instanceof HttpErrorResponse && error.error?.error === 'CardFieldRequired') {
          this.messages.add({ severity: 'warn', summary: 'Не всі поля заповнено', detail: error.error.message });
          return;
        }

        if (this.isCreate) {
          console.error('Error creating member:', error, { isCreate: true });
        } else {
          console.error('Error updating member:', error);
        }
      }
    });
  }

  confirmDeleteMember(event: Event) {
      this.confirmationService.confirm({
          target: event.target as EventTarget,
          // Not «Вивести»: this removes the person from Лілейка in every kurin, so it says so and points at the gentler way.
          message: 'Людину буде видалено з Лілейки назавжди — разом із пробами, вмілостями, відзначеннями й членствами в усіх куренях. '
            + 'Якщо людина просто пішла з куреня, закрий її членство кнопкою «Вивести» на картці.',
          header: 'Видалити профіль',
          icon: 'pi pi-exclamation-triangle',
          rejectLabel: 'Скасувати',
          rejectButtonProps: {
              label: 'Скасувати',
              severity: 'secondary',
              outlined: true,
          },
          acceptButtonProps: {
              label: 'Видалити',
              severity: 'danger',
          },

          accept: () => {
            this.memberService.delete(this.memberKey).subscribe({
              next: () => {
                this.done = true;
                this.router.navigate(this.scopeRoute());
              },
              error: (error) => {
                console.error('Error deleting member:', error);
              }
            });
          }
      });
  }

  onPhotoPicked(event: Event): void {
    this.fileChangeEvent({ files: (event.target as HTMLInputElement).files });
  }

  fileChangeEvent(event: { files?: ArrayLike<File> | null }): void {
    const file = event.files?.[0];
    if (!file) {
      console.warn('No file selected');
      return;
    }
    if (this.objectUrlToRevoke) {
      URL.revokeObjectURL(this.objectUrlToRevoke);
      this.objectUrlToRevoke = null;
    }
    this.imageFile = file;
    this.croppedImage = '';
    this.croppedFile = null;
    
    this.removeProfilePhoto = false;
    
    this.displayCropper = true;
  }

  imageCropped(event: ImageCroppedEvent) {
    if (event.base64) {
      this.croppedImage = event.base64;
      this.croppedFile = null;
      return;
    }

    if (event.blob) {
      this.croppedFile = new File(
        [event.blob],
        this.imageFile?.name?.replace(/\.[^.]+$/, '.jpg') || 'profile.jpg',
        { type: event.blob.type || 'image/jpeg' }
      );

      const url = event.objectUrl || URL.createObjectURL(event.blob);
      if (this.objectUrlToRevoke && this.objectUrlToRevoke !== url) {
        URL.revokeObjectURL(this.objectUrlToRevoke);
      }
      this.objectUrlToRevoke = url;
      this.croppedImage = url;
    } else {
      console.warn('Crop event без base64 і blob', event);
    }
  }

  save(form: FormGroup) {
    this.displayCropper = false;
    this.fileToUpload = this.croppedFile ?? (this.croppedImage ? base64ToBlob(this.croppedImage) : null);

    if (this.fileToUpload) {
      this.removeProfilePhoto = false;
      form.markAsDirty();
    }
    else {
      console.warn('Nothing to upload (cropped image undefined)');
    }
  }

  onCancelCrop() {
    this.displayCropper = false;
    this.croppedImage = '';
  }

  clearProfilePhoto(form: FormGroup) {
    if (this.objectUrlToRevoke) {
      URL.revokeObjectURL(this.objectUrlToRevoke);
      this.objectUrlToRevoke = null;
    }
    this.imageFile = undefined;
    this.croppedImage = '';
    this.croppedFile = null;
    this.fileToUpload = null;
    this.member.profilePhotoUrl = null;

    form.markAsDirty();
    this.removeProfilePhoto = true;
  }
}

type LevelGroupKey = 'base' | 'upu' | 'usp' | 'ups';

type ParentField = 'motherFirstName' | 'motherLastName' | 'motherPhone' | 'fatherFirstName' | 'fatherLastName' | 'fatherPhone';
type PrivateField = 'address' | 'school' | ParentField;

const PARENT_FIELDS: PrivateField[] = ['motherFirstName', 'motherLastName', 'motherPhone', 'fatherFirstName', 'fatherLastName', 'fatherPhone'];

interface LevelGroup {
  key: LevelGroupKey;
  title: string;
  levels: { level: PlastLevel; title: string }[];
}
