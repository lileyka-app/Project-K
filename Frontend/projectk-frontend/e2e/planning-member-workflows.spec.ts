import { expect, test } from '@playwright/test';

import { getFirstSeededGroupMemberKey, getPlanningSessions, getSeededKurinKey } from './support/api-client';
import { describeRole } from './support/role-test';
import { closeLastPrimeDialog } from './support/ui';

describeRole('manager', 'Planning and member detail workflows', ({ user }) => {
  test('manager can create inspect and delete a planning session', async ({ page, request }) => {
    const kurinKey = await getSeededKurinKey(request, user);
    const sessionName = `E2E Planning ${Date.now()}`;

    await page.goto(`/planning/create/${kurinKey}`);
    await expect(page.locator('#planning-name')).toBeVisible();
    await page.locator('#planning-name').fill(sessionName);
    await page.locator('form').getByRole('button').last().click();

    await expect(page).toHaveURL(new RegExp(`/planning/${kurinKey}`), { timeout: 15000 });
    const row = page.locator('tr', { hasText: sessionName }).first();
    await expect(row).toBeVisible({ timeout: 15000 });

    await row.getByRole('button').first().click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await page.getByRole('dialog').getByRole('button').last().click();

    await row.getByRole('button', { name: 'Видалити планування' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Видалити', exact: true }).click();
    await expect(row).toBeHidden();

    const sessions = await getPlanningSessions(request, user, kurinKey);
    expect(sessions.some(session => session.name === sessionName)).toBeFalsy();
  });

  test('member profile exposes skills awards and probe detail read paths', async ({ page, request }) => {
    const memberKey = await getFirstSeededGroupMemberKey(request, user);

    await page.goto(`/member/${memberKey}`);
    await expect(page.locator('.member-profile-tile')).toBeVisible();

    await page.locator('.member-tile').nth(1).getByRole('button').first().click();
    const allSkillsDialog = page.getByRole('dialog').last();
    await expect(allSkillsDialog).toBeVisible();
    await expect(allSkillsDialog.locator('.dialog-actions')).toBeVisible();

    const addSkillButton = allSkillsDialog.locator('.dialog-actions').getByRole('button').last();
    if (await addSkillButton.isVisible()) {
      await addSkillButton.click();
      const addSkillDialog = page.getByRole('dialog').last();
      await expect(addSkillDialog).toBeVisible();
      await addSkillDialog.locator('input:visible').last().fill(`not-found-${Date.now()}`);
      await expect(addSkillDialog.locator('.member-empty-state')).toBeVisible();
      await closeLastPrimeDialog(page);
    }
    await closeLastPrimeDialog(page);

    const awardTile = page.locator('app-member-awards-tile');
    await expect(awardTile).toBeVisible();
    const addAward = awardTile.getByRole('button').first();
    if (await addAward.isVisible()) {
      await addAward.click();
      const awardDialog = page.getByRole('dialog').last();
      await expect(awardDialog).toBeVisible();
      await expect(awardDialog.locator('.award-dialog-main-actions').getByRole('button').last()).toBeDisabled();
      await closeLastPrimeDialog(page);
    }

    const probeDetailsButton = page.locator('.probe-summary-row').first().getByRole('button');
    await expect(probeDetailsButton).toBeVisible();
    await probeDetailsButton.click();
    await expect(page).toHaveURL(new RegExp(`/member/${memberKey}/probe/`));
    await expect(page.locator('.probe-page-title')).toBeVisible();
  });
});
