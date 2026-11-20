import { expect, test } from '@playwright/test';

import { describeRole } from './support/role-test';
import { cancelManagePanel, chooseMenuItem, dialog, fillManagePanelFields, openRowMenu } from './support/ui';

describeRole('admin', 'Admin management surfaces', () => {
  test('admin panel exposes kurin manage-panel create update and delete modes', async ({ page }) => {
    await page.goto('/panel');
    await expect(page.getByRole('heading', { name: 'Адміністрація' })).toBeVisible();

    await page.getByRole('button', { name: /Створити/ }).click();
    await expect(dialog(page)).toBeVisible();
    await expect(dialog(page).locator('.actions').last().getByRole('button').last()).toBeDisabled();
    // Only the number: the Зв'язковий fields are optional, and none of them is a password — the
    // founder chooses one from the invitation letter (SEC-4.4, kurin-founder-invitation.spec).
    await fillManagePanelFields(page, [`91${Date.now().toString().slice(-3)}`]);
    await expect(dialog(page).locator('.actions').last().getByRole('button').last()).toBeEnabled();
    await cancelManagePanel(page);

    const row = page.locator('.admin-kurin-row').first();
    await expect(row).toBeVisible();
    await openRowMenu(row);
    await chooseMenuItem(page, 0);
    await expect(dialog(page)).toBeVisible();
    // Update mode locks the system key only; the number stays editable, and the manager email
    // field that used to be locked here is gone with the password flow it belonged to.
    await expect(dialog(page).locator('input:disabled')).toHaveCount(1);
    await cancelManagePanel(page);

    await openRowMenu(row);
    await chooseMenuItem(page, 1);
    await expect(dialog(page)).toBeVisible();
    await expect(dialog(page).locator('.actions').last().getByRole('button').last()).toBeEnabled();
    await cancelManagePanel(page);
  });

  test('users list supports group expansion row edit cancel and delete confirmation cancel', async ({ page }) => {
    await page.goto('/users');
    await expect(page.locator('.p-datatable')).toBeVisible();

    await page.locator('tbody tr').first().getByRole('button').first().click();

    const firstUserRow = page.locator('.users-list-row').first();
    await expect(firstUserRow).toBeVisible();

    await firstUserRow.getByRole('button').first().click();
    await expect(firstUserRow.getByRole('button').filter({ has: page.locator('.pi-check') })).toBeVisible();
    await firstUserRow.getByRole('button').last().click();

    await firstUserRow.getByRole('button').last().click();
    const confirm = page.getByRole('alertdialog').last();
    await expect(confirm).toBeVisible();
    await confirm.getByRole('button').first().click();
    await expect(confirm).toBeHidden();
  });

  test('waitlist management lists applications and kurin requests in one inbox', async ({ page }) => {
    await page.goto('/waitlist');
    await expect(page.getByRole('heading', { name: 'Заявки', exact: true })).toBeVisible();

    const list = page.locator('.inbox');
    await expect(list.or(page.getByText('Нічого не чекає'))).toBeVisible();

    await page.getByRole('button', { name: 'Усі', exact: true }).click();
    await expect(page.locator('.inbox__item').first()).toBeVisible();
    await expect(page.locator('.inbox__item').first().locator('.inbox__actions')).toBeAttached();
  });
});
