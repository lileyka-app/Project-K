import type { Mock, MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavigationEnd, Router } from '@angular/router';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { DevRoleSwitcherComponent, memberOnScreen } from './dev-role-switcher';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import { DevToolsService } from '../../services/dev-tools-service/dev-tools.service';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { MemberDto } from '../../../kurinModule/models/member.dto';

describe('memberOnScreen', () => {
  it('reads the member key off a card and off the pages under it', () => {
    expect(memberOnScreen('/member/m-1')).toBe('m-1');
    expect(memberOnScreen('/member/m-1/probe/2?tab=skills')).toBe('m-1');
  });

  it('answers null anywhere else, including the member forms', () => {
    expect(memberOnScreen('/kurin')).toBeNull();
    expect(memberOnScreen('/kurin/k-1/member/upsert/m-1')).toBeNull();
  });
});

describe('DevRoleSwitcherComponent', () => {
  let fixture: ComponentFixture<DevRoleSwitcherComponent>;
  let component: DevRoleSwitcherComponent;
  let state$: BehaviorSubject<AuthState | null>;
  let devTools: MockedObject<DevToolsService>;
  let members: MockedObject<MemberService>;
  let messages: MockedObject<MessageService>;
  let navigation$: Subject<NavigationEnd>;

  const admin: AuthState = {
    userKey: 'admin-1', memberKey: null, email: 'admin@example.com', isAdmin: true,
    permissions: [], roles: ['Admin'], kurinKey: 'k-1', accessToken: 'token'
  };
  const youth: AuthState = { ...admin, userKey: 'y-1', email: 'youth@example.com', isAdmin: false, roles: [] };

  function create(state: AuthState | null, borrowed: 'Member' | 'Person' | null = null, url = '/panel'): void {
    state$ = new BehaviorSubject<AuthState | null>(state);
    navigation$ = new Subject<NavigationEnd>();
    devTools = {
      borrowedRole: vi.fn(),
      hasReturnTicket: vi.fn(),
      impersonate: vi.fn(),
      impersonateMember: vi.fn(),
      returnToAdmin: vi.fn(),
      forgetTicket: vi.fn()
    } as MockedObject<DevToolsService>;
    devTools.borrowedRole.mockReturnValue(borrowed);
    devTools.hasReturnTicket.mockReturnValue(borrowed !== null);
    members = {
      getByKey: vi.fn()
    } as MockedObject<MemberService>;
    members.getByKey.mockReturnValue(of({ memberKey: 'm-1', firstName: 'Марта', lastName: 'Сова' } as MemberDto));
    messages = {
      add: vi.fn()
    } as MockedObject<MessageService>;

    TestBed.configureTestingModule({
      imports: [DevRoleSwitcherComponent],
      providers: [
        { provide: AuthService, useValue: { getAuthState: () => state$.asObservable(), getAuthStateValue: () => state$.value } },
        { provide: DevToolsService, useValue: devTools },
        { provide: MemberService, useValue: members },
        { provide: MessageService, useValue: messages },
        { provide: Router, useValue: { url, events: navigation$.asObservable() } }
      ]
    });
    fixture = TestBed.createComponent(DevRoleSwitcherComponent);
    component = fixture.componentInstance;
    vi.spyOn(component as unknown as {
      reload: (path: string) => void;
    }, 'reload').mockReturnValue(undefined);
    fixture.detectChanges();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the tab to an administrator on a non-production build', () => {
    create(admin);

    expect(component.visible()).toBe(true);
    expect(element().querySelector('.dev-switch__tab')).not.toBeNull();
  });

  it('stays hidden for anyone who is neither an administrator nor borrowing a seat', () => {
    create(youth);

    expect(component.visible()).toBe(false);
    expect(element().querySelector('.dev-switch')).toBeNull();
  });

  it('keeps the way back visible while a seat is borrowed', () => {
    create(youth, 'Member');
    component.toggle();
    fixture.detectChanges();

    expect(component.visible()).toBe(true);
    expect(element().querySelector('.dev-switch__return')).not.toBeNull();
  });

  it('borrows the chosen seat and reloads into the kurin', () => {
    create(admin);
    devTools.impersonate.mockReturnValue(of({ login: {} as never, returnTicket: 't', role: 'Vykhovnyk', kurinKey: 'k-1' }));

    component.switchTo('Vykhovnyk');

    expect(devTools.impersonate).toHaveBeenCalledWith('Vykhovnyk');
    expect((component as unknown as {
      reload: Mock;
    }).reload).toHaveBeenCalledWith('/kurin');
  });

  it('explains when nobody in the kurin holds the seat', () => {
    create(admin);
    devTools.impersonate.mockReturnValue(throwError(() => ({ status: 404 })));

    component.switchTo('Skarbnyk');

    expect(component.busy()).toBeNull();
    expect(messages.add).toHaveBeenCalledWith(expect.objectContaining({ detail: expect.stringContaining('ніхто з таким діловодством') }));
  });

  it('offers the person whose card is open, by name, once the panel opens', () => {
    create(admin, null, '/member/m-1');
    expect(members.getByKey).not.toHaveBeenCalled();

    component.toggle();
    fixture.detectChanges();
    fixture.detectChanges();

    expect(members.getByKey).toHaveBeenCalledWith('m-1');
    expect(component.personName()).toBe('Марта Сова');
    expect(element().querySelector('.dev-switch__option--person')?.textContent).toContain('Марта Сова');
  });

  it('offers nobody in particular away from a member card', () => {
    create(admin);
    component.toggle();
    fixture.detectChanges();

    expect(component.personKey()).toBeNull();
    expect(element().querySelector('.dev-switch__option--person')).toBeNull();
    expect(members.getByKey).not.toHaveBeenCalled();
  });

  it('follows navigation onto a card', () => {
    create(admin);

    navigation$.next(new NavigationEnd(1, '/member/m-2', '/member/m-2'));

    expect(component.personKey()).toBe('m-2');
  });

  it('borrows the person on screen and reloads their own card', () => {
    create(admin, null, '/member/m-1');
    devTools.impersonateMember.mockReturnValue(of({ login: {} as never, returnTicket: 't', role: 'Person', kurinKey: 'k-1' }));

    component.switchToPerson();

    expect(devTools.impersonateMember).toHaveBeenCalledWith('m-1');
    expect((component as unknown as {
      reload: Mock;
    }).reload).toHaveBeenCalledWith('/member/m-1');
  });

  it('explains when the person on screen is an administrator', () => {
    create(admin, null, '/member/m-1');
    devTools.impersonateMember.mockReturnValue(throwError(() => ({ status: 409 })));

    component.switchToPerson();

    expect(component.busy()).toBeNull();
    expect(messages.add).toHaveBeenCalledWith(expect.objectContaining({ detail: expect.stringContaining('адміністратора') }));
  });

  it('drops a dead ticket instead of offering it again', () => {
    create(youth, 'Member');
    devTools.returnToAdmin.mockReturnValue(throwError(() => ({ status: 401 })));

    component.returnToAdmin();

    expect(devTools.forgetTicket).toHaveBeenCalled();
    expect(component.borrowed()).toBeNull();
  });
});
