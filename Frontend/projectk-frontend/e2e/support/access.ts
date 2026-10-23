import { expect, Page } from '@playwright/test';

export async function expectForbiddenRoute(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await expect(page).toHaveURL(/\/forbidden/);
}

export async function expectLoginRedirect(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await expect(page).toHaveURL(/\/login/);
}

export async function expectAllowedRoute(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await expect(page).toHaveURL(new RegExp(escapeRegExp(path)));
  await expect(page.locator('body')).not.toContainText('Forbidden');
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`);
}
