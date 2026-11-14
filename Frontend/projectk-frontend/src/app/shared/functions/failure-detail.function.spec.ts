import { HttpErrorResponse } from '@angular/common/http';
import {
  failureDetail,
  FORBIDDEN_DETAIL,
  isMfaRequiredRefusal,
  MFA_REQUIRED_CODE,
  MFA_REQUIRED_DETAIL,
  RETRY_DETAIL
} from './failure-detail.function';

describe('failureDetail', () => {
  const mfaRefusal = new HttpErrorResponse({ status: 403, error: { error: MFA_REQUIRED_CODE } });

  it('names the missing second factor instead of asking to retry', () => {
    expect(isMfaRequiredRefusal(mfaRefusal)).toBe(true);
    expect(failureDetail(mfaRefusal)).toBe(MFA_REQUIRED_DETAIL);
    expect(failureDetail(mfaRefusal)).not.toContain(RETRY_DETAIL);
  });

  it('does not offer a retry on any other 403', () => {
    const forbidden = new HttpErrorResponse({ status: 403, error: { message: 'Forbidden' } });

    expect(isMfaRequiredRefusal(forbidden)).toBe(false);
    expect(failureDetail(forbidden)).toBe(FORBIDDEN_DETAIL);
  });

  it('keeps the retry line for failures that may pass next time', () => {
    expect(failureDetail(new HttpErrorResponse({ status: 503 }))).toBe(RETRY_DETAIL);
    expect(failureDetail(new HttpErrorResponse({ status: 0 }))).toBe(RETRY_DETAIL);
    expect(failureDetail(new Error('network'))).toBe(RETRY_DETAIL);
    expect(failureDetail(undefined, 'Не вдалося. Спробуй ще раз.')).toBe('Не вдалося. Спробуй ще раз.');
  });

  it('treats the MFA code as a refusal only on a 403', () => {
    const wrongStatus = new HttpErrorResponse({ status: 401, error: { error: MFA_REQUIRED_CODE } });

    expect(isMfaRequiredRefusal(wrongStatus)).toBe(false);
  });
});
