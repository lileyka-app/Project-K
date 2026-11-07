import { expect, test } from '@playwright/test';

import { loginViaApi } from './support/api-client';
import {
  createOrdenPerson,
  memberKeyByEmail,
  openMemberCard,
  ordenKurinKey,
  ordenLeader,
  ordenScriptorEmail,
  ordenWarnedEmail,
  signInAs
} from './support/senior-honours';
import { closeLastPrimeDialog } from './support/ui';

/**
 * A senior kurin's card as its зв'язковий sees it (BRANCH-03, AWARD-02): the kurin and the role under
 * the kurin's own names, перестороги, the highest ступінь only, and senior відзначення in blocks.
 *
 * Signs in as the seeded зв'язковий of «Орден Хрестоносців», so it runs in one project and in order.
 */
test.describe.configure({ mode: 'serial' });

test.describe('Senior kurin card', () => {
  test.skip(({ browserName, isMobile }) => browserName !== 'chromium' || isMobile, 'signs in as a shared seeded account');

  let kurinKey: string;

  test.beforeEach(async ({ page, request }) => {
    kurinKey ??= await ordenKurinKey(request);
    await signInAs(page, request, ordenLeader);
  });

  test('shows the kurin, the role by the kurin’s own name and a пластунка’s highest ступінь', async ({ page, request }) => {
    await openMemberCard(page, await memberKeyByEmail(request, ordenLeader, kurinKey, ordenScriptorEmail));

    const kurinTile = page.getByTestId('senior-kurin-tile');
    await expect(kurinTile.getByRole('heading', { name: 'Орден Хрестоносців' })).toBeVisible();
    await expect(kurinTile).toContainText('5-й курінь УСП');
    await expect(kurinTile).toContainText('20-й курінь УПС');
    await expect(kurinTile).toContainText('Скриптор');
    await expect(kurinTile).not.toContainText('Писар');

    const highest = page.getByTestId('degree-highest');
    await expect(highest).toContainText('Старша пластунка');
    await expect(highest.getByRole('img', { name: 'Старша пластунка' })).toBeVisible();
  });

  test('lists a running пересторога before one that has run out', async ({ page, request }) => {
    await openMemberCard(page, await memberKeyByEmail(request, ordenLeader, kurinKey, ordenWarnedEmail));

    const rows = page.getByTestId('senior-warnings-tile').locator('li');
    await expect(rows).toHaveCount(2);
    await expect(rows.first()).toContainText('ще');
    await expect(rows.last()).toContainText('минула');
  });

  test('own card: only the highest ступінь, pinned first, repeats in one block with what each was for', async ({ page, request }) => {
    const login = await loginViaApi(request, ordenLeader);
    await openMemberCard(page, login.memberKey!);

    const degrees = page.getByTestId('degrees-tile');
    await expect(degrees.getByTestId('degree-highest')).toContainText('Пластун сеніор праці');
    await expect(degrees).not.toContainText('Старший пластун');

    const blocks = page.getByTestId('senior-awards-tile').getByTestId('award-block');
    await expect(blocks.first()).toContainText('Орден святого Юрія в бронзі');
    await expect(blocks.first().getByLabel('Закріплене')).toBeVisible();

    const charter = blocks.filter({ hasText: 'Грамота заслуги' });
    await expect(charter).toContainText('×2');
    await charter.click();

    const details = page.getByRole('dialog', { name: 'Грамота заслуги' });
    await expect(details.getByTestId('award-grant')).toHaveCount(2);
    await expect(details).toContainText('За табір «Вогні Карпат»');
    await expect(details).toContainText('За вишкіл новаків');
    await expect(details.getByRole('button', { name: 'Закріпити' })).toBeVisible();
    await closeLastPrimeDialog(page);
  });

  test('the провід adds a senior відзначення to someone’s card and takes it back, but cannot pin it', async ({ page, request }, testInfo) => {
    test.slow();
    const person = await createOrdenPerson(request, testInfo, 'Провід', 'provid');
    await openMemberCard(page, person.member.memberKey);

    const tile = page.getByTestId('senior-awards-tile');
    await tile.getByRole('button', { name: 'Додати відзначення' }).click();
    const form = page.getByRole('dialog', { name: 'Нове відзначення' });
    await form.locator('#senior-award-item').click();
    await page.getByRole('option', { name: 'Грамота вирізнення' }).click();
    await form.getByLabel('За що').fill('За крайовий табір');
    await form.getByRole('button', { name: 'Зберегти' }).click();
    await expect(form).toBeHidden();

    const block = tile.getByTestId('award-block').filter({ hasText: 'Грамота вирізнення' });
    await expect(block).toBeVisible();
    await expect(block).not.toContainText('Очікує');
    await block.click();

    const details = page.getByRole('dialog', { name: 'Грамота вирізнення' });
    await expect(details).toContainText('За крайовий табір');
    await expect(details.getByRole('button', { name: /Закріпити|Відкріпити/ })).toHaveCount(0);

    await details.getByRole('button', { name: 'Відкрити це надання' }).click();
    await page.getByRole('dialog', { name: 'Відзначення' }).getByRole('button', { name: 'Видалити відзначення' }).click();
    await expect.poll(() => tile.getByTestId('award-block').count()).toBe(0);
  });
});
