import {
  journalActionLabel,
  journalChangeView,
  journalFieldLabel,
  journalRoleLabel,
  journalValueLabel
} from './journal-labels.function';

describe('journal labels', () => {
  it('words a known action and falls back to its area for one this build does not know', () => {
    expect(journalActionLabel({ area: 'Probe', action: 'Probe.PointSigned' })).toBe('Підписано точку проби');
    expect(journalActionLabel({ area: 'Warning', action: 'Warning.Cancelled' })).toBe('Скасовано пересторогу');
    expect(journalActionLabel({ area: 'Agenda', action: 'Agenda.SomethingNew' })).toBe('Зміна в календарі чи задачах');
  });

  it('names the actor by office, admin and the baseline', () => {
    expect(journalRoleLabel('KV.Vykhovnyk')).toBe('Впорядник');
    expect(journalRoleLabel('Admin')).toBe('Адміністратор');
    expect(journalRoleLabel('Member')).toBe('Учасник');
    expect(journalRoleLabel('')).toBeNull();
  });

  it('reads states, levels and dates as the rest of the site does', () => {
    expect(journalValueLabel('status', 'Confirmed')).toBe('Підтверджено');
    expect(journalValueLabel('level', 'Level2')).toBe('Друга');
    expect(journalValueLabel('level', 'Fourth')).toBe('Четверте');
    expect(journalValueLabel('dateAcquired', '2026-10-23')).toBe('23.10.2026');
    expect(journalValueLabel('note', null)).toBe('—');
    expect(journalValueLabel('note', 'як є')).toBe('як є');
  });

  it('reads a policy cell as the capability and the audience, and its values as reaches', () => {
    expect(journalFieldLabel('SeeContacts/Youth')).toBe('Пошта й телефон · Юнаки');
    expect(journalValueLabel('SeeContacts/Youth', 'OwnGroup')).toBe('Свій гурток');
  });

  it('says only that private data changed, never to what', () => {
    expect(journalChangeView({ field: 'phoneNumber', redacted: true }))
      .toEqual({ field: 'Телефон', from: '', to: 'змінено', redacted: true });
    expect(journalChangeView({ field: 'status', from: 'Submitted', to: 'Confirmed' }))
      .toEqual({ field: 'Стан', from: 'Очікує підтвердження', to: 'Підтверджено', redacted: false });
    expect(journalChangeView({ field: 'point', to: 'Знає гімн' }))
      .toEqual({ field: 'Точка', from: '', to: 'Знає гімн', redacted: false });
  });
});
