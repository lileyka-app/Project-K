import { expect, type APIRequestContext } from '@playwright/test';

import { loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { e2eUsers } from './test-users';

/**
 * The first kurin number from `start` on (wrapping within 101–199) that no kurin holds, in any
 * branch, and no join request claims. The e2e database outlives a run, so a number picked at random
 * can already belong to a kurin an earlier run opened, and the server answers `KurinNumberTaken`.
 */
export async function freeKurinNumber(request: APIRequestContext, start: number): Promise<number> {
  const taken = await takenKurinNumbers(request);
  for (let offset = 0; offset < 99; offset++) {
    const candidate = 101 + ((start - 101 + offset) % 99);
    if (!taken.has(candidate)) {
      return candidate;
    }
  }

  throw new Error('Every kurin number from 101 to 199 is taken in the e2e database; reset it.');
}

async function takenKurinNumbers(request: APIRequestContext): Promise<Set<number>> {
  const login = await loginViaApi(request, e2eUsers.admin);
  expect(login.tokens?.accessToken, 'The admin API login returned no access token.').toBeTruthy();
  const headers = { Authorization: `Bearer ${login.tokens!.accessToken}` };

  const taken: (number | null | undefined)[] = [];
  const kurins = await request.get(`${e2eApiUrl}/kurin/kurins`, { headers });
  expect(kurins.ok(), `Failed to list kurins: ${kurins.status()} ${await kurins.text()}`).toBe(true);
  for (const kurin of await kurins.json() as { number?: number | null; branches?: { number: number | null }[] }[]) {
    taken.push(kurin.number, ...(kurin.branches ?? []).map(branch => branch.number));
  }

  const waitlist = await request.get(`${e2eApiUrl}/auth/onboarding/waitlist`, { headers });
  expect(waitlist.ok(), `Failed to list join requests: ${waitlist.status()} ${await waitlist.text()}`).toBe(true);
  for (const entry of await waitlist.json() as { claimedKurinNameOrNumber?: string | null; claimedUspNumber?: number | null }[]) {
    taken.push(Number(entry.claimedKurinNameOrNumber), entry.claimedUspNumber);
  }

  return new Set(taken.filter((n): n is number => Number.isInteger(n)));
}
