import { DuesEntryKind } from '../models/dues.enums';
import { currencySymbol, money, duesAmountLabel, isIncomingDues, signedDuesAmount } from './dues-format.function';

describe('dues entry sign', () => {
  it('signs money in with a plus and money out with a minus', () => {
    expect(signedDuesAmount({ kind: DuesEntryKind.Contribution, amount: 300 })).toBe(300);
    expect(signedDuesAmount({ kind: DuesEntryKind.OtherIncome, amount: 50 })).toBe(50);
    expect(signedDuesAmount({ kind: DuesEntryKind.Refund, amount: 40 })).toBe(-40);
    expect(signedDuesAmount({ kind: DuesEntryKind.TransferToKurin, amount: 255 })).toBe(-255);
    expect(duesAmountLabel({ kind: DuesEntryKind.Expense, amount: 120 })).toBe('−120 ₴');
  });

  it('takes a correction as entered, either way', () => {
    expect(duesAmountLabel({ kind: DuesEntryKind.Correction, amount: -60 })).toBe('−60 ₴');
    expect(isIncomingDues({ kind: DuesEntryKind.Correction, amount: -60 })).toBeFalse();
    expect(isIncomingDues({ kind: DuesEntryKind.Correction, amount: 60 })).toBeTrue();
  });

  it('leaves an exchange between cash and card unsigned', () => {
    expect(signedDuesAmount({ kind: DuesEntryKind.Exchange, amount: 100 })).toBeNull();
    expect(duesAmountLabel({ kind: DuesEntryKind.Exchange, amount: 100 })).toBe('100 ₴');
    expect(isIncomingDues({ kind: DuesEntryKind.Exchange, amount: 100 })).toBeNull();
  });
});

describe('money in the край\'s currency', () => {
  it('writes hryvnias when no currency is given, as before profiles', () => {
    expect(money(-300)).toBe('−300 ₴');
  });

  it('writes the край\'s own currency after the amount', () => {
    expect(money(1240.5, 'CZK')).toBe(`${money(1240.5).slice(0, -2)} Kč`);
    expect(currencySymbol('PLN')).toBe('zł');
  });

  it('falls back to the code for a currency the browser does not know', () => {
    expect(currencySymbol('XYZW')).toBe('XYZW');
  });

  it("writes the number the край's way", () => {
    expect(money(1240.5, 'CAD', 'en-CA')).toBe('1,240.5 $');
    expect(duesAmountLabel({ kind: DuesEntryKind.Expense, amount: 1240.5 }, 'CAD', 'en-CA')).toBe('−1,240.5 $');
  });

  it('falls back to uk-UA for a locale the browser does not know', () => {
    expect(money(1240.5, 'UAH', 'not a locale')).toBe(money(1240.5));
  });
});
