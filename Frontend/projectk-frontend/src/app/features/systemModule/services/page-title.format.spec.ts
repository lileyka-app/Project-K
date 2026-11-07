import { KurinBranch } from '../../kurinModule/models/enums/kurin-branch.enum';
import {
  composePageTitle,
  formatGroupTitle,
  formatKurinTitle,
  formatMemberTitle
} from './page-title.format';

describe('page title formatting', () => {
  describe('formatKurinTitle', () => {
    it('renders the kurin number in scouting shorthand', () => {
      expect(formatKurinTitle(12)).toBe('к. ч. 12');
    });

    it('returns null when the number is missing', () => {
      expect(formatKurinTitle(null)).toBeNull();
      expect(formatKurinTitle(undefined)).toBeNull();
    });

    it('accepts zero as a real number', () => {
      expect(formatKurinTitle(0)).toBe('к. ч. 0');
      expect(formatKurinTitle(5, KurinBranch.USP)).toBe('5-й курінь УСП');
    });
  });

  describe('formatGroupTitle', () => {
    it('prefixes the group name', () => {
      expect(formatGroupTitle('Соколи')).toBe('г. Соколи');
    });

    it('returns null for blank names', () => {
      expect(formatGroupTitle('   ')).toBeNull();
      expect(formatGroupTitle(null)).toBeNull();
    });
  });

  describe('formatMemberTitle', () => {
    it('puts the last name first', () => {
      expect(formatMemberTitle('Муха', 'Ростислав')).toBe('Муха Ростислав');
    });

    it('falls back to whichever part is present', () => {
      expect(formatMemberTitle('Муха', null)).toBe('Муха');
      expect(formatMemberTitle('', 'Ростислав')).toBe('Ростислав');
    });

    it('returns null when both parts are missing', () => {
      expect(formatMemberTitle(null, undefined)).toBeNull();
    });
  });

  describe('composePageTitle', () => {
    it('joins context and app name with a middle dot', () => {
      expect(composePageTitle('к. ч. 12', 'ProjectK')).toBe('к. ч. 12 · ProjectK');
    });

    it('falls back to the app name alone without context', () => {
      expect(composePageTitle(null, 'ProjectK')).toBe('ProjectK');
      expect(composePageTitle('  ', 'ProjectK')).toBe('ProjectK');
    });

    it('uses the configured app name so a rebrand needs no code change', () => {
      expect(composePageTitle('Адміністрація', 'Plastown')).toBe('Адміністрація · Plastown');
    });
  });
});
