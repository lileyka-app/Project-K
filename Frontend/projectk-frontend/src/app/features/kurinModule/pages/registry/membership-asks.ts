import { HttpErrorResponse } from '@angular/common/http';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { kurinInText } from '../../../../shared/functions/kurin-label.function';
import {
  MembershipAskOfferDto,
  MembershipKindName,
  MembershipRequestKind,
  MembershipRequestKurinDto,
  MembershipRequestSentDto
} from '../../services/membership-service/membership.service';

/**
 * Asking for a person, said the same way wherever the registry asks: taking someone in by their code
 * (Переходи) and taking a former member back (Архів) are one request to the server.
 */
export const REQUEST_KIND_OPTIONS: { label: string; value: MembershipRequestKind }[] = [
  { label: 'Перевести до нас', value: 'Transfer' },
  { label: 'Прийняти ще й до нас', value: 'AlsoJoin' }
];

/** Між УПЮ і старшими куренями нікого не переводять: лише «прийняти ще й до нас». */
export function kindOptionsFor(offer: MembershipAskOfferDto | null | undefined): { label: string; value: MembershipRequestKind }[] {
  return offer?.transferAllowed ? REQUEST_KIND_OPTIONS : REQUEST_KIND_OPTIONS.filter(o => o.value === 'AlsoJoin');
}

/** The kind a fresh form opens on: «перевести» where it is possible at all. */
export function defaultKind(offer: MembershipAskOfferDto | null | undefined): MembershipRequestKind {
  return offer?.transferAllowed ? 'Transfer' : 'AlsoJoin';
}

/** The send button: taken in at once, an invitation to the person, or a request to a kurin. */
export function askButtonLabel(offer: MembershipAskOfferDto | null | undefined, joinLabel: string): string {
  if (offer?.joinsAtOnce) {
    return joinLabel;
  }

  return offer?.personAnswers ? 'Надіслати запрошення' : 'Надіслати запит';
}

export const MEMBERSHIP_KIND_OPTIONS: { label: string; value: MembershipKindName }[] = [
  { label: 'Юнак', value: 'Youth' },
  { label: 'Впорядник', value: 'Staff' }
];

/** What the registry says to the API's error codes for requests. */
const REQUEST_ERRORS: Record<string, string> = {
  AlreadyAMember: 'Ця людина вже у твоєму курені.',
  RequestPending: 'Запит щодо цієї людини вже чекає відповіді.',
  ChooseKurin: 'Людина стоїть у кількох куренях — вибери, кого питати.',
  NotInThatKurin: 'Людина вже не стоїть у тому курені. Знайди її за кодом ще раз.',
  MemberOwesDues: 'Людина винна куреню вкладку за квартали, що минули. Перевести можна, коли її сплачено повністю.',
  RequestClosed: 'На цей запит уже відповіли, його скасовано або минув строк.',
  MemberNotHere: 'Людини вже немає в курені.',
  TransferAcrossLines: 'Між УПЮ і старшими куренями не переводять. Можна лише прийняти ще й до нас.',
  SeniorKurinTakesMembers: 'Старший курінь приймає лише членом.',
  PersonStillYouth: 'Людина ще в УПЮ.'
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

/** The toast after a request: the person came at once, they answer themselves, or the other kurin will. */
export function requestSentMessage(sent: Pick<MembershipRequestSentDto, 'joined' | 'personAnswers'>, name: string, asked: MembershipRequestKurinDto | null, joinedSummary: string) {
  if (sent.personAnswers && !sent.joined) {
    return { severity: 'success', summary: 'Запрошення надіслано', detail: `${name} відповість сам. Запрошення діє 14 днів.` };
  }

  return sent.joined
    ? { severity: 'success', summary: joinedSummary, detail: `${name} тепер у твоєму курені.` }
    : { severity: 'success', summary: 'Запит надіслано', detail: `Відповідь дасть Звʼязковий ${kurinInText(asked?.branches, asked?.number)}. Запит діє 14 днів.` };
}
