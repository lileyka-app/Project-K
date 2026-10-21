import { expect, test, type Page } from '@playwright/test';
import { kurin, ok, refuse, shot, signIn, type ExtraApi } from './support/mock-api';

/**
 * The kurin's box (the web's kurin-dues): its cash, the гуртки and their quarters, the transfers
 * waiting for «Отримано», the kurin's own operations and its rates. Fixtures are shaped like the
 * API's KurinDuesResponse.
 */

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();
const q = (year: number, number: number) => ({ year, number });

/** The курінний скарбник and the Звʼязковий have these; a youth has none. */
const TREASURER = ['KurinDues:Read:KurinWide', 'KurinDues:Create:KurinWide', 'KurinDues:Update:KurinWide'];
const ZVYAZKOVYI = { canKeep: true, canVerify: true, canSetRates: true };

const quarterRow = (groupKey: string, groupName: string, youthCount: number, collectedUp: number, stanytsiaCollected: number) => ({
  groupKey, groupName, youthCount, expectedUp: youthCount * 130, collectedUp, debtUp: youthCount * 130 - collectedUp,
  stanytsiaExpected: youthCount * 100, stanytsiaCollected, kraiExpected: 0, kraiCollected: 0,
});
const total = (rows: ReturnType<typeof quarterRow>[]) => ({
  groupKey: '00000000-0000-0000-0000-000000000000', groupName: 'Разом',
  youthCount: rows.reduce((s, r) => s + r.youthCount, 0),
  expectedUp: rows.reduce((s, r) => s + r.expectedUp, 0),
  collectedUp: rows.reduce((s, r) => s + r.collectedUp, 0),
  debtUp: rows.reduce((s, r) => s + r.debtUp, 0),
  stanytsiaExpected: rows.reduce((s, r) => s + r.stanytsiaExpected, 0),
  stanytsiaCollected: rows.reduce((s, r) => s + r.stanytsiaCollected, 0),
});
const quarter = (at: { year: number; number: number }, rows: ReturnType<typeof quarterRow>[]) => ({ quarter: at, groups: rows, total: total(rows) });

const entry = (duesEntryKey: string, kind: string, method: string, amount: number, occurredOn: string, extra: Record<string, unknown> = {}) => ({
  duesEntryKey, kind, method, counterMethod: null, amount, occurredOn, membershipKey: null, memberName: null,
  collectedByMemberKey: null, collectedByName: null, note: null, isVerified: false, verifiedAtUtc: null, verifiedByName: null,
  receivedAtUtc: null, createdAtUtc: ago(60 * 24 * 3), ...extra,
});

function kurinDues(viewer = ZVYAZKOVYI) {
  return {
    kurinKey: 'k1', currentQuarter: q(2026, 4),
    years: [{ startYear: 2026, label: '2026/27', quarters: [q(2026, 3), q(2026, 4), q(2027, 1), q(2027, 2)] }],
    rates: [{ fromQuarter: q(2026, 3), articles: [{ name: 'Станиця', recipient: 'Stanytsia', amount: 100, reducedAmount: 50 }, { name: 'Курінь', recipient: 'Kurin', amount: 30, reducedAmount: 30 }], stanytsiaFull: 100, stanytsiaReduced: 50, kurinShare: 30, kurinReduced: 30, kraiFull: 0, kraiReduced: 0 }],
    box: { cash: 520, card: 180, total: 700, toForward: 400, own: 300, inTransit: 130 },
    toStanytsia: 400, toKrai: 0, sentToStanytsia: 200, sentToKrai: 0,
    groups: [
      { groupKey: 'g1', groupName: 'Соколи', owedUp: 600, transferred: 400, received: 270, outstanding: 200, inTransit: 130 },
      { groupKey: 'g2', groupName: 'Вовки', owedUp: 260, transferred: 260, received: 260, outstanding: 0, inTransit: 0 },
    ],
    quarters: [
      quarter(q(2026, 3), [quarterRow('g1', 'Соколи', 4, 520, 400), quarterRow('g2', 'Вовки', 2, 260, 200)]),
      quarter(q(2026, 4), [quarterRow('g1', 'Соколи', 4, 130, 100), quarterRow('g2', 'Вовки', 2, 260, 200)]),
    ],
    transfers: [
      { duesEntryKey: 't1', groupKey: 'g1', groupName: 'Соколи', amount: 130, method: 'Cash', occurredOn: '2026-10-06',
        collectedByName: 'Марта Шевчук', note: 'За жовтень', isReceived: false, receivedAtUtc: null, receivedByName: null },
      { duesEntryKey: 't2', groupKey: 'g1', groupName: 'Соколи', amount: 270, method: 'Card', occurredOn: '2026-09-20',
        collectedByName: 'Марта Шевчук', note: null, isReceived: true, receivedAtUtc: ago(60 * 24 * 18), receivedByName: 'Олена Петренко' },
      { duesEntryKey: 't3', groupKey: 'g2', groupName: 'Вовки', amount: 260, method: 'Cash', occurredOn: '2026-09-15',
        collectedByName: null, note: null, isReceived: true, receivedAtUtc: ago(60 * 24 * 22), receivedByName: 'Олена Петренко' },
    ],
    entries: [
      entry('k2', 'Expense', 'Cash', 60, '2026-10-03', { note: 'Прапор куреня', collectedByMemberKey: 'm3', collectedByName: 'Олена Петренко' }),
      entry('k3', 'OtherIncome', 'Cash', 100, '2026-10-01', { note: 'Ярмарок' }),
      entry('k1', 'TransferToStanytsia', 'Card', 200, '2026-09-30', {
        isVerified: true, verifiedAtUtc: ago(60 * 24 * 8), verifiedByName: 'Олена Петренко',
      }),
    ],
    people: [{ memberKey: 'm1', fullName: 'Остап Коваль' }, { memberKey: 'm3', fullName: 'Олена Петренко' }],
    viewer,
  };
}

