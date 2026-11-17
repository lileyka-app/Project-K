import { HttpErrorResponse } from '@angular/common/http';

/**
 * The `error` code the API answers with when a privileged account (admin, Зв'язковий) tries to
 * change anything before it has enabled two-factor authentication. Sent by
 * PrivilegedMfaEnforcementMiddleware with status 403 on every non-GET request until the account
 * turns it on.
 */
export const MFA_REQUIRED_CODE = 'PrivilegedMfaRequired';

export const RETRY_DETAIL = 'Спробуй ще раз.';
export const MFA_REQUIRED_DETAIL =
  'Провід без двофакторної автентифікації може лише переглядати. Увімкни її в налаштуваннях акаунта.';
export const FORBIDDEN_DETAIL = 'Немає доступу для цієї дії.';

export function isMfaRequiredRefusal(error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 403) {
    return false;
  }

  const body = error.error as { error?: unknown } | null | undefined;
  return body?.error === MFA_REQUIRED_CODE;
}

/**
 * The line under a failed action's toast or inline message.
 *
 * «Спробуй ще раз» is honest only for a transient failure. A 403 is a standing condition: the
 * account may not do this, and pressing the button again changes nothing — and when the reason is
 * a missing second factor, the person has to be told what to turn on, not to keep trying.
 */
export function failureDetail(error: unknown, retryDetail: string = RETRY_DETAIL): string {
  if (isMfaRequiredRefusal(error)) {
    return MFA_REQUIRED_DETAIL;
  }

  if (error instanceof HttpErrorResponse && error.status === 403) {
    return FORBIDDEN_DETAIL;
  }

  return retryDetail;
}
