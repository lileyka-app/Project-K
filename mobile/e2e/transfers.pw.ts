import { expect, test } from '@playwright/test';
import { member, ok, shot, signIn, type ExtraApi } from './support/mock-api';

/**
 * «Склад куреня» on a phone, the Звʼязковий's (web pages/transfers): another kurin's request is approved
 * from a sheet, a person is asked for by code, and «перевести» is not offered while the person owes.
 */

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Leadership:Manage:KurinWide'];
const k = (number: number, branch = 'UPYu') => ({ kurinKey: `k${number}`, number, namedAfter: null, branches: [{ branch, number }] });
const request = (over: Record<string, unknown>) => ({
  membershipRequestKey: 'r1', kind: 'Transfer', status: 'Pending', memberKey: 'm2', memberName: 'Назар Коваль',
  fromKurin: k(7), toKurin: k(15), membershipKind: 'Youth', createdAtUtc: '2026-10-30T10:00:00Z',
  expiresAtUtc: '2026-11-13T10:00:00Z', decidedAtUtc: null, owedToLeave: 0, ...over,
});

function transfersApi(world: { incoming: Record<string, unknown>[]; outgoing: Record<string, unknown>[]; former?: Record<string, unknown>[] }): ExtraApi {
  return ({ method, path, body }) => {
    if (method === 'GET' && path === 'kurin/k1/membership-requests/former') return ok(world.former ?? []);
    if (method === 'GET' && path === 'kurin/k1/membership-requests') return ok(world);
    if (method === 'GET' && path === 'kurin/k1/memberships/candidate') {
      return ok({ member: { memberKey: 'm9', publicId: 'PL-4F7A2-9D4A1', fullName: 'Віталій Романюк' }, alreadyInThisKurin: false, currentKurins: [k(1)] });
    }
    if (method === 'POST' && path === 'kurin/k1/membership-requests') {
      world.outgoing.push(request({ membershipRequestKey: 'r2', memberName: 'Віталій Романюк', fromKurin: k(1), toKurin: k(7), ...(body as object) }));
      return ok({ membershipRequestKey: 'r2', joined: false });
    }
    if (method === 'POST' && path === 'kurin/k1/membership-requests/r1/approve') {
      world.incoming = world.incoming.map((r) => ({ ...r, status: 'Approved' }));
      return ok({});
    }
    return null;
  };
}

test('approves another kurin\'s request from a sheet', async ({ page }, info) => {
  const world = { incoming: [request({ fromKurin: k(7) })], outgoing: [] as Record<string, unknown>[] };
  const log = await signIn(page, member, transfersApi(world), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/transfers');
  await expect(page.getByTestId('request')).toContainText('к. ч. 15 просить перевести до себе');
  await shot(page, info.project.name, 'transfers-incoming');

  await page.getByTestId('request').click();
  await page.locator('ion-action-sheet:not(.overlay-hidden)').getByRole('button', { name: 'Перевести', exact: true }).click();
  await expect(page.locator('ion-toast').last()).toContainText('Підтверджено.');
  expect(log.writes.some((w) => w.method === 'POST' && w.path === 'kurin/k1/membership-requests/r1/approve')).toBe(true);
  await expect(page.getByTestId('request')).toContainText('підтверджено');
});

test('names a senior kurin by its гілка, not as the УПЮ kurin of the same number', async ({ page }) => {
  const world = { incoming: [request({ fromKurin: k(5), toKurin: k(5, 'USP') })], outgoing: [] as Record<string, unknown>[] };
  await signIn(page, member, transfersApi(world), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/transfers');
  await expect(page.getByTestId('request')).toContainText('5-й курінь УСП просить перевести до себе');
  await expect(page.getByTestId('request')).not.toContainText('к. ч. 5 просить');
});

test('does not offer «перевести» while the person owes', async ({ page }) => {
  const world = { incoming: [request({ owedToLeave: 460 })], outgoing: [] as Record<string, unknown>[] };
  await signIn(page, member, transfersApi(world), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/transfers');
  await expect(page.getByTestId('request')).toContainText('Борг за минулі квартали');

  await page.getByTestId('request').click();
  const sheet = page.locator('ion-action-sheet:not(.overlay-hidden)');
  await expect(sheet.getByRole('button', { name: 'Відхилити', exact: true })).toBeVisible();
  await expect(sheet.getByRole('button', { name: 'Перевести', exact: true })).toHaveCount(0);
});

test('asks for a person by the code they gave', async ({ page }, info) => {
  const world = { incoming: [] as Record<string, unknown>[], outgoing: [] as Record<string, unknown>[] };
  const log = await signIn(page, member, transfersApi(world), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/transfers');
  await page.getByTestId('code').locator('input').fill('pl-4f7a2-9d4a1');
  await page.getByTestId('find').click();
  await expect(page.getByTestId('ask')).toContainText('Зараз у к. ч. 1');
  await page.getByTestId('send').click();
  await expect(page.locator('ion-toast').last()).toContainText('Запит надіслано');
  expect(log.writes.find((w) => w.path === 'kurin/k1/membership-requests')?.body).toEqual({
    publicId: 'PL-4F7A2-9D4A1', kind: 'Transfer', membershipKind: 'Youth', fromKurinKey: 'k1',
  });
  await expect(page.getByTestId('outgoing')).toContainText('Віталій Романюк');
  await shot(page, info.project.name, 'transfers-sent');
});

test('asks back a former member who now stands in another kurin, «ще й до нас»', async ({ page }, info) => {
  const former = (memberKey: string, firstName: string, standsIn: ReturnType<typeof k>[]) => ({
    former: { memberKey, firstName, lastName: 'Коваль', groupName: 'Ведмеді', joinedAtUtc: '2022-10-05T00:00:00Z', leftAtUtc: '2026-10-30T00:00:00Z' },
    standsIn,
  });
  const world = {
    incoming: [] as Record<string, unknown>[],
    outgoing: [] as Record<string, unknown>[],
    former: [former('m2', 'Назар', [k(15)]), former('m3', 'Віталій', [])],
  };
  const log = await signIn(page, member, transfersApi(world), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/transfers');
  await page.locator('ion-segment-button[value="former"]').click();
  await expect(page.getByTestId('former')).toContainText('Зараз у к. ч. 15');
  await expect(page.getByTestId('former')).toContainText('Зараз ніде не стоїть');
  await shot(page, info.project.name, 'transfers-former');

  await page.getByTestId('former-member').first().click();
  const sheet = page.locator('ion-action-sheet:not(.overlay-hidden)');
  await sheet.getByRole('button', { name: 'Прийняти ще й до нас', exact: true }).click();
  await page.locator('ion-action-sheet:not(.overlay-hidden)').getByRole('button', { name: 'Впорядник', exact: true }).click();
  await expect(page.locator('ion-toast').last()).toContainText('Запит надіслано');
  expect(log.writes.find((w) => w.path === 'kurin/k1/membership-requests')?.body).toEqual({
    memberKey: 'm2', kind: 'AlsoJoin', membershipKind: 'Staff', fromKurinKey: 'k15',
  });
  await expect(page.getByTestId('former-member').first()).toContainText('запит чекає');
});

test('a scanned transfer QR opens with the code already looked up', async ({ page }) => {
  const world = { incoming: [] as Record<string, unknown>[], outgoing: [] as Record<string, unknown>[] };
  await signIn(page, member, transfersApi(world), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/transfers?code=PL-4F7A2-9D4A1');
  await expect(page.getByTestId('ask')).toContainText('Віталій Романюк');
  await expect(page.getByTestId('ask')).toContainText('Зараз у к. ч. 1');
});
