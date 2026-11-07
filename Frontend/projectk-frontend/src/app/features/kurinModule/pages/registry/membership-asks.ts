import { HttpErrorResponse } from '@angular/common/http';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { kurinInText } from '../../../../shared/functions/kurin-label.function';
import {
  MembershipKindName,
  MembershipRequestKind,
  MembershipRequestKurinDto
} from '../../services/membership-service/membership.service';

/**
 * Asking for a person, said the same way wherever the registry asks: taking someone in by their code
 * (Переходи) and taking a former member back (Архів) are one request to the server.
 */
export const REQUEST_KIND_OPTIONS: { label: string; value: MembershipRequestKind }[] = [
  { label: 'Перевести до нас', value: 'Transfer' },
  { label: 'Прийняти ще й до нас', value: 'AlsoJoin' }
];

export const MEMBERSHIP_KIND_OPTIONS: { label: string; value: MembershipKindName }[] = [
  { label: 'Юнак', value: 'Youth' },
  { label: 'Впорядник', value: 'Staff' }
];

/** What the registry says to the API's error codes for requests. */
const REQUEST_ERRORS: Record<string, string> = {
  AlreadyAMember: 'Ця людина вже у вашому курені.',
  RequestPending: 'Запит щодо цієї людини вже чекає відповіді.',
  ChooseKurin: 'Людина стоїть у кількох куренях — вибери, кого питати.',
  NotInThatKurin: 'Людина вже не стоїть у тому курені. Знайди її за кодом ще раз.',
  MemberOwesDues: 'Людина винна куреню вкладку за квартали, що минули. Перевести можна, коли її сплачено повністю.',
  RequestClosed: 'На цей запит уже відповіли, його скасовано або минув строк.',
  MemberNotHere: 'Людини вже немає в курені.'
};

export function requestErrorText(error: unknown): string {
  const code = error instanceof HttpErrorResponse ? (error.error as { error?: string } | null)?.error : undefined;
  return (code && REQUEST_ERRORS[code]) || failureDetail(error);
}

export function kurinName(kurin: MembershipRequestKurinDto): string {
  const label = kurinInText(kurin.branches, kurin.number);
  return kurin.namedAfter ? `${label} · ${kurin.namedAfter}` : label;
}

/** Where a person stands now, for the line under their name. */
export function standsInLine(standsIn: MembershipRequestKurinDto[]): string {
  return standsIn.length ? standsIn.map(k => kurinInText(k.branches, k.number)).join(', ') : 'Ніде';
}

/** The toast after a request: the person came at once, or the other kurin will answer. */
export function requestSentMessage(joined: boolean, name: string, asked: MembershipRequestKurinDto | null, joinedSummary: string) {
  return joined
    ? { severity: 'success', summary: joinedSummary, detail: `${name} тепер у вашому курені.` }
    : { severity: 'success', summary: 'Запит надіслано', detail: `Відповідь дасть Звʼязковий ${kurinInText(asked?.branches, asked?.number)}. Запит діє 14 днів.` };
}
