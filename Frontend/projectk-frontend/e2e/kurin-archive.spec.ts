import { expect, Page, test } from '@playwright/test';

import { loginThroughUi } from './support/login';
import { openOwnKurin } from './support/kurin-archive';
import { dialog, fillDialogDate, openRowMenu } from './support/ui';

/**
 * The archive (ARCH-01), a mode of the kurin's «Реєстр»: former roles and memberships, corrections,
 * taking a former member back, who sees what, and a гурток dissolved into the archive. Each test opens
 * a kurin of its own; see `support/kurin-archive.ts`.
 */
test.describe('Архів куреня', () => {
  test.setTimeout(90_000);

  const modeTabs = (page: Page) => page.getByRole('navigation', { name: 'Режими реєстру' }).getByRole('link');

  /** The archive's address; `old` is the one cards and links used before the «Реєстр» was one page. */
  async function openArchive(page: Page, kurinKey: string, old = false): Promise<void> {
    await page.goto(old ? `/kurin/${kurinKey}/archive` : '/kurin/registry/archive');
    await expect(page).toHaveURL(/\/kurin\/registry\/archive/);
    await expect(page.getByRole('heading', { name: 'Реєстр' })).toBeVisible();
  }

  const officeRow = (page: Page, text: string) => page.getByTestId('archive-office').filter({ hasText: text });
  const membershipRow = (page: Page, text: string) => page.getByTestId('archive-membership').filter({ hasText: text });

  test('Звʼязковий бачить колишні ролі й членства і виправляє закрите членство', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    const sovy = await kurin.api.group('Сови');
    const gone = await kurin.api.member(sovy, 'Ярема', 'Колишній');
    const leader = await kurin.api.member(sovy, 'Остап', 'Провідний');
    const provid = await kurin.api.seatGroupLeaders(sovy, [{ member: leader, role: 'Hurtkoviy' }]);
    await kurin.api.seatGroupLeaders(sovy, [], provid);
    await kurin.api.leave(gone.memberKey);

    await loginThroughUi(page, kurin.founder);
    await openArchive(page, kurin.kurinKey);
    await expect(modeTabs(page)).toHaveText([/Склад/, /Переходи/, /Архів/, /Імпорт/]);
    await expect(modeTabs(page).filter({ hasText: 'Архів' })).toHaveAttribute('aria-current', 'page');

    await expect(officeRow(page, leader.name)).toContainText('Сови');
    await expect(membershipRow(page, gone.name)).toContainText('Сови');

    await membershipRow(page, gone.name).getByRole('button', { name: 'Виправити' }).click();
    const edit = page.getByRole('dialog', { name: 'Виправити членство' });
    await expect(edit).toBeVisible();
    await fillDialogDate(edit.locator('#archive-from'), '01.09.2024');
    await edit.getByRole('button', { name: 'Зберегти' }).click();
    await expect(edit).toBeHidden();

    await expect(membershipRow(page, gone.name)).toContainText('01.09.2024');
  });

  test('Звʼязковий повертає колишнього просто з архіву', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    const sovy = await kurin.api.group('Сови');
    const gone = await kurin.api.member(sovy, 'Ярема', 'Повернений');
    await kurin.api.leave(gone.memberKey);

    await loginThroughUi(page, kurin.founder);
    await openArchive(page, kurin.kurinKey);

    const row = membershipRow(page, gone.name);
    await expect(row).toContainText('Ніде');
    await row.getByRole('button', { name: 'Повернути' }).click();
    const back = page.getByRole('dialog', { name: 'Повернути до куреня' });
    await expect(back).toBeVisible();
    await back.getByRole('button', { name: 'Повернути' }).click();
    await expect(back).toBeHidden();

    await expect(page.locator('.p-toast').getByText('Людину повернуто')).toBeVisible();
    // The stretch stays in the archive as history; there is nobody to take back any more.
    await expect(row.getByRole('button', { name: 'Повернути' })).toHaveCount(0);
  });

  test('свій запис Звʼязковий не виправляє', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    const sovy = await kurin.api.group('Сови');
    const founder = { memberKey: kurin.founderMemberKey, name: `Founder${kurin.suffix} Архівна` };
    const provid = await kurin.api.seatGroupLeaders(sovy, [{ member: founder, role: 'Hurtkoviy' }]);
    await kurin.api.seatGroupLeaders(sovy, [], provid);

    await loginThroughUi(page, kurin.founder);
    // By the address the member card and old links used: it opens the archive mode.
    await openArchive(page, kurin.kurinKey, true);

    const own = officeRow(page, 'Сови');
    await expect(own).toContainText('своє');
    await expect(own.getByRole('button', { name: 'Виправити' })).toHaveCount(0);
  });

  test('гуртковий бачить архів лише свого гуртка', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    const sovy = await kurin.api.group('Сови');
    const lysy = await kurin.api.group('Лиси');
    const hurtkovyi = await kurin.api.memberWithAccount(sovy, 'Гнат', 'Гуртковий', kurin.suffix);
    await kurin.api.seatGroupLeaders(sovy, [{ member: hurtkovyi, role: 'Hurtkoviy' }]);
    const ours = await kurin.api.member(sovy, 'Антін', 'Свійгурток');
    const theirs = await kurin.api.member(lysy, 'Богдан', 'Чужийгурток');
    await kurin.api.leave(ours.memberKey);
    await kurin.api.leave(theirs.memberKey);

    await loginThroughUi(page, hurtkovyi.user);
    await openArchive(page, kurin.kurinKey);

    await expect(membershipRow(page, ours.name)).toBeVisible();
    await expect(membershipRow(page, ours.name).getByRole('button', { name: 'Виправити' })).toBeVisible();
    await expect(membershipRow(page, theirs.name)).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Небезпечний режим' })).toHaveCount(0);
    // Taking people back and asking other kurins for them stay the Звʼязковий's. The archive is the
    // гуртковий's only mode, so the «Реєстр» shows no tabs at all.
    await expect(membershipRow(page, ours.name).getByRole('button', { name: 'Повернути' })).toHaveCount(0);
    await expect(modeTabs(page)).toHaveCount(0);
  });

  test('розформований гурток іде в архів, а гурток із грошима в касі лишається', async ({ page, request }, testInfo) => {
    const kurin = await openOwnKurin(request, testInfo);
    await kurin.api.group('Сови');
    const kruky = await kurin.api.group('Круки');
    await kurin.api.pay(kruky, 50);

    await loginThroughUi(page, kurin.founder);
    await page.goto('/kurin');
    const groupRow = (name: string) => page.locator('.group-row').filter({ hasText: name });

    async function dissolve(name: string): Promise<void> {
      await openRowMenu(groupRow(name));
      await page.getByRole('menuitem', { name: 'Розформувати' }).click();
      const confirm = dialog(page);
      await expect(confirm).toContainText(`«${name}»`);
      await confirm.getByRole('button', { name: 'Розформувати' }).click();
      await expect(confirm).toBeHidden();
    }

    await dissolve('Круки');
    await expect(page.locator('.p-toast').getByText('У скриньці гуртка ще є гроші чи борги')).toBeVisible();
    await expect(groupRow('Круки')).toBeVisible();

    await dissolve('Сови');
    await expect(groupRow('Сови')).toHaveCount(0);

    await openArchive(page, kurin.kurinKey);
    await expect(page.getByTestId('archive-group').filter({ hasText: 'Сови' })).toBeVisible();
    await expect(page.getByTestId('archive-group').filter({ hasText: 'Круки' })).toHaveCount(0);
  });
});
