import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { NotificationService } from '../../services/notification-service/notification.service';
import { NotificationBellComponent } from './notification-bell';

describe('NotificationBellComponent', () => {
  let component: NotificationBellComponent;
  let fixture: ComponentFixture<NotificationBellComponent>;
  let notificationService: MockedObject<NotificationService>;

  beforeEach(async () => {
    notificationService = {
      getInbox: vi.fn(),
      refreshUnreadCount: vi.fn(),
      markAllAsRead: vi.fn(),
      markAsRead: vi.fn(),
      setUnreadCount: vi.fn(),
      unreadCount$: new BehaviorSubject(0)
    } as unknown as MockedObject<NotificationService>;
    notificationService.getInbox.mockReturnValue(of([]));
    notificationService.refreshUnreadCount.mockReturnValue(of(0));

    await TestBed.configureTestingModule({
      imports: [NotificationBellComponent],
      providers: [
        { provide: NotificationService, useValue: notificationService },
        { provide: Router, useValue: {
            navigateByUrl: vi.fn()
          } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationBellComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the inbox controls in Ukrainian', () => {
    component.isOpen = true;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Сповіщення');
    expect(fixture.nativeElement.textContent).toContain('Сповіщень немає');
    expect(fixture.nativeElement.querySelector('[aria-label="Позначити всі як прочитані"]')).not.toBeNull();
  });

  it('closes the inbox when clicking outside the component', () => {
    component.isOpen = true;

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(component.isOpen).toBe(false);
  });

  it('keeps the inbox open when clicking inside the component', () => {
    component.isOpen = true;

    fixture.nativeElement.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(component.isOpen).toBe(true);
  });
});
