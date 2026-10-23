// Records the API exchanges the static demo answers from. Runs against the docker `demo` stack
// (./scripts/dev.sh up demo; web on 4220) and writes public/assets/demo/{public,<seat>}.json.
// Run from Frontend/projectk-frontend: node scripts/record-demo-fixtures.mjs
//
// Each seat is walked as a visitor would: every sidebar page, every group, every member card and
// its first probe, the calendar a month either side. Whatever the app asked the API is kept as
// `{ m, p, s, b }`; the interceptor looks entries up by method and path.
import { chromium } from 'playwright';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const base = process.env.LILEYKA_DEMO_URL ?? 'http://localhost:4220';
const out = resolve('public/assets/demo');
mkdirSync(out, { recursive: true });

const seats = ['Zvyazkovyi', 'Vykhovnyk', 'Youth'];
const seatLabel = { Zvyazkovyi: 'Звʼязковий', Vykhovnyk: 'Впорядник', Youth: 'Юнак' };

// LILEYKA_BROWSER_CHANNEL=chrome uses the installed Chrome when Playwright's own build is missing.
const browser = await chromium.launch(process.env.LILEYKA_BROWSER_CHANNEL ? { channel: process.env.LILEYKA_BROWSER_CHANNEL } : {});

/** Set by the recorder when the API answered 429; `visit` then waits and asks again. */
let throttled = false;

/** Collects API responses into `entries` while `page` is used; later hits on the same key win. */
function record(page, entries) {
  page.on('response', async response => {
    if (response.status() === 429) {
      throttled = true;
      return;
    }
    const url = new URL(response.url());
    const marker = url.pathname.indexOf('/api/');
    const isHealth = url.pathname.endsWith('/health');
    if (marker < 0 && !isHealth) {
      return;
    }
    const method = response.request().method();
    const path = isHealth ? '/health' : url.pathname.slice(marker) + url.search;
    let body = null;
    try {
      const text = await response.text();
      try { body = JSON.parse(text); } catch { body = text; }
    } catch {
      return;
    }
    entries.set(`${method} ${path}`, { m: method, p: path, s: response.status(), b: body });
  });
}

function save(name, entries) {
  const file = { seat: name, recordedAt: new Date().toISOString(), entries: [...entries.values()] };
  const json = JSON.stringify(file);
  writeFileSync(resolve(out, `${name}.json`), json);
  console.log(`${name}: ${entries.size} entries, ${(json.length / 1024).toFixed(0)} kB`);
}

async function settle(page) {
  await page.waitForLoadState('networkidle').catch(() => undefined);
  await page.waitForTimeout(400);
}

// The demo API rate-limits like production (300 requests a minute per account), and a card is
// six requests; when a page was throttled, wait the window out and load it again so the recording
// never keeps a 429 or a hole where the answer should be.
async function visit(page, path) {
  for (let attempt = 0; attempt < 4; attempt++) {
    throttled = false;
    await page.goto(base + path, { waitUntil: 'domcontentloaded' });
    await settle(page);
    if (!throttled) {
      return;
    }
    console.log(`  429 on ${path}, waiting 65 s`);
    await page.waitForTimeout(65000);
  }
}

/** Everything in the sidebar that is a link, so a seat's own menu decides what gets recorded. */
async function sidebarLinks(page) {
  await page.locator('button').filter({ has: page.locator('.pi-bars') }).first().click();
  await page.locator('.p-panelmenu').first().waitFor();
  const hrefs = await page.locator('.p-panelmenu a[href]').evaluateAll(links => links.map(a => a.getAttribute('href')));
  await page.keyboard.press('Escape');
  return [...new Set(hrefs.filter(h => h?.startsWith('/')))];
}

/** Keys of every group and member that any recorded list mentioned. */
function keysIn(entries, field) {
  const keys = new Set();
  const walk = value => {
    if (Array.isArray(value)) { value.forEach(walk); return; }
    if (value && typeof value === 'object') {
      if (typeof value[field] === 'string') keys.add(value[field]);
      Object.values(value).forEach(walk);
    }
  };
  for (const e of entries.values()) walk(e.b);
  return [...keys];
}

/** File names of every badge picture any recorded answer mentions. */
function badgeFilesIn(entries) {
  const files = new Set();
  for (const e of entries.values()) {
    for (const match of JSON.stringify(e.b ?? '').matchAll(/badges_images\/([A-Za-z0-9_\-.]+\.(?:svg|png|webp))/g)) {
      files.add(match[1]);
    }
  }
  return files;
}

// Exclusive create rather than "check, then write": a picture that appeared meanwhile is kept.
function saveNew(path, body) {
  try {
    writeFileSync(path, body, { flag: 'wx' });
    return true;
  } catch (error) {
    if (error?.code === 'EEXIST') return false;
    throw error;
  }
}

/**
 * The catalogue names its pictures as /badges_images/<file>.svg and the API serves them behind
 * the token. Every one mentioned in this seat's recording is fetched with that token and kept
 * next to the fixtures, so the static demo can hand them back as blobs.
 */
