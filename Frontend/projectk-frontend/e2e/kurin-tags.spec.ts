import { expect, test } from '@playwright/test';

import { e2eApiUrl } from './support/e2e-api';
import {
  accessToken,
  addPerson,
  closeOverlayIn,
  createTag,
  createTagKurin,
  exportedColumns,
  openRegistry,
  openTagManager,
  registryCells,
  saveSettings,
  setMemberTags,
  setTagsEnabled
} from './support/kurin-tags';
import { signInAs } from './support/senior-honours';

/**
 * Kurin tags (TAG-01…03) on the main path: the Звʼязковий switches them on, keeps the kurin's list,
 * puts them on people, reads them in the registry and its file, and addresses a task to everyone with
 * a tag. Each test opens a kurin of its own, since switching tags off is kurin-wide.
 */
test.describe('Kurin tags', () => {
  // A kurin, its founder's activation and a few password logins come before the first click.
  test.slow();

  test('switched on in the settings, kept in the registry, switched off again', async ({ page, request }, testInfo) => {
    const kurin = await createTagKurin(request, testInfo);
    await signInAs(page, request, kurin.founder);
    const toggle = page.getByLabel('Курінь користується тегами');

    await test.step('a youth kurin starts with tags off; the Звʼязковий switches them on', async () => {
      await page.goto(`/kurin/${kurin.kurinKey}/settings`);
      await expect(toggle).not.toBeChecked({ timeout: 15_000 });
      await toggle.click();
      await expect(toggle).toBeChecked();
      await saveSettings(page);
    });

    await test.step('creates, renames and deletes tags in «Теги куреня»', async () => {
      await openRegistry(page);
      const manager = await openTagManager(page);

      for (const name of ['Новики', 'Табір']) {
        await manager.getByRole('button', { name: 'Новий тег' }).click();
        const form = page.getByRole('dialog', { name: 'Новий тег' });
        await form.getByLabel('Назва').fill(name);
        await form.getByRole('button', { name: 'Зберегти' }).click();
        await expect(form).toBeHidden();
      }
      await expect(manager.locator('.tag-list__name')).toHaveText(['Новики', 'Табір']);

      await manager.getByRole('button', { name: 'Змінити тег «Табір»' }).click();
      const edit = page.getByRole('dialog', { name: 'Змінити тег' });
      await edit.getByLabel('Назва').fill('Табірники');
      await edit.getByRole('button', { name: 'Зберегти' }).click();
      await expect(edit).toBeHidden();
      await expect(manager.locator('.tag-list__name')).toHaveText(['Новики', 'Табірники']);

      await manager.getByRole('button', { name: 'Видалити тег «Табірники»' }).click();
      const confirm = page.getByRole('alertdialog').filter({ hasText: 'Видалити тег?' });
      await confirm.getByRole('button', { name: 'Видалити' }).click();
      await expect(manager.locator('.tag-list__name')).toHaveText(['Новики']);
    });

    await test.step('switched off, the registry has neither the tag list nor the column', async () => {
      await page.goto(`/kurin/${kurin.kurinKey}/settings`);
      await expect(toggle).toBeChecked({ timeout: 15_000 });
      await toggle.click();
      await expect(toggle).not.toBeChecked();
      await saveSettings(page);

      await openRegistry(page);
      await expect(registryCells(page, 'Телефон').first()).toBeVisible();
      await expect(registryCells(page, 'Теги')).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Теги куреня' })).toHaveCount(0);
    });
  });

  test('put on a person from the card, shown and filtered in the registry and its file', async ({ page, request }, testInfo) => {
    const kurin = await createTagKurin(request, testInfo);
    const token = await accessToken(request, kurin.founder);
    await setTagsEnabled(request, token, kurin.kurinKey, true);
    await createTag(request, token, kurin.kurinKey, 'Новики');
    await createTag(request, token, kurin.kurinKey, 'Табір');
    const tagged = await addPerson(request, kurin, 'Тегований', 'tagged');
    const untagged = await addPerson(request, kurin, 'Безтеговий', 'untagged');
    await signInAs(page, request, kurin.founder);

    await test.step('«Змінити теги» on the card puts a tag on the person', async () => {
      await page.goto(`/member/${tagged.memberKey}`);
      const tags = page.locator('.member-tags');
      await expect(tags).toContainText('Тегів ще немає', { timeout: 15_000 });
      await tags.getByRole('button', { name: 'Змінити теги' }).click();

      const form = page.getByRole('dialog', { name: 'Теги людини' });
      await form.locator('.member-tags__select').click();
      await page.getByRole('option', { name: 'Новики' }).click();
      await closeOverlayIn(form);
      await form.getByRole('button', { name: 'Зберегти' }).click();
      await expect(form).toBeHidden();
      await expect(tags).toContainText('Новики');
      await expect(tags).not.toContainText('Тегів ще немає');
    });

    const taggedRow = page.locator('tr').filter({ hasText: tagged.lastName });
    const untaggedRow = page.locator('tr').filter({ hasText: untagged.lastName });

    await test.step('the registry shows the tag and filters by it', async () => {
      await openRegistry(page);
      await expect(registryCells(page, 'Теги').first()).toBeVisible();
      await expect(taggedRow.locator('td[data-mobile-label="Теги"]')).toContainText('Новики');
      await expect(untaggedRow).toBeVisible();

      await page.locator('.registry__tags').click();
      await page.getByRole('option', { name: 'Новики' }).click();
      // On a phone Escape leaves the list open over «Вивантажити»; a click outside closes it everywhere.
      await page.getByRole('heading', { name: 'Реєстр' }).click();
      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(taggedRow).toBeVisible();
      await expect(untaggedRow).toHaveCount(0);
    });

    await test.step('the file carries the «Теги» column', async () => {
      expect(await exportedColumns(page)).toContain('tags');
    });

    await test.step('with tags off, neither the registry nor its file has the column', async () => {
      await setTagsEnabled(request, token, kurin.kurinKey, false);
      await openRegistry(page);
      await expect(untaggedRow).toBeVisible();
      await expect(registryCells(page, 'Телефон').first()).toBeVisible();
      await expect(registryCells(page, 'Теги')).toHaveCount(0);
      await expect(page.locator('.registry__tags')).toHaveCount(0);

      const columns = await exportedColumns(page);
      expect(columns).toContain('phoneNumber');
      expect(columns).not.toContain('tags');
    });
  });

  test('a task goes to everyone with a tag, and not once tags are off', async ({ page, request }, testInfo) => {
    const kurin = await createTagKurin(request, testInfo);
    const token = await accessToken(request, kurin.founder);
    await setTagsEnabled(request, token, kurin.kurinKey, true);
    const camp = await createTag(request, token, kurin.kurinKey, 'Табір');
    const carrier = await addPerson(request, kurin, 'Табірний', 'carrier');
    expect(await setMemberTags(request, token, carrier.memberKey, [camp.kurinTagKey])).toBe(200);
    await signInAs(page, request, kurin.founder);
    const title = `Спакувати намети ${kurin.suffix}`;
    let payload: Record<string, unknown> = {};

    await test.step('«Нова задача» addresses «Усі з тегом «Табір»»', async () => {
      await page.goto(`/tasks/${kurin.kurinKey}`);
      await page.getByRole('button', { name: 'Нова задача' }).first().click();
      const form = page.getByRole('dialog', { name: 'Нове призначення' });
      await form.getByLabel('Назва').fill(title);

      const assign = form.locator('.agenda-assign-select');
      await expect(assign).toContainText('Оберіть курінь, гуртки, учасників або теги');
      await assign.click();
      const tagBranch = page.getByRole('treeitem', { name: 'Теги', exact: true });
      await tagBranch.getByRole('button').first().click();
      await page.getByRole('treeitem', { name: 'Усі з тегом «Табір»' }).click();
      await closeOverlayIn(form);
      await expect(assign).toContainText('Усі з тегом «Табір»');

      const created = page.waitForRequest(request => request.method() === 'POST' && /\/api\/agenda$/.test(request.url()));
      await form.getByRole('button', { name: 'Зберегти' }).click();
      payload = (await created).postDataJSON() as Record<string, unknown>;
      expect(payload['targets']).toEqual([expect.objectContaining({ targetType: 'Tag', targetKey: camp.kurinTagKey })]);
      await expect(form).toBeHidden();
      await expect(page.getByText(title)).toBeVisible();
    });

    await test.step('with tags off the API refuses the very same task', async () => {
      await setTagsEnabled(request, token, kurin.kurinKey, false);
      const response = await request.post(`${e2eApiUrl}/agenda`, {
        headers: { Authorization: `Bearer ${token}` },
        data: { ...payload, title: `${title} ще раз` }
      });
      expect(response.ok(), `a tag target was accepted with tags off: ${response.status()}`).toBe(false);
    });
  });
});
