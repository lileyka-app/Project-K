import { AuthState } from '../auth/auth.models';
import { MenuContext, currentEntry, menuEntries } from './menu-items';

function state(overrides: Partial<AuthState>): AuthState {
  return {
    userKey: 'u1',
    memberKey: 'm1',
    email: 'ostap@example.com',
    isAdmin: false,
    permissions: [],
    roles: [],
    kurinKey: 'k1',
    accessToken: 't',
    ...overrides,
  };
}

const none: MenuContext = { youthKurin: true, duesGroups: [] };
const labels = (s: AuthState, context = none) => menuEntries(s, context).map((entry) => entry.label);

describe('menuEntries', () => {
  it('gives a youth the web sidebar of a youth, in its order', () => {
    expect(labels(state({}))).toEqual([
      'Головна',
      'Мій профіль',
      'Курінь',
      'Календар',
      'Задачі',
      'Планування',
      'Точкування',
      'Налаштування акаунта',
      'Довідка',
      'Повідомити про проблему',
      'Про Лілейку',
    ]);
  });

  it('adds what a Звʼязковий sees, with the web-only pages opening the web', () => {
    const entries = menuEntries(
      state({
        permissions: [
          'Group:Manage:KurinWide',
          'Kurin:Update:KurinWide',
          'KurinDues:Read',
          'GroupDues:Read:KurinWide',
          'BadgeProgress:Update:KurinWide',
          'KurinJournal:Read:KurinWide',
        ],
      }),
      { youthKurin: true, duesGroups: [{ groupKey: 'g1', groupName: 'Соколи' }] },
    );
    expect(entries.map((entry) => entry.label)).toEqual([
      'Головна',
      'Мій профіль',
      'Курінь',
      'Реєстр',
      'Імпорт складу',
      'Календар',
      'Задачі',
      'Планування',
      'Точкування',
      'Модерація вмілостей',
      'Журнал дій',
      'Вкладка гуртка',
      'Вкладка куреня',
      'Налаштування куреня',
      'Налаштування акаунта',
      'Довідка',
      'Повідомити про проблему',
      'Про Лілейку',
    ]);
    const byKey = Object.fromEntries(entries.map((entry) => [entry.key, entry]));
    expect(byKey['registry'].web).toBe('/kurin/registry');
    expect(byKey['planning'].link).toBe('/tabs/calendar/planning');
    expect(byKey['planning'].web).toBeUndefined();
    expect(byKey['kurin-settings'].web).toBe('/kurin/k1/settings');
    expect(byKey['dues-g1'].link).toBe('/tabs/kurin/group/g1/dues');
    expect(byKey['kurin-dues'].link).toBe('/tabs/kurin/dues');
    expect(byKey['kurin-dues'].web).toBeUndefined();
    expect(byKey['review'].link).toBe('/tabs/kurin/review/skills');
    expect(byKey['journal'].web).toBe('/kurin/k1/journal');
    expect(byKey['journal'].icon).toBe('time');
  });

  it('follows the kurin policy for skill review and the journal, not leading a гурток', () => {
    const leader = labels(state({ permissions: ['Group:Update:OwnGroups'] }));
    expect(leader).not.toContain('Модерація вмілостей');
    expect(leader).not.toContain('Журнал дій');

    const instructor = labels(state({ permissions: ['BadgeProgress:Update:OwnGroups', 'KurinJournal:Read:KurinWide'] }));
    expect(instructor).toContain('Модерація вмілостей');
    expect(instructor).toContain('Журнал дій');
  });

  it('names each гурток when one keeps several boxes, and hides skill review outside УПЮ', () => {
    const result = labels(state({ permissions: ['Group:Update', 'GroupDues:Read:OwnGroups'] }), {
      youthKurin: false,
      duesGroups: [
        { groupKey: 'g1', groupName: 'Соколи' },
        { groupKey: 'g2', groupName: 'Вовки' },
      ],
    });
    expect(result).toContain('Вкладка: Соколи');
    expect(result).toContain('Вкладка: Вовки');
    expect(result).not.toContain('Модерація вмілостей');
  });

  it('gives the курінний скарбник the kurin’s box in the app, without a гурток’s', () => {
    const entries = menuEntries(state({ permissions: ['KurinDues:Read:KurinWide', 'KurinDues:Update:KurinWide'] }), none);
    expect(entries.filter((entry) => entry.key.includes('dues')).map((entry) => [entry.label, entry.link])).toEqual([
      ['Вкладка куреня', '/tabs/kurin/dues'],
    ]);
  });

  it('gives an administrator outside a kurin the administration, with the applications on the phone', () => {
    const admin = state({ isAdmin: true, kurinKey: null, memberKey: null });
    expect(labels(admin)).toEqual([
      'Адміністрація',
      'Заявки',
      'Користувачі',
      'Системні налаштування',
      'Налаштування акаунта',
      'Довідка',
      'Повідомити про проблему',
      'Про Лілейку',
    ]);
    expect(menuEntries(admin, none).find((entry) => entry.key === 'waitlist')?.link).toBe('/tabs/more/waitlist');
  });

  it('keeps the help and the app’s details when signed out (the native shells)', () => {
    expect(menuEntries(null, none).map((entry) => entry.label)).toEqual(['Довідка', 'Про Лілейку']);
  });
});

describe('currentEntry', () => {
  const entries = menuEntries(state({ permissions: ['Group:Update'] }), none);

  it('marks the longest link the URL is under', () => {
    expect(currentEntry(entries, '/tabs/kurin/score?period=1')).toBe('score');
    expect(currentEntry(entries, '/tabs/kurin/member/m2')).toBe('kurin');
    expect(currentEntry(entries, '/tabs/calendar')).toBe('calendar');
    expect(currentEntry(entries, '/tabs/calendar/planning/s1')).toBe('planning');
  });

  it('marks nothing on a page the menu does not list', () => {
    expect(currentEntry(entries, '/tabs/more/privacy')).toBeNull();
  });
});