async function downloadBadgePictures(page, entries) {
  const files = badgeFilesIn(entries);
  if (files.size === 0) {
    return;
  }
  const dir = resolve(out, 'badges_images');
  mkdirSync(dir, { recursive: true });
  const token = await page.evaluate(() => JSON.parse(localStorage.getItem('authState') ?? '{}').accessToken);
  const apiOrigin = (process.env.LILEYKA_DEMO_API ?? 'http://localhost:5255');
  let saved = 0;
  for (const file of files) {
    const target = resolve(dir, file);
    if (existsSync(target)) {
      continue;
    }
    const response = await page.request.get(`${apiOrigin}/badges_images/${file}`, { headers: { Authorization: `Bearer ${token}` } });
    if (response.ok()) {
      if (saveNew(target, await response.body())) saved++;
    } else if (response.status() === 429) {
      await page.waitForTimeout(65000);
      const again = await page.request.get(`${apiOrigin}/badges_images/${file}`, { headers: { Authorization: `Bearer ${token}` } });
      if (again.ok() && saveNew(target, await again.body())) saved++;
    }
  }
  console.log(`  badge pictures: ${files.size} named, ${saved} downloaded now`);
}

// Signed-out pages: login, join, about, privacy, plus the health and setup probes.
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
  const entries = new Map();
  record(page, entries);
  for (const path of ['/login', '/join', '/about', '/privacy', '/forgot-password']) {
    await visit(page, path);
  }
  save('public', entries);
  await page.close();
}

for (const seat of seats) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();
  const entries = new Map();
  record(page, entries);

  await visit(page, '/login');
  await page.getByRole('button', { name: seatLabel[seat] }).click();
  // Every seat now lands on the home page, so anything past the login page means signed in.
  await page.waitForURL(url => !url.pathname.startsWith('/login'));
  await settle(page);
  // A reload records the refresh the app does on every start.
  await page.reload({ waitUntil: 'domcontentloaded' });
  await settle(page);

  const kurinKey = await page.evaluate(() => JSON.parse(localStorage.getItem('authState') ?? '{}').kurinKey);
  const links = await sidebarLinks(page);
  for (const href of links) {
    await visit(page, href);
  }

  // The whole journal, page by page: the demo filters and pages these entries itself, the way the
  // API does, so one full pass covers every filter a visitor can pick.
  if (links.some(href => href.endsWith('/journal'))) {
    await visit(page, `/kurin/${kurinKey}/journal`);
    const more = page.getByRole('button', { name: /Завантажити ще/ });
    for (let pages = 0; pages < 50 && await more.count(); pages++) {
      await more.click();
      await settle(page);
    }
  }

  // The calendar a month either side, so paging in the demo does not fall off the recording.
  await visit(page, `/calendar/${kurinKey}`);
  for (const name of ['Назад', 'Далі']) {
    const button = page.locator('app-agenda-calendar').getByRole('button', { name, exact: true });
    if (await button.count()) {
      await button.click();
      await settle(page);
      await visit(page, `/calendar/${kurinKey}`);
    }
  }

  // A гурток's вкладка and points are reached from its page, and the sidebar names only the
  // seat's own гуртки, so every гурток is visited for both.
  for (const groupKey of keysIn(entries, 'groupKey')) {
    await visit(page, `/group/${groupKey}`);
    await visit(page, `/group/${groupKey}/dues`);
    await visit(page, `/group/${groupKey}/score`);
  }

  // Attendance sheets open from the entries of a гурток's table; three of them are enough.
  const sheets = new Set();
  for (const groupKey of keysIn(entries, 'groupKey')) {
    const path = `/group/${groupKey}/score`;
    await visit(page, path);
    const links = await page.locator('a[href*="/score/events/"]').evaluateAll(as => as.map(a => a.getAttribute('href')));
    links.filter(Boolean).forEach(href => sheets.add(href));
    if (sheets.size >= 3) break;
  }
  for (const href of [...sheets].slice(0, 3)) {
    await visit(page, href);
  }

  for (const path of [`/kurin/${kurinKey}/score/settings`, `/kurin/${kurinKey}/score/private`]) {
    await visit(page, path);
  }

  let forms = 0;
  for (const memberKey of keysIn(entries, 'memberKey')) {
    await visit(page, `/member/${memberKey}`);
    // The member form, for the first few cards the seat may edit.
    const edit = page.locator('button:has-text("Редагувати"):not([disabled])').first();
    if (forms < 3 && await edit.count()) {
      forms++;
      await edit.click();
      await page.waitForURL(/\/member\/upsert\//).catch(() => undefined);
      await settle(page);
      await visit(page, `/member/${memberKey}`);
    }
    const details = page.locator('button:has-text("Деталі"):not([disabled])').first();
    if (await details.count()) {
      throttled = false;
      await details.click();
      await page.waitForURL(/\/probe\//).catch(() => undefined);
      await settle(page);
      if (throttled) {
        console.log('  429 on a probe page, waiting 65 s');
        await page.waitForTimeout(65000);
        await page.reload({ waitUntil: 'domcontentloaded' });
        await settle(page);
      }
    }
  }

  // Planning sessions and their detail pages, when the seat can see them.
  await visit(page, `/planning/${kurinKey}`);
  const sessionLinks = await page.locator('a[href*="/planning/"]').evaluateAll(links => links.map(a => a.getAttribute('href')));
  for (const href of [...new Set(sessionLinks)].slice(0, 5)) {
    await visit(page, href);
  }

  await visit(page, '/settings/account');
  await visit(page, '/about');
  await visit(page, '/privacy');

  save(seat, entries);
  await downloadBadgePictures(page, entries);
  await context.close();
}

await browser.close();
console.log('done →', out);
