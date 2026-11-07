import { APIRequestContext, expect } from '@playwright/test';

import {
  activateAccount,
  approveWaitlistEntryByEmail,
  createMemberViaApi,
  CreatedMember,
  getLatestInvitationByEmail,
  loginViaApi
} from './api-client';
import { e2eApiUrl } from './e2e-api';
import { createActivatedMemberAccount, ActivatedMemberAccount, scenarioEmail } from './scenarios';
import { E2eUser, e2eUsers } from './test-users';

/** A senior kurin of the test's own: a УСП kurin opened from a join request, its Зв'язковий signed up. */
export interface SeniorKurin {
  kurinKey: string;
  founder: E2eUser;
  founderMemberKey: string;
}

export interface SeniorDuesAccount {
  membershipKey: string;
  memberKey: string;
  fullName: string;
  standing: string;
  rateNow: number | null;
  rateTagKey: string | null;
  tagKeys: string[];
  balance: number;
  quarters: { quarter: { year: number; number: number }; charged: { total: number }; paid: { total: number }; balance: number }[];
}

export interface SeniorKurinDues {
  isSenior: boolean;
  currentQuarter: { year: number; number: number };
  accounts: SeniorDuesAccount[];
  toKrai: number;
  toStanytsia: number;
  sentToKrai: number;
  sentToStanytsia: number;
}

// One spelling of every number in a kurin page: «1 800 ₴», with whatever space Intl puts in.
export function moneyText(amount: number): RegExp {
  const plain = Math.abs(amount).toString();
  const groups: string[] = [];
  for (let end = plain.length; end > 0; end -= 3) {
    groups.unshift(plain.slice(Math.max(0, end - 3), end));
  }
  const digits = groups.join('\\s');
  return new RegExp(`${amount < 0 ? '[−-]' : ''}${digits}\\s₴`);
}

async function bearer(request: APIRequestContext, user: E2eUser): Promise<Record<string, string>> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  return { Authorization: `Bearer ${login.tokens!.accessToken}` };
}

/**
 * Opens a УСП kurin the way a senior kurin's leader does: a join request with a УСП number, the
 * admin's approval, the invitation. Numbers 101–199 like the УПЮ flows, but in УСП, so the two
 * never meet; the suffix keeps two projects apart.
 */
export async function createSeniorKurin(request: APIRequestContext, suffix: string, password: string): Promise<SeniorKurin> {
  const email = scenarioEmail(suffix, 'senior.founder');
  let number = 101 + ((Date.now() + suffix.length * 37) % 99);

  for (let attempt = 0; ; attempt++) {
    const submitted = await request.post(`${e2eApiUrl}/auth/onboarding/waitlist`, {
      data: {
        firstName: `Senior${suffix}`,
        lastName: 'Zvyazkovyi',
        email,
        phoneNumber: '+380501230000',
        dateOfBirth: '1985-03-01',
        stanytsia: 'Львів',
        isKurinLeaderCandidate: true,
        claimedKurinNameOrNumber: null,
        kraiCode: 'ua',
        uspNumber: number
      }
    });
    if (submitted.ok()) {
      break;
    }

    // A УСП number another run holds is refused; try the next one.
    expect(attempt < 5, `Join request for a УСП kurin failed: ${submitted.status()} ${await submitted.text()}`).toBe(true);
    number = 101 + ((number - 100) % 99);
  }

  await approveWaitlistEntryByEmail(request, e2eUsers.admin, email);
  const invitation = await getLatestInvitationByEmail(request, email);
  await activateAccount(request, invitation.token, password);

  const founder: E2eUser = { role: 'manager', email, password };
  const login = await loginViaApi(request, founder);
  expect(login.kurinKey, 'The senior founder stands in no kurin.').toBeTruthy();
  return { kurinKey: login.kurinKey!, founder, founderMemberKey: login.memberKey! };
}

export async function createTag(request: APIRequestContext, kurin: SeniorKurin, name: string, colorHex = '#2F855A'): Promise<string> {
  const response = await request.post(`${e2eApiUrl}/kurin/${kurin.kurinKey}/tags`, {
    headers: await bearer(request, kurin.founder),
    data: { name, colorHex }
  });
  expect(response.ok(), `Failed to create tag ${name}: ${response.status()} ${await response.text()}`).toBe(true);
  const body = await response.json() as { kurinTagKey?: string; tagKey?: string };
  const key = body.kurinTagKey ?? body.tagKey;
  expect(key, `Tag ${name} came back without a key.`).toBeTruthy();
  return key!;
}

