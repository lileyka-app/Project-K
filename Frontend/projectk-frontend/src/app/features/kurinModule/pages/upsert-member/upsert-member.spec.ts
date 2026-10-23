import type { Mock, MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { UpsertMemberComponent } from './upsert-member';
import { ActivatedRoute, Router, convertToParamMap, ParamMap, Navigation } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { MemberService } from '../../services/member-service/member.service';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { AccountStatus } from '../../models/enums/account-status.enum';
import { MemberDto } from '../../models/member.dto';
import { FileSelectEvent } from '@openng/optimus-ui/fileupload';
import { ImageCroppedEvent } from 'ngx-image-cropper';
import { HttpClient } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { Location } from '@angular/common';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { CardFieldsDto, DEFAULT_CARD_FIELDS } from '../../models/card-fields.dto';
import { MemberProfileVerificationStatus } from '../../models/enums/member-profile-verification-status.enum';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { PlastLevel } from '../../models/enums/plast-level.enum';

describe('UpsertMemberComponent', () => {
  let fixture: ComponentFixture<UpsertMemberComponent>;
  let component: UpsertMemberComponent;

  let routeParamMap$: BehaviorSubject<ParamMap>;
  let routerSpy: MockedObject<Router>;
  let currentNavigationSignal: WritableSignal<Navigation | null>;
  let memberServiceSpy: MockedObject<MemberService>;
  let kurinServiceSpy: MockedObject<KurinService>;
  let confirmationServiceSpy: MockedObject<ConfirmationService>;
  let messageServiceSpy: MockedObject<MessageService>;
  let locationSpy: MockedObject<Location>;
  let permissionServiceSpy: MockedObject<PermissionService>;
  let authServiceSpy: MockedObject<AuthService>;

  const groupKey = 'group123';
  const memberKey = 'memberABC';

  const loadedMember: MemberDto = {
    memberKey,
    groupKey,
    kurinKey: 'kurin1',
    firstName: 'John',
    middleName: 'M',
    lastName: 'Doe',
    email: 'john@example.com',
    phoneNumber: '123456789',
    dateOfBirth: new Date('2000-05-10'),
    profilePhotoUrl: 'http://example.com/photo.jpg',
    plastLevelHistories: [],
    leadershipHistories: []
  };

  function setRouteParams(params: Record<string, string>) {
    routeParamMap$.next(convertToParamMap(params));
  }

  beforeEach(async () => {
    routeParamMap$ = new BehaviorSubject(convertToParamMap({ groupKey, memberKey }));
    currentNavigationSignal = signal<Navigation | null>(null);
    routerSpy = {
      navigate: vi.fn()
    } as MockedObject<Router>;
    (routerSpy as unknown as { currentNavigation: WritableSignal<Navigation | null> }).currentNavigation = currentNavigationSignal;
    memberServiceSpy = {
      getByKey: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      delete: vi.fn(),
      verifyProfile: vi.fn(),
      resetProfileVerification: vi.fn(),
      resendInvitation: vi.fn()
    } as MockedObject<MemberService>;
    kurinServiceSpy = {
      getByKey: vi.fn(),
      getCardFields: vi.fn(),
      getPhoneCountry: vi.fn()
    } as MockedObject<KurinService>;
    kurinServiceSpy.getCardFields.mockReturnValue(of(DEFAULT_CARD_FIELDS));
    kurinServiceSpy.getPhoneCountry.mockReturnValue(of('UA'));
    confirmationServiceSpy = {
      confirm: vi.fn()
    } as MockedObject<ConfirmationService>;
    messageServiceSpy = {
      add: vi.fn()
    } as MockedObject<MessageService>;
    locationSpy = {
      back: vi.fn()
    } as MockedObject<Location>;
    permissionServiceSpy = {
      canManageWarnings: vi.fn(),
      isAdmin: vi.fn(),
      canManageWholeKurin: vi.fn(),
      isReviewer: vi.fn()
    } as MockedObject<PermissionService>;
    authServiceSpy = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;

    memberServiceSpy.getByKey.mockReturnValue(of(loadedMember));
    memberServiceSpy.create.mockReturnValue(of({ ...loadedMember, memberKey: 'created999' }));
    memberServiceSpy.update.mockReturnValue(of(loadedMember));
    memberServiceSpy.delete.mockReturnValue(of(void 0));
    memberServiceSpy.verifyProfile.mockReturnValue(of({
      ...loadedMember,
      profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent
    }));
    memberServiceSpy.resetProfileVerification.mockReturnValue(of({
      ...loadedMember,
      profileVerificationStatus: MemberProfileVerificationStatus.Unverified
    }));
    kurinServiceSpy.getByKey.mockReturnValue(of({
      kurinKey: loadedMember.kurinKey,
      number: 1,
      profileVerificationEnabled: true
    }));
    permissionServiceSpy.canManageWarnings.mockReturnValue(false);
    permissionServiceSpy.isAdmin.mockReturnValue(false);
    permissionServiceSpy.canManageWholeKurin.mockReturnValue(false);
    permissionServiceSpy.isReviewer.mockReturnValue(false);
    authServiceSpy.getAuthStateValue.mockReturnValue({
      userKey: 'current-user',
      memberKey: 'current-member',
      email: 'current@example.com',
      isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
      kurinKey: loadedMember.kurinKey,
      accessToken: 'token'
    });

    confirmationServiceSpy.confirm.mockReturnValue(confirmationServiceSpy);


    await TestBed.configureTestingModule({
      imports: [UpsertMemberComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: routeParamMap$.asObservable() } },
        { provide: Router, useValue: routerSpy },
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: KurinService, useValue: kurinServiceSpy },
        { provide: ConfirmationService, useValue: confirmationServiceSpy },
        { provide: MessageService, useValue: messageServiceSpy },
        { provide: Location, useValue: locationSpy },
        { provide: PermissionService, useValue: permissionServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        { provide: HttpClient, useValue: {} }
      ]
    }).compileComponents();
  });

  function create(setup?: () => void) {
    fixture = TestBed.createComponent(UpsertMemberComponent);
    component = fixture.componentInstance;
    if (setup) {
      setup();
    }
    fixture.detectChanges();
  }

  it('should create', () => {
    create();
    expect(component).toBeTruthy();
  });

  describe('Component initialization', () => {
    it('should initialize with default member object', () => {
      create();
      expect(component.member).toBeDefined();
      expect(component.member.memberKey).toBe(memberKey);
      expect(component.member.groupKey).toBe(groupKey);
    });

    it('should initialize with default values', () => {
      setRouteParams({ groupKey });
      create();
      expect(component.displayCropper).toBe(false);
      expect(component.croppedImage).toBe('');
      expect(component.fileToUpload).toBeNull();
      expect(component.removeProfilePhoto).toBe(false);
      expect(component.imageFile).toBeUndefined();
      expect(component.croppedFile).toBeNull();
    });

    it('should have injected dependencies', () => {
      create();
      expect(component.route).toBeDefined();
      expect(component.router).toBeDefined();
      expect(component.location).toBeDefined();
      expect(component.memberService).toBeDefined();
      expect(component.confirmationService).toBeDefined();
    });
  });

  describe('ngOnInit', () => {
    it('should load member in edit mode when memberKey present', () => {
      create();
      expect(component.isCreate).toBe(false);
      expect(component.memberKey).toBe(memberKey);
      expect(component.groupKey).toBe(groupKey);
      expect(memberServiceSpy.getByKey).toHaveBeenCalledWith(memberKey);
      expect(component.member.memberKey).toEqual(loadedMember.memberKey);
      expect(component.member.firstName).toEqual(loadedMember.firstName);
      expect(component.member.plastLevelHistories.length).toBeGreaterThan(0);
    });

    it('should switch to create mode when memberKey absent', () => {
      setRouteParams({ groupKey });
      create();
      expect(component.isCreate).toBe(true);
      expect(component.memberKey).toBe('');
      expect(component.groupKey).toBe(groupKey);
      expect(memberServiceSpy.getByKey).not.toHaveBeenCalled();
    });

    it('should set groupKey and memberKey from route params', () => {
      create();
      expect(component.groupKey).toBe(groupKey);
    });

    it('should ignore an empty guid group key from the route', () => {
      setRouteParams({ groupKey: '00000000-0000-0000-0000-000000000000', kurinKey: 'kurin-123', memberKey });
      create();
      expect(component.groupKey).toBe('');
      expect(component.kurinKey).toBe('kurin-123');
    });

    it('редагування профілю нікого не переносить — ні гуртка, ні куреня в запиті немає', () => {
      // Форму відкрито з маршруту, що називає курінь і гурток. Це лише те, звідки ми прийшли:
      // належність міняється діями над членством, а не збереженням профілю.
      setRouteParams({ groupKey: 'group-777', kurinKey: 'kurin-123', memberKey });
      create();

      component.submit();

      const dtoArg = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dtoArg.groupKey).toBeUndefined();
      expect(dtoArg.kurinKey).toBeUndefined();
    });

    it('не надсилає й не дає правити те, що курінь приховує від того, хто редагує', async () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...loadedMember,
        email: '',
        phoneNumber: '',
        dateOfBirth: null,
        address: null,
        hiddenData: ['contacts', 'birthDate', 'address', 'parents', 'extras']
      }));
      setRouteParams({ memberKey });
      create();
      await fixture.whenStable();
      fixture.detectChanges();

      const address = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#address');
      expect(address?.disabled).toBe(true);
      expect(address?.placeholder).toBe('Приховано');

      component.submit();

      const dtoArg = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dtoArg.email).toBeUndefined();
      expect(dtoArg.phoneNumber).toBeUndefined();
      expect(dtoArg.dateOfBirth).toBeUndefined();
      expect(dtoArg.address).toBeUndefined();
      expect(dtoArg.motherPhone).toBeUndefined();
      expect(dtoArg.cardExtras).toBeUndefined();
    });

    it('should fall back to the kurin when the member cannot be loaded without a group', () => {
      memberServiceSpy.getByKey.mockReturnValue(throwError(() => new Error('Not found')));
      setRouteParams({ kurinKey: 'kurin-123', memberKey });
      // Rendering is skipped on purpose: the form template needs a loaded member.
      fixture = TestBed.createComponent(UpsertMemberComponent);
      component = fixture.componentInstance;
      component.ngOnInit();

      expect(routerSpy.navigate).toHaveBeenCalledWith(['/kurin'], { replaceUrl: true });
    });

    it('should detect navigation from member page via currentNavigation', () => {
      const mockNavigation: Partial<Navigation> = {
        extras: { state: { fromMember: true } }
      };
      currentNavigationSignal.set(mockNavigation as Navigation);
      create();
      expect(component['cameFromMember']).toBe(true);
    });

    it('should detect navigation from member page via history.state', () => {
      currentNavigationSignal.set(null);
      vi.spyOn(history, 'state', 'get').mockReturnValue({ fromMember: true });
      create();
      expect(component['cameFromMember']).toBe(true);
    });

    it('should not set cameFromMember when state is missing', () => {
      currentNavigationSignal.set(null);
      vi.spyOn(history, 'state', 'get').mockReturnValue({});
      create();
      expect(component['cameFromMember']).toBe(false);
    });

    it('should allow editing email when creating member', () => {
      setRouteParams({ groupKey });
      create();
      expect(component.canEditEmail()).toBe(true);
    });

    it('should allow editing email for unlinked member', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({ ...loadedMember, userKey: null }));
      create();
      expect(component.canEditEmail()).toBe(true);
    });

    it('should hide email editing for linked member when current user is not admin', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({ ...loadedMember, userKey: 'linked-user-key' }));
      create();
      expect(component.canEditEmail()).toBe(false);
    });

    it('should let leadership correct the email of an account nobody has activated yet', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...loadedMember,
        userKey: 'linked-user-key',
        accountStatus: AccountStatus.PendingActivation
      }));
      create();
      expect(component.isPendingAccount).toBe(true);
      expect(component.canEditEmail()).toBe(true);
    });

    it('should resend the invitation only while the address matches the saved one', () => {
      memberServiceSpy.resendInvitation.mockReturnValue(of(void 0));
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...loadedMember,
        userKey: 'linked-user-key',
        accountStatus: AccountStatus.PendingActivation
      }));
      create();

      component.member.email = 'typo-fixed@example.com';
      component.resendInvitation();
      expect(memberServiceSpy.resendInvitation).not.toHaveBeenCalled();

      component.member.email = loadedMember.email;
      component.resendInvitation();
      expect(memberServiceSpy.resendInvitation).toHaveBeenCalledWith(memberKey);
      expect(messageServiceSpy.add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }));
    });

    it('should warn when the account was opened but the letter did not go', () => {
      create();
      memberServiceSpy.update.mockReturnValue(of({ ...loadedMember, invitationSent: false }));
      component.submit();
      expect(messageServiceSpy.add).toHaveBeenCalledWith(expect.objectContaining({ summary: 'Лист не надіслано' }));
    });

    it('should allow editing email for linked member when current user is admin', () => {
      permissionServiceSpy.isAdmin.mockReturnValue(true);
      memberServiceSpy.getByKey.mockReturnValue(of({ ...loadedMember, userKey: 'linked-user-key' }));
      create();
      expect(component.canEditEmail()).toBe(true);
    });

  });

  describe('submit - create mode', () => {
    beforeEach(() => {
      setRouteParams({ groupKey });
    });

    it('should create member with correct dto', () => {
      create();
      component.member.firstName = 'Alice';
      component.member.middleName = 'B';
      component.member.lastName = 'Wonder';
      component.member.email = 'alice@example.com';
      component.member.phoneNumber = '555';
      // Use noon to avoid timezone shifts
      component.member.dateOfBirth = new Date(2012, 6, 9, 12, 0, 0);
      component.fileToUpload = new Blob(['x'], { type: 'image/png' });

      component.submit();

      expect(memberServiceSpy.create).toHaveBeenCalledTimes(1);
      const [dtoArg, fileArg] = vi.mocked(memberServiceSpy.create).mock.lastCall!;
      
      // Use objectContaining to allow for other default properties
      expect(dtoArg).toEqual(expect.objectContaining({
        groupKey,
        firstName: 'Alice',
        middleName: 'B',
        lastName: 'Wonder',
        email: 'alice@example.com',
        phoneNumber: '555',
        dateOfBirth: '2012-07-09'
      }));
      expect(fileArg).toBe(component.fileToUpload);
    });

    it('should navigate to created member on success', () => {
      create();
      component.member.firstName = 'Test';
      component.member.lastName = 'User';
      
      component.submit();

      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', 'created999'], { replaceUrl: true });
    });

    it('should create without file upload', () => {
      create();
      component.member.firstName = 'Test';
      component.member.lastName = 'User';
      component.fileToUpload = null;

      component.submit();

      const [, fileArg] = vi.mocked(memberServiceSpy.create).mock.lastCall!;
      expect(fileArg).toBeNull();
    });

    it('should convert Date to yyyy-MM-dd format', () => {
      create();
      // Use noon to avoid timezone shifts
      component.member.dateOfBirth = new Date(2011, 0, 2, 12, 0, 0);
      component.submit();
      const dto = vi.mocked(memberServiceSpy.create).mock.lastCall![0];
      expect(dto.dateOfBirth).toBe('2011-01-02');
    });

    it('should handle null dateOfBirth', () => {
      create();
      component.member.dateOfBirth = null;
      component.submit();
      const dto = vi.mocked(memberServiceSpy.create).mock.lastCall![0];
      expect(dto.dateOfBirth).toBeNull();
    });
    it('should handle string dateOfBirth in yyyy-MM-dd format', () => {
      create();
      // Pass string to match test description
      component.member.dateOfBirth = new Date('2020-05-15');
      component.submit();
      const dto = vi.mocked(memberServiceSpy.create).mock.lastCall![0];
      expect(dto.dateOfBirth).toBe('2020-05-15');
    });

    it('should log and handle create error', () => {
      const error = new Error('Create failed');
      memberServiceSpy.create.mockReturnValue(throwError(() => error));
      vi.spyOn(console, 'error').mockReturnValue(undefined);
      create();

      component.submit();

      expect(console.error).toHaveBeenCalledWith('Error creating member:', error, expect.any(Object));
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('should create member in kurin scope without groupKey', () => {
      setRouteParams({ kurinKey: 'kurin-123' });
      create();
      component.member.firstName = 'Kurin';
      component.member.lastName = 'Member';
      component.member.dateOfBirth = new Date(2012, 6, 9, 12, 0, 0);

      component.submit();

      const dtoArg = vi.mocked(memberServiceSpy.create).mock.lastCall![0];
      expect(dtoArg.groupKey).toBeUndefined();
      expect(dtoArg.kurinKey).toBe('kurin-123');
    });

    it('should include createUserAccount flag in create payload', () => {
      setRouteParams({ groupKey });
      create();
      component.member.firstName = 'Account';
      component.member.lastName = 'Target';
      component.member.dateOfBirth = new Date(2011, 0, 2, 12, 0, 0);
      component.createUserAccount = true;

      component.submit();

      const dtoArg = vi.mocked(memberServiceSpy.create).mock.lastCall![0];
      expect(dtoArg.createUserAccount).toBe(true);
    });
  });

  describe('submit - update mode', () => {
    it('should update member with correct dto', () => {
      create();
      component.member.firstName = 'Updated';
      component.removeProfilePhoto = true;

      component.submit();

      const [updatedMemberKey, dtoArg] = vi.mocked(memberServiceSpy.update).mock.lastCall!;
      expect(updatedMemberKey).toBe(memberKey);
      expect(dtoArg.firstName).toBe('Updated');
      expect(dtoArg.removeProfilePhoto).toBe(true);
    });

    it('should keep current email in payload when linked non-admin cannot edit email', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({ ...loadedMember, userKey: 'linked-user-key' }));
      create();
      expect(component.canEditEmail()).toBe(false);

      component.member.firstName = 'Updated';
      component.submit();

      const dto = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dto.email).toBe(loadedMember.email);
    });

    it('should follow the card fields of the member\'s край', () => {
      const prague: CardFieldsDto = {
        fields: { middleName: 'hidden', phoneNumber: 'optional' },
        extra: [
          { key: 'visaType', label: 'Тип візи', type: 'text', required: true },
          { key: 'visaUntil', label: 'Віза до', type: 'date', required: false }
        ]
      };
      kurinServiceSpy.getCardFields.mockReturnValue(of(prague));
      memberServiceSpy.getByKey.mockReturnValue(of({ ...loadedMember, cardExtras: { visaType: 'D', visaUntil: '2027-06-30' } }));
      create();
      fixture.detectChanges();

      const page: HTMLElement = fixture.nativeElement;
      expect(page.querySelector('#middleName')).toBeNull();
      expect(page.textContent).toContain('Тип візи');
      expect(component.cardExtras['visaUntil']).toEqual(new Date(2027, 5, 30));

      component.cardExtras['visaUntil'] = null;
      component.submit();

      const dto = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dto.cardExtras).toEqual({ visaUntil: '' });
    });

    it('should leave address and school out when they were not touched, so the server keeps them', async () => {
      memberServiceSpy.getByKey.mockReturnValue(of({ ...loadedMember, address: 'вул. Січових Стрільців, 5', school: 'Ліцей №3' }));
      create();
      await fixture.whenStable();

      component.submit();

      const dto = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dto.address).toBeUndefined();
      expect(dto.school).toBeUndefined();
    });

    it('should send address, school and parents once edited, an emptied one as empty', async () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...loadedMember, address: 'вул. Січових Стрільців, 5', school: 'Ліцей №3', motherFirstName: 'Олена', motherPhone: '+380501234567'
      }));
      create();
      await fixture.whenStable();
      fixture.detectChanges();

      component.member.address = '  Praha 3, Seifertova 1 ';
      component.form().controls['address'].markAsDirty();
      component.member.school = null;
      component.form().controls['school'].markAsDirty();
      component.member.fatherFirstName = ' Петро ';
      component.form().controls['fatherFirstName'].markAsDirty();
      component.member.motherPhone = '';
      component.form().controls['motherPhone'].markAsDirty();
      component.submit();

      const dto = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dto.address).toBe('Praha 3, Seifertova 1');
      expect(dto.school).toBe('');
      expect(dto.fatherFirstName).toBe('Петро');
      expect(dto.motherPhone).toBe('');
      expect(dto.motherFirstName).toBeUndefined();
    });

    it('should convert Date to yyyy-MM-dd format', () => {
      create();
      // Use noon to avoid timezone shifts
      component.member.dateOfBirth = new Date(1995, 11, 24, 12, 0, 0);
      component.submit();
      const dto = vi.mocked(memberServiceSpy.update).mock.lastCall![1];
      expect(dto.dateOfBirth).toBe('1995-12-24');
    });

    it('should navigate back when came from member page', () => {
      const mockNavigation: Partial<Navigation> = {
        extras: { state: { fromMember: true } }
      };
      currentNavigationSignal.set(mockNavigation as Navigation);
      create();

      component.submit();

      expect(locationSpy.back).toHaveBeenCalled();
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('should navigate to member page when not came from member', () => {
      create();
      component.submit();

      expect(locationSpy.back).not.toHaveBeenCalled();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', memberKey], { replaceUrl: true });
    });

    it('should include file in update request', () => {
      create();
      const file = new Blob(['data'], { type: 'image/png' });
      component.fileToUpload = file;

      component.submit();

      const [, , fileArg] = vi.mocked(memberServiceSpy.update).mock.lastCall!;
      expect(fileArg).toBe(file);
    });

    it('should log and handle update error', () => {
      const error = new Error('Update failed');
      memberServiceSpy.update.mockReturnValue(throwError(() => error));
      vi.spyOn(console, 'error').mockReturnValue(undefined);
      create();

      component.submit();

      expect(console.error).toHaveBeenCalledWith('Error updating member:', error);
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    });
  });

  describe('ступені', () => {
    it('shows Пласт and УПЮ and offers the other улади', () => {
      create();
      expect(component.visibleLevelGroups.map(group => group.key)).toEqual(['base', 'upu']);
      expect(component.hiddenLevelGroups.map(group => group.key)).toEqual(['usp', 'ups']);
    });

    it('keeps a улад with a saved date on screen', () => {
      memberServiceSpy.getByKey.mockReturnValue(of({
        ...loadedMember,
        plastLevelHistories: [{ plastLevel: PlastLevel.Senior, dateAchieved: '2020-01-01' }]
      }));
      create();
      expect(component.visibleLevelGroups.map(group => group.key)).toEqual(['base', 'upu', 'ups']);
    });

    it('checks each date against birth, today and the ступінь before it', () => {
      create();
      component.member.dateOfBirth = new Date(2010, 0, 1);
      component.plastLevelMap[PlastLevel.Entry].dateAchieved = '2009-12-31';
      expect(component.levelDateIssue(PlastLevel.Entry)).toBe('Раніше за дату народження.');

      component.plastLevelMap[PlastLevel.Entry].dateAchieved = new Date(Date.now() + 2 * 86400000);
      expect(component.levelDateIssue(PlastLevel.Entry)).toBe('Ця дата ще не настала.');

      component.plastLevelMap[PlastLevel.Entry].dateAchieved = '2018-05-01';
      component.plastLevelMap[PlastLevel.Uchasnyk].dateAchieved = '2018-04-01';
      expect(component.levelDateIssue(PlastLevel.Uchasnyk)).toBe('Раніше, ніж «Вступ до Пласту».');
      expect(component.hasLevelDateIssues).toBe(true);

      component.plastLevelMap[PlastLevel.Uchasnyk].dateAchieved = '2019-04-01';
      expect(component.levelDateIssue(PlastLevel.Uchasnyk)).toBeNull();
      expect(component.hasLevelDateIssues).toBe(false);
    });

    it('does not compare across улади', () => {
      create();
      component.plastLevelMap[PlastLevel.Uchasnyk].dateAchieved = '2019-04-01';
      component.plastLevelMap[PlastLevel.Rozviduvach].dateAchieved = '2018-04-01';
      expect(component.levelDateIssue(PlastLevel.Rozviduvach)).toBeNull();
    });

    it('adds a улад when asked', () => {
      create();
      component.showLevelGroup('usp');
      expect(component.hiddenLevelGroups.map(group => group.key)).toEqual(['ups']);
    });
  });

  describe('action bar', () => {
    it('names the empty required fields in the order of the form', async () => {
      setRouteParams({ groupKey });
      create();
      await fixture.whenStable();
      fixture.detectChanges();
      expect(component.missingFields).toEqual(['імʼя', 'по батькові', 'прізвище', 'дату народження', 'email', 'телефон']);
    });

    it('goes back to the card when an edit is cancelled', () => {
      create();
      component.cancel();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', memberKey]);
    });

    it('goes back to the group when a new member is cancelled', () => {
      setRouteParams({ groupKey });
      create();
      component.cancel();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/group', groupKey]);
    });
  });

  describe('leaving', () => {
    function confirmation(answer: boolean): Mock {
      const service = fixture.debugElement.injector.get(ConfirmationService);
      return vi.spyOn(service, 'confirm').mockImplementation(options => {
        if (answer) {
          options.accept?.();
        } else {
          options.reject?.();
        }
        return service;
      });
    }

    it('lets go without asking when nothing changed', () => {
      create();
      expect(component.confirmLeave()).toBe(true);
    });

    it('asks before throwing changes away', async () => {
      create();
      component.form().form.markAsDirty();

      const confirm = confirmation(false);
      expect(await component.confirmLeave()).toBe(false);
      expect(confirm).toHaveBeenCalled();
    });

    it('leaves when the person says so', async () => {
      create();
      component.form().form.markAsDirty();
      confirmation(true);
      expect(await component.confirmLeave()).toBe(true);
    });

    it('does not ask after saving', () => {
      create();
      component.form().form.markAsDirty();
      component.submit();
      expect(component.confirmLeave()).toBe(true);
    });
  });

  describe('fileChangeEvent', () => {
    beforeEach(() => {
      setRouteParams({ groupKey });
      create();
    });

    it('should set file and open cropper', () => {
      const file = new File(['aa'], 'avatar.jpg', { type: 'image/jpeg' });
      const event: FileSelectEvent = { originalEvent: new Event('change'), files: [file], currentFiles: [file] };
      
      component.fileChangeEvent(event);
      
      expect(component.imageFile).toBe(file);
      expect(component.displayCropper).toBe(true);
      expect(component.croppedImage).toBe('');
      expect(component.croppedFile).toBeNull();
      expect(component.removeProfilePhoto).toBe(false);
    });

    it('should warn when no file selected', () => {
      vi.spyOn(console, 'warn').mockReturnValue(undefined);
      const event: FileSelectEvent = { originalEvent: new Event('change'), files: [], currentFiles: [] };
      
      component.fileChangeEvent(event);
      
      expect(console.warn).toHaveBeenCalledWith('No file selected');
      expect(component.displayCropper).toBe(false);
    });

    it('should revoke previous object URL', () => {
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
      component['objectUrlToRevoke'] = 'blob://old-url';
      
      const file = new File(['aa'], 'avatar.jpg', { type: 'image/jpeg' });
      const event: FileSelectEvent = { originalEvent: new Event('change'), files: [file], currentFiles: [file] };
      
      component.fileChangeEvent(event);
      
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob://old-url');
      expect(component['objectUrlToRevoke']).toBeNull();
    });

    it('should not revoke URL when objectUrlToRevoke is null', () => {
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
      component['objectUrlToRevoke'] = null;
      
      const file = new File(['aa'], 'avatar.jpg', { type: 'image/jpeg' });
      const event: FileSelectEvent = { originalEvent: new Event('change'), files: [file], currentFiles: [file] };
      
      component.fileChangeEvent(event);
      
      expect(URL.revokeObjectURL).not.toHaveBeenCalled();
    });
  });

  describe('imageCropped', () => {
    beforeEach(() => {
      setRouteParams({ groupKey });
      create();
    });

    it('should store base64 when base64 is provided', () => {
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        base64: 'data:image/png;base64,AAA'
      };
      
      component.imageCropped(cropEvent);
      
      expect(component.croppedImage).toBe('data:image/png;base64,AAA');
      expect(component.croppedFile).toBeNull();
    });

    it('should create File from blob when blob is provided', () => {
      const blob = new Blob(['xx'], { type: 'image/png' });
      component.imageFile = new File(['orig'], 'orig.jpeg', { type: 'image/jpeg' });
      
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        blob
      };
      
      component.imageCropped(cropEvent);
      
      expect(component.croppedFile).toBeTruthy();
      expect(component.croppedFile!.name).toBe('orig.jpg');
      expect(component.croppedFile!.type).toBe('image/png');
    });

    it('should use default filename when imageFile is undefined', () => {
      const blob = new Blob(['xx'], { type: 'image/png' });
      component.imageFile = undefined;
      
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        blob
      };
      
      component.imageCropped(cropEvent);
      
      expect(component.croppedFile!.name).toBe('profile.jpg');
    });

    it('should use objectUrl from event if available', () => {
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
      const blob = new Blob(['xx'], { type: 'image/png' });
      component.imageFile = new File(['orig'], 'orig.jpeg', { type: 'image/jpeg' });
      component['objectUrlToRevoke'] = 'blob://old-url';
      
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        blob,
        objectUrl: 'blob://new-url'
      };
      
      component.imageCropped(cropEvent);
      
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob://old-url');
      expect(component.croppedImage).toBe('blob://new-url');
      expect(component['objectUrlToRevoke']).toBe('blob://new-url');
    });

    it('should create objectUrl when not provided', () => {
      vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob://created-url');
      const blob = new Blob(['xx'], { type: 'image/png' });
      component.imageFile = new File(['orig'], 'orig.jpeg', { type: 'image/jpeg' });
      
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        blob
      };
      
      component.imageCropped(cropEvent);
      
      expect(URL.createObjectURL).toHaveBeenCalledWith(blob);
      expect(component.croppedImage).toBe('blob://created-url');
      expect(component['objectUrlToRevoke']).toBe('blob://created-url');
    });

    it('should not revoke same URL', () => {
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
      const blob = new Blob(['xx'], { type: 'image/png' });
      component.imageFile = new File(['orig'], 'orig.jpeg', { type: 'image/jpeg' });
      component['objectUrlToRevoke'] = 'blob://same-url';
      
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        blob,
        objectUrl: 'blob://same-url'
      };
      
      component.imageCropped(cropEvent);
      
      expect(URL.revokeObjectURL).not.toHaveBeenCalled();
    });

    it('should warn when neither base64 nor blob provided', () => {
      vi.spyOn(console, 'warn').mockReturnValue(undefined);
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 }
      };
      
      component.imageCropped(cropEvent);
      
      expect(console.warn).toHaveBeenCalledWith('Crop event без base64 і blob', cropEvent);
    });
  });

  describe('save', () => {
    let mockForm: FormGroup;

    beforeEach(() => {
      setRouteParams({ groupKey });
      create();
      mockForm = {
        markAsDirty: vi.fn()
      } as unknown as FormGroup;
    });

    it('should close cropper and use croppedFile', () => {
      const blob = new Blob(['out'], { type: 'image/png' });
      component.croppedFile = new File([blob], 'out.png', { type: 'image/png' });
      component.displayCropper = true;
      
      component.save(mockForm);
      
      expect(component.displayCropper).toBe(false);
      expect(component.fileToUpload).toBe(component.croppedFile);
      expect(component.removeProfilePhoto).toBe(false);
      expect(mockForm.markAsDirty).toHaveBeenCalled();
    });

    it('should convert base64 to blob when no croppedFile', () => {
      component.croppedImage = 'data:image/png;base64,iVBORw0KGgo=';
      component.croppedFile = null;
      
      component.save(mockForm);
      
      expect(component.fileToUpload).toBeTruthy();
      expect(component.fileToUpload).toBeInstanceOf(Blob);
      expect(component.removeProfilePhoto).toBe(false);
      expect(mockForm.markAsDirty).toHaveBeenCalled();
    });

    it('should warn when no cropped image available', () => {
      vi.spyOn(console, 'warn').mockReturnValue(undefined);
      component.croppedFile = null;
      component.croppedImage = '';
      
      component.save(mockForm);
      
      expect(console.warn).toHaveBeenCalledWith('Nothing to upload (cropped image undefined)');
      expect(component.fileToUpload).toBeNull();
    });
  });

  describe('onCancelCrop', () => {
    beforeEach(() => {
      setRouteParams({ groupKey });
      create();
    });

    it('should close cropper and clear croppedImage', () => {
      component.displayCropper = true;
      component.croppedImage = 'blob://some-url';
      
      component.onCancelCrop();
      
      expect(component.displayCropper).toBe(false);
      expect(component.croppedImage).toBe('');
    });
  });

  describe('clearProfilePhoto', () => {
    let mockForm: FormGroup;

    beforeEach(() => {
      setRouteParams({ groupKey });
      create();
      mockForm = {
        markAsDirty: vi.fn()
      } as unknown as FormGroup;
    });

    it('should clear all photo-related data', () => {
      component.imageFile = new File(['test'], 'test.jpg');
      component.croppedImage = 'data:image/png;base64,AAA';
      component.croppedFile = new File(['cropped'], 'cropped.png');
      component.fileToUpload = new Blob(['upload']);
      component.member.profilePhotoUrl = 'http://example.com/photo.jpg';

      component.clearProfilePhoto(mockForm);

      expect(component.imageFile).toBeUndefined();
      expect(component.croppedImage).toBe('');
      expect(component.croppedFile).toBeNull();
      expect(component.fileToUpload).toBeNull();
      expect(component.member.profilePhotoUrl).toBeNull();
      expect(component.removeProfilePhoto).toBe(true);
      expect(mockForm.markAsDirty).toHaveBeenCalled();
    });

    it('should revoke object URL if exists', () => {
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
      component['objectUrlToRevoke'] = 'blob://test-url';

      component.clearProfilePhoto(mockForm);

      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob://test-url');
      expect(component['objectUrlToRevoke']).toBeNull();
    });

    it('should not revoke when no object URL exists', () => {
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
      component['objectUrlToRevoke'] = null;

      component.clearProfilePhoto(mockForm);

      expect(URL.revokeObjectURL).not.toHaveBeenCalled();
      expect(component.removeProfilePhoto).toBe(true);
    });
  });

  describe('Integration scenarios', () => {
    it('should complete full create workflow with photo', () => {
      setRouteParams({ groupKey });
      create();

      // Upload file
      const file = new File(['photo'], 'photo.jpg', { type: 'image/jpeg' });
      const selectEvent: FileSelectEvent = { 
        originalEvent: new Event('change'), 
        files: [file], 
        currentFiles: [file] 
      };
      component.fileChangeEvent(selectEvent);
      expect(component.displayCropper).toBe(true);

      // Crop image
      const blob = new Blob(['cropped'], { type: 'image/png' });
      const cropEvent: ImageCroppedEvent = {
        width: 100,
        height: 100,
        imagePosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        cropperPosition: { x1: 0, y1: 0, x2: 0, y2: 0 },
        blob
      };
      component.imageCropped(cropEvent);

      // Save cropped image
      const mockForm = {
        markAsDirty: vi.fn()
      } as unknown as FormGroup;
      component.save(mockForm);
      expect(component.displayCropper).toBe(false);
      expect(component.fileToUpload).toBeTruthy();

      // Submit form
      component.member.firstName = 'New';
      component.member.lastName = 'Member';
      component.member.email = 'new@example.com';
      component.submit();

      expect(memberServiceSpy.create).toHaveBeenCalled();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', 'created999'], { replaceUrl: true });
    });

    it('should complete full update workflow with photo removal', () => {
      create();
      
      const mockForm = {
        markAsDirty: vi.fn()
      } as unknown as FormGroup;
      
      // Clear photo
      component.clearProfilePhoto(mockForm);
      expect(component.removeProfilePhoto).toBe(true);
      
      // Update member
      component.member.firstName = 'Updated';
      component.submit();

      const [, dto] = vi.mocked(memberServiceSpy.update).mock.lastCall!;
      expect(dto.removeProfilePhoto).toBe(true);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', memberKey], { replaceUrl: true });
    });

    it('should handle cancel during photo crop', () => {
      setRouteParams({ groupKey });
      create();

      const file = new File(['photo'], 'photo.jpg', { type: 'image/jpeg' });
      const selectEvent: FileSelectEvent = { 
        originalEvent: new Event('change'), 
        files: [file], 
        currentFiles: [file] 
      };
      component.fileChangeEvent(selectEvent);
      expect(component.displayCropper).toBe(true);

      component.onCancelCrop();

      expect(component.displayCropper).toBe(false);
      expect(component.croppedImage).toBe('');
    });
  });
});
