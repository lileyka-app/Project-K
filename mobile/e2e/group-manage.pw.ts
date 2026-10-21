import { expect, test, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';
import { png, pngDataUrl } from './support/png';

/**
 * Making and changing гуртки on the phone (web kurin-panel's manage-panel and group-panel's
 * «Редагувати»): the Звʼязковий creates, renames and deletes; a Виховник changes the description
 * and silhouette of a гурток the server lets them; a youth changes nothing.
 */

interface Group {
  groupKey: string;
  kurinKey: string;
  name: string;
  description: string | null;
  silhouetteUrl: string | null;
  kurinNumber: number;
}

const kurin = { kurinKey: 'k1', number: 7, branch: 'UPYu', namedAfter: 'Івана Франка', description: null };

/** The гуртки as the server keeps them between calls; `canUpdate` is its check-access answer for a гурток. */
function groupApi(canUpdate: boolean): ExtraApi {
  const groups: Group[] = [
    { groupKey: 'g1', kurinKey: 'k1', name: 'Соколи', description: 'Гурток, що любить мандрівки.', silhouetteUrl: pngDataUrl(64, 64), kurinNumber: 7 },
    { groupKey: 'g2', kurinKey: 'k1', name: 'Орли', description: null, silhouetteUrl: null, kurinNumber: 7 },
  ];
  const find = (key: string) => groups.find((g) => g.groupKey === key);
  const read = (path: string, one: RegExpExecArray | null): Answer | null => {
    switch (path) {
      case 'kurin/k1': return ok(kurin);
      case 'group/groups': return ok(groups);
      case 'member/kurins/k1/members':
      case 'member/members/kv/k1':
      case 'group/groups/k1/mentor-assignments':
      case 'kurin/k1/dues/groups':
        return ok([]);
    }
    if (/^member\/groups\/g\d\/members$/.test(path)) return ok([]);
    if (one && !one[2]) return find(one[1]) ? ok(find(one[1])) : refuse(404);
    return null;
  };
  return ({ method, path, body }): Answer | null => {
    const one = /^group\/(g\d)(\/silhouette)?$/.exec(path);
    if (method === 'GET') return read(path, one);
    if (path === 'auth/check-access') return ok(body?.entityType === 'group' && canUpdate);
    if (path === 'group' && method === 'POST') {
      const created = { groupKey: `g${groups.length + 1}`, kurinKey: body.kurinKey, name: body.name, description: body.description, silhouetteUrl: null, kurinNumber: 7 };
      groups.push(created);
      return { status: 201, json: created };
    }
    if (!one) return null;
    const group = find(one[1]);
    if (!group) return refuse(404);
    if (one[2]) {
      group.silhouetteUrl = method === 'POST' ? pngDataUrl(64, 64) : null;
      return ok(group);
    }
    if (method === 'PUT') {
      Object.assign(group, { name: body.name, description: body.description });
      return ok(group);
    }
    if (method === 'DELETE') {
      groups.splice(groups.indexOf(group), 1);
      return { status: 204, empty: true };
    }
    return null;
  };
}

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide'];
const VYKHOVNYK = ['Group:Update:OwnGroups'];

const openKurinTab = async (page: Page) => {
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('kurin-head')).toContainText('7 курінь');
};

const openGroup = async (page: Page) => {
  await page.getByTestId('groups').getByText('Соколи').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/group\/g1$/);
  await expect(page.getByTestId('group-head')).toContainText('Гурток, що любить мандрівки.');
};

const sheetOf = (page: Page) => page.locator('ion-modal app-group-edit-sheet');

