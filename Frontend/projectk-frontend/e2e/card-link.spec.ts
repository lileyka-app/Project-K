import { expect, test } from '@playwright/test';

import { loginThroughUi } from './support/login';
import { scenarioSuffix } from './support/scenarios';
import { freshKurin, personIn } from './support/transfers';

/**
 * A person's permanent card link (1.4): made in the account settings, read by anyone without signing
 * in, and gone for good once switched off. The person is a fresh adult, who decides alone.
 */

// The person and their kurin are opened over the API before anyone signs in.
test.describe.configure({ timeout: 90_000 });

test('a permanent card link opens without signing in, and stops opening once switched off', async ({ page, request, browser }, testInfo) => {
  const suffix = scenarioSuffix(testInfo);
  const kurin = await freshKurin(request, suffix, 'card');
  const person = await personIn(request, kurin, suffix, 'card', { lastName: `Картковий${suffix.replace(/\W/g, '').slice(-4)}` });
  const lastName = person.fullName.split(' ')[1];

  await loginThroughUi(page, person.user);
  await page.goto('/settings/account');
  const section = page.locator('#card');
  // The settings page reads the card after the account; on a busy machine that outlasts the default wait.
  await section.getByRole('button', { name: 'Створити посилання' }).click({ timeout: 20_000 });
  const link = section.getByRole('textbox', { name: 'Посилання на картку' });
  await expect(link).toHaveValue(/\/c\//);
  const url = await link.inputValue();

  // Someone who was handed the link, in a browser that never signed in.
  const stranger = await browser.newContext();
  try {
    const card = await stranger.newPage();
    await card.goto(url);
    await expect(card.getByText(lastName)).toBeVisible({ timeout: 20_000 });

    await section.getByRole('button', { name: 'Вимкнути' }).click();
    const dialog = page.getByRole('alertdialog').filter({ hasText: 'Вимкнути посилання?' });
    await dialog.getByRole('button', { name: 'Вимкнути' }).click();
    await expect(section.getByRole('button', { name: 'Створити посилання' })).toBeVisible();

    await card.reload();
    await expect(card.getByRole('heading', { name: 'Картку не знайдено' })).toBeVisible();
    await expect(card.getByText(lastName)).toHaveCount(0);
  } finally {
    await stranger.close();
  }
});
