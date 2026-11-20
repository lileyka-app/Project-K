import { APIRequestContext, expect } from '@playwright/test';

import { activateAccount, getLatestInvitationByEmail, loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { E2eUser, e2eUsers } from './test-users';

/**
 * Fresh kurins and people for the 1.4 transfer and card specs. Every kurin here is a new підготовчий
 * курінь (no number, so nothing collides between runs or projects) and every person an @example.com
 * account of its own: the specs move people between kurins, which they must never do to seeded ones.
 */

const password = 'User@12345';

/**
 * The Звʼязковий of «Лісові Чорти», the seeded 7-й курінь УСП (DemoDataSeeder.Senior). Used only over
 * the API, to send a request whose kurin must read by its гілка.
 */
export const seniorZvyazkovyi: E2eUser = { role: 'manager', email: 'chorty@projectk.com', password };

export interface FreshKurin {
  kurinKey: string;
  zvyazkovyi: E2eUser;
}

export interface FreshPerson {
  memberKey: string;
  fullName: string;
  user: E2eUser;
  /** The code the person hands over to be asked for, PL-XXXXX-XXXXX. */
  publicId: string;
}

// Signing in hashes the password, which is slow on purpose; one sign-in per account is plenty here.
const tokens = new Map<string, string>();

async function tokenOf(request: APIRequestContext, user: E2eUser): Promise<string> {
  let token = tokens.get(user.email);
  if (!token) {
    token = (await loginViaApi(request, user)).tokens!.accessToken!;
    tokens.set(user.email, token);
  }
  return token;
}

async function ok(response: { ok(): boolean; status(): number; text(): Promise<string> }, what: string): Promise<void> {
  expect(response.ok(), `${what}: ${response.status()} ${await response.text()}`).toBe(true);
}

async function api<T>(
  request: APIRequestContext,
  user: E2eUser,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  path: string,
  data?: unknown
): Promise<T> {
  const response = await request.fetch(`${e2eApiUrl}/${path}`, {
    method,
    headers: { Authorization: `Bearer ${await tokenOf(request, user)}` },
    data
  });
  await ok(response, `${method} ${path}`);
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/** A new kurin opened by the administrator, and its founder seated as Звʼязковий. */
export async function freshKurin(request: APIRequestContext, suffix: string, label: string): Promise<FreshKurin> {
  const email = `e2e.transfers.${label}.${suffix}@example.com`;
  await api(request, e2eUsers.admin, 'POST', 'auth/register/kurin', {
    email,
    firstName: 'Звʼязковий',
    lastName: `Курінь${label.toUpperCase()}`,
    phoneNumber: '+380501112233',
    kurinNumber: null,
    kraiCode: null
  });
  await activateAccount(request, (await getLatestInvitationByEmail(request, email)).token, password);

  const zvyazkovyi: E2eUser = { role: 'manager', email, password };
  const login = await loginViaApi(request, zvyazkovyi);
  expect(login.kurinKey, `${email} was not seated in a kurin`).toBeTruthy();
  tokens.set(email, login.tokens!.accessToken!);
  return { kurinKey: login.kurinKey!, zvyazkovyi };
}

/** A гурток in the kurin; dues are paid to a гурток, so a debt needs one. */
export async function groupIn(request: APIRequestContext, kurin: FreshKurin, name: string): Promise<string> {
  const group = await api<{ groupKey: string }>(request, kurin.zvyazkovyi, 'POST', 'group', {
    groupKey: '00000000-0000-0000-0000-000000000000',
    kurinKey: kurin.kurinKey,
    name
  });
  return group.groupKey;
}

/** A person with an activated account, in the kurin or in one of its гуртки. */
export async function personIn(
  request: APIRequestContext,
  kurin: FreshKurin,
  suffix: string,
  label: string,
  options: { groupKey?: string; dateOfBirth?: string; lastName?: string } = {}
): Promise<FreshPerson> {
  const email = `e2e.transfers.person.${label}.${suffix}@example.com`;
  const firstName = 'Назар';
  const lastName = options.lastName ?? `Перехід${label}`;
  const multipart = new FormData();
  multipart.append('firstName', firstName);
  multipart.append('middleName', 'Тестович');
  multipart.append('lastName', lastName);
  multipart.append('email', email);
  multipart.append('phoneNumber', '+380501234567');
  multipart.append('dateOfBirth', options.dateOfBirth ?? '2000-01-01');
  multipart.append('createUserAccount', 'true');
  if (options.groupKey) {
    multipart.append('groupKey', options.groupKey);
  }

  const url = options.groupKey ? `${e2eApiUrl}/member` : `${e2eApiUrl}/member/kurins/${kurin.kurinKey}/members`;
  const response = await request.post(url, {
    headers: { Authorization: `Bearer ${await tokenOf(request, kurin.zvyazkovyi)}` },
    multipart
  });
  await ok(response, `create ${email}`);
  const { memberKey } = await response.json() as { memberKey: string };
  await activateAccount(request, (await getLatestInvitationByEmail(request, email)).token, password);

  const user: E2eUser = { role: 'member', email, password };
  const code = await api<{ url: string }>(request, user, 'GET', 'me/transfer-code');
  const publicId = new URL(code.url).searchParams.get('code')!;
  return { memberKey, fullName: `${firstName} ${lastName}`, user, publicId };
}

/** One kurin asks the person's kurin for them, as its Звʼязковий would from «Склад куреня». */
export async function askFor(
  request: APIRequestContext,
  asking: { kurinKey: string; zvyazkovyi: E2eUser },
  person: FreshPerson,
  kind: 'Transfer' | 'AlsoJoin'
): Promise<string> {
  const sent = await api<{ membershipRequestKey: string; joined: boolean }>(
    request, asking.zvyazkovyi, 'POST', `kurin/${asking.kurinKey}/membership-requests`,
    { publicId: person.publicId, kind, membershipKind: 'Youth' });
  expect(sent.joined, 'the person stands in a kurin, so this must be a request').toBe(false);
  return sent.membershipRequestKey;
}

/** The asking kurin takes its request back, so a seeded kurin is left with nothing waiting. */
export async function withdraw(
  request: APIRequestContext,
  asking: { kurinKey: string; zvyazkovyi: E2eUser },
  membershipRequestKey: string
): Promise<void> {
  const response = await request.delete(`${e2eApiUrl}/kurin/${asking.kurinKey}/membership-requests/${membershipRequestKey}`, {
    headers: { Authorization: `Bearer ${await tokenOf(request, asking.zvyazkovyi)}` }
  });
  // Already answered or withdrawn is fine: nothing is left waiting either way.
  expect([204, 409]).toContain(response.status());
}

/** The person's membership becomes staff (впорядник): out of the kurin and straight back in as staff. */
export async function asStaffIn(request: APIRequestContext, kurin: FreshKurin, person: FreshPerson): Promise<void> {
  const headers = { Authorization: `Bearer ${await tokenOf(request, kurin.zvyazkovyi)}` };
  await ok(await request.delete(`${e2eApiUrl}/kurin/${kurin.kurinKey}/memberships/${person.memberKey}`, { headers }), 'release');
  await api(request, kurin.zvyazkovyi, 'POST', `kurin/${kurin.kurinKey}/memberships`, { memberKey: person.memberKey, publicId: null, kind: 'Staff' });
}

/** The asking kurin's request as the server answers it, unread: for the refusals. */
export async function askRaw(
  request: APIRequestContext,
  asking: { kurinKey: string; zvyazkovyi: E2eUser },
  person: FreshPerson,
  kind: 'Transfer' | 'AlsoJoin'
): Promise<{ status: number; error: string | undefined }> {
  const response = await request.post(`${e2eApiUrl}/kurin/${asking.kurinKey}/membership-requests`, {
    headers: { Authorization: `Bearer ${await tokenOf(request, asking.zvyazkovyi)}` },
    data: { publicId: person.publicId, kind, membershipKind: 'Youth' }
  });
  const body = await response.json().catch(() => ({})) as { error?: string };
  return { status: response.status(), error: body.error };
}

/** The kurin the seeded senior Звʼязковий stands in. */
export async function seniorKurin(request: APIRequestContext): Promise<{ kurinKey: string; zvyazkovyi: E2eUser }> {
  const login = await loginViaApi(request, seniorZvyazkovyi);
  tokens.set(seniorZvyazkovyi.email, login.tokens!.accessToken!);
  return { kurinKey: login.kurinKey!, zvyazkovyi: seniorZvyazkovyi };
}

/** The kurins the person currently stands in, as their kurin's Звʼязковий reads the dossier. */
export async function currentKurins(request: APIRequestContext, reader: E2eUser, person: FreshPerson): Promise<string[]> {
  const memberships = await api<{ kurinKey: string; isCurrent: boolean }[]>(
    request, reader, 'GET', `member/${person.memberKey}/dossier/memberships`);
  return memberships.filter(m => m.isCurrent).map(m => m.kurinKey).sort();
}

/** The titles in someone's notifications, newest first. */
export async function noticeTitles(request: APIRequestContext, user: E2eUser): Promise<string[]> {
  const notices = await api<{ title: string }[]>(request, user, 'GET', 'notifications');
  return notices.map(n => n.title);
}

/**
 * Leaves the person owing the kurin for quarters already behind them. A membership opened today has
 * no past, so the person is let go, their old stretch is dated back two quarters through the archive,
 * the kurin's вкладка is set from then on, and they are taken back. The debt of that old stretch stays
 * with the kurin, which is what keeps «перевести» from being approved.
 */
export async function leaveOwingPastQuarters(request: APIRequestContext, kurin: FreshKurin, person: FreshPerson): Promise<void> {
  const memberships = await api<{ membershipKey: string; kurinKey: string; isCurrent: boolean }[]>(
    request, kurin.zvyazkovyi, 'GET', `member/${person.memberKey}/dossier/memberships`);
  const stretch = memberships.find(m => m.isCurrent && m.kurinKey === kurin.kurinKey)!.membershipKey;

  await api(request, kurin.zvyazkovyi, 'DELETE', `kurin/${kurin.kurinKey}/memberships/${person.memberKey}`);

  const day = (date: Date) => date.toISOString().slice(0, 10);
  const now = new Date();
  const quarterStart = new Date(Date.UTC(now.getUTCFullYear(), Math.floor(now.getUTCMonth() / 3) * 3, 1));
  const joined = new Date(quarterStart);
  joined.setUTCMonth(joined.getUTCMonth() - 6);
  const left = new Date(quarterStart);
  left.setUTCDate(left.getUTCDate() - 10);
  await api(request, kurin.zvyazkovyi, 'PUT', `kurin/${kurin.kurinKey}/archive/memberships/${stretch}`,
    { kind: 'Youth', joinedOn: day(joined), leftOn: day(left) });

  await api(request, kurin.zvyazkovyi, 'PUT', `kurin/${kurin.kurinKey}/dues/rate`, {
    fromQuarter: { year: joined.getUTCFullYear(), number: Math.floor(joined.getUTCMonth() / 3) + 1 },
    stanytsiaFull: 100,
    stanytsiaReduced: 50,
    kurinShare: 20
  });

  await api(request, kurin.zvyazkovyi, 'POST', `kurin/${kurin.kurinKey}/memberships`,
    { memberKey: person.memberKey, publicId: null, kind: 'Youth' });
}

/** Seats the person as виховник in the kurin's КВ, beside the Звʼязковий already there. */
export async function seatVykhovnyk(request: APIRequestContext, kurin: FreshKurin, person: FreshPerson): Promise<void> {
  const kv = await api<{
    leadershipKey: string;
    startDate: string;
    leadershipHistories: { role: string; startDate: string; member: { memberKey: string; firstName: string; lastName: string; middleName: string | null } }[];
  }>(request, kurin.zvyazkovyi, 'GET', `leadership/type/KV/${kurin.kurinKey}`);

  await api(request, kurin.zvyazkovyi, 'PUT', `leadership/${kv.leadershipKey}`, {
    type: 'KV',
    entityKey: kurin.kurinKey,
    startDate: kv.startDate,
    leadershipHistories: [
      ...kv.leadershipHistories.map(h => ({
        role: h.role,
        startDate: h.startDate,
        member: { ...h.member, middleName: h.member.middleName ?? '' },
        endDate: null
      })),
      {
        role: 'Vykhovnyk',
        startDate: new Date().toISOString().slice(0, 10),
        member: { memberKey: person.memberKey, firstName: 'Назар', lastName: person.fullName.split(' ')[1], middleName: 'Тестович' },
        endDate: null
      }
    ]
  });
}