test('lets the Звʼязковий start a гурток with its silhouette', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, groupApi(true), ZVYAZKOVYI);
  await openKurinTab(page);
  await page.getByTestId('new-group').click();
  const sheet = sheetOf(page);
  await expect(sheet.locator('ion-title')).toContainText('Новий гурток');
  await expect(sheet.getByTestId('delete-group')).toHaveCount(0);
  await sheet.locator('[data-testid=group-name] input').fill('Вовки');
  await sheet.locator('[data-testid=group-description] textarea').fill('Новий гурток новаків.');

  // The silhouette: cropped and processed as on the web, in grey with a ring.
  await sheet.getByTestId('silhouette-input').setInputFiles({ name: 'vovky.png', mimeType: 'image/png', buffer: png(200, 300) });
  const crop = page.locator('app-image-crop-sheet');
  await expect(crop.getByTestId('crop-stage').locator('img')).toBeVisible();
  await expect(crop.getByTestId('silhouette-options')).toContainText('Чорно-біле');
  await expect(crop.getByRole('button', { name: 'Без обробки' })).toBeVisible();
  await shot(page, project, 'group-silhouette-crop');
  await crop.getByTestId('crop-done').click();
  await expect(crop).toHaveCount(0);
  await expect(sheet.getByTestId('group-edit-silhouette').locator('img')).toHaveAttribute('src', /^blob:/);
  // Leave the description and, once the scroll that kept it in view has run out, show the sheet from its top.
  await sheet.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.waitForTimeout(1000);
  await sheet.locator('ion-content').evaluate((content: HTMLIonContentElement) => content.scrollToTop(0));
  await shot(page, project, 'group-new');

  await sheet.getByTestId('save-group').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'POST' && w.path === 'group')?.body).toEqual({
    name: 'Вовки',
    description: 'Новий гурток новаків.',
    kurinKey: 'k1',
  });
  await expect.poll(() => String(log.writes.find((w) => w.path === 'group/g3/silhouette')?.body ?? '')).toMatch(/name="file"; filename="vovky\.png"/);
  expect(String(log.writes.find((w) => w.path === 'group/g3/silhouette')?.body)).toContain('Content-Type: image/png');
  // The list follows.
  await expect(page.getByTestId('groups')).toContainText('Вовки');
});

test('lets the Звʼязковий rename a гурток, drop its silhouette and delete it', async ({ page }, info) => {
  const log = await signIn(page, member, groupApi(true), ZVYAZKOVYI);
  await openKurinTab(page);
  await openGroup(page);
  await expect(page.getByTestId('group-silhouette').locator('img')).toBeVisible();
  await page.getByTestId('edit-group').click();
  const sheet = sheetOf(page);
  const name = sheet.locator('[data-testid=group-name] input');
  await expect(name).toHaveValue(/^Соколи/);
  await expect(name).toBeEnabled();
  // The silhouette first: a tap right after typing can land while iOS scrolls the focused field into
  // view, and move off the button with it.
  await sheet.getByTestId('clear-silhouette').click();
  await expect(sheet.getByTestId('pick-silhouette')).toContainText('Завантажити сильветку');
  await name.fill('Соколи Чорні');
  await expect(sheet.getByTestId('delete-group')).toBeVisible();
  await shot(page, info.project.name, 'group-edit');
  await sheet.getByTestId('save-group').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'PUT' && w.path === 'group/g1')?.body).toEqual({
    name: 'Соколи Чорні',
    description: 'Гурток, що любить мандрівки.',
  });
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'group/g1/silhouette')).toBe(true);
  await expect(page.getByTestId('group-head')).toContainText('Соколи Чорні');
  await expect(page.getByTestId('group-silhouette').locator('img')).toHaveCount(0);

  // Deleting asks first, then leaves for the kurin, whose list no longer has it.
  await page.getByTestId('edit-group').click();
  await sheet.getByTestId('delete-group').click();
  await expect(page.locator('ion-action-sheet')).toContainText('Видалити гурток «Соколи Чорні»?');
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Видалити', exact: true }).click();
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'group/g1')).toBe(true);
  await expect(page).toHaveURL(/\/tabs\/kurin$/);
  await expect(page.getByTestId('groups')).not.toContainText('Соколи');
  await expect(page.getByTestId('groups')).toContainText('Орли');
});

test('lets a Виховник change the description but not the name, nor delete', async ({ page }) => {
  const log = await signIn(page, member, groupApi(true), VYKHOVNYK);
  await openKurinTab(page);
  await expect(page.getByTestId('new-group')).toHaveCount(0);
  await openGroup(page);
  await page.getByTestId('edit-group').click();
  const sheet = sheetOf(page);
  await expect(sheet.locator('[data-testid=group-name] input')).toBeDisabled();
  await expect(sheet).toContainText('Назву гуртка змінює Звʼязковий.');
  await expect(sheet.getByTestId('delete-group')).toHaveCount(0);
  await sheet.locator('[data-testid=group-description] textarea').fill('Мандруємо щосуботи.');
  await sheet.getByTestId('save-group').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'PUT' && w.path === 'group/g1')?.body).toEqual({
    name: 'Соколи',
    description: 'Мандруємо щосуботи.',
  });
});

test('gives a youth no way to make or change a гурток', async ({ page }) => {
  await signIn(page, member, groupApi(false));
  await openKurinTab(page);
  await expect(page.getByTestId('groups')).toContainText('Орли');
  await expect(page.getByTestId('new-group')).toHaveCount(0);
  await openGroup(page);
  await expect(page.getByText('Точкування гуртка')).toBeVisible();
  await expect(page.getByTestId('edit-group')).toHaveCount(0);
});
