import { expect, test, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';

/**
 * The kurin's own «Редагувати» (web kurin-panel's menu of that name): the Звʼязковий changes the
 * kurin's record and exports its PDF report, shared from the phone or downloaded; nobody else
 * sees the button.
 */

const PDF = Buffer.from('%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n');

interface Seen {
  reportQuery: URLSearchParams | null;
}

function kurinApi(seen: Seen, options: { reportFails?: boolean } = {}): ExtraApi {
  const kurin = {
    kurinKey: 'k1', number: 7, branch: 'UPYu', stanytsia: 'Львів', regionOrCountry: null,
    namedAfter: 'Івана Франка', description: 'Курінь юнаків.', currentUserCount: 12, profileVerificationEnabled: true,
  };
  return ({ method, path, body, query }): Answer | null => {
    if (method === 'GET') {
      switch (path) {
        case 'kurin/k1': return ok(kurin);
        case 'group/groups': return ok([]);
        case 'member/kurins/k1/members':
        case 'member/members/kv/k1':
        case 'group/groups/k1/mentor-assignments':
        case 'kurin/k1/dues/groups':
          return ok([]);
        case 'kurin/k1/report/pdf':
          seen.reportQuery = query;
          return options.reportFails ? refuse(500) : { status: 200, file: PDF, contentType: 'application/pdf' };
      }
      return null;
    }
    if (method === 'PUT' && path === 'kurin/k1') {
      Object.assign(kurin, body);
      return ok(kurin);
    }
    return null;
  };
}

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide'];

const openKurinTab = async (page: Page) => {
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('kurin-head')).toContainText('7 курінь');
};

/** A button of the action sheet that is open now (a dismissed one can linger while it animates out). */
const sheetButton = (page: Page, name: string) =>
  page.locator('ion-action-sheet:not(.overlay-hidden)').last().getByRole('button', { name, exact: true });

const editSheet = (page: Page) => page.locator('ion-modal app-kurin-edit-sheet');

test('lets the Звʼязковий change the kurin’s record', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, kurinApi({ reportQuery: null }), ZVYAZKOVYI);
  await openKurinTab(page);
  await shot(page, project, 'kurin-edit-button');

  await page.getByTestId('edit-kurin').click();
  await expect(sheetButton(page, 'Експорт звіту куреня')).toBeVisible();
  await sheetButton(page, 'Редагування даних куреня').click();
  const sheet = editSheet(page);
  await expect(sheet.locator('[data-testid=kurin-namedAfter] input')).toHaveValue('Івана Франка');
  await expect(sheet.locator('[data-testid=kurin-stanytsia] input')).toHaveValue('Львів');
  await expect(sheet.locator('[data-testid=kurin-regionOrCountry] input')).toHaveValue('');

  // Nothing changed: the sheet closes without a write (the API refuses an empty update).
  await sheet.getByTestId('save-kurin').click();
  await expect(sheet).toHaveCount(0);
  expect(log.writes.filter((w) => w.path === 'kurin/k1')).toHaveLength(0);

  await page.getByTestId('edit-kurin').click();
  await sheetButton(page, 'Редагування даних куреня').click();
  await sheet.locator('[data-testid=kurin-stanytsia] input').fill('  Київ ');
  await sheet.locator('[data-testid=kurin-regionOrCountry] input').fill('Київщина');
  await sheet.locator('[data-testid=kurin-description] textarea').fill('Курінь юнаків і юначок столиці.');
  await sheet.evaluate(() => {
    let focused = document.activeElement;
    while (focused?.shadowRoot?.activeElement) focused = focused.shadowRoot.activeElement;
    (focused as HTMLElement | null)?.blur();
  });
  // Leave the description and, once the scroll that kept it in view has run out, show the sheet from its top.
  await page.waitForTimeout(1000);
  // A headless WebKit never says the keyboard went away, so Ionic keeps its room under the fields.
  await sheet.locator('ion-content').evaluate((content: HTMLIonContentElement) => {
    content.style.setProperty('--keyboard-offset', '0px');
    return content.scrollToTop(0);
  });
  await shot(page, project, 'kurin-edit');
  await sheet.getByTestId('save-kurin').click();
  await expect(sheet).toHaveCount(0);

  // The whole record goes back, as the web sends it.
  expect(log.writes.find((w) => w.method === 'PUT' && w.path === 'kurin/k1')?.body).toEqual({
    kurinKey: 'k1', number: 7, branch: 'UPYu', stanytsia: 'Київ', regionOrCountry: 'Київщина', namedAfter: 'Івана Франка',
    description: 'Курінь юнаків і юначок столиці.', currentUserCount: 12, profileVerificationEnabled: true,
  });
  const head = page.getByTestId('kurin-head');
  await expect(head).toContainText('Київ');
  await expect(head).toContainText('Київщина');
  await expect(head).toContainText('Курінь юнаків і юначок столиці.');
});

