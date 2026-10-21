import { expect, Locator, Page } from '@playwright/test';

export function dialog(page: Page): Locator {
  return page.getByRole('dialog').last();
}

export async function closeDialog(page: Page): Promise<void> {
  const activeDialog = dialog(page);
  await activeDialog.getByRole('button').first().click();
  await expect(activeDialog).toBeHidden();
}

export async function closePrimeDialog(activeDialog: Locator): Promise<void> {
  await activeDialog.locator('.p-dialog-header button').first().click();
  await expect(activeDialog).toBeHidden();
}

export async function closeLastPrimeDialog(page: Page): Promise<void> {
  const dialogs = page.getByRole('dialog');
  const dialogCount = await dialogs.count();
  await dialogs.last().locator('.p-dialog-header button').first().click();
  await expect(dialogs).toHaveCount(dialogCount - 1);
}

export async function openRowMenu(row: Locator): Promise<void> {
  await row.getByRole('button').last().click();
}

export async function chooseMenuItem(page: Page, index: number): Promise<void> {
  await page.getByRole('menuitem').nth(index).click();
}

export function visibleMenuItems(page: Page): Locator {
  return page.getByRole('menuitem');
}

export async function openGroupActionMenu(page: Page): Promise<void> {
  await page.locator('.group-actions').getByRole('button').first().click();
  await expect(visibleMenuItems(page).first()).toBeVisible();
}

export async function submitManagePanel(page: Page): Promise<void> {
  await dialog(page).locator('.actions').last().getByRole('button').last().click();
}

export async function cancelManagePanel(page: Page): Promise<void> {
  await dialog(page).locator('.actions').last().getByRole('button').first().click();
}

export async function fillManagePanelFields(page: Page, values: string[]): Promise<void> {
  const inputs = dialog(page).locator('input:visible:not([disabled]), textarea:visible:not([disabled])');
  for (let index = 0; index < values.length; index += 1) {
    await inputs.nth(index).fill(values[index]);
  }
}

export function submitButton(scope: Locator): Locator {
  return scope.getByRole('button').last();
}

/**
 * Puts a date into a `p-datePicker`, and makes sure it stuck.
 *
 * The component only accepts real keystrokes — setting the value outright leaves its model empty and
 * the field blanks itself on blur — so this has to type. Typing races it, though: it parses on every
 * keystroke, a half-typed "01.01.20" is already a valid date it rewrites formatted, and clicking the
 * field first opens the overlay, where Enter picks whatever the panel highlights. Under the load of a
 * full parallel run that produced a cleared field often enough to fail assertions, in three slightly
 * different spellings scattered across the suite.
 *
 * So: one spelling, the overlay dismissed before blurring, and the result checked — a rejected value
 * blanks the field, which is exactly the case worth retrying rather than reporting as a failure.
 */
/**
 * Types the national part of a phone into the phone field (the country is picked beside it) and
 * leaves the box, which is when the field spaces the number out.
 */
export async function fillPhone(input: Locator, national: string): Promise<void> {
  await input.fill(national);
  await input.blur();
}

export async function fillDatePicker(input: Locator, value: string): Promise<void> {
  for (let attempt = 0; attempt < 3; attempt++) {
    await input.click();
    await input.fill('');
    await input.pressSequentially(value, { delay: 50 });
    await input.press('Escape');
    await input.blur();

    if (await input.inputValue() === value) {
      return;
    }
  }

  await expect(input).toHaveValue(value);
}

export async function fillMemberRequiredFields(page: Page, data: {
  firstName: string;
  middleName: string;
  lastName: string;
  email: string;
  phone?: string;
  dateOfBirth?: string;
}): Promise<void> {
  await page.locator('input[name="firstName"]').fill(data.firstName);
  await page.locator('input[name="middleName"]').fill(data.middleName);
  await page.locator('input[name="lastName"]').fill(data.lastName);
  await page.locator('input[name="email"]').fill(data.email);

  await fillPhone(page.locator('input#phoneNumber'), data.phone ?? '501234567');

  await fillDatePicker(page.locator('input[name="dateOfBirth"]'), data.dateOfBirth ?? '12.12.2000');
}

export async function openWarningPanel(page: Page): Promise<void> {
  // Перестороги live on the member card, in a dialog behind their own button.
  await page.locator('.member-actions').getByRole('button', { name: 'Перестороги' }).click();
  await expect(page.getByRole('dialog', { name: 'Перестороги' })).toBeVisible();
}

export function acceptBrowserConfirm(page: Page): void {
  page.once('dialog', async confirmDialog => {
    await confirmDialog.accept();
  });
}
