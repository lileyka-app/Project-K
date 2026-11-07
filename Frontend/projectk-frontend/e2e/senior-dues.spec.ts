import { expect, Locator, Page, test } from '@playwright/test';

import { loginThroughUi } from './support/login';
import { scenarioSuffix } from './support/scenarios';
import {
  accountOf,
  addSeniorMember,
  addSeniorMemberWithAccount,
  createSeniorKurin,
  createTag,
  getSeniorDues,
  getSeniorDuesStatus,
  moneyText,
  rejoinAsStaff,
  SeniorKurin,
  setMemberTags,
  setSeniorRate
} from './support/senior-dues';
import { dialog } from './support/ui';

// DUES-06: a kurin without гуртки keeps each person's вкладка in its own box, priced by tag.
// Every test opens a УСП kurin of its own, so nothing seeded is touched.

const PASSWORD = 'Senior@12345';
const ROMAN = ['I', 'II', 'III', 'IV'];

interface Quarter { year: number; number: number }
const quarterLabel = (q: Quarter) => `${ROMAN[q.number - 1]} кв. ${q.year}`;
const plusQuarters = (q: Quarter, n: number): Quarter => {
  const i = q.year * 4 + q.number - 1 + n;
  return { year: Math.floor(i / 4), number: (i % 4) + 1 };
};

