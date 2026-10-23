import type { MockedObject } from 'vitest';
import { registerLocaleData } from '@angular/common';
import localeUk from '@angular/common/locales/uk';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { of } from 'rxjs';
import {
  CalendarFeedService, CalendarFeedStatus, googleCalendarUrlOf, webcalUrlOf
} from '../../services/calendar-feed-service/calendar-feed.service';
import { CalendarFeedSectionComponent } from './calendar-feed-section';

const none: CalendarFeedStatus = {
  state: 'None', includeSchedules: false, includeTasks: false, createdUtc: null, lastFetchedUtc: null, revokedUtc: null, revokedReason: null
};
const active: CalendarFeedStatus = { ...none, state: 'Active', createdUtc: '2026-10-20T09:00:00Z' };
const url = 'https://api.lileyka.app/api/calendar/feed/abc.ics';

describe('CalendarFeedSectionComponent', () => {
  let fixture: ComponentFixture<CalendarFeedSectionComponent>;
  let feeds: MockedObject<CalendarFeedService>;

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  const create = (status: CalendarFeedStatus) => {
    feeds.getStatus.mockReturnValue(of(status));
    fixture = TestBed.createComponent(CalendarFeedSectionComponent);
    fixture.detectChanges();
  };

  beforeEach(() => {
    registerLocaleData(localeUk, 'uk-UA');
    feeds = {
      getStatus: vi.fn(),
      issue: vi.fn().mockReturnValue(of({ feedUrl: url, status: active })),
      update: vi.fn().mockReturnValue(of(active)),
      disable: vi.fn().mockReturnValue(of({ ...none, state: 'Revoked', revokedReason: 'Disabled' }))
    } as unknown as MockedObject<CalendarFeedService>;
    TestBed.configureTestingModule({
      imports: [CalendarFeedSectionComponent],
      providers: [
        { provide: CalendarFeedService, useValue: feeds },
        { provide: MessageService, useValue: { add: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: null } } }
      ]
    });
  });

  it('offers to create a link when there is none, and shows it once created', () => {
    create(none);
    expect(text()).toContain('Створити посилання');

    (fixture.componentInstance as unknown as { issue(): void }).issue();
    fixture.detectChanges();

    expect(feeds.issue).toHaveBeenCalledWith({ includeSchedules: false, includeTasks: false });
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[readonly]')?.value).toBe(url);
    expect(text()).toContain('Посилання показується лише зараз');
  });

  it('tells the person when a security change switched the link off', () => {
    create({ ...none, state: 'Revoked', revokedReason: 'SecurityChange', revokedUtc: '2026-10-21T10:00:00Z' });

    expect(text()).toContain('після зміни пароля');
    expect(text()).toContain('Створи нове посилання');
  });

  it('asks before replacing a live link', () => {
    create(active);
    const confirmation = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = vi.spyOn(confirmation, 'confirm');

    (fixture.componentInstance as unknown as { issue(): void }).issue();

    expect(confirm).toHaveBeenCalled();
    expect(feeds.issue).not.toHaveBeenCalled();
  });

  it('builds the links the calendar apps open', () => {
    expect(webcalUrlOf(url)).toBe('webcal://api.lileyka.app/api/calendar/feed/abc.ics');
    expect(googleCalendarUrlOf(url)).toBe(
      'https://calendar.google.com/calendar/r?cid=webcal%3A%2F%2Fapi.lileyka.app%2Fapi%2Fcalendar%2Ffeed%2Fabc.ics'
    );
  });
});
