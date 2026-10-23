import { AppNotification } from './leader.models';

/**
 * The phone's route for a notification's web route (the API's `route`, built in
 * MemberNotificationHandlers / AgendaNotificationHandlers). Agenda routes carry the kurin, not the
 * item, so the item comes from `entityKey`. Null for anything the phone has no screen for (a route
 * of another kurin's review queue, the registry): the tap then only marks it read.
 */
export function mobileRouteOf(
  notification: Pick<AppNotification, 'type' | 'route' | 'entityType' | 'entityKey'>,
  currentKurinKey: string | null,
): string | null {
  const route = (notification.route ?? '').split(/[?#]/)[0];

  const member = /^\/member\/([^/]+)\/?$/.exec(route);
  if (member) return `/tabs/kurin/member/${member[1]}`;

  const review = /^\/kurin\/([^/]+)\/review\/skills\/?$/.exec(route);
  if (review) return review[1] === currentKurinKey ? '/tabs/kurin/review/skills' : null;

  // The calendar link switched off by a security change: its section on the account page.
  if (/^\/settings\/account\/?$/.test(route)) return '/tabs/more/account';

  // An application to join, sent to system admins only.
  if (/^\/waitlist\/?$/.test(route)) return '/tabs/more/waitlist';

  const agenda = /^\/(tasks|calendar)(\/|$)/.exec(route);
  if (agenda) {
    const isTask = agenda[1] === 'tasks';
    const item = notification.entityType === 'AgendaItem' ? notification.entityKey : null;
    // A deleted item has no page left; its list is the nearest place.
    if (!item || notification.type === 'AgendaItemDeleted') return isTask ? '/tabs/tasks' : '/tabs/calendar';
    return isTask ? `/tabs/tasks/task/${item}` : `/tabs/calendar/event/${item}`;
  }

  return null;
}
