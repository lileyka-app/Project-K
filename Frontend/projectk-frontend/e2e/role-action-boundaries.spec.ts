import { expect, test } from '@playwright/test';

import { getMembersByGroup, getSeededGroupKey, getSeededKurinKey, loginViaApi } from './support/api-client';
import { expectAllowedRoute, expectForbiddenRoute, expectLoginRedirect } from './support/access';
import { describeRole } from './support/role-test';

test.describe('Route action boundaries', () => {
  test('anonymous users are redirected from protected organization routes', async ({ page }) => {
    for (const path of ['/kurin', '/settings/account', '/panel', '/users', '/waitlist']) {
      await expectLoginRedirect(page, path);
    }
  });

  describeRole('mentor', 'Mentor action routes', ({ user }) => {
    test('mentor can open assigned group member creation but not unassigned group creation', async ({ page, request }) => {
      const assignedGroupKey = await getSeededGroupKey(request, user, 'Gurtok 1');
      const unassignedGroupKey = await getSeededGroupKey(request, user, 'Gurtok 2');

      await expectAllowedRoute(page, `/group/${assignedGroupKey}/member/upsert`);
      await expect(page.getByRole('button', { name: 'Додати учасника', exact: true })).toBeDisabled();
      await expect(page.getByRole('button', { name: 'Видалити профіль' })).toBeHidden();

      await expectForbiddenRoute(page, `/group/${unassignedGroupKey}/member/upsert`);
    });

    // A впорядник seats his own гурток's провід (STAB-08), but not another гурток's, the kurin's or the КВ.
    test('mentor sets up only his own гурток provid', async ({ page, request }) => {
      const kurinKey = await getSeededKurinKey(request, user);
      const assignedGroupKey = await getSeededGroupKey(request, user, 'Gurtok 1');
      const unassignedGroupKey = await getSeededGroupKey(request, user, 'Gurtok 2');

      await expectAllowedRoute(page, `/leadership/create/group/${assignedGroupKey}`);
      await expectForbiddenRoute(page, `/leadership/create/group/${unassignedGroupKey}`);
      await expectForbiddenRoute(page, `/leadership/create/kurin/${kurinKey}`);
      await expectForbiddenRoute(page, `/leadership/create/kv/${kurinKey}`);
    });
  });

  describeRole('member', 'Member action routes', ({ user }) => {
    test('member cannot open create routes or another member edit route', async ({ page, request }) => {
      const kurinKey = await getSeededKurinKey(request, user);
      const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');
      const members = await getMembersByGroup(request, user, groupKey);
      const otherMember = members.find(member => member.email !== user.email);
      expect(otherMember?.memberKey, 'Expected a seeded group member different from the logged-in member.').toBeTruthy();

      await expectForbiddenRoute(page, `/group/${groupKey}/member/upsert`);
      await expectForbiddenRoute(page, `/kurin/${kurinKey}/member/upsert`);
      await expectForbiddenRoute(page, `/group/${groupKey}/member/upsert/${otherMember!.memberKey}`);
    });

    test('member can open own edit route without destructive or warning controls', async ({ page, request }) => {
      const login = await loginViaApi(request, user);
      const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');
      expect(login.memberKey, `API login for ${user.email} did not return a memberKey.`).toBeTruthy();

      await expectAllowedRoute(page, `/group/${groupKey}/member/upsert/${login.memberKey}`);
      await expect(page.getByRole('button', { name: 'Зберегти', exact: true })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Видалити профіль' })).toBeHidden();
      await expect(page.locator('#email')).toBeHidden();
      await expect(page.locator('#warning-Level1')).toBeHidden();
    });

    test('member cannot open leadership setup routes directly', async ({ page, request }) => {
      const kurinKey = await getSeededKurinKey(request, user);
      const groupKey = await getSeededGroupKey(request, user, 'Gurtok 1');

      await expectForbiddenRoute(page, `/leadership/create/kurin/${kurinKey}`);
      await expectForbiddenRoute(page, `/leadership/create/group/${groupKey}`);
      await expectForbiddenRoute(page, `/leadership/create/kv/${kurinKey}`);
    });
  });
});
