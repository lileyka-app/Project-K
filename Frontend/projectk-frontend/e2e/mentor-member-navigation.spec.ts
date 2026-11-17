import { expect, test } from '@playwright/test';

import { getSeededGroupKey, getSeededKurinKey } from './support/api-client';
import { describeRole } from './support/role-test';

test.describe('Mentor and Member navigation checks', () => {
  describeRole('mentor', 'Mentor', ({ user }) => {
    test('mentor can navigate groups and view members without edit controls', async ({ page, request }) => {
      const kurinKey = await getSeededKurinKey(request, user);
      const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

      await page.goto(`/group/${groupKey}`);
      await expect(page).toHaveURL(new RegExp(`/group/${groupKey}`));
      await expect(page.getByRole('button', { name: /Редагувати/ })).toBeHidden();

      // Planning is the one thing a Виховник may start here: RolePermissionRules gives every провід
      // member PlanningSession:Create, scoped to their own гуртки. This used to assert the opposite
      // — the pre-refactor world where UserRole.Mentor could only read — and passed only while the
      // fixture was seated without its office.
      await page.goto(`/planning/${kurinKey}`);
      await expect(page).toHaveURL(new RegExp(`/planning/${kurinKey}`));
      await expect(page.getByRole('button', { name: /Створити/ }).first()).toBeVisible();
    });
  });

  describeRole('member', 'Member', ({ user }) => {
    test('member can navigate group and view profile without edit controls for unauthorized items', async ({ page, request }) => {
      const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

      await page.goto(`/group/${groupKey}`);
      await expect(page).toHaveURL(new RegExp(`/group/${groupKey}`));
      await expect(page.getByRole('button', { name: /Редагувати/ })).toBeHidden();

      await page.goto(`/group/${groupKey}/member/upsert`);
      await expect(page).toHaveURL(/\/forbidden|\/kurin|\/group/);
    });
  });
});
