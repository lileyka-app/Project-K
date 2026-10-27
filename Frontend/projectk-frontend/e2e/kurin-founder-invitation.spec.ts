import { expect, test } from '@playwright/test';

import { getKurinByKey, getLatestInvitationByEmail, loginViaApi } from './support/api-client';
import { e2eUsers } from './support/test-users';
import { loginThroughUi } from './support/login';
import { dialog, submitButton } from './support/ui';
import { scenarioEmail, scenarioSuffix } from './support/scenarios';

test.describe('Admin opens a kurin with an invited Зв\'язковий (SEC-4.4)', () => {
  test('the founder sets their own password from the letter and lands as Зв\'язковий', async ({ page, browser, request }, testInfo) => {
    test.setTimeout(90_000);

    const suffix = scenarioSuffix(testInfo);
    const password = 'Founder@12345';
    // Kurin numbers are capped (KurinNumbers:MaxNumber, 200); the onboarding flow takes 101–199
    // the same way, so a clash between the two is as unlikely as one between two of its runs.
    const kurinNumber = String(101 + ((Date.now() + 53) % 99));
    const founderEmail = scenarioEmail(suffix, 'founder');

    await test.step('admin opens the kurin and names its Зв\'язковий — no password field', async () => {
      await loginThroughUi(page, e2eUsers.admin);
      await page.goto('/panel');
      await page.getByRole('button', { name: /Створити/ }).first().click();
      const panel = dialog(page);
      await expect(panel).toBeVisible();
      await expect(panel.locator('input[type="password"]')).toHaveCount(0);

      await panel.locator('#manage-field-number').fill(kurinNumber);
      await panel.locator('#manage-field-founderEmail').fill(founderEmail);
      await panel.locator('#manage-field-founderFirstName').fill(`Founder${suffix}`);
      await panel.locator('#manage-field-founderLastName').fill('Zvyazkova');
      await submitButton(panel.locator('.actions').last()).click();

      await expect(page.locator('.p-toast').getByText(`Запрошення надіслано на ${founderEmail}.`)).toBeVisible();
    });

    await test.step('the founder activates from the invitation in a clean browser', async () => {
      const invitation = await getLatestInvitationByEmail(request, founderEmail);
      expect(invitation.token).toBeTruthy();

      const founderContext = await browser.newContext();
      const founderPage = await founderContext.newPage();
      await founderPage.addInitScript(() => localStorage.setItem('lk.preferWeb', '1'));
      await founderPage.goto(`/activate/${invitation.token}`);
      await expect(founderPage.getByText('Активація акаунта')).toBeVisible();

      await founderPage.locator('p-password').first().locator('input').fill(password);
      await founderPage.locator('p-password').nth(1).locator('input').fill(password);
      await founderPage.locator('form').getByRole('button').last().click();
      await expect(founderPage).not.toHaveURL(/\/activate\//, { timeout: 10_000 });
      await expect(founderPage).not.toHaveURL(/\/login/);
      await founderContext.close();
    });

    await test.step('the founder holds the kurin they were invited to', async () => {
      const founder = { role: 'manager' as const, email: founderEmail, password };
      const login = await loginViaApi(request, founder);
      expect(login.permissions).toContain('Group:Manage:KurinWide');
      expect(login.kurinKey).toBeTruthy();

      const kurin = await getKurinByKey(request, founder, login.kurinKey!);
      expect(String(kurin.number)).toBe(kurinNumber);
    });
  });
});
