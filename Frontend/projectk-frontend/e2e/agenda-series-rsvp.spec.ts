import { APIRequestContext, expect, test } from '@playwright/test';

import { getSeededKurinKey, loginViaApi } from './support/api-client';
import { e2eApiUrl } from './support/e2e-api';
import { describeRole } from './support/role-test';
import { scenarioSuffix } from './support/scenarios';
import { E2eUser } from './support/test-users';

/**
 * An RSVP on a recurring event is an answer to one occurrence (AGENDA-05): «Іду» on the first сходини
 * of a series must leave the second untouched, and a day the series does not meet on cannot be
 * answered at all.
 */
interface SeriesScenario {
  agendaItemKey: string;
  title: string;
  firstStartUtc: string;
  secondStartUtc: string;
}

async function bearer(request: APIRequestContext, user: E2eUser): Promise<{ Authorization: string }> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  return { Authorization: `Bearer ${login.tokens!.accessToken}` };
}

/** The seeded kurin's offset from UTC at that moment, in minutes; it lives in Ukraine. */
function kurinOffsetMinutes(at: Date): number {
  const zone = new Intl.DateTimeFormat('en-US', { timeZone: 'Europe/Kyiv', timeZoneName: 'longOffset' })
    .formatToParts(at)
    .find(part => part.type === 'timeZoneName')!.value;
  const match = /GMT([+-])(\d{2}):(\d{2})/.exec(zone);
  return match ? (match[1] === '-' ? -1 : 1) * (Number(match[2]) * 60 + Number(match[3])) : 0;
}

/**
 * A weekly series of exactly two occurrences, the first tomorrow at 17:00 UTC, aimed at the whole kurin.
 * The series repeats on the kurin's wall clock, so a week that crosses a DST change moves the second
 * occurrence by an hour in UTC — adding seven days of milliseconds would name a time it never meets.
 */
async function createTwoOccurrenceSeries(request: APIRequestContext, user: E2eUser, suffix: string): Promise<SeriesScenario> {
  const kurinKey = await getSeededKurinKey(request, user);
  const first = new Date();
  first.setUTCDate(first.getUTCDate() + 1);
  first.setUTCHours(17, 0, 0, 0);
  const weekLater = new Date(first.getTime() + 7 * 24 * 60 * 60 * 1000);
  const second = new Date(weekLater.getTime() + (kurinOffsetMinutes(first) - kurinOffsetMinutes(weekLater)) * 60 * 1000);
  const title = `Сходини серії ${suffix}`;

  const response = await request.post(`${e2eApiUrl}/agenda`, {
    headers: await bearer(request, user),
    data: {
      kurinKey,
      kind: 'Event',
      title,
      description: null,
      location: null,
      startUtc: first.toISOString(),
      endUtc: new Date(first.getTime() + 60 * 60 * 1000).toISOString(),
      isAllDay: false,
      agendaCategoryKey: null,
      recurrenceFrequency: 'Weekly',
      recurrenceInterval: 1,
      recurrenceByWeekday: 1 << first.getUTCDay(),
      recurrenceEndUtc: null,
      recurrenceCount: 2,
      targets: [{ targetType: 'Kurin', targetKey: kurinKey, completionMode: 'Shared' }]
    }
  });
  expect(response.ok(), `Failed to create the series: ${response.status()} ${await response.text()}`).toBe(true);
  const agendaItemKey = (await response.json()) as string;

  return { agendaItemKey, title, firstStartUtc: first.toISOString(), secondStartUtc: second.toISOString() };
}

async function myStatusOn(request: APIRequestContext, headers: { Authorization: string }, series: SeriesScenario, occurrenceStartUtc: string): Promise<string | null> {
  const response = await request.get(`${e2eApiUrl}/agenda/${series.agendaItemKey}/responses`, {
    headers,
    params: { occurrenceStartUtc }
  });
  expect(response.ok(), `Reading responses failed: ${response.status()} ${await response.text()}`).toBe(true);
  const picture = (await response.json()) as { myStatus: string | null; occurrenceStartUtc: string | null };
  expect(new Date(picture.occurrenceStartUtc!).toISOString()).toBe(occurrenceStartUtc);
  return picture.myStatus;
}

describeRole('manager', 'RSVP on a recurring event', ({ user }) => {
  test('answers one occurrence without touching the other, and refuses a day the series skips', async ({ request }, testInfo) => {
    const series = await createTwoOccurrenceSeries(request, user, scenarioSuffix(testInfo));
    const headers = await bearer(request, user);

    const going = await request.put(`${e2eApiUrl}/agenda/${series.agendaItemKey}/response`, {
      headers,
      data: { status: 'Going', occurrenceStartUtc: series.firstStartUtc }
    });
    expect(going.ok(), `RSVP failed: ${going.status()} ${await going.text()}`).toBe(true);

    expect(await myStatusOn(request, headers, series, series.firstStartUtc)).toBe('Going');
    expect(await myStatusOn(request, headers, series, series.secondStartUtc)).toBeNull();

    const notGoing = await request.put(`${e2eApiUrl}/agenda/${series.agendaItemKey}/response`, {
      headers,
      data: { status: 'NotGoing', occurrenceStartUtc: series.secondStartUtc }
    });
    expect(notGoing.ok()).toBe(true);
    expect(await myStatusOn(request, headers, series, series.firstStartUtc)).toBe('Going');
    expect(await myStatusOn(request, headers, series, series.secondStartUtc)).toBe('NotGoing');

    const dayAfter = new Date(new Date(series.firstStartUtc).getTime() + 24 * 60 * 60 * 1000).toISOString();
    const refused = await request.put(`${e2eApiUrl}/agenda/${series.agendaItemKey}/response`, {
      headers,
      data: { status: 'Going', occurrenceStartUtc: dayAfter }
    });
    expect(refused.status()).toBe(400);
    const withoutOccurrence = await request.put(`${e2eApiUrl}/agenda/${series.agendaItemKey}/response`, {
      headers,
      data: { status: 'Going', occurrenceStartUtc: null }
    });
    expect(withoutOccurrence.status()).toBe(400);
  });

  test('the dashboard shows each occurrence with its own answer', async ({ page, request }, testInfo) => {
    const series = await createTwoOccurrenceSeries(request, user, scenarioSuffix(testInfo));
    const headers = await bearer(request, user);
    await request.put(`${e2eApiUrl}/agenda/${series.agendaItemKey}/response`, {
      headers,
      data: { status: 'Going', occurrenceStartUtc: series.firstStartUtc }
    });

    await page.goto('/');
    const rows = page.locator('.events-tile__row', { hasText: series.title });
    await expect(rows).toHaveCount(2);

    // The answered occurrence shows «Іду» pressed; the other carries no answer (and, with no RSVP
    // asked for by a group, no answer buttons at all).
    const firstAnswers = rows.nth(0).getByRole('group', { name: `Відповідь: ${series.title}` });
    await expect(firstAnswers.locator('button[data-status="Going"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(rows.nth(1).getByRole('group')).toHaveCount(0);

    // Changing the answer from the dashboard stays on that one row.
    await firstAnswers.locator('button[data-status="NotGoing"]').click();
    await expect(firstAnswers.locator('button[data-status="NotGoing"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(rows.nth(1).getByRole('group')).toHaveCount(0);
    expect(await myStatusOn(request, headers, series, series.secondStartUtc)).toBeNull();
  });
});