export async function setMemberTags(request: APIRequestContext, kurin: SeniorKurin, memberKey: string, tagKeys: string[]): Promise<void> {
  const response = await request.put(`${e2eApiUrl}/member/${memberKey}/tags`, {
    headers: await bearer(request, kurin.founder),
    data: { tagKeys }
  });
  expect(response.ok(), `Failed to tag ${memberKey}: ${response.status()} ${await response.text()}`).toBe(true);
}

export async function addSeniorMember(
  request: APIRequestContext,
  kurin: SeniorKurin,
  suffix: string,
  label: string,
  lastName: string
): Promise<CreatedMember> {
  return createMemberViaApi(request, kurin.founder, { kurinKey: kurin.kurinKey }, {
    firstName: `${label}${suffix}`.replace(/\W/g, ''),
    middleName: 'Senior',
    lastName,
    email: scenarioEmail(suffix, `senior.${label.toLowerCase()}`)
  });
}

export async function addSeniorMemberWithAccount(
  request: APIRequestContext,
  kurin: SeniorKurin,
  suffix: string,
  label: string,
  lastName: string,
  password: string
): Promise<ActivatedMemberAccount> {
  return createActivatedMemberAccount(request, kurin.founder, { kurinKey: kurin.kurinKey }, {
    firstName: `${label}${suffix}`.replace(/\W/g, ''),
    middleName: 'Senior',
    lastName,
    email: scenarioEmail(suffix, `senior.${label.toLowerCase()}`)
  }, password, 'member');
}

/**
 * Seats a person in the kurin's провід the only way the API knows: out of the kurin and straight back
 * in as staff. A fresh person owes nothing for past quarters, so letting them go is not refused.
 */
export async function rejoinAsStaff(request: APIRequestContext, kurin: SeniorKurin, memberKey: string): Promise<void> {
  const headers = await bearer(request, kurin.founder);
  const left = await request.delete(`${e2eApiUrl}/kurin/${kurin.kurinKey}/memberships/${memberKey}`, { headers });
  expect(left.ok(), `Failed to release ${memberKey}: ${left.status()} ${await left.text()}`).toBe(true);

  const joined = await request.post(`${e2eApiUrl}/kurin/${kurin.kurinKey}/memberships`, {
    headers,
    data: { memberKey, kind: 'Staff' }
  });
  expect(joined.ok(), `Failed to take ${memberKey} back as staff: ${joined.status()} ${await joined.text()}`).toBe(true);
}

export interface RateArticle {
  name: string;
  recipient: 'Stanytsia' | 'Krai' | 'Kurin';
  amount: number;
  reducedAmount?: number;
}

export async function setSeniorRate(
  request: APIRequestContext,
  kurin: SeniorKurin,
  fromQuarter: { year: number; number: number },
  articles: RateArticle[],
  tagKey: string | null = null
): Promise<void> {
  const response = await request.put(`${e2eApiUrl}/kurin/${kurin.kurinKey}/dues/rate`, {
    headers: await bearer(request, kurin.founder),
    data: { fromQuarter, tagKey, articles }
  });
  expect(response.ok(), `Failed to set a rate: ${response.status()} ${await response.text()}`).toBe(true);
}

export async function getSeniorDues(request: APIRequestContext, user: E2eUser, kurinKey: string): Promise<SeniorKurinDues> {
  const response = await request.get(`${e2eApiUrl}/kurin/${kurinKey}/dues`, { headers: await bearer(request, user) });
  expect(response.ok(), `Failed to read kurin dues: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as SeniorKurinDues;
}

export async function getSeniorDuesStatus(request: APIRequestContext, user: E2eUser, kurinKey: string): Promise<number> {
  const response = await request.get(`${e2eApiUrl}/kurin/${kurinKey}/dues`, { headers: await bearer(request, user) });
  return response.status();
}

export function accountOf(dues: SeniorKurinDues, memberKey: string): SeniorDuesAccount {
  const account = dues.accounts.find(a => a.memberKey === memberKey);
  expect(account, `No account for ${memberKey} in the kurin's register.`).toBeTruthy();
  return account!;
}

/** What a person owes for quarters already over: what keeps them from being released. */
export function owedForPastQuarters(account: SeniorDuesAccount, current: { year: number; number: number }): number {
  const index = (q: { year: number; number: number }) => q.year * 4 + q.number;
  return account.quarters
    .filter(q => index(q.quarter) < index(current) && q.balance < 0)
    .reduce((sum, q) => sum - q.balance, 0);
}
