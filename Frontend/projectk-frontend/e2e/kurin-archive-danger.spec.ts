import { expect, Page, test } from '@playwright/test';

import { loginThroughUi } from './support/login';
import { OwnKurin, openOwnKurin } from './support/kurin-archive';

/**
 * The archive's danger zone (ARCH-01): the Звʼязковий proves themselves with the password, deletes
 * one record at a time after a confirmation, is refused what dues or a held role still lean on, and
 * loses the zone on leaving the archive mode. Each test has its own kurin, so the two projects never share a
 * zone; see `support/kurin-archive.ts`.
 */
test.describe('Небезпечний режим архіву', () => {
  test.setTimeout(90_000);

  const membershipRow = (page: Page, text: string) => page.getByTestId('archive-membership').filter({ hasText: text });
  const banner = (page: Page) => page.getByTestId('archive-danger-banner');
  const modeTab = (page: Page, name: string) =>
    page.getByRole('navigation', { name: 'Режими реєстру' }).getByRole('link', { name, exact: true });

  async function enterZone(page: Page, kurin: OwnKurin): Promise<void> {
    await page.goto('/kurin/registry/archive');
    await page.getByRole('button', { name: 'Небезпечний режим' }).click();
    const proof = page.getByRole('dialog', { name: 'Небезпечний режим' });
    await expect(proof).toBeVisible();
    await proof.getByRole('textbox', { name: 'Пароль акаунта' }).fill(kurin.founder.password);
    await proof.getByRole('button', { name: 'Увімкнути' }).click();
    await expect(proof).toBeHidden();
    await expect(banner(page)).toBeVisible();
  }

  async function deleteMembership(page: Page, name: string): Promise<void> {
    await membershipRow(page, name).getByRole('button', { name: 'Видалити' }).click();
    const confirm = page.getByRole('alertdialog', { name: 'Видалити членство?' });
    await expect(confirm).toContainText(name);
    await confirm.getByRole('button', { name: 'Видалити' }).click();
    await expect(confirm).toBeHidden();
  }

  test('пароль відкриває режим, видалення одне й з підтвердженням, внески й відкрита роль його спиняють', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    const sovy = await kurin.api.group('Сови');
    const gone = await kurin.api.member(sovy, 'Ярема', 'Видаленко');
    const payer = await kurin.api.member(sovy, 'Олесь', 'Внесків');
    const holder = await kurin.api.member(sovy, 'Остап', 'Ролевий');
    await kurin.api.pay(sovy, 100, await kurin.api.membershipKey(payer.memberKey));
    await kurin.api.seatGroupLeaders(sovy, [{ member: holder, role: 'Hurtkoviy' }]);
    for (const member of [gone, payer, holder]) {
      await kurin.api.leave(member.memberKey);
    }

    await loginThroughUi(page, kurin.founder);
    await page.goto('/kurin/registry/archive');
    await expect(membershipRow(page, gone.name).getByRole('button', { name: 'Видалити' })).toHaveCount(0);

    await test.step('a wrong password is refused', async () => {
      await page.getByRole('button', { name: 'Небезпечний режим' }).click();
      const proof = page.getByRole('dialog', { name: 'Небезпечний режим' });
      await proof.getByRole('textbox', { name: 'Пароль акаунта' }).fill('Wrong@12345');
      await proof.getByRole('button', { name: 'Увімкнути' }).click();
      await expect(proof).toContainText('Неправильний пароль');
      await proof.getByRole('button', { name: 'Скасувати' }).click();
      await expect(proof).toBeHidden();
      await expect(banner(page)).toHaveCount(0);
    });

    await test.step('the right one opens the zone', async () => {
      await enterZone(page, kurin);
      await expect(banner(page)).toContainText('ще 10 видалень');
    });

    await test.step('one deletion, after the confirmation', async () => {
      await deleteMembership(page, gone.name);
      await expect(membershipRow(page, gone.name)).toHaveCount(0);
      await expect(banner(page)).toContainText('ще 9 видалень');
    });

    await test.step('a membership with dues stays, and says why', async () => {
      await deleteMembership(page, payer.name);
      await expect(page.locator('.p-toast').getByText('За членством є внески')).toBeVisible();
      await expect(membershipRow(page, payer.name)).toBeVisible();
    });

    await test.step('so does one whose role is still held', async () => {
      await deleteMembership(page, holder.name);
      await expect(page.locator('.p-toast').getByText('Роль із того часу ще триває')).toBeVisible();
      await expect(membershipRow(page, holder.name)).toBeVisible();
    });
  });

  test('інша вкладка реєстру закриває режим', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    const sovy = await kurin.api.group('Сови');
    const gone = await kurin.api.member(sovy, 'Ярема', 'Лишенко');
    const membershipKey = await kurin.api.membershipKey(gone.memberKey);
    await kurin.api.leave(gone.memberKey);

    await loginThroughUi(page, kurin.founder);
    const opened = page.waitForResponse(response => response.url().endsWith('/archive/danger') && response.request().method() === 'POST');
    await enterZone(page, kurin);
    const openedResponse = await opened;
    const { ticket } = await openedResponse.json() as { ticket: string };
    // The page keeps its access token in memory; the ticket is only good with that session's token.
    const pageToken = (await openedResponse.request().allHeaders())['authorization'].replace(/^Bearer /, '');

    const ended = page.waitForRequest(sent => sent.url().includes('/archive/danger?') && sent.method() === 'DELETE');
    // Over to «Склад»: leaving the archive mode is leaving the zone, as leaving the page is.
    await modeTab(page, 'Склад').click();
    await expect(page).not.toHaveURL(/\/archive/);
    expect((await ended).url()).toContain('reason=Left');
    await expect(page.locator('.p-toast').getByText('Небезпечний режим вимкнено')).toBeVisible();

    await expect.poll(async () => (await kurin.api.deleteMembershipAsPage(membershipKey, ticket, pageToken)).code).toBe('DangerZoneClosed');

    await modeTab(page, 'Архів').click();
    await expect(membershipRow(page, gone.name)).toBeVisible();
    await expect(banner(page)).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Небезпечний режим' })).toBeVisible();
  });
});
