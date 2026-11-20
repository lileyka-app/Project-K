import { expect, test } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type ExtraApi } from './support/mock-api';

/**
 * «Моя картка» on a phone (web card-check-dialog, transfer-code-dialog, account settings): the QR to
 * be checked by, the transfer code as a QR, and what the card shows — within what the kurin allows.
 */

const QR = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=';

function cardApi(world: { fields: number; maxFields: number; mayShare: boolean }): ExtraApi {
  const card = () => ({
    allowance: { mayShare: world.mayShare, isAdult: false, maxFields: world.maxFields, mayLink: false },
    fields: world.fields, linkUrl: null, linkIssuedAtUtc: null,
    preview: { fullName: 'Оксана Паливода', photoUrl: null, kurins: [], level: null, awards: null, badges: null, probes: null, checkedAtUtc: null },
  });
  return ({ method, path, body }) => {
    if (method === 'GET' && path === 'me/card') return ok(card());
    if (method === 'PUT' && path === 'me/card/fields') {
      world.fields = (body as { fields: number }).fields;
      return ok(card());
    }
    if (method === 'GET' && path === 'me/card/check-code') {
      return world.mayShare
        ? ok({ url: 'http://x/v/t', qrCode: QR, expiresAtUtc: new Date(Date.now() + 180_000).toISOString() })
        : refuse(403, 'CardSharingNotAllowed');
    }
    if (method === 'GET' && path === 'me/transfer-code') {
      return ok({ url: 'http://x/kurin/transfers?code=PL-4F7A2-9D4A1', qrCode: QR, expiresAtUtc: null });
    }
    return null;
  };
}

test('shows the card as a QR with the time to the next one', async ({ page }, info) => {
  await signIn(page, member, cardApi({ fields: 6, maxFields: 46, mayShare: true }));
  await page.locator('#tab-button-more').click();
  await page.getByTestId('show-card').click();
  await expect(page.getByTestId('card-qr')).toBeVisible();
  await expect(page.getByTestId('check')).toContainText(/Новий QR за 2:[45]\d/);
  await shot(page, info.project.name, 'card-check');
});

test('says the kurin has not allowed it yet', async ({ page }) => {
  await signIn(page, member, cardApi({ fields: 6, maxFields: 0, mayShare: false }));
  await page.goto('./tabs/more/card');
  await expect(page.getByTestId('check')).toContainText('Курінь ще не дозволив');
});

test('shows the transfer code with its QR from the Меню tab', async ({ page }, info) => {
  await signIn(page, member, cardApi({ fields: 6, maxFields: 46, mayShare: true }));
  await page.locator('#tab-button-more').click();
  await page.getByTestId('transfer-code').click();
  await expect(page.getByTestId('transfer-code-value')).toHaveText('PL-4F7A2-9D4A1');
  await expect(page.getByTestId('transfer-qr')).toBeVisible();
  await shot(page, info.project.name, 'transfer-code');
});

test('turns a part on, and leaves what the kurin does not allow switched off', async ({ page }, info) => {
  const world = { fields: 6, maxFields: 46, mayShare: true };
  const log = await signIn(page, member, cardApi(world));
  await page.goto('./tabs/more/card?part=settings');
  await expect(page.getByTestId('field-16')).toHaveClass(/toggle-disabled/);
  await page.getByTestId('field-8').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'me/card/fields')?.body).toEqual({ fields: 14 });
  await shot(page, info.project.name, 'card-settings');
});
