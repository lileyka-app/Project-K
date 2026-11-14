import { expect, Locator, Page, test } from '@playwright/test';

import { getFirstSeededGroupMemberKey } from './support/api-client';
import { getLifeStoryMemberKey, ordenKozak, ordenZvyazkovyi } from './support/life-story';
import { loginThroughUi } from './support/login';
import { describeRole } from './support/role-test';
import { openLifeFromCardMenu } from './support/ui';

/** «Пластовий життєпис» (LIFE-01): the whole Plast way on one page — the person's own, nobody else's. */
const levelsTile = (page: Page): Locator => page.getByTestId('life-levels');
const stageNames = (page: Page): Locator => levelsTile(page).locator('.life-stage .p-tag');

describeRole('manager', 'Чужий пластовий життєпис', ({ user }) => {
  test('закритий: пряме посилання веде на картку, у «Ще» його немає', async ({ page, request }) => {
    const youthKey = await getFirstSeededGroupMemberKey(request, user);

    await page.goto(`/member/${youthKey}/life`);

    await expect(page).toHaveURL(new RegExp(`/member/${youthKey}$`));
    await page.locator('.member-actions').getByRole('button', { name: 'Ще' }).click();
    await expect(page.getByRole('menuitem', { name: 'Журнал дій' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: 'Пластовий життєпис' })).toHaveCount(0);
  });
});

/**
 * The senior's story is read with the Order's seeded accounts. Signing in as a shared seeded user in
 * the middle of a run takes over that account's session, so this half stays on chromium.
 */
test.describe('Свій пластовий життєпис', () => {
  test.skip(({ isMobile }) => isMobile, 'Signs in as shared seeded accounts; chromium reads it.');

  test('старший відкриває свій з «Ще»: усі етапи, вишкіл і PDF', async ({ page, request }) => {
    const kozakKey = await getLifeStoryMemberKey(request);
    await loginThroughUi(page, ordenKozak);

    await page.goto(`/member/${kozakKey}`);
    await openLifeFromCardMenu(page);
    await expect(page).toHaveURL(new RegExp(`/member/${kozakKey}/life`));

    await expect(stageNames(page)).toHaveText(['УПЮ', 'УСП']);
    await expect(levelsTile(page).locator('.life-stage__level')).toHaveText(['Вступ до Пласту', 'Пластун учасник', 'Пластун розвідувач', 'Пластун скоб', 'Старший пластун']);

    const trainings = page.getByTestId('life-training');
    await page.getByTestId('life-training-add').click();
    await page.locator('#life-course').click();
    await page.getByRole('option', { name: 'КВЗ' }).click();
    await page.locator('#life-number').fill('12');
    await page.locator('#life-start').fill('01.07.2024');
    await page.keyboard.press('Tab');
    await page.locator('#life-commandant').fill('Ярема Коваль');
    await page.getByTestId('life-training-save').click();
    await expect(trainings.getByRole('button', { name: /12-й КВЗ/ })).toContainText('комендант Ярема Коваль');

    const download = page.waitForEvent('download');
    await page.getByTestId('life-pdf').click();
    expect((await download).suggestedFilename()).toMatch(/\.pdf$/);

    await trainings.getByRole('button', { name: /12-й КВЗ/ }).click();
    await page.getByRole('button', { name: 'Видалити' }).click();
    await expect(trainings.getByRole('button', { name: /12-й КВЗ/ })).toHaveCount(0);
  });

  test('Звʼязковий куреня чужого життєпису не відкриє', async ({ page, request }) => {
    const kozakKey = await getLifeStoryMemberKey(request);
    await loginThroughUi(page, ordenZvyazkovyi);

    await page.goto(`/member/${kozakKey}/life`);

    await expect(page).toHaveURL(new RegExp(`/member/${kozakKey}$`));
  });
});
