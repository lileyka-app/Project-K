import { FormControl } from '@angular/forms';
import { PASSWORD_RULES, passwordMeetsRules, passwordRulesValidator } from './password-rules.function';

describe('password rules', () => {
  it('accepts what the API accepts', () => {
    expect(passwordMeetsRules('Plast#2026')).toBe(true);
  });

  it('names the rule a password misses', () => {
    const missed = (password: string) => PASSWORD_RULES.filter(rule => !rule.test(password)).map(rule => rule.id);

    expect(missed('Ab1!')).toEqual(['length']);
    expect(missed('plast#2026')).toEqual(['upper']);
    expect(missed('Plast#Plast')).toEqual(['digit']);
    expect(missed('Plast2026')).toEqual(['symbol']);
  });

  it('counts a Cyrillic letter as a symbol and not as a capital, as Identity does', () => {
    expect(passwordMeetsRules('пласт2026Ж')).toBe(false);
    expect(passwordMeetsRules('Plast2026ж')).toBe(true);
  });

  it('leaves an empty field to the required validator', () => {
    expect(passwordRulesValidator(new FormControl(''))).toBeNull();
    expect(passwordRulesValidator(new FormControl('short'))).toEqual({ passwordRules: true });
  });
});
