import { expect, Locator, Page, test } from '@playwright/test';

import { createGroupViaApi, assignMentorViaApi } from './support/api-client';
import { signInAs } from './support/senior-honours';
import {
  addPerson,
  addProject,
  apiStatus,
  createProbeKurin,
  getThirdProbe,
  openThirdProbe,
  pngFile,
  projectBody,
  thirdProbeId
} from './support/third-probe';
import { fillDialogDate } from './support/ui';

/**
 * The third-probe notebook (PROBE-02): the youth writes it, their впорядник signs it, nobody signs
 * their own, and a leader whose rights do not reach the person only reads it. Each test opens a
 * kurin of its own (support/third-probe.ts).
 */
test.describe('Third-probe notebook', () => {
  // A kurin, a гурток, two activated accounts and a few password logins come before the first click.
  test.slow();

  test('the youth writes the notebook: file, посвідка and a project with a picture', async ({ page, request }, testInfo) => {
    const kurin = await createProbeKurin(request, testInfo);
    const probeId = await thirdProbeId(request, kurin.youth.user, kurin.youth.memberKey);
    await signInAs(page, request, kurin.youth.user);
    await openThirdProbe(page, kurin.youth.memberKey, probeId);

    await test.step('the countdown runs to the 18th birthday, and nothing is done yet', async () => {
      const countdown = page.getByTestId('third-probe-countdown');
      await expect(countdown).toContainText('До закриття третьої проби');
      await expect(countdown.getByRole('timer')).toHaveAccessibleName(/^Лишилося \d+ (день|дні|днів)$/);
      const checklist = page.getByTestId('third-probe-checklist');
      await expect(checklist).toContainText('0 з 5');
      await expect(checklist).toContainText('Рефлексія');
      await expect(page.getByRole('button', { name: 'Змінити етапи' })).toHaveCount(0);
    });

    await test.step('mentors, the reflection and the camp go in through «Редагувати»', async () => {
      await page.getByTestId('third-probe-mentors').getByRole('button', { name: 'Редагувати' }).click();
      const file = page.getByRole('dialog', { name: 'Ментори, рефлексія й табір' });
      await file.getByLabel('Звʼязковий').fill('Володимир Іваненко');
      await file.getByLabel('Впорядник').fill(`${kurin.mentor.firstName} ${kurin.mentor.lastName}`);
      await fillDialogDate(file.getByLabel('Рефлексія відбулася'), '05.03.2026');
      await file.getByLabel('З ким').fill('Володимир Іваненко');
      await file.getByRole('combobox', { name: 'КВТ чи інший' }).click();
      await page.getByRole('option', { name: 'КВТ' }).click();
      await file.getByLabel('Назва табору').fill('Говерла');
      await file.getByRole('button', { name: 'Зберегти' }).click();
      await expect(file).toBeHidden();

      const mentors = page.getByTestId('third-probe-mentors');
      await expect(mentors).toContainText('Володимир Іваненко');
      await expect(mentors).toContainText('5 березня 2026');
      await expect(page.getByTestId('third-probe-camp')).toContainText('Говерла');
      await expect(page.getByTestId('third-probe-checklist')).toContainText('додано');
    });

    await test.step('the посвідка is attached as a picture and counts towards the files', async () => {
      const camp = page.getByTestId('third-probe-camp');
      await camp.locator('input[type="file"]').setInputFiles(pngFile('posvidka.png'));
      await expect(camp.getByRole('img', { name: 'Посвідка табору' })).toBeVisible();
      await expect(camp).toContainText('Файли третьої проби:');
      await expect(camp.getByText('Замінити посвідку')).toBeVisible();
    });

    await test.step('a project after the template, with a picture in its description', async () => {
      await page.getByRole('button', { name: 'Додати проєкт' }).click();
      await expect(page.getByRole('dialog', { name: 'Новий проєкт' })).toBeVisible();
      // The first picture saves the project quietly, and the title turns into «Проєкт».
      const editor = projectEditor(page);
      await editor.getByLabel('Назва проєкту').fill('Мандрівка Чорногорою');
      await editor.getByLabel('Локація').fill('Чорногора');
      await editor.getByLabel('Координатор / комендант').fill('Олена Коваль');
      const description = textField(editor, 'Опис і моя роль');
      // One atomic fill: keys typed one by one race the dialog's own focus and land in the title.
      await description.locator('.ql-editor').fill('Я склав маршрут і веду групу.');
      await expect(editor.getByLabel('Назва проєкту')).toHaveValue('Мандрівка Чорногорою');

      const chooser = page.waitForEvent('filechooser');
      await description.getByRole('button', { name: 'Зображення' }).click();
      await (await chooser).setFiles(pngFile('vershyna.png'));
      await expect(description.locator('.ql-editor img')).toHaveCount(1);
      await expect(editor.getByRole('button', { name: 'Зберегти' })).toBeEnabled();
      await editor.getByRole('button', { name: 'Зберегти' }).click();
      await expect(editor).toBeHidden();

      const card = projectCard(page, 'Мандрівка Чорногорою');
      await expect(card).toContainText('Я склав маршрут і веду групу.');
      await expect(card).toContainText('Олена Коваль');
      await expect(card.locator('.md__image')).toHaveCount(1);
      await expect(card.getByRole('button', { name: 'Дати дозвіл' })).toHaveCount(0);
    });

    await test.step('the youth corrects the project; it stays one project with its picture', async () => {
      const card = projectCard(page, 'Мандрівка Чорногорою');
      await card.getByRole('button', { name: 'Редагувати' }).click();
      const editor = projectEditor(page);
      await expect(editor).toHaveAccessibleName('Проєкт');
      await editor.getByLabel('Назва проєкту').fill('Мандрівка Чорногорою з новачками');
      await editor.getByRole('button', { name: 'Зберегти' }).click();
      await expect(editor).toBeHidden();

      await expect(page.getByTestId('probe-project')).toHaveCount(1);
      await expect(projectCard(page, 'Мандрівка Чорногорою з новачками').locator('.md__image')).toHaveCount(1);
    });
  });

  test('the впорядник signs the гурток\'s notebook, and the youth\'s edits keep it signed', async ({ page, request }, testInfo) => {
    const kurin = await createProbeKurin(request, testInfo);
    const probeId = await thirdProbeId(request, kurin.youth.user, kurin.youth.memberKey);
    const projectKey = await addProject(request, kurin.youth.user, kurin.youth.memberKey, probeId, 'Шпаківні для парку');
    expect(await apiStatus(request, kurin.youth.user, 'PUT', `member/${kurin.youth.memberKey}/probes/${probeId}/third-probe`, {
      reflectionOn: '2026-03-05', reflectionWith: 'Володимир Іваненко', resume: ''
    })).toBe(200);

    await signInAs(page, request, kurin.mentor.user);
    await openThirdProbe(page, kurin.youth.memberKey, probeId);

    await test.step('the впорядник gives «дозвіл», confirms the reflection and may set the stages', async () => {
      const card = projectCard(page, 'Шпаківні для парку');
      await card.getByRole('button', { name: 'Дати дозвіл' }).click();
      await expect(card).toContainText('Дозвіл є');
      await page.getByRole('button', { name: 'Підтвердити рефлексію' }).click();
      await expect(page.getByTestId('third-probe-mentors')).toContainText(`Підтвердив`);
      await expect(page.getByRole('button', { name: 'Змінити етапи' })).toBeVisible();
    });

    await test.step('the youth rewrites the project, and «дозвіл» stays', async () => {
      expect(await apiStatus(request, kurin.youth.user, 'PUT', `member/${kurin.youth.memberKey}/probes/${probeId}/projects/${projectKey}`,
        projectBody('Шпаківні для шкільного парку', 'Дошки дає столярна майстерня.'))).toBe(200);

      await page.reload();
      const card = projectCard(page, 'Шпаківні для шкільного парку');
      await expect(card).toContainText('Дошки дає столярна майстерня.');
      await expect(card).toContainText('Дозвіл є');
      const tab = await getThirdProbe(request, kurin.youth.user, kurin.youth.memberKey, probeId);
      expect(tab.projects[0].approvedAtUtc).toBeTruthy();
    });

    await test.step('the youth cannot sign their own notebook', async () => {
      const path = `member/${kurin.youth.memberKey}/probes/${probeId}`;
      expect(await apiStatus(request, kurin.youth.user, 'DELETE', `${path}/projects/${projectKey}/approval`)).toBe(403);
      expect(await apiStatus(request, kurin.youth.user, 'PUT', `${path}/third-probe/stages`, { openedOn: '2026-03-01' })).toBe(403);
    });
  });

  test('a впорядник writes their own notebook but never signs it', async ({ page, request }, testInfo) => {
    const kurin = await createProbeKurin(request, testInfo);
    const probeId = await thirdProbeId(request, kurin.mentor.user, kurin.mentor.memberKey);
    const projectKey = await addProject(request, kurin.mentor.user, kurin.mentor.memberKey, probeId, 'Ватра для новаків');

    await signInAs(page, request, kurin.mentor.user);
    await openThirdProbe(page, kurin.mentor.memberKey, probeId);

    const card = projectCard(page, 'Ватра для новаків');
    await expect(card.getByRole('button', { name: 'Редагувати' })).toBeVisible();
    await expect(card.getByRole('button', { name: 'Дати дозвіл' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Змінити етапи' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Додати проєкт' })).toBeVisible();

    const path = `member/${kurin.mentor.memberKey}/probes/${probeId}`;
    expect(await apiStatus(request, kurin.mentor.user, 'PUT', `${path}/projects/${projectKey}/approval`)).toBe(403);
    expect(await apiStatus(request, kurin.mentor.user, 'PUT', `${path}/third-probe/stages`, { openedOn: '2026-03-01' })).toBe(403);
    expect((await getThirdProbe(request, kurin.mentor.user, kurin.mentor.memberKey, probeId)).projects[0].approvedAtUtc).toBeNull();
  });

  test('a впорядник of another гурток only reads the notebook', async ({ page, request }, testInfo) => {
    const kurin = await createProbeKurin(request, testInfo);
    const probeId = await thirdProbeId(request, kurin.youth.user, kurin.youth.memberKey);
    const projectKey = await addProject(request, kurin.youth.user, kurin.youth.memberKey, probeId, 'Таборова газета');
    const otherGroup = await createGroupViaApi(request, kurin.founder, kurin.kurinKey, `Інший гурток ${kurin.suffix}`);
    const reader = await addPerson(request, kurin.founder, { kurinKey: kurin.kurinKey }, kurin.suffix, 'Сусідній', 'reader', 'mentor');
    await assignMentorViaApi(request, kurin.founder, otherGroup.groupKey, reader.userKey);

    await signInAs(page, request, reader.user);
    await openThirdProbe(page, kurin.youth.memberKey, probeId);

    const card = projectCard(page, 'Таборова газета');
    await expect(card).toContainText('Олена Коваль');
    for (const name of ['Редагувати', 'Видалити', 'Дати дозвіл']) {
      await expect(card.getByRole('button', { name })).toHaveCount(0);
    }
    await expect(page.getByRole('button', { name: 'Додати проєкт' })).toHaveCount(0);
    await expect(page.getByTestId('third-probe-mentors').getByRole('button', { name: 'Редагувати' })).toHaveCount(0);
    await expect(page.getByTestId('third-probe-camp').locator('input[type="file"]')).toHaveCount(0);

    const path = `member/${kurin.youth.memberKey}/probes/${probeId}`;
    expect(await apiStatus(request, reader.user, 'PUT', `${path}/projects/${projectKey}`, projectBody('Чужа правка', ''))).toBe(403);
    expect(await apiStatus(request, reader.user, 'PUT', `${path}/projects/${projectKey}/approval`)).toBe(403);
  });
});

function projectEditor(page: Page): Locator {
  return page.getByRole('dialog').filter({ has: page.getByLabel('Назва проєкту') });
}

/** One of the four rich texts of the project editor, by its caption. */
function textField(editor: Locator, label: string): Locator {
  return editor.locator('.project-field').filter({ has: editor.page().getByText(label, { exact: true }) });
}

function projectCard(page: Page, title: string): Locator {
  return page.getByTestId('probe-project').filter({ has: page.getByRole('heading', { name: title, exact: true }) });
}