test('shares the kurin’s PDF report from a phone that shares files', async ({ page }, info) => {
  const project = info.project.name;
  // The system share sheet, recorded: headless browsers have none.
  await page.addInitScript(() => {
    const shared: { name: string; type: string; size: number }[] = [];
    (window as unknown as { shared: typeof shared }).shared = shared;
    Object.defineProperty(navigator, 'canShare', { configurable: true, value: (data: ShareData) => !!data.files?.length });
    Object.defineProperty(navigator, 'share', {
      configurable: true,
      value: (data: ShareData): Promise<void> => {
        if (navigator.userActivation && !navigator.userActivation.isActive) {
          return Promise.reject(new DOMException('No tap', 'NotAllowedError'));
        }
        for (const file of data.files ?? []) shared.push({ name: file.name, type: file.type, size: file.size });
        return Promise.resolve();
      },
    });
  });
  const seen: Seen = { reportQuery: null };
  await signIn(page, member, kurinApi(seen), ZVYAZKOVYI);
  await openKurinTab(page);

  await page.getByTestId('edit-kurin').click();
  await sheetButton(page, 'Експорт звіту куреня').click();
  await expect(sheetButton(page, 'Поділитися…')).toBeVisible();
  await expect(page.getByRole('dialog', { name: /Звіт куреня готовий/ })).toBeVisible();
  await shot(page, project, 'kurin-report-ready');
  // The reader's zone goes along, as the web sends it.
  expect(seen.reportQuery?.get('timeZone')).toBeTruthy();

  await sheetButton(page, 'Поділитися…').click();
  await expect
    .poll(() => page.evaluate(() => (window as unknown as { shared: unknown[] }).shared))
    .toEqual([{ name: expect.stringMatching(/^kurin-7-report-\d{8}-\d{4}\.pdf$/), type: 'application/pdf', size: PDF.length }]);
});

test('downloads the report where the browser does not share files', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'share', { configurable: true, value: undefined });
    Object.defineProperty(navigator, 'canShare', { configurable: true, value: undefined });
  });
  await signIn(page, member, kurinApi({ reportQuery: null }), ZVYAZKOVYI);
  await openKurinTab(page);

  const download = page.waitForEvent('download');
  await page.getByTestId('edit-kurin').click();
  await sheetButton(page, 'Експорт звіту куреня').click();
  expect((await download).suggestedFilename()).toMatch(/^kurin-7-report-\d{8}-\d{4}\.pdf$/);
  await expect(page.getByTestId('edit-kurin')).toBeEnabled();
});

test('says when the report could not be made', async ({ page }) => {
  await signIn(page, member, kurinApi({ reportQuery: null }, { reportFails: true }), ZVYAZKOVYI);
  await openKurinTab(page);
  await page.getByTestId('edit-kurin').click();
  await sheetButton(page, 'Експорт звіту куреня').click();
  await expect(page.locator('ion-toast')).toContainText('Не вдалося підготувати звіт. Спробуй ще раз.');
  await expect(page.getByTestId('edit-kurin')).toBeEnabled();
});

test('shows no «Редагувати» on the kurin to a Виховник or a youth', async ({ page }) => {
  await signIn(page, member, kurinApi({ reportQuery: null }), ['Group:Update:OwnGroups']);
  await openKurinTab(page);
  await expect(page.getByTestId('kurin-head')).toContainText('Львів');
  await expect(page.getByTestId('edit-kurin')).toHaveCount(0);
});
