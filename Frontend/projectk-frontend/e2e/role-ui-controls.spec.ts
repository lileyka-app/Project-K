import { expect, test } from '@playwright/test';

import { getFirstSeededGroupMemberKey, getSeededGroupKey, getSeededKurinKey } from './support/api-client';
import { describeRole } from './support/role-test';
import { fillMemberRequiredFields, openWarningPanel } from './support/ui';

describeRole('manager', 'Manager role UI controls', ({ user }) => {
  test('manager sees mutating controls and member form validation gates save', async ({ page, request }) => {
    const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

    await page.goto('/kurin');
    await expect(page.locator('.edit-profile-button')).toBeVisible();
    await expect(page.locator('.group-panel').getByRole('button', { name: 'Створити' })).toBeVisible();
    // Named, not just "the button under the list": taking someone in by code sits beside adding
    // a new person, and a positional selector would match both.
    await expect(page.locator('app-member-list + div')
      .getByRole('button', { name: /Додати учасника/ })).toBeVisible();

    await page.goto(`/group/${groupKey}`);
    await expect(page.locator('.group-actions button')).toBeVisible();

    await page.goto(`/group/${groupKey}/member/upsert`);
    const saveButton = page.getByRole('button', { name: 'Додати учасника', exact: true });
    await expect(saveButton).toBeDisabled();

    await page.locator('input[name="firstName"]').fill('E2EValidationFirst');
    await page.locator('input[name="middleName"]').fill('E2EValidationMiddle');
    await page.locator('input[name="lastName"]').fill('E2EValidationLast');
    await page.locator('input[name="email"]').fill('not-an-email');
    await expect(saveButton).toBeDisabled();

    await fillMemberRequiredFields(page, {
      firstName: 'E2EValidationFirst',
      middleName: 'E2EValidationMiddle',
      lastName: 'E2EValidationLast',
      email: `e2e.validation.${Date.now()}@example.com`
    });

    await expect(saveButton).toBeEnabled();

    // Leaving with the form filled in asks first, and staying keeps what was typed.
    await page.getByRole('button', { name: 'Скасувати', exact: true }).click();
    const leave = page.getByRole('alertdialog', { name: 'Вийти без збереження?' });
    await expect(leave).toBeVisible();
    await leave.getByRole('button', { name: 'Лишитися' }).click();
    await expect(page).toHaveURL(new RegExp(`/group/${groupKey}/member/upsert$`));
    await expect(page.locator('input[name="firstName"]')).toHaveValue('E2EValidationFirst');
  });

  test('manager gives перестороги in order from the member card', async ({ page, request }) => {
    const memberKey = await getFirstSeededGroupMemberKey(request, user);

    await page.goto(`/member/${memberKey}`);
    await openWarningPanel(page);

    const dialog = page.getByRole('dialog', { name: 'Перестороги' });
    const give = dialog.getByRole('button', { name: /^Дати:/ });
    // Only one can be given at a time: the first that does not stand yet.
    await expect(give).toHaveCount(1);

    if (await dialog.locator('#warning-Level1').getByRole('button', { name: /^Дати:/ }).count()) {
      await expect(dialog.locator('#warning-Level3')).toHaveCount(0);
      await give.click();
      await page.getByRole('alertdialog').getByRole('button', { name: 'Дати', exact: true }).click();
      await expect(dialog.locator('#warning-Level2').getByRole('button', { name: /^Дати:/ })).toBeVisible();
      await expect(dialog.locator('#warning-Level1').getByRole('button', { name: /^Скасувати:/ })).toBeVisible();
    }
  });
});

describeRole('mentor', 'Mentor role UI controls', ({ user }) => {
  test('mentor edits a youth of his гурток from one main button and reviews skills', async ({ page, request }) => {
    const kurinKey = await getSeededKurinKey(request, user);
    const memberKey = await getFirstSeededGroupMemberKey(request, user);

    await page.goto(`/member/${memberKey}`);
    const head = page.locator('.member-actions');
    await expect(head.getByRole('button', { name: 'Редагувати' })).toBeVisible();
    await expect(head.getByRole('button', { name: 'Перестороги' })).toHaveCount(0);
    await expect(head.getByRole('button', { name: 'Журнал' })).toHaveCount(0);
    await head.getByRole('button', { name: 'Ще' }).click();
    await expect(page.getByRole('menuitem', { name: 'Перестороги' })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('member-life')).toBeVisible();

    await page.goto(`/kurin/${kurinKey}/review/skills`);
    await expect(page).toHaveURL(new RegExp(`/kurin/${kurinKey}/review/skills`));
    await expect(page.getByRole('heading', { level: 1, name: 'Модерація вмілостей' })).toBeVisible();
    await expect(page.locator('.p-message-warn')).toBeHidden();
  });
});

describeRole('member', 'Member role UI controls', ({ user }) => {
  test('member edits only their own card and is restricted from skill moderation controls', async ({ page, request }) => {
    const kurinKey = await getSeededKurinKey(request, user);
    // The first seeded member of Gurtok 1 is this member: their own card.
    const memberKey = await getFirstSeededGroupMemberKey(request, user);

    await page.goto(`/member/${memberKey}`);
    await expect(page.getByTestId('member-life')).toBeVisible();
    const head = page.locator('.member-actions');
    await expect(head.getByRole('button', { name: 'Редагувати' })).toBeVisible();
    await expect(head.getByRole('button')).toHaveCount(1);

    await page.goto(`/kurin/${kurinKey}/review/skills`);
    await expect(page).toHaveURL(new RegExp(`/kurin/${kurinKey}/review/skills`));
    await expect(page.locator('.p-message-warn')).toBeVisible();
    await expect(page.locator('.skills-review__row-actions')).toBeHidden();
  });
});
