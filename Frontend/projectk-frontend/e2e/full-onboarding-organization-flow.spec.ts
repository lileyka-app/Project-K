import { expect, test } from '@playwright/test';

import {
  approveWaitlistEntryByEmail,
  assignMentorViaApi,
  createGroupViaApi,
  createLeadershipViaApi,
  createMemberViaApi,
  getKurinByKey,
  getLatestInvitationByEmail,
  getMembersByGroup,
  getMembersByKurin,
  loginViaApi
} from './support/api-client';
import { e2eUsers } from './support/test-users';
import { loginThroughUi } from './support/login';
import { fillDatePicker, fillPhone } from './support/ui';
import { createActivatedMemberAccount, memberFullName, scenarioEmail, scenarioSuffix } from './support/scenarios';

test.describe.configure({ mode: 'serial' });

test.describe('Full onboarding organization workflow', () => {
  test('leader joins, activates a manager account, builds groups and assigned mentor scope', async ({ page, request }, testInfo) => {
    test.setTimeout(90_000);

    const suffix = scenarioSuffix(testInfo);
    const password = 'Flow@12345';
    const kurinNumber = `8${Date.now().toString().slice(-4)}`;
    const managerEmail = scenarioEmail(suffix, 'flow.manager');
    const mentorEmail = scenarioEmail(suffix, 'flow.mentor');
    const regularUserEmail = scenarioEmail(suffix, 'flow.user');

    const managerUser = { role: 'manager' as const, email: managerEmail, password };

    await test.step('submit public join form as a kurin leader candidate', async () => {
      await page.goto('/join');
      const submit = page.locator('form').getByRole('button').last();
      await expect(submit).toBeDisabled();

      await page.locator('#firstName').fill(`FlowManager${suffix}`);
      await page.locator('#lastName').fill('Leader');
      await page.locator('#email').fill(managerEmail);
      await page.locator('#stanytsia').fill('Flow Stanytsia');
      await expect(page.locator('#krai')).toHaveText('Україна');
      await fillPhone(page.locator('input#phone'), '501112233');

      const dateOfBirth = page.locator('#dob input');
      await fillDatePicker(dateOfBirth, '01.01.2000');
      await expect(dateOfBirth).toHaveValue('01.01.2000');

      await page.locator('#leader').check();
      await page.locator('#kurin').fill(`${kurinNumber}abc`);
      await expect(page.locator('#kurin')).toHaveValue(kurinNumber);
      await expect(submit).toBeEnabled();
      await submit.click();
      await expect(page.getByRole('alert').filter({ hasText: 'Дякуємо' })).toBeVisible();
    });

    await test.step('admin sees and approves the waitlist entry', async () => {
      await loginThroughUi(page, e2eUsers.admin);
      await page.goto('/waitlist');
      await expect(page.getByRole('heading', { name: 'Заявки на приєднання' })).toBeVisible();
      await approveWaitlistEntryByEmail(request, e2eUsers.admin, managerEmail);
    });

    await test.step('activate the manager from the captured invitation token', async () => {
      const invitation = await getLatestInvitationByEmail(request, managerEmail);
      expect(invitation.token).toBeTruthy();

      await page.evaluate(() => localStorage.clear());
      await page.context().clearCookies();
      await page.goto(`/activate/${invitation.token}`);
      await expect(page.getByText('Активація акаунта')).toBeVisible();

      await page.locator('p-password').first().locator('input').fill(password);
      await page.locator('p-password').nth(1).locator('input').fill(password);
      await page.locator('form').getByRole('button').last().click();
      // Activation applies the session itself now (PROD-01): the new leader lands inside the app,
      // not on the sign-in page.
      await expect(page).not.toHaveURL(/\/activate\//, { timeout: 10_000 });
      await expect(page).not.toHaveURL(/\/login/);

      const login = await loginViaApi(request, managerUser);
      // Активований керівник саджається Зв'язковим, а це і є повне керування куренем.
      expect(login.permissions).toContain('Group:Manage:KurinWide');
      expect(login.kurinKey).toBeTruthy();

      const kurin = await getKurinByKey(request, managerUser, login.kurinKey!);
      expect(String(kurin.number)).toBe(kurinNumber);
    });

    let kurinKey = '';
    let managerMemberKey = '';
    let groupA = { groupKey: '', name: '' };
    let groupB = { groupKey: '', name: '' };
    let mentorMember = {
      memberKey: '',
      userKey: null as string | null | undefined,
      email: mentorEmail,
      firstName: '',
      middleName: '',
      lastName: ''
    };
    const mentorUser = { role: 'mentor' as const, email: mentorEmail, password };

    const managerCreatedNames: string[] = [];
    const mentorCreatedNames: string[] = [];

    await test.step('manager creates groups and user-backed members', async () => {
      const login = await loginViaApi(request, managerUser);
      kurinKey = login.kurinKey!;
      managerMemberKey = login.memberKey!;
      expect(managerMemberKey).toBeTruthy();

      groupA = await createGroupViaApi(request, managerUser, kurinKey, `Flow Gurtok A ${suffix}`);
      groupB = await createGroupViaApi(request, managerUser, kurinKey, `Flow Gurtok B ${suffix}`);

      const mentorAccount = await createActivatedMemberAccount(request, managerUser, { kurinKey }, {
        firstName: `FlowMentor${suffix}`,
        middleName: 'Account',
        lastName: 'Candidate',
        email: mentorEmail
      }, password, 'mentor');
      mentorMember = mentorAccount.member;

      const regularAccount = await createActivatedMemberAccount(request, managerUser, { groupKey: groupB.groupKey }, {
        firstName: `FlowUser${suffix}`,
        middleName: 'Account',
        lastName: 'Candidate',
        email: regularUserEmail
      }, password, 'member');
      managerCreatedNames.push(memberFullName(regularAccount.member));

      for (const [index, group] of [groupA, groupB].entries()) {
        const member = await createMemberViaApi(request, managerUser, { groupKey: group.groupKey }, {
          firstName: `FlowScout${index}${suffix}`,
          middleName: 'Manager',
          lastName: 'Created'
        });
        managerCreatedNames.push(memberFullName(member));
      }

      expect(mentorMember.userKey).toBeTruthy();
    });

    await test.step('manager assigns mentor access and leadership records', async () => {
      await assignMentorViaApi(request, managerUser, groupA.groupKey, mentorMember.userKey!);

      const mentorLogin = await loginViaApi(request, mentorUser);
      // Призначення ментора саме по собі синкає офіс Виховника (LeadershipRoleSyncService).
      expect(mentorLogin.permissions).toContain('Group:Update:OwnGroups');
      expect(mentorLogin.kurinKey).toBe(kurinKey);

      await createLeadershipViaApi(request, managerUser, {
        type: 'Kurin',
        entityKey: kurinKey,
        role: 'Kurinnuy',
        memberKey: managerMemberKey,
        firstName: `FlowManager${suffix}`,
        lastName: 'Leader'
      });

      await createLeadershipViaApi(request, managerUser, {
        type: 'Group',
        entityKey: groupA.groupKey,
        role: 'Hurtkoviy',
        memberKey: mentorMember.memberKey,
        firstName: mentorMember.firstName,
        middleName: mentorMember.middleName,
        lastName: mentorMember.lastName
      });
    });

    await test.step('assigned mentor adds members inside the assigned group', async () => {
      for (let index = 0; index < 2; index += 1) {
        const member = await createMemberViaApi(request, mentorUser, { groupKey: groupA.groupKey }, {
          firstName: `FlowMentorScout${index}${suffix}`,
          middleName: 'Mentor',
          lastName: 'Created'
        });
        mentorCreatedNames.push(memberFullName(member));
      }
    });

    await test.step('member lists reflect all created users and assignments', async () => {
      const kurinMembers = await getMembersByKurin(request, managerUser, kurinKey);
      const groupMembers = await getMembersByGroup(request, managerUser, groupA.groupKey);
      const kurinMemberNames = kurinMembers.map(member => `${member.lastName} ${member.firstName}`);
      const groupMemberNames = groupMembers.map(member => `${member.lastName} ${member.firstName}`);

      expect(kurinMemberNames).toContain(`Candidate FlowMentor${suffix}`);
      for (const memberName of [...managerCreatedNames, ...mentorCreatedNames]) {
        expect(kurinMemberNames).toContain(memberName);
      }
      for (const memberName of mentorCreatedNames) {
        expect(groupMemberNames).toContain(memberName);
      }

      await page.evaluate(() => localStorage.clear());
      await page.context().clearCookies();
      await loginThroughUi(page, managerUser);
      await page.goto('/kurin');
      await expect(page.locator('body')).toContainText(groupA.name);
      await expect(page.locator('body')).toContainText(groupB.name);
      await expect(page.locator('body')).toContainText(`FlowMentor${suffix}`);

      await page.evaluate(() => localStorage.clear());
      await page.context().clearCookies();
      await loginThroughUi(page, mentorUser);
      await page.goto(`/group/${groupA.groupKey}`);
      await expect(page.locator('body')).toContainText(mentorMember.firstName);
      for (const memberName of mentorCreatedNames) {
        await expect(page.locator('body')).toContainText(memberName);
      }
    });
  });
});
