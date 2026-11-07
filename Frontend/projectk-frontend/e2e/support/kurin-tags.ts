import { APIRequestContext, expect, Locator, Page, TestInfo } from '@playwright/test';

import { activateAccount, createMemberViaApi, CreatedMember, getLatestInvitationByEmail, loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { ActivatedMemberAccount, createActivatedMemberAccount, scenarioEmail, scenarioSuffix } from './scenarios';
import { e2eUsers, E2eUser } from './test-users';

/**
 * Kurin tags (TAG-01…03). Tags are switched on and off per kurin, and the senior dues suite reads the
 * seeded tags of «Орден Хрестоносців», so every test here opens a kurin of its own and touches no
 * seeded tag.
 */
export interface TagKurin {
  kurinKey: string;
  /** Its Звʼязковий, who sets the kurin up and holds every tag right by default. */
  founder: E2eUser;
  suffix: string;
}

export interface KurinTag {
  kurinTagKey: string;
  name: string;
  colorHex: string;
}

const password = 'Tags@12345';

/**
 * Kurin numbers are capped at 200, and the onboarding and founder suites draw theirs from 101–199,
 * so this one draws from 20–99 and moves on when a number is already taken.
 */
export async function createTagKurin(request: APIRequestContext, testInfo: TestInfo): Promise<TagKurin> {
  const suffix = scenarioSuffix(testInfo);
  const email = scenarioEmail(suffix, 'tags.founder');
  const adminToken = await accessToken(request, e2eUsers.admin);

  let created = false;
  for (let attempt = 0; attempt < 8 && !created; attempt++) {
    const kurinNumber = 20 + ((Date.now() + attempt * 37) % 80);
    const response = await request.post(`${e2eApiUrl}/auth/register/kurin`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { email, firstName: `Теговий${suffix}`, lastName: 'Звʼязковий', kurinNumber, kraiCode: null }
    });
    created = response.ok();
    if (!created && response.status() !== 400 && response.status() !== 409) {
      expect(response.ok(), `Failed to open a kurin: ${response.status()} ${await response.text()}`).toBe(true);
    }
  }
  expect(created, 'no free kurin number in 20–99 after eight tries').toBe(true);

  const invitation = await getLatestInvitationByEmail(request, email);
  await activateAccount(request, invitation.token, password);
  const founder: E2eUser = { role: 'manager', email, password };
  const login = await loginViaApi(request, founder);
  expect(login.kurinKey, 'the founder is expected to stand in the new kurin').toBeTruthy();

  return { kurinKey: login.kurinKey!, founder, suffix };
}

/** A person of the kurin without an account; slug is ASCII, for the address. */
export async function addPerson(request: APIRequestContext, kurin: TagKurin, lastName: string, slug: string): Promise<CreatedMember> {
  return createMemberViaApi(request, kurin.founder, { kurinKey: kurin.kurinKey }, {
    firstName: `Тег${kurin.suffix}`,
    middleName: 'Тестовий',
    lastName,
    email: scenarioEmail(kurin.suffix, `tags.${slug}`)
  });
}

/** A plain youth of the kurin with an account: no office, no tag rights. */
export async function addYouthAccount(request: APIRequestContext, kurin: TagKurin, lastName: string, slug: string): Promise<ActivatedMemberAccount> {
  return createActivatedMemberAccount(
    request,
    kurin.founder,
    { kurinKey: kurin.kurinKey },
    { firstName: `Тег${kurin.suffix}`, middleName: 'Тестовий', lastName, email: scenarioEmail(kurin.suffix, `tags.${slug}`) },
    password,
    'member'
  );
}

export async function accessToken(request: APIRequestContext, user: E2eUser): Promise<string> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  return login.tokens!.accessToken!;
}

export async function setTagsEnabled(request: APIRequestContext, token: string, kurinKey: string, enabled: boolean): Promise<void> {
  const response = await request.put(`${e2eApiUrl}/kurin/${kurinKey}/tag-settings`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { enabled, ownTagsVisible: true }
  });
  expect(response.ok(), `Failed to switch tags ${enabled ? 'on' : 'off'}: ${response.status()} ${await response.text()}`).toBe(true);
}

export async function createTag(request: APIRequestContext, token: string, kurinKey: string, name: string): Promise<KurinTag> {
  const response = await request.post(`${e2eApiUrl}/kurin/${kurinKey}/tags`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name, colorHex: '#2F855A' }
  });
  expect(response.ok(), `Failed to create tag ${name}: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as KurinTag;
}

export async function setMemberTags(request: APIRequestContext, token: string, memberKey: string, tagKeys: string[]): Promise<number> {
  const response = await request.put(`${e2eApiUrl}/member/${memberKey}/tags`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { tagKeys }
  });
  return response.status();
}

/** Opens the kurin's own tag list in the registry, behind «Теги куреня». */
export async function openTagManager(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Теги куреня' }).click();
  const tile = page.getByRole('region', { name: 'Теги куреня' });
  await expect(tile).toBeVisible();
  return tile;
}

export async function openRegistry(page: Page): Promise<void> {
  await page.goto('/kurin/registry');
  await expect(page.getByRole('heading', { name: 'Реєстр' })).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole('button', { name: 'Вивантажити' })).toBeEnabled();
}

/** The registry cells of a column; the phone layout drops the header row but keeps these labels. */
export function registryCells(page: Page, header: string): Locator {
  return page.locator(`td[data-mobile-label="${header}"]`);
}

/**
 * Presses the settings page's «Зберегти» and waits for the tag settings to land. Not the «Збережено»
 * toast: one from an earlier save may still be on screen.
 */
export async function saveSettings(page: Page): Promise<void> {
  const saved = page.waitForResponse(response => response.request().method() === 'PUT' && response.url().endsWith('/tag-settings'));
  await page.getByRole('button', { name: 'Зберегти' }).click();
  expect((await saved).ok()).toBe(true);
}

/** Closes a list opened from a dialog by clicking the dialog's title: Escape would close the dialog too. */
export async function closeOverlayIn(form: Locator): Promise<void> {
  await form.locator('.p-dialog-title').click();
}

/** Presses «Вивантажити» and returns the columns the page asked the file for. */
export async function exportedColumns(page: Page): Promise<string[]> {
  const exported = page.waitForRequest(request => request.method() === 'POST' && request.url().includes('/registry/export'));
  await page.getByRole('button', { name: 'Вивантажити' }).click();
  return ((await exported).postDataJSON() as { columns: string[] }).columns;
}
