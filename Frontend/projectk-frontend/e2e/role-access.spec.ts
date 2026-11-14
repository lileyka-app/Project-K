import { expect, test } from '@playwright/test';
import { getSeededKurinKey } from './support/api-client';
import { describeRole } from './support/role-test';

describeRole('admin', 'Admin role access', () => {
  test('admin can open global admin pages', async ({ page }) => {
    await page.goto('/panel');
    await expect(page).toHaveURL(/\/panel/);
    await expect(page.locator('body')).not.toContainText('Forbidden');

    await page.goto('/waitlist');
    await expect(page).toHaveURL(/\/waitlist/);
    await expect(page.locator('body')).not.toContainText('Forbidden');

    await page.goto('/users');
    await expect(page).toHaveURL(/\/users/);
    await expect(page.locator('body')).not.toContainText('Forbidden');
  });

  test('admin is redirected from kurin to panel if they have no kurinKey', async ({ page }) => {
    await page.goto('/kurin');
    await expect(page).toHaveURL(/\/panel/);
  });
});

describeRole('manager', 'Manager role access', ({ user }) => {
  test('manager can access kurin and planning create but not admin pages', async ({ page, request }) => {
    await page.goto('/kurin');
    await expect(page).toHaveURL(/\/kurin/);
    await expect(page.locator('body')).not.toContainText('Forbidden');

    await page.goto('/panel');
    await expect(page).toHaveURL(/\/kurin/);

    await page.goto('/users');
    await expect(page).toHaveURL(/\/kurin/);

    const kurinKey = await getSeededKurinKey(request, user);
    await page.goto(`/planning/create/${kurinKey}`);
    await expect(page).toHaveURL(new RegExp(`/planning/create/${kurinKey}`));
    await expect(page.locator('body')).not.toContainText('Forbidden');
  });
});

describeRole('mentor', 'Mentor role access', ({ user }) => {
  test('mentor can access kurin, planning list and planning creation but not admin pages', async ({ page, request }) => {
    await page.goto('/kurin');
    await expect(page).toHaveURL(/\/kurin/);
    await expect(page.locator('body')).not.toContainText('Forbidden');

    await expect(page.getByRole('button', { name: 'Редагувати' })).toBeHidden();
    await expect(page.getByRole('button', { name: 'Створити' })).toBeHidden();
    await expect(page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' })).toBeHidden();

    for (const adminPage of ['/waitlist', '/users', '/panel']) {
      await page.goto(adminPage);
      await expect(page).toHaveURL(/\/kurin/);
    }

    const kurinKey = await getSeededKurinKey(request, user);
    await page.goto(`/planning/${kurinKey}`);
    await expect(page).toHaveURL(new RegExp(`/planning/${kurinKey}`));
    await expect(page.locator('body')).not.toContainText('Forbidden');

    // Виховник планує у своєму гуртку, тож форма створення йому відкрита; бекенд звужує scope при збереженні.
    await page.goto(`/planning/create/${kurinKey}`);
    await expect(page).toHaveURL(new RegExp(`/planning/create/${kurinKey}`));
  });
});

describeRole('member', 'Member role access', ({ user }) => {
  test('member can access kurin and read planning, but not create it or reach admin pages', async ({ page, request }) => {
    await page.goto('/kurin');
    await expect(page).toHaveURL(/\/kurin/);
    await expect(page.locator('body')).not.toContainText('Forbidden');

    await expect(page.getByRole('button', { name: 'Редагувати' })).toBeHidden();
    await expect(page.getByRole('button', { name: 'Створити' })).toBeHidden();
    await expect(page.locator('app-member-list').getByRole('button', { name: 'Додати учасника' })).toBeHidden();

    for (const adminPage of ['/panel', '/waitlist', '/users']) {
      await page.goto(adminPage);
      await expect(page).toHaveURL(/\/kurin/);
    }

    // Planning is the kurin's own record and every member may read it; opening a session stays
    // with the провід, so the list is reachable and the create page is not.
    const kurinKey = await getSeededKurinKey(request, user);
    await page.goto(`/planning/${kurinKey}`);
    await expect(page).toHaveURL(new RegExp(`/planning/${kurinKey}`));
    await expect(page.getByRole('heading', { name: 'Планування', level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Нове планування' })).toBeHidden();

    await page.goto(`/planning/create/${kurinKey}`);
    await expect(page).toHaveURL(/\/forbidden/);
  });
});