/** The kurin's box; `viewer` is what the server says the caller may do, `denied` a youth's 403. */
function kurinDuesApi(options: { viewer?: typeof ZVYAZKOVYI; denied?: boolean; rates?: unknown[]; extra?: Record<string, unknown> } = {}): ExtraApi {
  return ({ method, path }) => {
    if (method === 'GET' && path === 'kurin/k1/dues') return options.denied ? refuse(403) : ok({ ...kurinDues(options.viewer), ...(options.rates ? { rates: options.rates } : {}), ...options.extra });
    if (method === 'GET' && path === 'kurin/k1/dues/groups') return ok([]);
    if (method === 'GET' && path === 'group/g1/dues') return refuse(403);
    return null;
  };
}

async function choose(page: Page, name: string): Promise<void> {
  await page.locator('ion-action-sheet').getByRole('radio', { name, exact: true }).click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
}

function sheetButton(page: Page, name: string) {
  return page.locator('ion-action-sheet:not(.overlay-hidden)').last().getByRole('button', { name, exact: true });
}

async function openMore(page: Page): Promise<void> {
  await page.locator('#tab-button-more').click();
  await expect(page.locator('app-more').getByText('Вигляд')).toBeVisible();
}

const segment = (page: Page, value: string) => page.locator(`app-kurin-dues ion-segment-button[value=${value}]`);

// ── Reading ──────────────────────────────────────────────────────────────────────────────────

