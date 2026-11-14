import { expect, test } from '@playwright/test';

import { getSeededGroupKey, getSeededKurinKey } from './support/api-client';
import { describeRole } from './support/role-test';
import { openGroupActionMenu, visibleMenuItems } from './support/ui';

describeRole('manager', 'Manager conditional organization UI', ({ user }) => {
  test('manager sees kurin-level management controls', async ({ page }) => {
    await page.goto('/kurin');

    await expect(page.locator('.edit-profile-button')).toBeVisible();
    await expect(page.locator('.group-panel').getByRole('button', { name: 'Створити' })).toBeVisible();
    await expect(page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' })).toBeVisible();
    await expect(page.locator('app-kv-panel .lil-tile-head__actions button:has(.pi-plus)')).toBeVisible();
    await expect(page.locator('app-leadership-panel .lil-tile-head__actions button:has(.pi-cog)').first()).toBeVisible();
  });

  test('manager group action menu exposes profile, mentors and silhouette actions; adding sits on the list', async ({ page, request }) => {
    const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

    await page.goto(`/group/${groupKey}`);
    await expect(page.locator('body')).toContainText('Gurtok 1');
    await expect(page.locator('.group-actions').getByRole('button')).toBeVisible();
    await openGroupActionMenu(page);

    // expect.poll, not expect(await …count()): the latter reads the count once, and openGroupActionMenu
    // only waits for the first item, so the rest of the menu may still be rendering. Under the load of
    // a full parallel run that snapshot caught two items instead of three.
    await expect.poll(() => visibleMenuItems(page).count()).toBeGreaterThanOrEqual(3);
    await page.keyboard.press('Escape');
    await expect(page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' })).toBeVisible();
  });

  test('manager planning list exposes create and destructive row controls', async ({ page, request }) => {
    const kurinKey = await getSeededKurinKey(request, user);

    await page.goto(`/planning/${kurinKey}`);
    await expect(page.locator('button:has(.pi-plus)')).toBeVisible();

    const rows = page.locator('tr.planning-row');
    if (await rows.count()) {
      await expect(page.locator('button:has(.pi-trash)').first()).toBeVisible();
    }
  });
});

describeRole('mentor', 'Mentor conditional organization UI', ({ user }) => {
  test('mentor sees read-only kurin controls and no kurin-wide management buttons', async ({ page }) => {
    await page.goto('/kurin');

    await expect(page.locator('.edit-profile-button')).toBeHidden();
    await expect(page.locator('.group-panel').getByRole('button', { name: 'Створити' })).toBeHidden();
    await expect(page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' })).toBeHidden();
    await expect(page.locator('app-kv-panel .lil-tile-head__actions button:has(.pi-plus)')).toBeHidden();
    await expect(page.locator('app-leadership-panel .lil-tile-head__actions button:has(.pi-cog)').first()).toBeHidden();
  });

  // Виховник керує всім своїм гуртком, тож у призначеному гуртку меню дає повний набір дій.
  test('mentor manages an assigned group through the action menu', async ({ page, request }) => {
    const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

    await page.goto(`/group/${groupKey}`);
    await expect(page.locator('body')).toContainText('Gurtok 1');
    await openGroupActionMenu(page);
    await expect(page.getByRole('menuitem', { name: 'Редагувати профіль' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: 'Завантажити сильветку' })).toBeVisible();
    await page.keyboard.press('Escape');

    await page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' }).click();
    await expect(page).toHaveURL(new RegExp(`/group/${groupKey}/member/upsert`));
  });

  test('mentor planning list is inspect-only', async ({ page, request }) => {
    const kurinKey = await getSeededKurinKey(request, user);

    await page.goto(`/planning/${kurinKey}`);
    await expect(page.locator('button:has(.pi-plus)')).toBeHidden();
    await expect(page.locator('button:has(.pi-trash)')).toBeHidden();
  });
});

describeRole('member', 'Member conditional organization UI', ({ user }) => {
  test('member has no kurin or group management controls', async ({ page, request }) => {
    const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

    await page.goto('/kurin');
    await expect(page.locator('.edit-profile-button')).toBeHidden();
    await expect(page.locator('.group-panel').getByRole('button', { name: 'Створити' })).toBeHidden();
    await expect(page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' })).toBeHidden();
    await expect(page.locator('app-kv-panel .lil-tile-head__actions button:has(.pi-plus)')).toBeHidden();
    await expect(page.locator('app-leadership-panel .lil-tile-head__actions button:has(.pi-cog)').first()).toBeHidden();

    await page.goto(`/group/${groupKey}`);
    await expect(page.locator('body')).toContainText('Gurtok 1');
    await expect(page.locator('.group-actions').getByRole('button')).toBeHidden();
  });
});
