import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UsersListComponent } from './users-list';
import { provideHttpClient } from '@angular/common/http';
import { UserService } from '../../services/user-service/user.service';
import { UserDto } from '../../models/user.dto';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, MessageService } from '@openng/optimus-ui/api';

describe('UsersListComponent', () => {
  let component: UsersListComponent;
  let fixture: ComponentFixture<UsersListComponent>;
  let mockUserService: MockedObject<UserService>;

  const mockUsers: UserDto[] = [
    {
      userId: 'user-1',
      email: 'user1@example.com',
      firstName: 'John',
      lastName: 'Doe',
      role: 'Member',
      kurinNumber: 101, kurinKey: 'kurin-1'
    },
    {
      userId: 'user-2',
      email: 'user2@example.com',
      firstName: 'Jane',
      lastName: 'Smith',
      role: 'Admin',
      kurinNumber: 101, kurinKey: null
    },
    {
      userId: 'user-3',
      email: 'user3@example.com',
      firstName: 'Bob',
      lastName: 'Johnson',
      role: 'Member',
      kurinNumber: 101, kurinKey: 'kurin-2'
    }
  ];

  beforeEach(async () => {
    mockUserService = {
      getAllUsers: vi.fn(),
      changeUserRole: vi.fn(),
      suspendUser: vi.fn(),
      restoreUser: vi.fn()
    } as MockedObject<UserService>;
    mockUserService.getAllUsers.mockReturnValue(of(JSON.parse(JSON.stringify(mockUsers))));
    mockUserService.changeUserRole.mockReturnValue(of(true));
    mockUserService.suspendUser.mockReturnValue(of(true));
    mockUserService.restoreUser.mockReturnValue(of(true));

    await TestBed.configureTestingModule({
      imports: [UsersListComponent],
      providers: [
        provideHttpClient(),
        MessageService,
        { provide: UserService, useValue: mockUserService }
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(UsersListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // SEC-4.3: suspension is the reversible alternative to deletion.
  describe('suspendUser / restoreUser', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    // ConfirmationService is provided by the component itself, so it lives in the element injector.
    const confirmationService = () => fixture.debugElement.injector.get(ConfirmationService);

    it('marks the row suspended once the API confirms, after the admin confirmed the dialog', () => {
      const confirmation = confirmationService();
      vi.spyOn(confirmation, 'confirm').mockImplementation((options: Confirmation) => {
        options.accept?.();
        return confirmation;
      });
      const user = component.users[0];

      component.suspendUser(user);

      expect(mockUserService.suspendUser).toHaveBeenCalledWith('user-1');
      expect(user.isSuspended).toBe(true);
    });

    it('does nothing until the dialog is confirmed', () => {
      const confirmation = confirmationService();
      vi.spyOn(confirmation, 'confirm').mockReturnValue(confirmation);

      component.suspendUser(component.users[0]);

      expect(mockUserService.suspendUser).not.toHaveBeenCalled();
      expect(component.users[0].isSuspended).toBeFalsy();
    });

    it('clears the mark once the account is restored', () => {
      const user = component.users[1];
      user.isSuspended = true;

      component.restoreUser(user);

      expect(mockUserService.restoreUser).toHaveBeenCalledWith('user-2');
      expect(user.isSuspended).toBe(false);
    });

    it('leaves the mark in place when restoring failed', () => {
      mockUserService.restoreUser.mockReturnValue(throwError(() => new Error('down')));
      const user = component.users[1];
      user.isSuspended = true;

      component.restoreUser(user);

      expect(user.isSuspended).toBe(true);
    });
  });

  describe('Component initialization', () => {
    it('should initialize with empty users array', () => {
      expect(component.users).toEqual([]);
      expect(component.clonedUsers).toEqual({});
    });

    it('should have userService injected', () => {
      expect(component['userService']).toBeDefined();
    });
  });

  describe('ngOnInit', () => {
    it('should load users on initialization', () => {
      fixture.detectChanges();

      expect(mockUserService.getAllUsers).toHaveBeenCalled();
      expect(component.users).toEqual(mockUsers);
      expect(component.users).toHaveLength(3);
    });

    it('should handle empty users list', () => {
      mockUserService.getAllUsers.mockReturnValue(of([]));
      fixture.detectChanges();

      expect(component.users).toEqual([]);
      expect(component.users).toHaveLength(0);
    });

    it('should handle error when loading users', () => {
      const error = new Error('Failed to load users');
      mockUserService.getAllUsers.mockReturnValue(throwError(() => error));
      
      vi.spyOn(console, 'error').mockReturnValue(undefined);
      fixture.detectChanges();

      expect(mockUserService.getAllUsers).toHaveBeenCalled();
      // Component doesn't handle errors yet, but we test the service call
    });

    it('should populate users with correct data structure', () => {
      fixture.detectChanges();

      expect(component.users[0].userId).toBe('user-1');
      expect(component.users[0].email).toBe('user1@example.com');
      expect(component.users[0].role).toBe('Member');
      expect(component.users[1].userId).toBe('user-2');
      expect(component.users[1].role).toBe('Admin');
    });
  });

  describe('onRowEditInit', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should clone user when edit is initiated', () => {
      const user = component.users[0];
      component.onRowEditInit(user);

      expect(component.clonedUsers[user.userId as string]).toBeDefined();
      expect(component.clonedUsers[user.userId as string]).toEqual(user);
    });

    it('should create a deep copy of the user', () => {
      const user = component.users[0];
      component.onRowEditInit(user);

      const clonedUser = component.clonedUsers[user.userId as string];
      expect(clonedUser).not.toBe(user);
      expect(clonedUser).toEqual(user);
    });

    it('should store cloned user with userId as key', () => {
      const user = component.users[1];
      component.onRowEditInit(user);

      expect(component.clonedUsers['user-2']).toBeDefined();
      expect(component.clonedUsers['user-2'].email).toBe('user2@example.com');
    });

    it('should handle multiple users being edited simultaneously', () => {
      const user1 = component.users[0];
      const user2 = component.users[1];

      component.onRowEditInit(user1);
      component.onRowEditInit(user2);

      expect(Object.keys(component.clonedUsers)).toHaveLength(2);
      expect(component.clonedUsers['user-1']).toBeDefined();
      expect(component.clonedUsers['user-2']).toBeDefined();
    });

    it('should overwrite existing clone if same user is edited again', () => {
      const user = component.users[0];
      
      component.onRowEditInit(user);
      const firstClone = component.clonedUsers[user.userId as string];
      
      user.firstName = 'Modified';
      component.onRowEditInit(user);
      const secondClone = component.clonedUsers[user.userId as string];

      expect(secondClone.firstName).toBe('Modified');
      expect(firstClone).not.toEqual(secondClone);
    });
  });

  describe('onRowEditSave', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should delete cloned user when save is successful', () => {
      const user = component.users[0];
      component.onRowEditInit(user);

      expect(component.clonedUsers[user.userId as string]).toBeDefined();
      
      component.onRowEditSave(user);

      expect(component.clonedUsers[user.userId as string]).toBeUndefined();
    });

    it('should remove the correct cloned user', () => {
      const user1 = component.users[0];
      const user2 = component.users[1];

      component.onRowEditInit(user1);
      component.onRowEditInit(user2);

      component.onRowEditSave(user1);

      expect(component.clonedUsers['user-1']).toBeUndefined();
      expect(component.clonedUsers['user-2']).toBeDefined();
    });

    it('should not affect other cloned users', () => {
      const user1 = component.users[0];
      const user2 = component.users[1];
      const user3 = component.users[2];

      component.onRowEditInit(user1);
      component.onRowEditInit(user2);
      component.onRowEditInit(user3);

      component.onRowEditSave(user2);

      expect(component.clonedUsers['user-1']).toBeDefined();
      expect(component.clonedUsers['user-2']).toBeUndefined();
      expect(component.clonedUsers['user-3']).toBeDefined();
    });

    it('should keep modified user data in users array', () => {
      const user = component.users[0];
      component.onRowEditInit(user);

      user.firstName = 'Updated';
      component.onRowEditSave(user);

      expect(component.users[0].firstName).toBe('Updated');
    });
  });

  describe('onRowEditCancel', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should restore original user data when cancel is clicked', () => {
      const user = component.users[0];
      const originalFirstName = user.firstName;
      
      component.onRowEditInit(user);
      user.firstName = 'Modified';
      
      component.onRowEditCancel(user, 0);

      expect(component.users[0].firstName).toBe(originalFirstName);
    });

    it('should delete cloned user after cancel', () => {
      const user = component.users[0];
      component.onRowEditInit(user);

      component.onRowEditCancel(user, 0);

      expect(component.clonedUsers[user.userId as string]).toBeUndefined();
    });

    it('should restore user at correct index', () => {
      const user = component.users[1];
      const originalEmail = user.email;
      
      component.onRowEditInit(user);
      user.email = 'modified@example.com';
      
      component.onRowEditCancel(user, 1);

      expect(component.users[1].email).toBe(originalEmail);
      expect(component.users[0].email).toBe('user1@example.com'); // Other users unchanged
      expect(component.users[2].email).toBe('user3@example.com');
    });

    it('should handle cancel for different user properties', () => {
      const user = component.users[0];
      const originalUser = { ...user };
      
      component.onRowEditInit(user);
      user.firstName = 'Modified First';
      user.lastName = 'Modified Last';
      user.role = 'Admin';
      
      component.onRowEditCancel(user, 0);

      expect(component.users[0]).toEqual(originalUser);
    });

    it('should not affect other users when canceling edit', () => {
      const user1 = component.users[0];
      const user2 = component.users[1];
      
      component.onRowEditInit(user1);
      component.onRowEditInit(user2);
      
      user1.firstName = 'Modified1';
      user2.firstName = 'Modified2';
      
      component.onRowEditCancel(user1, 0);

      expect(component.users[0].firstName).not.toBe('Modified1');
      expect(component.users[1].firstName).toBe('Modified2');
    });
  });

  describe('Integration scenarios', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should handle complete edit flow', () => {
      const user = component.users[0];
      
      // Start editing
      component.onRowEditInit(user);
      expect(component.clonedUsers[user.userId as string]).toBeDefined();
      
      // Modify user
      user.firstName = 'Updated Name';
      
      // Save changes
      component.onRowEditSave(user);
      expect(component.clonedUsers[user.userId as string]).toBeUndefined();
      expect(component.users[0].firstName).toBe('Updated Name');
    });

    it('should handle complete cancel flow', () => {
      const user = component.users[0];
      const originalUser = { ...user };
      
      // Start editing
      component.onRowEditInit(user);
      
      // Modify user
      user.firstName = 'Modified';
      user.lastName = 'Modified';
      
      // Cancel changes
      component.onRowEditCancel(user, 0);
      
      expect(component.users[0]).toEqual(originalUser);
      expect(component.clonedUsers[user.userId as string]).toBeUndefined();
    });

    it('should handle editing multiple users sequentially', () => {
      const user1 = component.users[0];
      const user2 = component.users[1];
      
      // Edit first user
      component.onRowEditInit(user1);
      user1.firstName = 'Updated1';
      component.onRowEditSave(user1);
      
      // Edit second user
      component.onRowEditInit(user2);
      user2.firstName = 'Updated2';
      component.onRowEditSave(user2);
      
      expect(component.users[0].firstName).toBe('Updated1');
      expect(component.users[1].firstName).toBe('Updated2');
      expect(Object.keys(component.clonedUsers)).toHaveLength(0);
    });

    it('should handle mix of save and cancel operations', () => {
      const user1 = component.users[0];
      const user2 = component.users[1];
      const originalUser2 = { ...user2 };
      
      // Edit and save first user
      component.onRowEditInit(user1);
      user1.firstName = 'Updated1';
      component.onRowEditSave(user1);
      
      // Edit and cancel second user
      component.onRowEditInit(user2);
      user2.firstName = 'Modified2';
      component.onRowEditCancel(user2, 1);
      
      expect(component.users[0].firstName).toBe('Updated1');
      expect(component.users[1]).toEqual(originalUser2);
    });

    it('should maintain data integrity when loading and editing users', () => {
      expect(component.users).toHaveLength(3);
      
      const user = component.users[0];
      component.onRowEditInit(user);
      user.role = 'Admin';
      component.onRowEditSave(user);
      
      expect(component.users).toHaveLength(3);
      expect(component.users[0].userId).toBe('user-1');
      expect(component.users[0].role).toBe('Admin');
    });
  });

  describe('Edge cases', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should handle user with null kurinKey', () => {
      const user = component.users[1]; // Admin with null kurinKey
      component.onRowEditInit(user);
      
      expect(component.clonedUsers[user.userId as string].kurinKey).toBeNull();
    });

    it('should handle saving without any modifications', () => {
      const user = component.users[0];
      const originalUser = { ...user };
      
      component.onRowEditInit(user);
      component.onRowEditSave(user);
      
      expect(component.users[0]).toEqual(originalUser);
    });

    it('should handle canceling without any modifications', () => {
      const user = component.users[0];
      const originalUser = { ...user };
      
      component.onRowEditInit(user);
      component.onRowEditCancel(user, 0);
      
      expect(component.users[0]).toEqual(originalUser);
    });

    it('should handle empty users array gracefully', () => {
      mockUserService.getAllUsers.mockReturnValue(of([]));
      component.ngOnInit();
      
      expect(component.users).toHaveLength(0);
      expect(() => component.onRowEditInit({} as UserDto)).not.toThrow();
    });
  });
});