test('opens the kurin’s box from Меню and reads its cash, гуртки, transfers and operations', async ({ page }, info) => {
  const project = info.project.name;
  await signIn(page, undefined, kurinDuesApi(), TREASURER);
  await openMore(page);
  const item = page.locator('app-more').getByTestId('menu-kurin-dues');
  await expect(item).toContainText('Вкладка куреня');
  await item.click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/dues$/);

  const box = page.getByTestId('box');
  await expect(box).toContainText('700 ₴');
  await expect(box).toContainText('готівка 520 ₴ · картка 180 ₴');
  await expect(box).toContainText('передано 200 ₴');
  await expect(box).toContainText('ще в гуртках 200 ₴');
  await expect(page.getByTestId('subline')).toHaveText('зараз IV кв. 2026 · гуртків: 2 · чекають підтвердження: 1');
  await expect(page.getByTestId('rates')).toHaveText(
    'Станиця 100 ₴ (з пільгою 50 ₴) · Курінь 30 ₴ за квартал з кожного юнака. Гурткову частину кожен гурток задає собі.',
  );
  await expect(page.getByTestId('kurin-rate')).toBeVisible();
  await shot(page, project, 'kurin-dues');

  await segment(page, 'groups').click();
  const groups = page.getByTestId('group');
  await expect(groups).toHaveCount(2);
  await expect(groups.filter({ hasText: 'Соколи' })).toContainText('Зібрано вгору 600 ₴ · передано 400 ₴');
  await expect(groups.filter({ hasText: 'Соколи' })).toContainText('підтверджено 270 ₴ · в дорозі 130 ₴');
  await expect(groups.filter({ hasText: 'Вовки' })).toContainText('усе передано');
  // By quarter, the current one first.
  const rows = page.getByTestId('quarter-group');
  await expect(rows.filter({ hasText: 'Соколи' })).toContainText('Юнаків 4 · здали 130 ₴ з 520 ₴');
  await expect(rows.filter({ hasText: 'Соколи' })).toContainText('390 ₴');
  await expect(page.getByTestId('quarter-total')).toContainText('До станиці 300 ₴ з 600 ₴');
  await shot(page, project, 'kurin-dues-groups');
  await page.getByTestId('quarter-picker').click();
  await choose(page, 'III кв. 2026');
  await expect(rows.filter({ hasText: 'Соколи' })).toContainText('сплачено');

  // A гурток's row narrows the transfers to it, or opens its own box.
  await groups.filter({ hasText: 'Вовки' }).click();
  await sheetButton(page, 'Передачі цього гуртка').click();
  const transfers = page.getByTestId('transfer');
  await expect(transfers).toHaveCount(1);
  await expect(page.getByTestId('group-filter')).toContainText('Вовки');
  await page.getByTestId('group-filter').click();
  await expect(transfers).toHaveCount(3);
  await expect(transfers.first()).toContainText('в дорозі');
  await expect(transfers.first()).toContainText('06.10.2026 · Готівка · передав Марта Шевчук');
  await expect(transfers.first()).toContainText('+130 ₴');
  await expect(transfers.nth(1)).toContainText('підтвердив Олена Петренко');
  await page.getByTestId('pending-only').click();
  await expect(transfers).toHaveCount(1);
  await shot(page, project, 'kurin-dues-transfers');

  await segment(page, 'history').click();
  const entries = page.getByTestId('entry');
  await expect(entries).toHaveCount(3);
  await expect(entries.filter({ hasText: 'Прапор куреня' })).toContainText('−60 ₴');
  await expect(entries.filter({ hasText: 'Ярмарок' })).toContainText('+100 ₴');
  await expect(entries.filter({ hasText: 'Передача в станицю' })).toContainText('перевірив Олена Петренко');
  await shot(page, project, 'kurin-dues-history');
  await page.getByTestId('history-kind').click();
  await choose(page, 'Витрата');
  await expect(entries).toHaveCount(1);
  await page.getByTestId('history-kind').click();
  await choose(page, 'Усі види');
  await page.getByTestId('history-quarter').click();
  await choose(page, 'III кв. 2026');
  await expect(entries).toHaveCount(1);

  await segment(page, 'groups').click();
  await groups.filter({ hasText: 'Соколи' }).click();
  await sheetButton(page, 'Вкладка гуртка').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/group\/g1\/dues$/);
});

// ── Writing ──────────────────────────────────────────────────────────────────────────────────

