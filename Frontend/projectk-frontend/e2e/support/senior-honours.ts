import { APIRequestContext, expect, Page, TestInfo } from '@playwright/test';

import { interceptAuthRefresh, loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { loginThroughUi } from './login';
import { ActivatedMemberAccount, createActivatedMemberAccount, scenarioEmail, scenarioSuffix } from './scenarios';
import { E2eUser } from './test-users';

/**
 * The demo seed's senior kurin «Орден Хрестоносців» (5-й УСП · 20-й УПС): its зв'язковий, and the
 * people the read-only checks look at. The Order calls its писар «Скриптор».
 */
export const ordenLeader: E2eUser = {
  role: 'manager',
  email: 'orden@projectk.com',
  password: process.env.E2E_DEFAULT_PASSWORD ?? 'User@12345'
};

/** The Order's писар — «Скриптор», a пластунка. */
export const ordenScriptorEmail = 'orden.fedoriv@projectk.com';

/** Carries a running пересторога beside one that has run out. */
export const ordenWarnedEmail = 'orden.lysiak@projectk.com';

const password = 'Senior@12345';

export async function ordenKurinKey(request: APIRequestContext): Promise<string> {
  const login = await loginViaApi(request, ordenLeader);
  expect(login.kurinKey, 'the Order leader is expected to stand in one kurin').toBeTruthy();
  return login.kurinKey!;
}

export async function memberKeyByEmail(request: APIRequestContext, actor: E2eUser, kurinKey: string, email: string): Promise<string> {
  const login = await loginViaApi(request, actor);
  const response = await request.get(`${e2eApiUrl}/member/kurins/${kurinKey}/members`, {
    headers: { Authorization: `Bearer ${login.tokens!.accessToken}` }
  });
  expect(response.ok(), `Failed to load kurin members: ${response.status()} ${await response.text()}`).toBe(true);
  const members = await response.json() as { memberKey: string; email?: string }[];
  const found = members.find(member => member.email?.toLowerCase() === email.toLowerCase());
  expect(found, `${email} is expected in the kurin`).toBeTruthy();
  return found!.memberKey;
}

/** A new person with an account in the Order, made by its зв'язковий; the test's own, @example.com (slug: ASCII, for the address). */
export async function createOrdenPerson(request: APIRequestContext, testInfo: TestInfo, label: string, slug: string): Promise<ActivatedMemberAccount> {
  const suffix = scenarioSuffix(testInfo);
  return createActivatedMemberAccount(
    request,
    ordenLeader,
    { kurinKey: await ordenKurinKey(request) },
    { firstName: `Сеніор${suffix}`, middleName: 'Тестовий', lastName: label, email: scenarioEmail(suffix, `senior.${slug}`) },
    password,
    'member'
  );
}

/** One API sign-in for several calls: every login pays for password hashing. */
export async function accessTokenOf(request: APIRequestContext, user: E2eUser): Promise<string> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  return login.tokens!.accessToken!;
}

/** Records a senior відзначення from the catalogue straight through the API. */
export async function recordSeniorAward(
  request: APIRequestContext,
  accessToken: string,
  memberKey: string,
  award: { catalogId: string; dateAcquired: string; note?: string }
): Promise<{ status: number; body: Record<string, unknown> }> {
  const response = await request.post(`${e2eApiUrl}/member/${memberKey}/awards`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: award
  });
  const text = await response.text();
  return { status: response.status(), body: text ? JSON.parse(text) : {} };
}

/** Signs in through the UI and keeps the session alive across reloads, as the role suites do. */
export async function signInAs(page: Page, request: APIRequestContext, user: E2eUser): Promise<void> {
  await loginThroughUi(page, user);
  await interceptAuthRefresh(page, request, user);
}

export async function openMemberCard(page: Page, memberKey: string): Promise<void> {
  await page.goto(`/member/${memberKey}`);
  await expect(page.getByTestId('degrees-tile')).toBeVisible({ timeout: 15_000 });
}
