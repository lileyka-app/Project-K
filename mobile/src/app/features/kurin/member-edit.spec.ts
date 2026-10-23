import { HttpErrorResponse } from '@angular/common/http';
import { CardFields, MemberDto, MemberWarningDto } from './kurin.models';
import {
  DEFAULT_CARD_FIELDS,
  NO_WARNING_CHANGES,
  canClearWarning,
  canEditEmail,
  cardExtraRows,
  cardExtrasDraft,
  cardExtrasValid,
  cardFieldError,
  cardFieldsOf,
  changedCardExtras,
  fieldMode,
  canSelectWarning,
  closeBranch,
  hiddenGroupOf,
  memberFormData,
  newMemberFormData,
  oldEnough,
  openBranches,
  rankDates,
  rankHistories,
  toggleWarning,
  warningCalls,
  warningChecked,
} from './member-edit';

const now = Date.parse('2026-10-10T12:00:00Z');
const visaCard: CardFields = {
  fields: { middleName: 'hidden', phoneNumber: 'optional', address: 'optional', school: 'hidden', parents: 'optional' },
  extra: [
    { key: 'visaType', label: 'Тип візи', type: 'text', required: true },
    { key: 'visaUntil', label: 'Віза до', type: 'date', required: false },
  ],
};
const warning = (key: string, level: MemberWarningDto['level'], issuedBy: string, extra: Partial<MemberWarningDto> = {}): MemberWarningDto => ({
  memberWarningKey: key, level, issuedAtUtc: '2026-09-01T00:00:00Z', expiresAtUtc: '2026-12-01T00:00:00Z', issuedByUserKey: issuedBy, ...extra,
});


