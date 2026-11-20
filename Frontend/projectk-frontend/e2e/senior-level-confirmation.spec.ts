import { APIRequestContext, BrowserContextOptions, expect, test } from '@playwright/test';

import { createGroupViaApi, getSeededKurinKey, loginViaApi } from './support/api-client';
import { e2eApiUrl } from './support/e2e-api';
import { ActivatedMemberAccount, createActivatedMemberAccount, scenarioEmail, scenarioSuffix } from './support/scenarios';
import { openMemberCard, signInAs } from './support/senior-honours';
import { e2eUsers } from './support/test-users';

/**
 * BRANCH-09: a senior ступінь a person gives themselves waits for the Звʼязковий. Until then it counts
 * for nothing — the card marks it and the youth programme stays theirs; the Звʼязковий finds it in
 * «Справи», confirms it on the card, and the card turns senior.
 *
 * Signs in as the seeded зв'язковий of the УПЮ kurin, so it runs in one project only.
 */
test.describe('A senior ступінь entered for oneself', () => {
  // One seeded Звʼязковий signs in and answers: chromium alone reads it, as the other seeded-account specs do.
  test.skip(({ browserName, isMobile }) => browserName !== 'chromium' || isMobile, 'signs in as a shared seeded account');
  test.slow();

  const zvyazkovyi = e2eUsers.manager;

  /** The person's own profile write, as the card's form sends it: a юнацький ступінь and a senior one. */
  async function enterOwnLevels(request: APIRequestContext, person: ActivatedMemberAccount): Promise<void> {
    const login = await loginViaApi(request, person.user);
    const multipart = new FormData();
    multipart.append('firstName', person.member.firstName);
    multipart.append('middleName', person.member.middleName);
    multipart.append('lastName', person.member.lastName);
    multipart.append('email', person.member.email);
    multipart.append('phoneNumber', '+380501234567');
    multipart.append('dateOfBirth', '2000-01-01');
    multipart.append('plastLevelHistories[0].plastLevel', 'Uchasnyk');
    multipart.append('plastLevelHistories[0].dateAchieved', '2015-05-01');
    multipart.append('plastLevelHistories[1].plastLevel', 'Starshoplastun');
    multipart.append('plastLevelHistories[1].dateAchieved', '2024-05-01');

    const response = await request.put(`${e2eApiUrl}/member/${person.member.memberKey}`, {
      headers: { Authorization: `Bearer ${login.tokens!.accessToken}` },
      multipart
    });
    expect(response.ok(), `Own levels were not saved: ${response.status()} ${await response.text()}`).toBe(true);
  }

  test('waits, keeps the probes, shows in «Справи» and turns the card senior once confirmed', async ({ page, request, browser }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const kurinKey = await getSeededKurinKey(request, zvyazkovyi);
    const group = await createGroupViaApi(request, zvyazkovyi, kurinKey, `E2E Ступені ${suffix}`);
    const person = await createActivatedMemberAccount(
      request,
      zvyazkovyi,
      { groupKey: group.groupKey },
      { firstName: `Ступінь${suffix}`, middleName: 'Тестовий', lastName: 'Самовнесений', email: scenarioEmail(suffix, 'own.level') },
      'Level@12345',
      'member'
    );
    const fullName = `${person.member.firstName} ${person.member.lastName}`;

    await enterOwnLevels(request, person);

    await test.step('the person sees it waiting, and their probes stay', async () => {
      await signInAs(page, request, person.user);
      await openMemberCard(page, person.member.memberKey);

      const degrees = page.getByTestId('degrees-tile');
      await expect(degrees.getByTestId('degree-waiting')).toContainText('чекає підтвердження');
      await expect(degrees.getByTestId('degree-highest')).not.toContainText('Старш');
      await expect(degrees.getByRole('button', { name: /^Підтвердити/ })).toHaveCount(0);
      await expect(page.getByRole('heading', { name: 'Здобуті вмілості' })).toBeVisible();
      await expect(page.getByTestId('senior-awards-tile')).toHaveCount(0);
    });

    // The Звʼязковий answers in a browser of their own.
    const answering = await browser.newContext(testInfo.project.use as BrowserContextOptions);
    try {
      const zvPage = await answering.newPage();

      await test.step('the Звʼязковий finds it in «Справи» and confirms it on the card', async () => {
        await signInAs(zvPage, request, zvyazkovyi);
        await zvPage.goto('/');

        const duty = zvPage.getByRole('link', { name: `Ступінь підтвердити: ${fullName}` });
        await expect(duty).toBeVisible({ timeout: 15_000 });
        await duty.click();
        await expect(zvPage).toHaveURL(new RegExp(`/member/${person.member.memberKey}`));

        const degrees = zvPage.getByTestId('degrees-tile');
        await expect(degrees.getByTestId('degree-waiting')).toContainText('чекає підтвердження');
        await degrees.getByRole('button', { name: /^Підтвердити/ }).click();
        await expect(degrees.getByTestId('degree-waiting')).toHaveCount(0);
        await expect(degrees.getByTestId('degree-highest')).toContainText('Старший пластун');
      });

      await test.step('the card is senior now: no youth programme, the senior відзначення instead', async () => {
        await expect(zvPage.getByTestId('senior-awards-tile')).toBeVisible();
        await expect(zvPage.getByRole('heading', { name: 'Здобуті вмілості' })).toHaveCount(0);

        const login = await loginViaApi(request, zvyazkovyi);
        const card = await request.get(`${e2eApiUrl}/member/${person.member.memberKey}`, {
          headers: { Authorization: `Bearer ${login.tokens!.accessToken}` }
        });
        const body = await card.json() as { ulad: string; plastLevelHistories: { awaitsConfirmation: boolean }[] };
        expect(body.ulad).toBe('USP');
        expect(body.plastLevelHistories.every(level => !level.awaitsConfirmation)).toBe(true);
      });
    } finally {
      await answering.close();
    }
  });
});
