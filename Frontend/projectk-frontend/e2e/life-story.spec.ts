import { expect, Locator, Page, test } from '@playwright/test';

import { getFirstSeededGroupMemberKey } from './support/api-client';
import { getLifeStoryMemberKey, ordenSkryptor, ordenZvyazkovyi } from './support/life-story';
import { loginThroughUi } from './support/login';
import { describeRole } from './support/role-test';

/** «Пластовий життєпис» (LIFE-01): the whole Plast way on one page, opened from the card. */
const levelsTile = (page: Page): Locator => page.getByTestId('life-levels');
const stageNames = (page: Page): Locator => levelsTile(page).locator('.life-stage .p-tag');

async function openLifeFromCard(page: Page, memberKey: string): Promise<void> {
  await page.goto(`/member/${memberKey}`);
  await page.getByTestId('member-life').click();
  await expect(page).toHaveURL(new RegExp(`/member/${memberKey}/life`));
  await expect(page.getByTestId('life-head')).toBeVisible();
}

describeRole('manager', 'Пластовий життєпис юнака', ({ user }) => {
  test('у юнака лише етап УПЮ', async ({ page, request }) => {
    const youthKey = await getFirstSeededGroupMemberKey(request, user);

    await openLifeFromCard(page, youthKey);

    await expect(stageNames(page)).toHaveText(['УПЮ']);
  });
});

/**
 * The senior's story is read with the Order's seeded accounts. Signing in as a shared seeded user in
 * the middle of a run takes over that account's session, so this half stays on chromium.
 */
test.describe('Пластовий життєпис старшого', () => {
  test.skip(({ isMobile }) => isMobile, 'Signs in as shared seeded accounts; chromium reads it.');

  test('Звʼязковий відкриває з картки всі етапи й ступені і виправляє юнацький запис', async ({ page, request }) => {
    const kozakKey = await getLifeStoryMemberKey(request);
    await loginThroughUi(page, ordenZvyazkovyi);

    await openLifeFromCard(page, kozakKey);

    await expect(stageNames(page)).toHaveText(['УПЮ', 'УСП']);
    await expect(levelsTile(page).locator('.life-stage__level')).toHaveText(['Вступ до Пласту', 'Пластун учасник', 'Пластун розвідувач', 'Пластун скоб', 'Старший пластун']);
    await expect(levelsTile(page).getByRole('button', { name: 'Виправити' })).toBeVisible();
  });

  test('для чужого проводу юнацький запис старшого закритий', async ({ page, request }) => {
    const kozakKey = await getLifeStoryMemberKey(request);
    await loginThroughUi(page, ordenSkryptor);

    await openLifeFromCard(page, kozakKey);

    await expect(stageNames(page)).toHaveText(['УПЮ', 'УСП']);
    await expect(levelsTile(page).getByRole('button', { name: 'Виправити' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Додати відзначення' })).toHaveCount(0);
  });
});