async function choose(page: Page, scope: Locator, inputId: string, option: string): Promise<void> {
  await scope.locator(`#${inputId}`).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

function registerRow(page: Page, lastName: string): Locator {
  return page.locator('tr.dues-row--account').filter({ hasText: lastName });
}

function rateCell(row: Locator): Locator {
  return row.locator('td[data-mobile-label="Ставка"]');
}

async function openKurinDues(page: Page, kurin: SeniorKurin): Promise<void> {
  await page.goto(`/kurin/${kurin.kurinKey}/dues`);
  await expect(page.getByRole('heading', { name: 'Реєстр' })).toBeVisible({ timeout: 15_000 });
}

/** «Ставки куреня»: one rate for one tag (or for everyone), from the running quarter, two статті. */
async function setRateInDialog(
  page: Page,
  tag: string | null,
  upper: { recipient: 'Станиці' | 'Краю'; amount: number },
  kurinShare: number
): Promise<void> {
  await page.getByRole('button', { name: 'Ставки куреня' }).click();
  const panel = dialog(page);
  await expect(panel).toBeVisible();
  if (tag) {
    await choose(page, panel, 'kurin-rate-tag', tag);
  }
  await choose(page, panel, 'rate-article-recipient-0', upper.recipient);
  await panel.locator('#rate-article-amount-0').fill(String(upper.amount));
  await panel.locator('#rate-article-amount-1').fill(String(kurinShare));
  await expect(panel.getByTestId('rate-total')).toContainText(moneyText(upper.amount + kurinShare));
  await panel.getByRole('button', { name: 'Зберегти' }).click();
  await expect(panel).toBeHidden();
}

async function recordEntry(page: Page, kind: string, fill: (panel: Locator) => Promise<void>): Promise<void> {
  await page.getByRole('button', { name: 'Записати операцію' }).click();
  const panel = dialog(page);
  await expect(panel).toBeVisible();
  await choose(page, panel, 'dues-kind', kind);
  await fill(panel);
  await panel.getByRole('button', { name: 'Записати' }).click();
  await expect(panel).toBeHidden();
}

test.describe('Senior kurin dues (DUES-06)', () => {
  test('the treasurer prices by tag, takes a year ahead, filters the register and hands money up', async ({ page, request }, testInfo) => {
    test.setTimeout(150_000);
    const suffix = scenarioSuffix(testInfo);

    const kurin = await createSeniorKurin(request, suffix, PASSWORD);
    const usp = await createTag(request, kurin, 'УСП', '#2F855A');
    await createTag(request, kurin, 'УПС', '#2B6CB0');
    const andrii = await addSeniorMember(request, kurin, suffix, 'Andrii', 'Шевчук');
    const bohdana = await addSeniorMember(request, kurin, suffix, 'Bohdana', 'Бойко');
    const vira = await addSeniorMember(request, kurin, suffix, 'Vira', 'Лисенко');
    const hlib = await addSeniorMember(request, kurin, suffix, 'Hlib', 'Гнатів');
    await setMemberTags(request, kurin, andrii.memberKey, [usp]);
    await rejoinAsStaff(request, kurin, hlib.memberKey);

    await loginThroughUi(page, kurin.founder);
    await openKurinDues(page, kurin);
    await expect(page.getByText('Ставок ще немає')).toBeVisible();

    await test.step('rates for everyone and for УСП, from the running quarter', async () => {
      await setRateInDialog(page, null, { recipient: 'Станиці', amount: 100 }, 200);
      await setRateInDialog(page, 'УСП', { recipient: 'Краю', amount: 300 }, 150);

      const rates = page.locator('.dues-rates');
      await expect(rates).toContainText(/УСП\s+450\s₴/);
      await expect(rates).toContainText(/Усім іншим\s+300\s₴/);
      await expect(rateCell(registerRow(page, 'Шевчук'))).toContainText(moneyText(450));
      await expect(rateCell(registerRow(page, 'Бойко'))).toContainText(moneyText(300));
    });

    await test.step('the провід is charged like everyone', async () => {
      await expect(rateCell(registerRow(page, 'Гнатів'))).toContainText(moneyText(300));
      const dues = await getSeniorDues(request, kurin.founder, kurin.kurinKey);
      expect(accountOf(dues, hlib.memberKey).quarters.map(q => q.charged.total)).toEqual([300]);
    });

    await test.step('a tag put on after the quarter was charged reprices it while unpaid', async () => {
      await setMemberTags(request, kurin, bohdana.memberKey, [usp]);
      await page.reload();
      await expect(rateCell(registerRow(page, 'Бойко'))).toContainText(moneyText(450));
      await expect(registerRow(page, 'Бойко').locator('td.dues-total-col')).toContainText(moneyText(-450));
    });

    const dues = await getSeniorDues(request, kurin.founder, kurin.kurinKey);
    const current = dues.currentQuarter;

    await test.step('«Рік» takes the debt and four quarters ahead; the register says until when', async () => {
      await recordEntry(page, 'Вкладка', async panel => {
        await choose(page, panel, 'dues-person', `${andrii.firstName} ${andrii.lastName}`);
        await panel.getByRole('button', { name: /^Рік/ }).click();
        await expect(panel.locator('#dues-amount')).toHaveValue('2250');
      });

      const row = registerRow(page, 'Шевчук');
      await expect(row.locator('td.dues-total-col')).toContainText('+' + '1 800');
      await expect(row.locator('td[data-mobile-label="Сплачено до"]')).toHaveText(quarterLabel(plusQuarters(current, 4)));
      await expect(page.locator('.lil-stat').filter({ hasText: 'Сплачено наперед' })).toContainText(moneyText(1800));
    });

    await test.step('«Борг» settles exactly what is owed', async () => {
      await recordEntry(page, 'Вкладка', async panel => {
        await choose(page, panel, 'dues-person', `${vira.firstName} ${vira.lastName}`);
        await panel.getByRole('button', { name: /^Борг/ }).click();
        await expect(panel.locator('#dues-amount')).toHaveValue('300');
      });

      const row = registerRow(page, 'Лисенко');
      await expect(row.locator('td.dues-total-col')).toContainText('сплачено');
      await expect(row.locator('td[data-mobile-label="Сплачено до"]')).toHaveText(quarterLabel(current));
    });

    await test.step('the register filters by tag', async () => {
      // The combobox is the multiselect's hidden focus input; the box around it takes the click.
      await page.locator('.register-filter').click();
      await page.getByRole('option', { name: 'УСП' }).click();
      await page.keyboard.press('Escape');

      await expect(registerRow(page, 'Шевчук')).toBeVisible();
      await expect(registerRow(page, 'Бойко')).toBeVisible();
      await expect(registerRow(page, 'Лисенко')).toHaveCount(0);
      await expect(registerRow(page, 'Гнатів')).toHaveCount(0);
    });

    await test.step('the край and the станиця get their parts handed up', async () => {
      const krai = page.locator('.lil-stat').filter({ hasText: 'До передачі в край' });
      const stanytsia = page.locator('.lil-stat').filter({ hasText: 'До передачі в станицю' });
      await expect(krai.locator('.lil-stat__value')).toContainText(moneyText(300));
      await expect(stanytsia.locator('.lil-stat__value')).toContainText(moneyText(100));

      await recordEntry(page, 'Передача в край', async panel => {
        await panel.locator('#dues-amount').fill('300');
      });
      await recordEntry(page, 'Передача в станицю', async panel => {
        await panel.locator('#dues-amount').fill('100');
      });

      await expect(krai.locator('.lil-stat__value')).toContainText(moneyText(0));
      await expect(krai.locator('.lil-stat__sub')).toContainText(moneyText(300));
      await expect(stanytsia.locator('.lil-stat__value')).toContainText(moneyText(0));
      await expect(stanytsia.locator('.lil-stat__sub')).toContainText(moneyText(100));

      const after = await getSeniorDues(request, kurin.founder, kurin.kurinKey);
      expect({ krai: after.sentToKrai, stanytsia: after.sentToStanytsia }).toEqual({ krai: 300, stanytsia: 100 });
    });
  });

  test('a senior sees the rate of their quarter, and not the treasurer\'s register', async ({ page, request }, testInfo) => {
    test.setTimeout(120_000);
    const suffix = scenarioSuffix(testInfo);

    const kurin = await createSeniorKurin(request, suffix, PASSWORD);
    const usp = await createTag(request, kurin, 'УСП');
    const darka = await addSeniorMemberWithAccount(request, kurin, suffix, 'Daryna', 'Данилюк', PASSWORD);
    const before = await getSeniorDues(request, kurin.founder, kurin.kurinKey);
    await setSeniorRate(request, kurin, before.currentQuarter, [{ name: 'Курінна', recipient: 'Kurin', amount: 200 }]);
    await setSeniorRate(request, kurin, before.currentQuarter, [
      { name: 'Вкладка УСП', recipient: 'Krai', amount: 300 },
      { name: 'Курінна', recipient: 'Kurin', amount: 150 }
    ], usp);
    await setMemberTags(request, kurin, darka.member.memberKey, [usp]);

    await loginThroughUi(page, darka.user);

    await test.step('«Моя вкладка» on the home page names the quarter\'s rate by their tag', async () => {
      await page.goto('/');
      const tile = page.locator('.dues-tile');
      await expect(tile).toBeVisible({ timeout: 15_000 });
      await expect(tile.locator('.dues-tile__meta')).toContainText(/450\s₴ за квартал/);
      await expect(tile.locator('.dues-tile__amount')).toContainText(moneyText(-450));
    });

    await test.step('the kurin\'s register stays closed to someone without the treasurer\'s rights', async () => {
      await page.goto(`/kurin/${kurin.kurinKey}/dues`);
      await expect(page).toHaveURL(/\/forbidden/);
      await expect(page.getByRole('heading', { name: 'Реєстр' })).toHaveCount(0);
      expect(await getSeniorDuesStatus(request, darka.user, kurin.kurinKey)).toBe(403);
    });
  });
});
