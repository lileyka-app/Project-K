import { KurinDto } from './kurin.models';
import { kurinProfileChanged, kurinProfileDraft, kurinProfileRequest, kurinProfileValid, reportFileName } from './kurin-profile';

const kurin: KurinDto = {
  kurinKey: 'k1',
  number: 12,
  branch: 'UPYu',
  stanytsia: 'Львів',
  regionOrCountry: null,
  namedAfter: 'Івана Франка',
  description: 'Курінь юнаків.',
  profileVerificationEnabled: true,
};

describe('kurin profile', () => {
  it('starts from the record, with empty fields as empty text', () => {
    expect(kurinProfileDraft(kurin)).toEqual({
      namedAfter: 'Івана Франка',
      stanytsia: 'Львів',
      regionOrCountry: '',
      description: 'Курінь юнаків.',
    });
  });

  it('sends the whole record back, trimmed, with empty fields as null', () => {
    const draft = { ...kurinProfileDraft(kurin), stanytsia: '  Київ ', namedAfter: '   ' };
    expect(kurinProfileRequest(kurin, draft)).toEqual({
      ...kurin,
      stanytsia: 'Київ',
      namedAfter: null,
      regionOrCountry: null,
      description: 'Курінь юнаків.',
    });
  });

  it('leaves the role names out, which a УПЮ kurin is refused', () => {
    const request = kurinProfileRequest({ ...kurin, roleNames: {} }, kurinProfileDraft(kurin));
    expect('roleNames' in request).toBe(false);
  });

  it('sees no change when only spaces moved', () => {
    expect(kurinProfileChanged(kurin, kurinProfileDraft(kurin))).toBe(false);
    expect(kurinProfileChanged(kurin, { ...kurinProfileDraft(kurin), description: ' Курінь юнаків. ' })).toBe(false);
    expect(kurinProfileChanged(kurin, { ...kurinProfileDraft(kurin), regionOrCountry: 'Галичина' })).toBe(true);
    expect(kurinProfileChanged(kurin, { ...kurinProfileDraft(kurin), namedAfter: '' })).toBe(true);
  });

  it('holds the API’s limits', () => {
    const draft = kurinProfileDraft(kurin);
    expect(kurinProfileValid(draft)).toBe(true);
    expect(kurinProfileValid({ ...draft, stanytsia: 'а'.repeat(121) })).toBe(false);
    expect(kurinProfileValid({ ...draft, namedAfter: 'а'.repeat(200) })).toBe(true);
    expect(kurinProfileValid({ ...draft, description: 'а'.repeat(4001) })).toBe(false);
  });

  it('names the report as the server does', () => {
    expect(reportFileName(12, new Date(2026, 9, 1, 9, 5))).toBe('kurin-12-report-20261001-0905.pdf');
  });
});
