import { BrowserContextOptions, expect, Page, test } from '@playwright/test';

import { loginThroughUi } from './support/login';
import { scenarioSuffix } from './support/scenarios';
import { createSeniorKurin } from './support/senior-dues';
import {
  askFor,
  askRaw,
  asStaffIn,
  currentKurins,
  freshKurin,
  groupIn,
  leaveOwingPastQuarters,
  noticeTitles,
  personIn,
  seatVykhovnyk,
  seniorKurin,
  withdraw
} from './support/transfers';
import { E2eUser } from './support/test-users';

/**
 * «Переходи» (1.4), a mode of the kurin's «Реєстр»: one kurin asks another for a person, and the
 * person's kurin answers. Every test opens its own two kurins and its own person, so nobody seeded is
 * ever moved.
 */

const requestRow = (page: Page, name: string) => page.getByRole('row').filter({ hasText: name });
// The page keeps more than one confirm dialog mounted; the open one is the one with the question.
const confirmation = (page: Page, question: string) => page.getByRole('alertdialog').filter({ hasText: question });

// Each test opens two kurins and people over the API before it signs anyone in.
test.describe.configure({ timeout: 90_000 });

// The first page after signing in loads its lazy chunk and a handful of reads; on a busy machine that
// outlasts the default 7.5 s.
const firstPaint = 20_000;

const modeTab = (page: Page, name: RegExp) =>
  page.getByRole('navigation', { name: 'Режими реєстру' }).getByRole('link', { name });

/** Into the «Реєстр», then its «Переходи» tab, as a person would from the menu. */
async function openTransfersAs(page: Page, user: E2eUser): Promise<void> {
  await loginThroughUi(page, user);
  await page.goto('/kurin/registry');
  await expect(page.getByRole('heading', { name: 'Реєстр' })).toBeVisible({ timeout: firstPaint });
  await modeTab(page, /^Переходи/).click();
  await expect(page).toHaveURL(/\/kurin\/registry\/transfers/);
  await expect(modeTab(page, /^Переходи/)).toHaveAttribute('aria-current', 'page');
}

test.describe('Moving people between kurins', () => {
  test('a kurin asks for a person by code, the person’s kurin approves, both sides are told', async ({ page, request, browser }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'home');
    const asking = await freshKurin(request, suffix, 'asking');
    const person = await personIn(request, home, suffix, 'code');

    await openTransfersAs(page, asking.zvyazkovyi);
    await page.getByLabel('Код людини').fill(person.publicId);
    await page.getByRole('button', { name: 'Знайти' }).click();
    await expect(page.getByText(person.fullName)).toBeVisible();
    await page.getByText('Прийняти ще й до нас', { exact: true }).click();
    await page.getByRole('button', { name: 'Надіслати запит' }).click();
    await expect(requestRow(page, person.fullName)).toContainText('Прийняти ще й до нас');

    await expect.poll(() => noticeTitles(request, home.zvyazkovyi)).toContain('Запит на перехід');

    // The person's kurin answers in its own browser.
    const answering = await browser.newContext(testInfo.project.use as BrowserContextOptions);
    try {
      const answerPage = await answering.newPage();
      await openTransfersAs(answerPage, home.zvyazkovyi);
      const row = requestRow(answerPage, person.fullName);
      await expect(row).toContainText('просить прийняти ще й до себе');
      await row.getByRole('button', { name: 'Підтвердити' }).click();
      const dialog = confirmation(answerPage, 'Прийняти ще й туди?');
      await expect(dialog).toBeVisible();
      await dialog.getByRole('button', { name: 'Підтвердити' }).click();
      await expect(row).toContainText('Підтверджено');
    } finally {
      await answering.close();
    }

    await expect.poll(() => noticeTitles(request, asking.zvyazkovyi)).toContain('Запит підтверджено');
    await expect.poll(() => noticeTitles(request, person.user)).toContain('Тебе прийнято ще й до куреня');
    expect(await currentKurins(request, home.zvyazkovyi, person)).toEqual([home.kurinKey, asking.kurinKey].sort());
  });

  test('the address in the person’s QR still opens «Переходи» and looks their code up', async ({ page, request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'qr');
    const asking = await freshKurin(request, suffix, 'scan');
    const person = await personIn(request, home, suffix, 'qr');

    await loginThroughUi(page, asking.zvyazkovyi);
    await page.goto(`/kurin/transfers?code=${person.publicId}`);

    await expect(page).toHaveURL(/\/kurin\/registry\/transfers\?code=/, { timeout: firstPaint });
    await expect(page.getByLabel('Код людини')).toHaveValue(person.publicId);
    await expect(page.getByText(person.fullName)).toBeVisible();
  });

  test('«перевести» moves the person after the confirm dialog, out of one kurin and into the other', async ({ page, request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'from');
    const asking = await freshKurin(request, suffix, 'to');
    const person = await personIn(request, home, suffix, 'move');
    await askFor(request, asking, person, 'Transfer');

    await openTransfersAs(page, home.zvyazkovyi);
    const row = requestRow(page, person.fullName);
    await expect(row).toContainText('просить перевести до себе');
    await row.getByRole('button', { name: 'Підтвердити' }).click();

    const dialog = confirmation(page, 'Перевести людину?');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(`${person.fullName}: з вашого куреня до`);
    await dialog.getByRole('button', { name: 'Перевести' }).click();
    await expect(row).toContainText('Підтверджено');

    expect(await currentKurins(request, asking.zvyazkovyi, person)).toEqual([asking.kurinKey]);
    await expect.poll(() => noticeTitles(request, person.user)).toContain('Тебе переведено');
  });

  test('someone who owes the kurin for past quarters cannot be let go', async ({ page, request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'owed');
    const asking = await freshKurin(request, suffix, 'wants');
    const person = await personIn(request, home, suffix, 'debtor', { groupKey: await groupIn(request, home, `Борг ${suffix}`) });
    await leaveOwingPastQuarters(request, home, person);
    await askFor(request, asking, person, 'Transfer');

    await openTransfersAs(page, home.zvyazkovyi);
    const row = requestRow(page, person.fullName);
    await expect(row).toContainText('Борг за минулі квартали');
    await expect(row.getByRole('button', { name: 'Підтвердити' })).toBeDisabled();
  });

  test('a senior kurin asking reads by its гілка, not as the УПЮ kurin of the same number', async ({ page, request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'youth');
    const person = await personIn(request, home, suffix, 'senior');
    const senior = await seniorKurin(request);
    const asked = await askFor(request, senior, person, 'AlsoJoin');

    try {
      await openTransfersAs(page, home.zvyazkovyi);
      const row = requestRow(page, person.fullName);
      await expect(row).toContainText(/7-й курінь УСП.* просить прийняти ще й до себе/);
      await expect(row).not.toContainText('к. ч. 7');
    } finally {
      await withdraw(request, senior, asked);
    }
  });
});

