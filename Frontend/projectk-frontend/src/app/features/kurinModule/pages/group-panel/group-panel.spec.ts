import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GroupPanelComponent } from './group-panel';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, of, Subject } from 'rxjs';
import { MemberService } from '../../services/member-service/member.service';
import { GroupService } from '../../services/group-service/group.service';
import { LeadershipService } from '../../services/leadership-service/leadership.service';
import { GroupDto } from '../../models/group.dto';
import { LeadershipDto } from '../../models/requests/leadership/leadership.dto';
import { MemberDto } from '../../models/member.dto';
import { EntityService } from '../../../authModule/services/entity-service/entity.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';

describe('GroupPanelComponent', () => {
  let fixture: ComponentFixture<GroupPanelComponent>;
  let component: GroupPanelComponent;

  let memberServiceSpy: MockedObject<MemberService>;
  let groupServiceSpy: MockedObject<GroupService>;
  let leadershipServiceSpy: MockedObject<LeadershipService>;
  let entityServiceSpy: MockedObject<EntityService>;
  let permissionServiceSpy: MockedObject<PermissionService>;
  let routerSpy: MockedObject<Router>;
  let paramMapSubject: BehaviorSubject<ParamMap>;

  const groupKey = 'group1';
  const group: GroupDto = {
    groupKey,
    name: 'Test Group',
    description: 'Test group description',
    silhouetteUrl: null,
    kurinKey: 'kurin1',
    kurinNumber: 1
  };

  const leadership: LeadershipDto = {
    leadershipKey: 'l1',
    startDate: '2026-01-01',
    endDate: null,
    leadershipHistories: []
  };

  beforeEach(async () => {
    memberServiceSpy = {
      getAll: vi.fn(),
      getMentorCandidates: vi.fn()
    } as MockedObject<MemberService>;
    groupServiceSpy = {
      getByKey: vi.fn(),
      exists: vi.fn(),
      getMentors: vi.fn(),
      assignMentor: vi.fn(),
      revokeMentor: vi.fn(),
      update: vi.fn(),
      uploadSilhouette: vi.fn(),
      deleteSilhouette: vi.fn()
    } as MockedObject<GroupService>;
    leadershipServiceSpy = {
      getLeadershipByTypeAndKey: vi.fn()
    } as MockedObject<LeadershipService>;
    entityServiceSpy = {
      checkEntityAccess: vi.fn()
    } as MockedObject<EntityService>;
    permissionServiceSpy = {
      canManageMentors: vi.fn(),
      canSetupLeadership: vi.fn()
    } as MockedObject<PermissionService>;
    routerSpy = {
      navigate: vi.fn()
    } as MockedObject<Router>;
    paramMapSubject = new BehaviorSubject(convertToParamMap({ groupKey }));

    groupServiceSpy.exists.mockReturnValue(of(true));
    groupServiceSpy.getByKey.mockReturnValue(of(group));
    groupServiceSpy.getMentors.mockReturnValue(of([]));
    groupServiceSpy.assignMentor.mockReturnValue(of({}));
    groupServiceSpy.revokeMentor.mockReturnValue(of({}));
    groupServiceSpy.update.mockReturnValue(of(group));
    groupServiceSpy.uploadSilhouette.mockReturnValue(of(group));
    groupServiceSpy.deleteSilhouette.mockReturnValue(of(group));
    memberServiceSpy.getAll.mockReturnValue(of([]));
    memberServiceSpy.getMentorCandidates.mockReturnValue(of([]));
    leadershipServiceSpy.getLeadershipByTypeAndKey.mockReturnValue(of(leadership));
    entityServiceSpy.checkEntityAccess.mockReturnValue(of(true));
    permissionServiceSpy.canManageMentors.mockReturnValue(true);
    permissionServiceSpy.canSetupLeadership.mockReturnValue(true);

    await TestBed.configureTestingModule({
      imports: [GroupPanelComponent],
      providers: [
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: GroupService, useValue: groupServiceSpy },
        { provide: LeadershipService, useValue: leadershipServiceSpy },
        { provide: EntityService, useValue: entityServiceSpy },
        { provide: PermissionService, useValue: permissionServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: { paramMap: paramMapSubject.asObservable() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(GroupPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load group details on init', () => {
    expect(groupServiceSpy.getByKey).toHaveBeenCalledWith(groupKey);
    expect(component.group).toEqual(group);
    expect(component.profileForm.get('description')?.value).toBe('Test group description');
  });

  it('should navigate to panel if group does not exist', () => {
    groupServiceSpy.exists.mockReturnValue(of(false));
    component.refreshData();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/panel'], { replaceUrl: true });
  });

  it('onMemberCreate should navigate to upsert route', () => {
    component.onMemberCreate();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/group', groupKey, 'member', 'upsert']);
  });

  it('onMemberSelect should navigate to member card', () => {
    component.selectedMember = { memberKey: 'm1' } as unknown as MemberDto;
    component.onMemberSelect();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/member', 'm1']);
  });

  it('openMentorDialog should load management data and open dialog', () => {
    component.openMentorDialog();

    expect(memberServiceSpy.getMentorCandidates).toHaveBeenCalledWith(group.kurinKey);
    expect(groupServiceSpy.getMentors).toHaveBeenCalledWith(groupKey);
    expect(component.mentorDialogVisible).toBe(true);
  });

  it('saveProfile should update group description', () => {
    const updated = { ...group, description: 'Updated description' };
    groupServiceSpy.update.mockReturnValue(of(updated));

    component.startProfileEdit();
    component.profileForm.patchValue({ description: '  Updated description  ' });
    component.saveProfile();

    expect(groupServiceSpy.update).toHaveBeenCalledWith(groupKey, {
      name: 'Test Group',
      description: 'Updated description'
    });
    expect(component.group).toEqual(updated);
    expect(component.profileEditMode).toBe(false);
  });

  it('should collapse long description until toggled', () => {
    component.group = {
      ...group,
      description: 'A'.repeat(component.descriptionCollapseLimit + 1)
    };

    expect(component.isDescriptionLong).toBe(true);
    expect(component.descriptionExpanded).toBe(false);

    component.toggleDescription();

    expect(component.descriptionExpanded).toBe(true);
  });

  // Built on the first answer alone, the menu opened without «Редагувати профіль» for whoever
  // clicked at once; the page now waits for both rights and offers the menu only then.
  it('should offer the action menu only once both access answers are in', () => {
    const create = new Subject<boolean>();
    const update = new Subject<boolean>();
    entityServiceSpy.checkEntityAccess.mockImplementation((_type: string, _key: string, action?: string) =>
      action === 'Create' ? create.asObservable() : update.asObservable());

    component.refreshData();
    expect(component.accessResolved).toBe(false);

    create.next(true);
    create.complete();
    expect(component.accessResolved).toBe(false);
    expect(component.canEditGroupProfile).toBe(false);

    update.next(true);
    update.complete();
    expect(component.accessResolved).toBe(true);
    expect(component.canCreateMembers).toBe(true);
    expect(component.canEditGroupProfile).toBe(true);
  });

  it('groupEditMenuItems should expose permitted actions in one menu', () => {
    component.group = group;
    component.canEditGroupProfile = true;
    component.canCreateMembers = true;
    permissionServiceSpy.canManageMentors.mockReturnValue(true);

    expect(component.groupEditMenuItems.map(item => item.label)).toEqual([
      'Редагувати профіль',
      'Додати учасника',
      'Впорядники',
      'Завантажити сильветку'
    ]);

    component.group = { ...group, silhouetteUrl: 'group-silhouettes/2026/05/27/test.png' };
    expect(component.groupEditMenuItems.map(item => item.label)).toEqual([
      'Редагувати профіль',
      'Додати учасника',
      'Впорядники',
      'Замінити сильветку',
      'Видалити сильветку'
    ]);
  });

  it('groupEditMenuItems should keep same reference until menu state changes', () => {
    component.group = group;
    component.canEditGroupProfile = true;
    component.canCreateMembers = true;
    permissionServiceSpy.canManageMentors.mockReturnValue(true);

    const first = component.groupEditMenuItems;
    const second = component.groupEditMenuItems;

    expect(second).toBe(first);

    component.group = { ...group, silhouetteUrl: 'group-silhouettes/2026/05/27/test.png' };

    expect(component.groupEditMenuItems).not.toBe(first);
  });

  it('onSilhouetteSelected should open editor for valid image', () => {
    const file = new File(['image'], 'silhouette.png', { type: 'image/png' });
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [file] });

    component.canEditGroupProfile = true;
    component.onSilhouetteSelected({ target: input } as unknown as Event);

    expect(groupServiceSpy.uploadSilhouette).not.toHaveBeenCalled();
    expect(component.silhouetteDialogVisible).toBe(true);
    expect(component.silhouetteImageFile).toBe(file);
    expect(component.silhouetteError).toBeNull();
  });

  it('onSilhouetteSelected should reject unsupported image type', () => {
    const file = new File(['image'], 'silhouette.gif', { type: 'image/gif' });
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [file] });

    component.canEditGroupProfile = true;
    component.onSilhouetteSelected({ target: input } as unknown as Event);

    expect(groupServiceSpy.uploadSilhouette).not.toHaveBeenCalled();
    expect(component.silhouetteError).toBe('Підтримуються лише PNG, JPEG або WebP.');
  });

  it('uploadOriginalSilhouette should send selected file and close editor', () => {
    const updated = { ...group, silhouetteUrl: 'group-silhouettes/2026/05/27/test.png' };
    const file = new File(['image'], 'silhouette.png', { type: 'image/png' });
    groupServiceSpy.uploadSilhouette.mockReturnValue(of(updated));
    component.silhouetteImageFile = file;
    component.silhouetteDialogVisible = true;

    component.uploadOriginalSilhouette();

    expect(groupServiceSpy.uploadSilhouette).toHaveBeenCalledWith(groupKey, file);
    expect(component.group).toEqual(updated);
    expect(component.silhouetteDialogVisible).toBe(false);
    expect(component.silhouetteSaving).toBe(false);
  });

  it('uploadProcessedSilhouette should send processed png file', () => {
    const updated = { ...group, silhouetteUrl: 'group-silhouettes/2026/05/27/test.png' };
    const sourceFile = new File(['image'], 'source.jpg', { type: 'image/jpeg' });
    const processed = new Blob(['png'], { type: 'image/png' });
    groupServiceSpy.uploadSilhouette.mockReturnValue(of(updated));
    component.silhouetteImageFile = sourceFile;
    component.silhouetteProcessedBlob = processed;
    component.silhouetteDialogVisible = true;

    component.uploadProcessedSilhouette();

    const uploaded = vi.mocked(groupServiceSpy.uploadSilhouette).mock.lastCall![1] as File;
    expect(groupServiceSpy.uploadSilhouette).toHaveBeenCalledWith(groupKey, expect.any(File));
    expect(uploaded.name).toBe('source.png');
    expect(uploaded.type).toBe('image/png');
    expect(component.group).toEqual(updated);
  });

  it('uploadProcessedSilhouette should require cropped image', () => {
    component.uploadProcessedSilhouette();

    expect(groupServiceSpy.uploadSilhouette).not.toHaveBeenCalled();
    expect(component.silhouetteError).toBe('Оберіть область зображення перед завантаженням.');
  });

  it('deleteSilhouette should delete existing image and update group', () => {
    const withSilhouette = { ...group, silhouetteUrl: 'group-silhouettes/2026/05/27/test.png' };
    const updated = { ...group, silhouetteUrl: null };
    groupServiceSpy.deleteSilhouette.mockReturnValue(of(updated));
    component.group = withSilhouette;
    component.canEditGroupProfile = true;

    component.deleteSilhouette();

    expect(groupServiceSpy.deleteSilhouette).toHaveBeenCalledWith(groupKey);
    expect(component.group).toEqual(updated);
    expect(component.silhouetteSaving).toBe(false);
  });

  it('saveMentorAssignments should call assign and revoke based on diff', () => {
    component.initialMentorUserKeys = ['u1', 'u2'];
    component.selectedMentorUserKeys = ['u2', 'u3'];
    component.mentorDialogVisible = true;

    component.saveMentorAssignments();

    expect(groupServiceSpy.assignMentor).toHaveBeenCalledWith(groupKey, 'u3');
    expect(groupServiceSpy.revokeMentor).toHaveBeenCalledWith(groupKey, 'u1');
    expect(component.mentorDialogVisible).toBe(false);
  });
});
