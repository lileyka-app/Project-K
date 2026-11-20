import { actInKurin, leadershipRoleDisplayName, rememberKurinRoleNames } from './leadership-role-display.function';

describe('leadershipRoleDisplayName', () => {
  afterEach(() => actInKurin(null));

  it('says the current kurin\'s own name, and the usual one elsewhere', () => {
    rememberKurinRoleNames('orden', { Pysar: 'Скриптор' });
    rememberKurinRoleNames('youth', {});

    actInKurin('orden');
    expect(leadershipRoleDisplayName('Pysar')).toBe('Скриптор');
    expect(leadershipRoleDisplayName('Skarbnyk')).toBe('Скарбник');

    actInKurin('youth');
    expect(leadershipRoleDisplayName('Pysar')).toBe('Писар');
    expect(leadershipRoleDisplayName('Pysar', 'orden')).toBe('Скриптор');
  });

  it('keeps the current kurin\'s names when another kurin\'s are saved', () => {
    rememberKurinRoleNames('orden', { Pysar: 'Скриптор' });
    actInKurin('orden');

    rememberKurinRoleNames('chorty', { Pysar: 'Літописець' });

    expect(leadershipRoleDisplayName('Pysar')).toBe('Скриптор');
  });
});