const NO_PARENTS = { motherFirstName: '', motherLastName: '', motherPhone: '', fatherFirstName: '', fatherLastName: '', fatherPhone: '' };
describe('member edit rules', () => {
  it('shows the email only without a claimed account, or to an admin', () => {
    expect(canEditEmail({ userKey: null }, false)).toBe(true);
    expect(canEditEmail({ userKey: 'u1', accountStatus: 'PendingActivation' }, false)).toBe(true);
    expect(canEditEmail({ userKey: 'u1', accountStatus: 'Active' }, false)).toBe(false);
    expect(canEditEmail({ userKey: 'u1', accountStatus: 'Active' }, true)).toBe(true);
  });

  it('opens the branches that have dates and clears a branch that is switched off', () => {
    const dates = rankDates([
      { plastLevel: 'Uchasnyk', dateAchieved: '2022-04-23' },
      { plastLevel: 'Skob', dateAchieved: '2024-05-01T00:00:00' },
    ]);
    expect(dates['Skob']).toBe('2024-05-01');
    expect(openBranches(dates)).toEqual({ UPYu: true, USP: false, UPS: false });
    const closed = closeBranch(dates, 'UPYu');
    expect(closed['Skob']).toBe('');
    expect(closed['Uchasnyk']).toBe('2022-04-23');
  });

  it('sends the achieved ступені, keeping the keys of rows that exist', () => {
    const existing = [{ plastLevelHistoryKey: 'pl1', memberKey: 'm1', plastLevel: 'Uchasnyk', dateAchieved: '2022-04-23' }];
    const dates = { ...rankDates(existing), Uchasnyk: '2022-04-24', Rozviduvach: '2024-01-02', Entry: '' };
    expect(rankHistories(existing, dates)).toEqual([
      { plastLevelHistoryKey: 'pl1', memberKey: 'm1', plastLevel: 'Uchasnyk', dateAchieved: '2022-04-24' },
      { plastLevel: 'Rozviduvach', dateAchieved: '2024-01-02' },
    ]);
  });

  it('lets each пересторога follow the one before and only its author lift it', () => {
    const warnings = [warning('w1', 'Level1', 'U-LEAD'), warning('old', 'Level2', 'u-lead', { revokedAtUtc: '2026-09-02T00:00:00Z' })];
    const draft = NO_WARNING_CHANGES;
    expect(warningChecked(warnings, draft, 'Level1', now)).toBe(true);
    expect(warningChecked(warnings, draft, 'Level2', now)).toBe(false);
    expect(canSelectWarning(warnings, draft, 'Level2', now)).toBe(true);
    expect(canSelectWarning(warnings, draft, 'Level3', now)).toBe(false);
    expect(canClearWarning(warnings, draft, 'Level1', 'u-lead', now)).toBe(true);
    expect(canClearWarning(warnings, draft, 'Level1', 'u-other', now)).toBe(false);
    // Somebody else's stays as it is.
    expect(toggleWarning(warnings, draft, 'Level1', false, 'u-other', now)).toBe(draft);
  });

  it('plans lifts first, then additions from the lowest level', () => {
    const warnings = [warning('w1', 'Level1', 'u-lead')];
    let draft = toggleWarning(warnings, NO_WARNING_CHANGES, 'Level2', true, 'u-lead', now);
    draft = toggleWarning(warnings, draft, 'Level3', true, 'u-lead', now);
    draft = toggleWarning(warnings, draft, 'Level1', false, 'u-lead', now);
    expect(warningCalls(draft)).toEqual([
      { kind: 'cancel', key: 'w1' },
      { kind: 'assign', level: 'Level2' },
      { kind: 'assign', level: 'Level3' },
    ]);
    // Ticking a lifted one back just takes the lift away again.
    draft = toggleWarning(warnings, draft, 'Level1', true, 'u-lead', now);
    expect(draft.cancel).toEqual([]);
  });

  it('expired перестороги are not in force', () => {
    const warnings = [warning('w1', 'Level1', 'u-lead', { expiresAtUtc: '2026-10-01T00:00:00' })];
    expect(warningChecked(warnings, NO_WARNING_CHANGES, 'Level1', now)).toBe(false);
  });

  it('builds the multipart body as the web does, sending the address and school only when changed', () => {
    const member = { memberKey: 'm1', firstName: 'Остап', lastName: 'Коваль', email: 'o@example.com', address: 'Львів', school: null } as unknown as MemberDto;
    const photo = new Blob(['x'], { type: 'image/jpeg' });
    const body = memberFormData(
      member,
      { firstName: ' Остап ', middleName: 'Петрович', lastName: 'Коваль', email: 'o@example.com', phoneNumber: '+380', dateOfBirth: '2012-03-14', address: 'Львів', school: ' Ліцей 3 ', ...NO_PARENTS, motherPhone: '+420777000222' },
      [{ plastLevelHistoryKey: 'pl1', plastLevel: 'Uchasnyk', dateAchieved: '2022-04-23T00:00:00' }],
      { blob: photo, name: 'me.jpg', remove: false },
    );
    expect(body.get('firstName')).toBe('Остап');
    expect(body.has('address')).toBe(false);
    expect(body.get('school')).toBe('Ліцей 3');
    expect(body.get('motherPhone')).toBe('+420777000222');
    expect(body.has('motherFirstName')).toBe(false);
    expect(body.get('plastLevelHistories[0].dateAchieved')).toBe('2022-04-23');
    expect(body.get('plastLevelHistories[0].plastLevelHistoryKey')).toBe('pl1');
    expect(body.get('removeProfilePhoto')).toBe('false');
    expect((body.get('blob') as File).name).toBe('me.jpg');

    const cleared = memberFormData(member, { firstName: 'a', middleName: 'b', lastName: 'c', email: '', phoneNumber: '', dateOfBirth: '', address: '', school: '', ...NO_PARENTS }, [], { blob: null, name: '', remove: true });
    expect(cleared.get('removeProfilePhoto')).toBe('true');
    expect(cleared.get('address')).toBe('');
    expect(cleared.has('blob')).toBe(false);
  });

  it('builds a new member’s body with its place and an account, as the web’s create does', () => {
    const form = { firstName: 'Марта ', middleName: 'Іванівна', lastName: 'Шевчук', email: ' marta@example.com ', phoneNumber: '+380 67 111 22 33', dateOfBirth: '2013-05-02', address: ' Київ ', school: '', ...NO_PARENTS, fatherFirstName: ' Петро ' };
    const photo = new Blob(['x'], { type: 'image/jpeg' });
    const inGroup = newMemberFormData({ groupKey: 'g1' }, form, [{ plastLevel: 'Prykhylnyk', dateAchieved: '2024-01-10' }], { blob: photo, name: 'marta.jpg' });
    expect(inGroup.get('groupKey')).toBe('g1');
    expect(inGroup.has('kurinKey')).toBe(false);
    expect(inGroup.get('createUserAccount')).toBe('true');
    expect(inGroup.get('firstName')).toBe('Марта');
    expect(inGroup.get('email')).toBe('marta@example.com');
    expect(inGroup.get('plastLevelHistories[0].plastLevel')).toBe('Prykhylnyk');
    expect(inGroup.has('removeProfilePhoto')).toBe(false);
    expect(inGroup.get('address')).toBe('Київ');
    expect(inGroup.has('school')).toBe(false);
    expect(inGroup.get('fatherFirstName')).toBe('Петро');
    expect(inGroup.has('motherPhone')).toBe(false);
    expect((inGroup.get('blob') as File).name).toBe('marta.jpg');

    const inKurin = newMemberFormData({ kurinKey: 'k1' }, form, [], { blob: null, name: '' });
    expect(inKurin.get('kurinKey')).toBe('k1');
    expect(inKurin.has('groupKey')).toBe(false);
    expect(inKurin.has('blob')).toBe(false);
  });

  it('takes a member from their sixth birthday on, as the web’s minAge does', () => {
    expect(oldEnough('2020-10-11', '2026-10-11')).toBe(true);
    expect(oldEnough('2020-10-12', '2026-10-11')).toBe(false);
    expect(oldEnough('2020-11-01', '2026-10-11')).toBe(false);
    expect(oldEnough('2012-03-14', '2026-10-11')).toBe(true);
    expect(oldEnough('', '2026-10-11')).toBe(false);
  });

  it('reads the край’s card fields, falling back to both fields asked and nothing more', () => {
    expect(cardFieldsOf(null)).toEqual(DEFAULT_CARD_FIELDS);
    expect(cardFieldsOf({})).toEqual(DEFAULT_CARD_FIELDS);
    expect(cardFieldsOf({ cardFields: { fields: { middleName: 'hidden', phoneNumber: 'sometimes', address: 'sometimes' } } })).toEqual({
      fields: { middleName: 'hidden', phoneNumber: 'required', address: 'optional', school: 'optional', parents: 'optional' },
      extra: [],
    });
    expect(cardFieldsOf({ cardFields: { fields: { middleName: 'hidden', phoneNumber: 'optional', school: 'hidden' }, extra: [...visaCard.extra, { key: '', label: 'x' }] } })).toEqual(visaCard);
    expect(fieldMode(visaCard, 'middleName')).toBe('hidden');
    expect(fieldMode(visaCard, 'phoneNumber')).toBe('optional');
    expect(fieldMode(visaCard, 'firstName')).toBe('required');
    expect(fieldMode(visaCard, 'address')).toBe('optional');
    expect(fieldMode(visaCard, 'school')).toBe('hidden');
    expect(fieldMode(visaCard, 'motherPhone')).toBe('optional');
    expect(fieldMode({ ...visaCard, fields: { ...visaCard.fields, parents: 'required' } }, 'fatherPhone')).toBe('optional');
    expect(fieldMode({ ...visaCard, fields: { ...visaCard.fields, parents: 'hidden' } }, 'fatherPhone')).toBe('hidden');
  });

  it('edits one value per extra field and holds required ones and dates', () => {
    const draft = cardExtrasDraft(visaCard, { visaType: 'D', retired: 'x' });
    expect(draft).toEqual({ visaType: 'D', visaUntil: '' });
    expect(cardExtrasValid(visaCard, draft)).toBe(true);
    expect(cardExtrasValid(visaCard, { ...draft, visaType: '  ' })).toBe(false);
    expect(cardExtrasValid(visaCard, { ...draft, visaUntil: '2027-13' })).toBe(false);
    expect(cardExtrasValid(visaCard, { ...draft, visaUntil: '2027-03-01' })).toBe(true);
    expect(cardExtrasValid(DEFAULT_CARD_FIELDS, {})).toBe(true);
  });

  it('sends every extra field, a blank one as empty', () => {
    const member = { memberKey: 'm1', firstName: 'Остап', lastName: 'Коваль', email: 'o@example.com' } as unknown as MemberDto;
    const form = { firstName: 'Остап', middleName: '', lastName: 'Коваль', email: 'o@example.com', phoneNumber: '', dateOfBirth: '2012-03-14', address: '', school: '', ...NO_PARENTS };
    const body = memberFormData(member, form, [], { blob: null, name: '', remove: false }, { visaType: ' D ', visaUntil: '' });
    expect(body.get('cardExtras[visaType]')).toBe('D');
    expect(body.get('cardExtras[visaUntil]')).toBe('');
    const created = newMemberFormData({ kurinKey: 'k1' }, form, [], { blob: null, name: '' }, { visaType: 'C' });
    expect(created.get('cardExtras[visaType]')).toBe('C');
    expect([...memberFormData(member, form, [], { blob: null, name: '', remove: false }).keys()].some((k) => k.startsWith('cardExtras'))).toBe(false);
  });

  it('leaves out every group of data the kurin hides from the writer, so the server keeps it', () => {
    const member = {
      memberKey: 'm1',
      firstName: 'Остап',
      lastName: 'Коваль',
      email: '',
      address: null,
      hiddenData: ['contacts', 'birthDate', 'address', 'parents', 'extras'],
    } as unknown as MemberDto;
    const form = { firstName: 'Остап', middleName: '', lastName: 'Коваль', email: '', phoneNumber: '', dateOfBirth: '', address: 'Нова', school: '', ...NO_PARENTS, motherPhone: '+380' };

    const body = memberFormData(member, form, [], { blob: null, name: '', remove: false }, { visaType: '' });

    expect(body.get('firstName')).toBe('Остап');
    for (const key of ['email', 'phoneNumber', 'dateOfBirth', 'address', 'school', 'motherPhone']) expect(body.has(key)).toBe(false);
    expect([...body.keys()].some((k) => k.startsWith('cardExtras'))).toBe(false);
    expect(['email', 'dateOfBirth', 'school', 'fatherPhone', 'firstName'].map(hiddenGroupOf)).toEqual(['contacts', 'birthDate', 'address', 'parents', null]);
  });

  it('sends only the extra fields changed from what was read, an emptied one as empty', () => {
    const loaded = { visaType: 'D', visaUntil: '2027-03-01' };
    expect(changedCardExtras(loaded, { visaType: ' D ', visaUntil: '' })).toEqual({ visaUntil: '' });
    expect(changedCardExtras(loaded, loaded)).toEqual({});
    // A viewer shown no values: what they leave empty stays as stored.
    const unseen = cardExtrasDraft(visaCard, {});
    expect(changedCardExtras(unseen, { visaType: '', visaUntil: '2027-05-01' })).toEqual({ visaUntil: '2027-05-01' });
    // A new member: only the filled ones.
    expect(changedCardExtras(unseen, { visaType: 'C', visaUntil: '' })).toEqual({ visaType: 'C' });
  });

  it('shows the extra fields that have a value, dates as the card writes them', () => {
    expect(cardExtraRows(visaCard, { visaType: 'D', visaUntil: '2027-03-01' })).toEqual([
      { key: 'visaType', label: 'Тип візи', value: 'D' },
      { key: 'visaUntil', label: 'Віза до', value: '1 березня 2027' },
    ]);
    expect(cardExtraRows(visaCard, { visaType: ' ' })).toEqual([]);
    expect(cardExtraRows(visaCard, undefined)).toEqual([]);
  });

  it('passes on the server’s wording only for a missing required field', () => {
    const refused = (body: unknown) => new HttpErrorResponse({ status: 400, error: body });
    expect(cardFieldError(refused({ error: 'CardFieldRequired', message: 'Заповни поле «Тип візи».' }))).toBe('Заповни поле «Тип візи».');
    expect(cardFieldError(refused({ error: 'ValidationFailed', message: 'x' }))).toBeNull();
    expect(cardFieldError(new Error('x'))).toBeNull();
  });
});