test('confirms a transfer, records and checks operations and sets the kurin’s rates', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, undefined, kurinDuesApi(), TREASURER);
  await page.goto('./tabs/kurin/dues');
  await expect(page.getByTestId('box')).toBeVisible();

  // The shortcut from the box opens the transfers still on the way.
  await page.getByTestId('pending').click();
  const transfers = page.getByTestId('transfer');
  await expect(transfers).toHaveCount(1);
  await transfers.first().click();
  await shot(page, project, 'kurin-dues-transfer-actions');
  await sheetButton(page, 'Отримано').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'kurin/k1/dues/transfers/t1/received')?.body).toEqual({ isReceived: true });

  await page.getByTestId('pending-only').click();
  await transfers.filter({ hasText: 'Вовки' }).click();
  await sheetButton(page, 'Зняти підтвердження').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'kurin/k1/dues/transfers/t3/received')?.body).toEqual({ isReceived: false });

  // «Записати операцію» offers the kurin’s kinds and starts on a витрата, with no person to pick.
  await page.getByRole('button', { name: 'Записати операцію' }).click();
  const form = page.locator('app-dues-entry-form');
  await expect(form.getByText('Нова операція')).toBeVisible();
  await expect(form.getByTestId('entry-person')).toHaveCount(0);
  await form.getByTestId('entry-kind').click();
  await expect(page.locator('ion-action-sheet').getByRole('radio')).toHaveText([
    'Витрата', 'Інший прихід', 'Передача в станицю', 'Обмін готівка ↔ картка',
  ]);
  await choose(page, 'Передача в станицю');
  await form.getByTestId('entry-amount').locator('input').fill('400');
  await shot(page, project, 'kurin-dues-entry');
  await form.getByTestId('entry-save').click();
  await expect(form).toHaveCount(0);
  const created = log.writes.find((w) => w.method === 'POST' && w.path === 'kurin/k1/dues/entries')?.body;
  expect(created).toMatchObject({ kind: 'TransferToStanytsia', method: 'Cash', counterMethod: null, amount: 400, membershipKey: null });

  // «Перевірено», changing and deleting go through a sheet on the row.
  await segment(page, 'history').click();
  await page.getByTestId('entry').filter({ hasText: 'Ярмарок' }).click();
  await sheetButton(page, 'Позначити перевіреною').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'kurin/k1/dues/entries/k3/verified')?.body).toEqual({ isVerified: true });

  await page.getByTestId('entry').filter({ hasText: 'Прапор куреня' }).click();
  await sheetButton(page, 'Змінити').click();
  await expect(form.getByText('Операція', { exact: true })).toBeVisible();
  await form.getByTestId('entry-amount').locator('input').fill('65');
  await form.getByTestId('entry-save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'PUT' && w.path === 'kurin/k1/dues/entries/k2')?.body).toMatchObject({
    kind: 'Expense', amount: 65, collectedByMemberKey: 'm3', note: 'Прапор куреня',
  });

  await page.getByTestId('entry').filter({ hasText: 'Прапор куреня' }).click();
  await sheetButton(page, 'Видалити').click();
  await expect(page.getByRole('dialog', { name: /Видалити операцію/ })).toContainText('Витрата на 60 ₴ зникне з каси.');
  await sheetButton(page, 'Видалити').click();
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'kurin/k1/dues/entries/k2')).toBe(true);

  // A checked operation is not changed any more: only the mark comes off.
  await page.getByTestId('entry').filter({ hasText: 'Передача в станицю' }).click();
  await expect(sheetButton(page, 'Зняти позначку «Перевірено»')).toBeVisible();
  await expect(sheetButton(page, 'Змінити')).toHaveCount(0);
  await sheetButton(page, 'Скасувати').click();

  // «Ставки куреня» starts from the rates in force and checks the пільгова against the full.
  await segment(page, 'box').click();
  await page.getByTestId('kurin-rate').click();
  const rate = page.locator('app-dues-kurin-rate-form');
  await expect(rate.getByTestId('rate-full').locator('input')).toHaveValue('100');
  await expect(rate.getByTestId('rate-reduced').locator('input')).toHaveValue('50');
  await expect(rate.getByTestId('rate-kurin').locator('input')).toHaveValue('30');
  await rate.getByTestId('rate-reduced').locator('input').fill('150');
  await expect(rate.getByText('Пільгова не може бути вищою за повну.')).toBeVisible();
  await expect(rate.getByTestId('kurin-rate-save')).toHaveAttribute('disabled', '');
  await rate.getByTestId('rate-full').locator('input').fill('160');
  await rate.getByTestId('rate-kurin').locator('input').fill('40');
  // Typing scrolls the sheet to the field; the picture shows it whole.
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await rate.locator('ion-content').evaluate((content: HTMLIonContentElement) => content.scrollToTop(0));
  await shot(page, project, 'kurin-dues-rates');
  await rate.getByTestId('kurin-rate-save').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'kurin/k1/dues/rate')?.body).toEqual({
    fromQuarter: { year: 2026, number: 4 },
    stanytsiaFull: 160,
    stanytsiaReduced: 150,
    kurinShare: 40,
  });
});

