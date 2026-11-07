import { APIRequestContext, expect, Page, TestInfo } from '@playwright/test';
import { randomInt } from 'node:crypto';
import { crc32, deflateSync } from 'node:zlib';

import { activateAccount, assignMentorViaApi, createGroupViaApi, getLatestInvitationByEmail, loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { scenarioEmail, scenarioSuffix } from './scenarios';
import { e2eUsers, E2eUser } from './test-users';

/**
 * The third-probe notebook (PROBE-02). Every test opens a kurin of its own with its own гурток,
 * впорядник and youth, so no seeded probe or project is touched and the rights are the kurin's
 * defaults: the Звʼязковий everywhere, the впорядник in their own гурток.
 */
export interface ProbeKurin {
  kurinKey: string;
  /** Its Звʼязковий. */
  founder: E2eUser;
  suffix: string;
  groupKey: string;
  /** Впорядник of the kurin's гурток. */
  mentor: Person;
  /** A youth of that гурток, on the third probe. */
  youth: Person;
}

export interface Person {
  memberKey: string;
  userKey: string;
  firstName: string;
  lastName: string;
  user: E2eUser;
}

const password = 'Probe@12345';

/** Sixteen today: the countdown to the 18th birthday runs, two years away. */
function sixteenYearsAgo(): string {
  const born = new Date();
  born.setFullYear(born.getFullYear() - 16);
  return born.toISOString().slice(0, 10);
}

/**
 * Kurin numbers are capped at 200; onboarding and founder suites draw from 101–199 and the tag suite
 * from 20–99, so this one draws from 20–99 too, at random per worker, and moves on when a number is taken.
 */
export async function createProbeKurin(request: APIRequestContext, testInfo: TestInfo): Promise<ProbeKurin> {
  const suffix = scenarioSuffix(testInfo);
  const email = scenarioEmail(suffix, 'probe.founder');
  const adminToken = await accessToken(request, e2eUsers.admin);

  let created = false;
  for (let attempt = 0; attempt < 8 && !created; attempt++) {
    const kurinNumber = 20 + ((testInfo.workerIndex * 23 + randomInt(80) + attempt * 53) % 80);
    const response = await request.post(`${e2eApiUrl}/auth/register/kurin`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { email, firstName: `Пробний${suffix}`, lastName: 'Звʼязковий', kurinNumber, kraiCode: null }
    });
    created = response.ok();
    // Two parallel tests that pick the same free number both pass the server's check, and the second
    // insert fails on the unique index with a 500, so that too means «taken».
    if (!created && ![400, 409, 500].includes(response.status())) {
      expect(response.ok(), `Failed to open a kurin: ${response.status()} ${await response.text()}`).toBe(true);
    }
  }
  expect(created, 'no free kurin number in 20–99 after eight tries').toBe(true);

  const invitation = await getLatestInvitationByEmail(request, email);
  await activateAccount(request, invitation.token, password);
  const founder: E2eUser = { role: 'manager', email, password };
  const login = await loginViaApi(request, founder);
  expect(login.kurinKey, 'the founder is expected to stand in the new kurin').toBeTruthy();
  const kurinKey = login.kurinKey!;

  const group = await createGroupViaApi(request, founder, kurinKey, `Гурток проби ${suffix}`);
  const mentor = await addPerson(request, founder, { kurinKey }, suffix, 'Впорядник', 'mentor', 'mentor');
  await assignMentorViaApi(request, founder, group.groupKey, mentor.userKey);
  const youth = await addPerson(request, founder, { groupKey: group.groupKey }, suffix, 'Третьопробник', 'youth', 'member', sixteenYearsAgo());

  return { kurinKey, founder, suffix, groupKey: group.groupKey, mentor, youth };
}

/** A person of the kurin with an activated account; slug is ASCII, for the address. */
export async function addPerson(
  request: APIRequestContext,
  owner: E2eUser,
  scope: { groupKey: string } | { kurinKey: string },
  suffix: string,
  lastName: string,
  slug: string,
  role: E2eUser['role'],
  dateOfBirth = '2000-01-01'
): Promise<Person> {
  const email = scenarioEmail(suffix, `probe.${slug}`);
  const firstName = `Проба${suffix.slice(-4)}`;
  const multipart = new FormData();
  multipart.append('firstName', firstName);
  multipart.append('middleName', 'Тестовий');
  multipart.append('lastName', lastName);
  multipart.append('email', email);
  multipart.append('phoneNumber', '+380501234567');
  multipart.append('dateOfBirth', dateOfBirth);
  multipart.append('createUserAccount', 'true');
  if ('groupKey' in scope) {
    multipart.append('groupKey', scope.groupKey);
  }

  const url = 'groupKey' in scope ? `${e2eApiUrl}/member` : `${e2eApiUrl}/member/kurins/${scope.kurinKey}/members`;
  const response = await request.post(url, { headers: { Authorization: `Bearer ${await accessToken(request, owner)}` }, multipart });
  expect(response.ok(), `Failed to create ${email}: ${response.status()} ${await response.text()}`).toBe(true);
  const member = await response.json() as { memberKey: string; userKey: string };

  const invitation = await getLatestInvitationByEmail(request, email);
  await activateAccount(request, invitation.token, password);
  return { memberKey: member.memberKey, userKey: member.userKey, firstName, lastName, user: { role, email, password } };
}

