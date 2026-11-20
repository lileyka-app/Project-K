import { expect, test } from '@playwright/test';

import { getMembersByKurin, loginViaApi } from './support/api-client';
import { e2eApiUrl } from './support/e2e-api';
import { loginThroughUi } from './support/login';
import { getSeniorDues, moneyText, owedForPastQuarters } from './support/senior-dues';
import { E2eUser } from './support/test-users';

// A person who owes for quarters already over stays in the kurin (DUES-06 with stream C's check).
// A fresh person owes nothing for the past, so this reads the seeded Орден Хрестоносців and changes
// nothing in it: the release is refused before anything is written.
const orden: E2eUser = { role: 'manager', email: 'orden@projectk.com', password: process.env.E2E_DEFAULT_PASSWORD ?? 'User@12345' };

test.describe('Senior kurin dues: a debt keeps a person in the kurin', () => {
  test('«Вивести» on a debtor\'s card names the debt and releases nobody', async ({ page, request }, testInfo) => {
    // Signing in as the Order's seeded Зв'язковий holds that account's session: one project only.
    test.skip(testInfo.project.name !== 'chromium', 'chromium only: one seeded account, one session');

    const login = await loginViaApi(request, orden);
    const kurinKey = login.kurinKey!;
    const dues = await getSeniorDues(request, orden, kurinKey);
    expect(dues.isSenior).toBe(true);

    const emails = new Map((await getMembersByKurin(request, orden, kurinKey)).map(m => [m.memberKey, m.email ?? '']));
    const debtor = dues.accounts
      .filter(a => a.standing === 'Current' && owedForPastQuarters(a, dues.currentQuarter) > 0)
      .sort((a, b) => (emails.get(a.memberKey) ?? '').localeCompare(emails.get(b.memberKey) ?? ''))[0];
    expect(debtor, 'The seeded Order has nobody owing for past quarters.').toBeTruthy();
    const owed = owedForPastQuarters(debtor, dues.currentQuarter);

    await loginThroughUi(page, orden);
    await page.goto(`/member/${debtor.memberKey}`);
    // The card checks the debt before asking; until its вкладка has loaded it would only ask.
    await expect(page.getByTestId('dues-tile')).toContainText(moneyText(debtor.balance));
    await page.getByRole('button', { name: 'Вивести' }).click();

    const refusal = page.getByRole('alertdialog').or(page.getByRole('dialog')).filter({ hasText: 'Спершу — вкладка' });
    await expect(refusal).toBeVisible();
    await expect(refusal).toContainText(moneyText(owed));
    await refusal.getByRole('button', { name: 'Зрозуміло' }).click();
    await expect(refusal).toBeHidden();

    const leave = await request.delete(`${e2eApiUrl}/kurin/${kurinKey}/memberships/${debtor.memberKey}`, {
      headers: { Authorization: `Bearer ${login.tokens!.accessToken}` }
    });
    expect(leave.status()).toBe(409);
    expect((await leave.json()).error).toBe('MemberOwesDues');

    const after = await getSeniorDues(request, orden, kurinKey);
    expect(after.accounts.find(a => a.memberKey === debtor.memberKey)?.standing).toBe('Current');
  });
});
