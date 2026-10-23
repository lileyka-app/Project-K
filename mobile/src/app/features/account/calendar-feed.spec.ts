import { CalendarFeedStatus, googleCalendarUrlOf, revokedBySecurity, webcalUrlOf } from './calendar-feed';

const status = (shape: Partial<CalendarFeedStatus>): CalendarFeedStatus => ({
  state: 'None',
  includeSchedules: false,
  includeTasks: false,
  createdUtc: null,
  lastFetchedUtc: null,
  revokedUtc: null,
  revokedReason: null,
  ...shape,
});

describe('calendar feed', () => {
  const url = 'https://api.lileyka.app/api/calendar/feed/abc.ics';

  it('hands the phone a webcal link and Google a prefilled one', () => {
    expect(webcalUrlOf(url)).toBe('webcal://api.lileyka.app/api/calendar/feed/abc.ics');
    expect(googleCalendarUrlOf(url)).toBe(
      'https://calendar.google.com/calendar/r?cid=webcal%3A%2F%2Fapi.lileyka.app%2Fapi%2Fcalendar%2Ffeed%2Fabc.ics',
    );
  });

  it('warns only about a link that went off on its own', () => {
    expect(revokedBySecurity(status({ state: 'Revoked', revokedReason: 'SecurityChange' }))).toBe(true);
    expect(revokedBySecurity(status({ state: 'Revoked', revokedReason: 'Suspended' }))).toBe(true);
    expect(revokedBySecurity(status({ state: 'Revoked', revokedReason: 'Disabled' }))).toBe(false);
    expect(revokedBySecurity(status({ state: 'Active' }))).toBe(false);
  });
});
