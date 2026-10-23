import { mobileRouteOf } from './notification-route';
import { AppNotification } from './leader.models';

type Shape = Pick<AppNotification, 'type' | 'route' | 'entityType' | 'entityKey'>;

const note = (shape: Partial<Shape>): Shape => ({
  type: 'MemberProfileVerified',
  route: null,
  entityType: null,
  entityKey: null,
  ...shape,
});

describe('mobileRouteOf', () => {
  it('opens a member card for every member route', () => {
    for (const type of ['MemberProfileVerified', 'MemberAwardReviewed', 'MemberWarningAssigned', 'MemberSkillReviewed'] as const) {
      expect(mobileRouteOf(note({ type, route: '/member/m-7', entityType: 'Member', entityKey: 'm-7' }), 'k1')).toBe(
        '/tabs/kurin/member/m-7',
      );
    }
  });

  it('opens the review queue of the current kurin only', () => {
    const submitted = note({ type: 'MemberSkillSubmittedForReview', route: '/kurin/k1/review/skills' });
    expect(mobileRouteOf(submitted, 'k1')).toBe('/tabs/kurin/review/skills');
    expect(mobileRouteOf(submitted, 'k2')).toBeNull();
  });

  it('opens an agenda item by its key, not the kurin in the route', () => {
    expect(
      mobileRouteOf(note({ type: 'AgendaItemAssigned', route: '/tasks/k1', entityType: 'AgendaItem', entityKey: 't9' }), 'k1'),
    ).toBe('/tabs/tasks/task/t9');
    expect(
      mobileRouteOf(note({ type: 'AgendaItemUpdated', route: '/calendar/k1', entityType: 'AgendaItem', entityKey: 'e9' }), 'k1'),
    ).toBe('/tabs/calendar/event/e9');
  });

  it('opens the list for a deleted item or one without a key', () => {
    expect(
      mobileRouteOf(note({ type: 'AgendaItemDeleted', route: '/calendar/k1', entityType: 'AgendaItem', entityKey: 'e9' }), 'k1'),
    ).toBe('/tabs/calendar');
    expect(mobileRouteOf(note({ type: 'AgendaItemStatusChanged', route: '/tasks/k1' }), 'k1')).toBe('/tabs/tasks');
  });

  it('opens the applications to join for an admin', () => {
    expect(mobileRouteOf(note({ type: 'WaitlistEntrySubmitted', route: '/waitlist', entityType: 'WaitlistEntry', entityKey: 'w1' }), null)).toBe(
      '/tabs/more/waitlist',
    );
  });

  it('opens the account page for a calendar link that went off', () => {
    expect(mobileRouteOf(note({ type: 'CalendarFeedRevoked', route: '/settings/account#calendar' }), null)).toBe('/tabs/more/account');
  });

  it('only marks read what the phone has no screen for', () => {
    expect(mobileRouteOf(note({ route: null }), 'k1')).toBeNull();
    expect(mobileRouteOf(note({ route: '/kurin/k1/registry' }), 'k1')).toBeNull();
  });
});