test.describe('Youth and senior kurins', () => {
  test('a senior kurin invites a впорядник of a youth kurin, only alongside, and the person answers themself', async ({ page, request, browser }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'staff');
    const person = await personIn(request, home, suffix, 'staff', { dateOfBirth: '1995-01-01' });
    await asStaffIn(request, home, person);
    const senior = await createSeniorKurin(request, suffix, 'User@12345');
    const asking = { kurinKey: senior.kurinKey, zvyazkovyi: senior.founder };

    await openTransfersAs(page, asking.zvyazkovyi);
    await page.getByLabel('Код людини').fill(person.publicId);
    await page.getByRole('button', { name: 'Знайти' }).click();
    await expect(page.getByText(person.fullName)).toBeVisible();
    await expect(page.getByText('Відповідає сама людина')).toBeVisible();
    await expect(page.getByText('Перевести до нас')).toHaveCount(0);
    await expect(page.getByText('У вашому курені як')).toHaveCount(0);
    await page.getByRole('button', { name: 'Надіслати запрошення' }).click();
    await expect(requestRow(page, person.fullName)).toContainText('Запрошення людині');

    await expect.poll(() => noticeTitles(request, person.user)).toContain('Запрошення до куреня');

    const own = await browser.newContext(testInfo.project.use as BrowserContextOptions);
    try {
      const personPage = await own.newPage();
      await loginThroughUi(personPage, person.user);
      await personPage.goto('/invitations');
      const invitation = personPage.getByTestId('invitation');
      await expect(invitation).toContainText('УСП', { timeout: firstPaint });
      await invitation.getByRole('button', { name: 'Прийняти' }).click();
      await expect(personPage.getByText('Запрошень немає')).toBeVisible();
    } finally {
      await own.close();
    }

    await expect.poll(() => noticeTitles(request, asking.zvyazkovyi)).toContain('Запит підтверджено');
    expect(await currentKurins(request, home.zvyazkovyi, person)).toEqual([home.kurinKey, senior.kurinKey].sort());
  });

  test('a senior kurin never takes a юнак from a youth kurin, and asks the youth kurin to take them in alongside', async ({ request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'junak');
    const person = await personIn(request, home, suffix, 'junak');
    const senior = await createSeniorKurin(request, suffix, 'User@12345');
    const asking = { kurinKey: senior.kurinKey, zvyazkovyi: senior.founder };

    expect(await askRaw(request, asking, person, 'Transfer')).toEqual({ status: 400, error: 'TransferAcrossLines' });
    await askFor(request, asking, person, 'AlsoJoin');
    await expect.poll(() => noticeTitles(request, home.zvyazkovyi)).toContain('Запит на перехід');
    expect(await noticeTitles(request, person.user)).not.toContain('Запрошення до куреня');
  });
});

test.describe('Who sees what', () => {
  test('a виховник reads the kurin settings but cannot save them', async ({ page, request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'kv');
    const vykhovnyk = await personIn(request, home, suffix, 'kv', { dateOfBirth: '1995-01-01' });
    await seatVykhovnyk(request, home, vykhovnyk);

    await loginThroughUi(page, vykhovnyk.user);
    await page.goto(`/kurin/${home.kurinKey}/settings`);
    await expect(page.getByText('Лише перегляд')).toBeVisible({ timeout: firstPaint });
    await expect(page.getByRole('button', { name: 'Зберегти' })).toHaveCount(0);
  });

  test('someone without the kurin’s management is refused «Переходи», by the old address too', async ({ page, request }, testInfo) => {
    const suffix = scenarioSuffix(testInfo);
    const home = await freshKurin(request, suffix, 'plain');
    const person = await personIn(request, home, suffix, 'plain');

    await loginThroughUi(page, person.user);
    await page.goto('/kurin/registry/transfers');
    await expect(page).toHaveURL(/\/forbidden/, { timeout: firstPaint });
    await page.goto('/kurin/transfers');
    await expect(page).toHaveURL(/\/forbidden/, { timeout: firstPaint });
    await expect(page.getByRole('button', { name: 'Надіслати запит' })).toHaveCount(0);
  });
});
