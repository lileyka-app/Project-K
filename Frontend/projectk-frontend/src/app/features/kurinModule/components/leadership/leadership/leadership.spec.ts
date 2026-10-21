import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LeadershipComponent } from './leadership';
import { ActivatedRoute, Router } from '@angular/router';
import { LeadershipService } from '../../../services/leadership-service/leadership.service';
import { MemberService } from '../../../services/member-service/member.service';
import { of, throwError, BehaviorSubject } from 'rxjs';
import { LeadershipRole } from '../../../models/enums/leadership-role.enum';
import { LeadershipDto } from '../../../models/requests/leadership/leadership.dto';
import { MemberDto } from '../../../models/member.dto';
import { ReactiveFormsModule } from '@angular/forms';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('LeadershipComponent', () => {
  let component: LeadershipComponent;
  let fixture: ComponentFixture<LeadershipComponent>;
  let leadershipServiceSpy: jasmine.SpyObj<LeadershipService>;
  let memberServiceSpy: jasmine.SpyObj<MemberService>;
  let routerSpy: jasmine.SpyObj<Router>;
  
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const paramMapSubject = new BehaviorSubject<any>({ get: () => null });

  const mockMember: MemberDto = {
    memberKey: 'm1', firstName: 'John', lastName: 'Doe', middleName: 'M',
    email: 'test@test.com', phoneNumber: '123', dateOfBirth: new Date('2000-01-01'),
    groupKey: 'g1', kurinKey: 'k1', plastLevelHistories: [], leadershipHistories: [],
    profilePhotoUrl: null
  };

  const mockLeadership: LeadershipDto = {
    leadershipKey: 'l1',
    type: 'kurin',
    entityKey: 'k1',
    startDate: '2023-01-01',
    endDate: null,
    leadershipHistories: [
      {
        leadershipHistoryKey: 'lh1',
        leadershipKey: 'l1',
        role: LeadershipRole.Kurinnuy,
        member: mockMember,
        startDate: '2023-01-01',
        endDate: null
      }
    ]
  };

  beforeEach(async () => {
    leadershipServiceSpy = jasmine.createSpyObj('LeadershipService', ['getLeadershipByKey', 'create', 'update']);
    memberServiceSpy = jasmine.createSpyObj('MemberService', ['getAll']);
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [
        LeadershipComponent, 
        ReactiveFormsModule,
        NoopAnimationsModule 
      ],
      providers: [
        { provide: LeadershipService, useValue: leadershipServiceSpy },
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMapSubject.asObservable()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LeadershipComponent);
    component = fixture.componentInstance;
    
    memberServiceSpy.getAll.and.returnValue(of([mockMember]));
    leadershipServiceSpy.getLeadershipByKey.and.returnValue(of(mockLeadership));
    leadershipServiceSpy.create.and.returnValue(of(mockLeadership));
    leadershipServiceSpy.update.and.returnValue(of(mockLeadership));
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should load data when leadershipKey is present', () => {
      paramMapSubject.next({
        get: (key: string) => key === 'leadershipKey' ? 'l1' : null
      });
      
      fixture.detectChanges(); 

      expect(leadershipServiceSpy.getLeadershipByKey).toHaveBeenCalledWith('l1');
      expect(component.leadershipKey).toBe('l1');
      expect(component.leadershipType).toBe('kurin'); 
      expect(memberServiceSpy.getAll).toHaveBeenCalled();
    });

    it('should initialize for creation when type and entityKey are present', () => {
      paramMapSubject.next({
        get: (key: string) => {
          if (key === 'type') return 'kurin';
          if (key === 'entityKey') return 'k1';
          return null;
        }
      });

      fixture.detectChanges();

      expect(component.leadershipType).toBe('kurin');
      expect(component.entityKey).toBe('k1');
      expect(memberServiceSpy.getAll).toHaveBeenCalled();
      expect(component.leadershipHistories.length).toBeGreaterThan(0);
    });

    it('should handle error when loading data fails', () => {
      paramMapSubject.next({
        get: (key: string) => key === 'leadershipKey' ? 'l1' : null
      });
      leadershipServiceSpy.getLeadershipByKey.and.returnValue(throwError(() => new Error('Error')));
      spyOn(console, 'error');

      fixture.detectChanges();

      expect(console.error).toHaveBeenCalled();
    });
  });

  describe('loadAllMembers', () => {
    it('should load members for group type', () => {
      component.leadershipType = 'group';
      component.entityKey = 'g1';
      
      component.loadAllMembers();

      expect(memberServiceSpy.getAll).toHaveBeenCalledWith('g1', undefined);
    });

    it('should load members for kurin type', () => {
      component.leadershipType = 'kurin';
      component.entityKey = 'k1';
      
      component.loadAllMembers();

      expect(memberServiceSpy.getAll).toHaveBeenCalledWith(undefined, 'k1');
    });

    it('should handle error in loadAllMembers', () => {
      component.leadershipType = 'kurin';
      memberServiceSpy.getAll.and.returnValue(throwError(() => new Error('Err')));
      spyOn(console, 'error');

      component.loadAllMembers();
      expect(console.error).toHaveBeenCalled();
    });
  });

  describe('patchForm', () => {
    beforeEach(() => {
      component.leadershipType = 'kurin';
    });

    it('should create history rows without form-level cadence dates', () => {
      const data = { ...mockLeadership };
      component.patchForm(data);

      expect(component.leadershipForm.get('startDate')).toBeNull();
      expect(component.leadershipHistories.length).toBeGreaterThan(0);
      expect(component.leadershipHistories.at(0).get('startDate')?.value).toEqual(new Date('2023-01-01'));
    });

    it('should sort histories with active rows before archived rows', () => {
      const h1 = { ...mockLeadership.leadershipHistories[0], endDate: '2023-02-01', startDate: '2023-01-01' };
      const h2 = { ...mockLeadership.leadershipHistories[0], endDate: null, startDate: '2023-03-01' };
      
      const data: LeadershipDto = {
        ...mockLeadership,
        leadershipHistories: [h2, h1] 
      };

      component.patchForm(data);

      const rows = component.leadershipHistories.controls
        .map(c => c.getRawValue())
        .filter(v => v.role === LeadershipRole.Kurinnuy);
      
      expect(rows[0].endDate).toBeFalsy(); // h2
      expect(rows[1].endDate).toBeTruthy(); // h1
    });

    it('should build default rows in fixed leadership role order', () => {
      component.buildFormRowsFromDefaults('kurin');

      const roles = component.leadershipHistories.controls.map(control => control.getRawValue().role);

      expect(roles.slice(0, 4)).toEqual([
        LeadershipRole.Kurinnuy,
        LeadershipRole.Suddya,
        LeadershipRole.Pysar,
        LeadershipRole.Skarbnyk
      ]);
    });

    it('should add empty row if no active member exists for role', () => {
       const h1 = { ...mockLeadership.leadershipHistories[0], endDate: '2023-02-01' }; 
       const data: LeadershipDto = {
        ...mockLeadership,
        leadershipHistories: [h1]
      };

      component.patchForm(data);

      const rows = component.leadershipHistories.controls
        .map(c => c.getRawValue())
        .filter(v => v.role === LeadershipRole.Kurinnuy);
      
      expect(rows).toHaveSize(2);
      expect(rows[0].member).toBeTruthy(); 
      expect(rows[1].member).toBeNull(); 
    });
  });

  describe('UI Logic', () => {
    beforeEach(() => {
      component.leadershipType = 'kurin';
      component.buildFormRowsFromDefaults('kurin');
      const control = component.leadershipHistories.at(0);
      control.patchValue({
        role: LeadershipRole.Kurinnuy,
        member: { firstName: 'John', lastName: 'Doe' },
        endDate: new Date() 
      });
    });

    it('isRowVisible should filter archived rows', () => {
      component.showArchived = false;
      expect(component.isRowVisible(0)).toBeFalse();

      component.showArchived = true;
      expect(component.isRowVisible(0)).toBeTrue();
    });

    it('isRowVisible should filter by search term (member name)', () => {
      component.showArchived = true;
      component.searchTerm = 'John';
      expect(component.isRowVisible(0)).toBeTrue();

      component.searchTerm = 'Alice';
      expect(component.isRowVisible(0)).toBeFalse();
    });

    it('isRowVisible should filter by search term (role name)', () => {
      spyOn(component, 'getRoleDisplayName').and.returnValue('Курінний');
      component.showArchived = true;
      component.searchTerm = 'Курінний'; 
      expect(component.isRowVisible(0)).toBeTrue();
    });
  });

  describe('Row Manipulation', () => {
    beforeEach(() => {
      component.leadershipType = 'kurin';
      component.leadershipHistories.clear();
    });

    it('addRoleRow should add a row', () => {
      component.addRoleRow(LeadershipRole.Kurinnuy);
      expect(component.leadershipHistories).toHaveSize(1);
    });

    it('onRemoveRow should remove row', () => {
      component.addRoleRow(LeadershipRole.Kurinnuy);
      component.addRoleRow(LeadershipRole.Kurinnuy);
      expect(component.leadershipHistories).toHaveSize(2);

      component.onRemoveRow(0);
      expect(component.leadershipHistories).toHaveSize(1);
    });

    it('onRemoveRow should restore empty row for single-seat roles if last one removed', () => {
      component.addRoleRow(LeadershipRole.Kurinnuy);
      expect(component.leadershipHistories).toHaveSize(1);

      component.onRemoveRow(0);
      
      expect(component.leadershipHistories).toHaveSize(1);
      expect(component.leadershipHistories.at(0).get('member')?.value).toBeNull();
    });
  });

  describe('saveCadence', () => {
    beforeEach(() => {
      component.leadershipType = 'kurin';
      component.entityKey = 'k1';
    });

    it('should not save if form is invalid', () => {
      component.leadershipForm.setErrors({ required: true });
      component.saveCadence();
      expect(leadershipServiceSpy.create).not.toHaveBeenCalled();
      expect(leadershipServiceSpy.update).not.toHaveBeenCalled();
    });

    it('зберігає провід, де обсаджено лише частину ролей', () => {
      component.buildFormRowsFromDefaults('group');
      const seat = (role: LeadershipRole) => component.leadershipHistories.controls
        .find(c => c.getRawValue().role === role)!
        .patchValue({ member: mockMember });
      seat(LeadershipRole.Hurtkoviy);
      seat(LeadershipRole.Pysar);

      expect(component.leadershipForm.valid).toBeTrue();
      component.saveCadence();

      expect(leadershipServiceSpy.create).toHaveBeenCalled();
      const payload = leadershipServiceSpy.create.calls.mostRecent().args[0];
      expect(payload.leadershipHistories.map(h => h.role)).toEqual([LeadershipRole.Hurtkoviy, LeadershipRole.Pysar]);
    });

    it('новий провід без жодної людини не зберігає', () => {
      component.buildFormRowsFromDefaults('kurin');

      expect(component.canSave).toBeFalse();
      component.saveCadence();

      expect(leadershipServiceSpy.create).not.toHaveBeenCalled();
    });

    it('should call create when no leadershipKey exists', () => {
      component.addRoleRow(LeadershipRole.Kurinnuy);
      const row = component.leadershipHistories.at(0);
      row.patchValue({
        member: mockMember,
        startDate: new Date('2023-01-01')
      });

      component.saveCadence();

      expect(leadershipServiceSpy.create).toHaveBeenCalled();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/kurin']);
    });

    it('should call update when leadershipKey exists', () => {
      component.leadershipKey = 'l1';
      component.addRoleRow(LeadershipRole.Kurinnuy);
      const row = component.leadershipHistories.at(0);
      row.patchValue({
        member: mockMember,
        startDate: new Date('2023-01-01')
      });

      component.saveCadence();

      expect(leadershipServiceSpy.update).toHaveBeenCalled();
    });

    it('should navigate to group page if type is group', () => {
      component.leadershipType = 'group';
      component.entityKey = 'g1';
      component.addRoleRow(LeadershipRole.Hurtkoviy);
      const row = component.leadershipHistories.at(0);
      row.patchValue({
        member: mockMember,
        startDate: new Date('2023-01-01')
      });

      component.saveCadence();

      expect(routerSpy.navigate).toHaveBeenCalledWith(['/group', 'g1']);
    });

    it('should handle save error', () => {
      component.addRoleRow(LeadershipRole.Kurinnuy);
      const row = component.leadershipHistories.at(0);
      row.patchValue({ member: mockMember, startDate: new Date() });

      leadershipServiceSpy.create.and.returnValue(throwError(() => new Error('Save failed')));
      spyOn(console, 'error');

      component.saveCadence();

      expect(console.error).toHaveBeenCalled();
      expect(component.isLoading).toBeFalse();
    });
  });
  
  describe('Helpers', () => {
      it('canHaveMultipleMembers should return true for Vykhovnyk', () => {
          expect(component.canHaveMultipleMembers(LeadershipRole.Vykhovnyk)).toBeTrue();
      });
      
      it('canHaveMultipleMembers should return false for Kurinnuy', () => {
          expect(component.canHaveMultipleMembers(LeadershipRole.Kurinnuy)).toBeFalse();
      });

      it('getRoleRowsCount should return correct count', () => {
          component.leadershipHistories.clear();
          component.addRoleRow(LeadershipRole.Kurinnuy);
          component.addRoleRow(LeadershipRole.Kurinnuy);
          component.addRoleRow(LeadershipRole.Pysar);
          
          expect(component.getRoleRowsCount(LeadershipRole.Kurinnuy)).toBe(2);
          expect(component.getRoleRowsCount(LeadershipRole.Pysar)).toBe(1);
      });
  });
});
