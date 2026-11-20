import { expect, test } from '@playwright/test';

import { getFirstSeededGroupMemberKey } from './support/api-client';
import { describeRole } from './support/role-test';
import { accessTokenOf, createOrdenPerson, openMemberCard, recordSeniorAward, signInAs } from './support/senior-honours';
import { closeLastPrimeDialog } from './support/ui';

/**
 * Senior відзначення (AWARD-02): a person in a senior kurin records their own from the catalogue, it
 * counts at once, repeats gather in one block, and only the person pins. An УПЮ kurin keeps its own.
 */
test.describe('Senior awards on one’s own card', () => {
  // Each test makes its own person with an account first: invitation, activation and a few password
  // logins, which alone eat most of the default 30 s under a full parallel run.
  test.slow();

  test('recorded by the person, counted at once, repeated in one block and pinned', async ({ page, request }, testInfo) => {
    const person = await createOrdenPerson(request, testInfo, 'Власні', 'own');
    await signInAs(page, request, person.user);
    await openMemberCard(page, person.member.memberKey);

    const tile = page.getByTestId('senior-awards-tile');
    for (const reason of ['За табір', 'За вишкіл']) {
      await tile.getByRole('button', { name: 'Додати відзначення' }).click();
      const form = page.getByRole('dialog', { name: 'Нове відзначення' });
      await form.locator('#senior-award-item').click();
      await page.getByRole('option', { name: 'Грамота подяки' }).click();
      await form.getByLabel('За що').fill(reason);
      await form.getByRole('button', { name: 'Зберегти' }).click();
      await expect(form).toBeHidden();
    }

    const block = tile.getByTestId('award-block').filter({ hasText: 'Грамота подяки' });
    await expect(block).toContainText('×2');
    await expect(block).not.toContainText('Очікує');
    await block.click();

    const details = page.getByRole('dialog', { name: 'Грамота подяки' });
    await expect(details.getByTestId('award-grant')).toHaveCount(2);
    await expect(details).toContainText('За табір');
    await expect(details).toContainText('За вишкіл');

    await details.getByRole('button', { name: 'Закріпити' }).click();
    await expect(details.getByRole('button', { name: 'Відкріпити' })).toBeVisible();
    await closeLastPrimeDialog(page);
    await expect(block.getByLabel('Закріплене')).toBeVisible();
  });

  test('more than the tile holds opens under «Переглянути всі»', async ({ page, request }, testInfo) => {
    const person = await createOrdenPerson(request, testInfo, 'Усі', 'all');
    const token = await accessTokenOf(request, person.user);
    const catalogIds = ['usp-hramota-podiaky', 'usp-hramota-vyriznennia', 'usp-hramota-zasluhy', 'orden-sv-yuriia-bronza', 'ups-hramota-pryznannia'];
    for (const [index, catalogId] of catalogIds.entries()) {
      const recorded = await recordSeniorAward(request, token, person.member.memberKey, { catalogId, dateAcquired: `202${index}-05-01` });
      expect(recorded.status, JSON.stringify(recorded.body)).toBe(200);
    }

    await signInAs(page, request, person.user);
    await openMemberCard(page, person.member.memberKey);

    const tile = page.getByTestId('senior-awards-tile');
    await expect.poll(() => tile.getByTestId('award-block').count()).toBeLessThan(catalogIds.length);
    await tile.getByRole('button', { name: `Переглянути всі (${catalogIds.length})` }).click();

    const all = page.getByRole('dialog', { name: 'Усі відзначення' });
    await expect(all.getByTestId('award-block')).toHaveCount(catalogIds.length);
    await closeLastPrimeDialog(page);
  });
});

describeRole('manager', 'Senior awards stay out of an УПЮ kurin', ({ user }) => {
  test('the API refuses one and the card offers none', async ({ page, request }) => {
    const memberKey = await getFirstSeededGroupMemberKey(request, user);

    const refused = await recordSeniorAward(request, await accessTokenOf(request, user), memberKey, { catalogId: 'usp-hramota-podiaky', dateAcquired: '2025-05-01' });
    expect(refused.status).toBe(400);
    expect(JSON.stringify(refused.body)).toContain('SeniorAwardOutsideSeniorKurin');

    await openMemberCard(page, memberKey);
    await expect(page.getByTestId('senior-awards-tile')).toHaveCount(0);
    await expect(page.getByTestId('senior-award-add')).toHaveCount(0);
  });
});