test('sends a kurin with its own статті to the web for its rates', async ({ page }) => {
  const own = {
    fromQuarter: q(2026, 3),
    articles: [
      { name: 'Станиця', recipient: 'Stanytsia', amount: 100, reducedAmount: 50 },
      { name: 'Крайова вкладка', recipient: 'Krai', amount: 60, reducedAmount: 30 },
      { name: 'Табірний фонд', recipient: 'Kurin', amount: 50, reducedAmount: 25 },
    ],
    stanytsiaFull: 100, stanytsiaReduced: 50, kurinShare: 50, kurinReduced: 25, kraiFull: 60, kraiReduced: 30,
  };
  await signIn(page, undefined, kurinDuesApi({ rates: [own] }), TREASURER);
  await page.goto('./tabs/kurin/dues');
  await expect(page.getByTestId('rates')).toContainText('Станиця 100 ₴ (з пільгою 50 ₴) · Крайова вкладка 60 ₴ (з пільгою 30 ₴) · Табірний фонд 50 ₴ (з пільгою 25 ₴)');
  await expect(page.getByTestId('to-krai')).toContainText('До передачі в край');
  await expect(page.getByTestId('kurin-rate')).toHaveCount(0);
  await expect(page.getByTestId('kurin-rate-web')).toContainText('їх змінюють у вебі');
});

test('in a край paid straight, shows no станиця and offers only the transfer to the край', async ({ page }) => {
  const straight = {
    fromQuarter: q(2026, 3),
    articles: [
      { name: 'Край', recipient: 'Krai', amount: 600, reducedAmount: 300 },
      { name: 'Курінь', recipient: 'Kurin', amount: 100, reducedAmount: 100 },
    ],
    stanytsiaFull: 0, stanytsiaReduced: 0, kurinShare: 100, kurinReduced: 100, kraiFull: 600, kraiReduced: 300,
  };
  await signIn(page, undefined, kurinDuesApi({ rates: [straight], extra: { upstream: 'krai', toStanytsia: 0, sentToStanytsia: 0, toKrai: 400 } }), TREASURER);
  await page.goto('./tabs/kurin/dues');
  await expect(page.getByTestId('to-krai')).toContainText('До передачі в край');
  await expect(page.getByTestId('to-stanytsia')).toHaveCount(0);
  await expect(page.getByTestId('kurin-rate')).toBeVisible();
  await page.getByRole('button', { name: 'Записати операцію' }).click();
  await page.locator('app-dues-entry-form').getByTestId('entry-kind').click();
  await expect(page.locator('ion-action-sheet').getByRole('radio')).toHaveText(['Витрата', 'Інший прихід', 'Передача в край', 'Обмін готівка ↔ картка']);
});

test('shows the box read-only to whoever the server lets read but not keep', async ({ page }) => {
  await signIn(page, undefined, kurinDuesApi({ viewer: { canKeep: false, canVerify: false, canSetRates: false } }), ['KurinDues:Read:KurinWide']);
  await page.goto('./tabs/kurin/dues');
  await expect(page.getByTestId('box')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Записати операцію' })).toHaveCount(0);
  await expect(page.getByTestId('kurin-rate')).toHaveCount(0);
  await segment(page, 'transfers').click();
  await page.getByTestId('transfer').first().click();
  await segment(page, 'history').click();
  await page.getByTestId('entry').first().click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
});

// ── Who may ──────────────────────────────────────────────────────────────────────────────────

test('opens the transfers from «Справи» on Home', async ({ page }) => {
  const box = kurinDuesApi();
  const duty = { kind: 'TransfersToConfirm', kurin, count: 1, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null };
  await signIn(page, undefined, (request) => (request.method === 'GET' && request.path === 'me/duties' ? ok([duty]) : box(request)), TREASURER);
  await page.getByTestId('duty').filter({ hasText: 'Передачі від гуртків підтвердити' }).click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/dues\?part=transfers$/);
  await expect(page.getByTestId('transfer')).toHaveCount(3);
});

test('keeps the kurin’s box from a youth, as the web does', async ({ page }) => {
  await signIn(page, undefined, kurinDuesApi({ denied: true }));
  await openMore(page);
  await expect(page.locator('app-more').getByTestId('menu-kurin-dues')).toHaveCount(0);
  await page.goto('./tabs/kurin/dues');
  await expect(page.getByText('Немає доступу до вкладки куреня.')).toBeVisible();
  await expect(page.getByTestId('box')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Записати операцію' })).toHaveCount(0);
});
