import { APIRequestContext, expect } from '@playwright/test';

import { loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { E2eUser } from './test-users';

const seededPassword = process.env.E2E_DEFAULT_PASSWORD ?? 'User@12345';

/**
 * The demo seed's «Пластовий життєпис» (`DemoDataSeeder.SeedLifeStoryAsync`): Андрій Козак grew up
 * in kurin 1's «Ведмеді» and now stands in the Сторожі Горган (УСП/УПС). Only he opens his life
 * story; the Order's Звʼязковий is sent back to his card.
 */
export const ordenZvyazkovyi: E2eUser = { role: 'manager', email: 'storozhi@projectk.com', password: seededPassword };
const lifeStoryEmail = 'storozhi.kozak@projectk.com';
export const ordenKozak: E2eUser = { role: 'member', email: lifeStoryEmail, password: seededPassword };

export async function getLifeStoryMemberKey(request: APIRequestContext): Promise<string> {
  const login = await loginViaApi(request, ordenZvyazkovyi);
  const response = await request.get(`${e2eApiUrl}/member/kurins/${login.kurinKey}/members`, {
    headers: { Authorization: `Bearer ${login.tokens!.accessToken}` }
  });
  expect(response.ok(), `Failed to load the Order's members: ${response.status()} ${await response.text()}`).toBe(true);
  const members = await response.json() as { memberKey: string; email?: string | null }[];
  const kozak = members.find(member => member.email === lifeStoryEmail);
  expect(kozak, `${lifeStoryEmail} is not in the demo seed.`).toBeTruthy();
  return kozak!.memberKey;
}
