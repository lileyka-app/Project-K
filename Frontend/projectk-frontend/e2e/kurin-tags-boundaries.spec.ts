import { expect, test } from '@playwright/test';

import { getSeededKurinKey } from './support/api-client';
import { e2eApiUrl } from './support/e2e-api';
import { accessToken, addYouthAccount, createTag, createTagKurin, setMemberTags, setTagsEnabled } from './support/kurin-tags';
import { describeRole } from './support/role-test';
import { signInAs } from './support/senior-honours';
import { e2eUsers } from './support/test-users';

/**
 * Who may not touch kurin tags: the КВ reads the tag settings but cannot change them, and a youth
 * sees their own tags but cannot put any on.
 */
describeRole('mentor', 'Kurin tags: a виховник of the КВ', () => {
  test('reads the tag settings without changing them', async ({ page, request }) => {
    const kurinKey = await getSeededKurinKey(request, e2eUsers.mentor);

    await page.goto(`/kurin/${kurinKey}/settings`);
    await expect(page.getByText('Лише перегляд')).toBeVisible({ timeout: 15_000 });
    await expect(page.getByLabel('Курінь користується тегами')).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Зберегти' })).toHaveCount(0);

    // The current values sent back unchanged: the seeded kurin stays as it is even if this were let through.
    const headers = { Authorization: `Bearer ${await accessToken(request, e2eUsers.mentor)}` };
    const current = await request.get(`${e2eApiUrl}/kurin/${kurinKey}/tag-settings`, { headers });
    expect(current.ok(), `the КВ could not read the tag settings: ${current.status()}`).toBe(true);
    const settings = await current.json() as { enabled: boolean; ownTagsVisible: boolean };
    const change = await request.put(`${e2eApiUrl}/kurin/${kurinKey}/tag-settings`, {
      headers,
      data: { enabled: settings.enabled, ownTagsVisible: settings.ownTagsVisible }
    });
    expect(change.status()).toBe(403);
  });
});

test.describe('Kurin tags: a youth without tag rights', () => {
  test.slow();

  test('sees their own tag but cannot put tags on', async ({ page, request }, testInfo) => {
    const kurin = await createTagKurin(request, testInfo);
    const founderToken = await accessToken(request, kurin.founder);
    await setTagsEnabled(request, founderToken, kurin.kurinKey, true);
    const newcomers = await createTag(request, founderToken, kurin.kurinKey, 'Новики');
    const youth = await addYouthAccount(request, kurin, 'Юний', 'youth');
    expect(await setMemberTags(request, founderToken, youth.member.memberKey, [newcomers.kurinTagKey])).toBe(200);

    await signInAs(page, request, youth.user);
    await page.goto(`/member/${youth.member.memberKey}`);
    const tags = page.locator('.member-tags');
    await expect(tags).toContainText('Новики', { timeout: 15_000 });
    await expect(page.getByRole('button', { name: 'Змінити теги' })).toHaveCount(0);

    const youthToken = await accessToken(request, youth.user);
    expect(await setMemberTags(request, youthToken, youth.member.memberKey, [])).toBe(403);
    const create = await request.post(`${e2eApiUrl}/kurin/${kurin.kurinKey}/tags`, {
      headers: { Authorization: `Bearer ${youthToken}` },
      data: { name: 'Свій', colorHex: newcomers.colorHex }
    });
    expect(create.status()).toBe(403);
  });
});