export async function accessToken(request: APIRequestContext, user: E2eUser): Promise<string> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  return login.tokens!.accessToken!;
}

/** The probe at the third place of the person's line, as the server finds it. */
export async function thirdProbeId(request: APIRequestContext, actor: E2eUser, memberKey: string): Promise<string> {
  const response = await request.get(`${e2eApiUrl}/member/${memberKey}/probes/line`, {
    headers: { Authorization: `Bearer ${await accessToken(request, actor)}` }
  });
  expect(response.ok(), `Failed to read the probe line: ${response.status()} ${await response.text()}`).toBe(true);
  const line = await response.json() as { id: string; order: number }[];
  const third = line.find(probe => probe.order === 3);
  expect(third, 'the line has no probe at the third place').toBeTruthy();
  return third!.id;
}

/** A project written straight through the API, as the person would through the editor. */
export async function addProject(request: APIRequestContext, actor: E2eUser, memberKey: string, probeId: string, title: string): Promise<string> {
  const response = await request.post(`${e2eApiUrl}/member/${memberKey}/probes/${probeId}/projects`, {
    headers: { Authorization: `Bearer ${await accessToken(request, actor)}` },
    data: projectBody(title, 'Підготовка: маршрут і спорядження.')
  });
  expect(response.ok(), `Failed to add project ${title}: ${response.status()} ${await response.text()}`).toBe(true);
  return (await response.json() as { projectKey: string }).projectKey;
}

export function projectBody(title: string, plan: string): Record<string, unknown> {
  return {
    title, area: 'Nature', reach: ['GroupOrKurin'], isShared: false, sharedWith: null, startsOn: null, endsOn: null,
    location: 'Чорногора', eventLeads: null, coordinatorName: 'Олена Коваль', coordinatorContact: null,
    description: 'Мандрівка для новачків гуртка.', goal: 'Пройти хребет.', plan, results: ''
  };
}

/** Status of a write the actor may or may not make; the boundary tests read only this. */
export async function apiStatus(
  request: APIRequestContext,
  actor: E2eUser,
  method: 'PUT' | 'POST' | 'DELETE',
  path: string,
  data?: unknown
): Promise<number> {
  const response = await request.fetch(`${e2eApiUrl}/${path}`, {
    method,
    headers: { Authorization: `Bearer ${await accessToken(request, actor)}` },
    data
  });
  return response.status();
}

export async function getThirdProbe(request: APIRequestContext, actor: E2eUser, memberKey: string, probeId: string): Promise<ThirdProbeTab> {
  const response = await request.get(`${e2eApiUrl}/member/${memberKey}/probes/${probeId}/third-probe`, {
    headers: { Authorization: `Bearer ${await accessToken(request, actor)}` }
  });
  expect(response.ok(), `Failed to read the third probe: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as ThirdProbeTab;
}

export interface ThirdProbeTab {
  canEdit: boolean;
  canSign: boolean;
  projects: { projectKey: string; title: string; approvedAtUtc: string | null; plan: string }[];
}

export async function openThirdProbe(page: Page, memberKey: string, probeId: string): Promise<void> {
  await page.goto(`/member/${memberKey}/probe/${probeId}`);
  await expect(page.getByTestId('third-probe-checklist')).toBeVisible({ timeout: 15_000 });
}

/** A small real PNG (solid colour), made here so its checksums are right; the server decodes and re-encodes it. */
export function pngFile(name: string, size = 24): { name: string; mimeType: string; buffer: Buffer } {
  const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(size * 3, 0x5a)]);
  const pixels = deflateSync(Buffer.concat(Array.from({ length: size }, () => row)));
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header[8] = 8;
  header[9] = 2;
  const chunk = (type: string, data: Buffer) => {
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, crc]);
  };
  const buffer = Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header), chunk('IDAT', pixels), chunk('IEND', Buffer.alloc(0))
  ]);
  return { name, mimeType: 'image/png', buffer };
}

