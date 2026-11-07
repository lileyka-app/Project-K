# ProjectK UI E2E Tests

Playwright is the UI automation layer for Stage 8 of `0.13.0-beta`.

## Current Scope

- Login through the real `/login` UI for seeded users.
- Persist authenticated browser state per role under `e2e/.auth`.
- Use `describeRole(role, title, tests)` from `e2e/support/role-test.ts` for authenticated role suites. It applies the correct `storageState` and refresh-token interception in one place.
- Use scenario builders from `e2e/support/scenarios.ts` for repeatable dynamic data setup such as activated member accounts and group-with-mentor test state.
- Smoke public pages: welcome, join, login.
- Check basic role access:
  - admin can open admin-only pages;
  - manager can open kurin panel;
  - member cannot open admin-only pages.
- Check the first authenticated mutation flow:
  - manager opens seeded `Gurtok 1`;
  - manager updates the group description through the `Редагувати` menu.

- Check role-sensitive UI controls:
  - manager-only mutate buttons and member form validation states;
  - warning checkboxes that unlock by warning level;
  - mentor review access without member edit controls;
  - member restriction from skill moderation controls.
- Check role/action route boundaries:
  - anonymous users redirect from protected organization routes;
  - member create/edit routes use backend `Create`/`Update` access checks instead of read-only checks;
  - mentors can create members only in assigned groups;
  - leadership setup routes are manager/admin-only;
  - member self-edit hides destructive and warning controls.
- Check moving people between kurins (1.4, `kurin-transfers.spec.ts`) in the «Переходи» mode of the
  «Реєстр», each test on its own fresh kurins and people from `e2e/support/transfers.ts`, so no seeded
  person is moved:
  - the mode opens from the «Реєстр» tab, and the old `/kurin/transfers?code=` address of a printed QR
    opens it with the code looked up;
  - a kurin finds a person by their code and asks «прийняти ще й до нас»; the person's kurin approves in
    the confirm dialog; the asking kurin, the person's kurin and the person are notified;
  - «перевести» after the confirm dialog leaves the person in the asking kurin only;
  - a person owing the kurin for past quarters cannot be let go (the approve button is disabled);
  - a senior kurin asking reads «7-й курінь УСП», not «к. ч. 7» (seeded `chorty@projectk.com`, over
    the API only, its request withdrawn afterwards);
  - a виховник sees the kurin settings read-only («Лише перегляд», no «Зберегти»);
  - a member without kurin management is sent to `/forbidden` from «Переходи», by its address and the old one.
- Check the permanent card link (1.4, `card-link.spec.ts`): made in the account settings, opened in a
  browser that never signed in, switched off, and then «Картку не знайдено».
- Check conditional organization UI:
  - kurin-level profile, group, KV, leadership and member controls by role;
  - group action menu contents by role and assigned mentor scope;
  - planning create/delete controls by role.
- Check the senior kurin card (`senior-kurin-card.spec.ts`, chromium only: it signs in as the seeded `orden@projectk.com`):
  - the kurin tile with both branches, and the role by the kurin's own name («Скриптор» in «Орден Хрестоносців»);
  - перестороги, a running one before one that has run out;
  - «Ступені» shows only the highest ступінь, drawn as a пластунка's;
  - own senior відзначення: pinned first, repeats in one block with «×2» and what each was for;
  - the провід adds a senior відзначення to someone's card and removes it, and gets no pin on someone else's card.
- Check senior відзначення on one's own card (`senior-awards.spec.ts`, helpers in `support/senior-honours.ts`):
  - a person records their own from the catalogue, it counts at once (no «Очікує»), a repeat joins the block as «×2», and the person pins it;
  - more blocks than the tile holds open under «Переглянути всі»;
  - an УПЮ kurin refuses a senior відзначення through the API and its card offers none.
- Check kurin tags (`kurin-tags.spec.ts`, `kurin-tags-boundaries.spec.ts`, helpers in `support/kurin-tags.ts`). Each test opens a kurin of its own, since tags switch on and off per kurin and the senior dues suite reads the Order's seeded tags:
  - the Звʼязковий switches tags on in the kurin settings, creates, renames and deletes tags under «Теги куреня» in the registry, and switches them off, after which the registry has neither the list nor the «Теги» column;
  - «Змінити теги» on a card puts a tag on a person; the registry shows it, filters by it and exports the «Теги» column, and with tags off has neither the column, the filter nor the column in the file;
  - «Нова задача» addresses «Усі з тегом …» through «Оберіть курінь, гуртки, учасників або теги», and with tags off the API refuses the same task;
  - a виховник of the КВ reads the tag settings («Лише перегляд») and gets 403 changing them;
  - a youth sees their own tag, has no «Змінити теги», and gets 403 putting tags on or creating one.

- Check senior kurin dues (DUES-06), each test in a УСП kurin of its own opened from a join request
  (`e2e/support/senior-dues.ts`):
  - «Ставки куреня» for everyone and for a tag from the running quarter; a person with the tag pays its rate;
  - a tag put on after the quarter was charged reprices the unpaid quarter;
  - the провід (a staff membership) is charged like everyone;
  - «Записати операцію» with «Рік» and «Борг», and the register's «Сплачено до» and «Сплачено наперед»;
  - the register's filter by tag;
  - «Передача в край» and «Передача в станицю» empty what the box owes up;
  - «Моя вкладка» on a senior's home page names the quarter's rate by their tag;
  - a senior without the treasurer's rights gets `/forbidden` on the kurin's dues and 403 from its API.
- Check that a debt for past quarters keeps a person in the kurin: «Вивести» on a debtor's card in the
  seeded Орден Хрестоносців names the debt and the release is refused (chromium only — it signs in as
  the Order's seeded Зв'язковий and changes nothing).
- Check the «Архів» mode of the «Реєстр» (`kurin-archive.spec.ts`), each test in a kurin of its own from `e2e/support/kurin-archive.ts`:
  - the Звʼязковий has all four tabs (Склад, Переходи, Архів, Імпорт), sees former roles and memberships and corrects a closed membership's dates;
  - the Звʼязковий takes a former member back from their archive row;
  - their own record carries «своє» and no correction; the old `/kurin/:kurinKey/archive` address opens the mode;
  - a гуртковий sees only their гурток's former memberships, no danger zone, no «Повернути», and no tabs (the archive is their only mode);
  - «Розформувати» sends an empty гурток to the archive and refuses one with money in its box, with a message.
- Check the archive's danger zone (`kurin-archive-danger.spec.ts`):
  - a wrong password is refused, the right one opens the zone with ten deletions;
  - one deletion at a time, after a confirmation;
  - a membership with dues and one whose role is still held are refused, each with its message;
  - another tab of the «Реєстр» ends the zone on the server, with a message: its ticket no longer deletes.
- Check «Пластовий життєпис» (`life-story.spec.ts`):
  - a youth's story has only the УПЮ stage (both projects);
  - for the senior from the demo seed: the button on the card, the УПЮ and УСП stages with every ступінь, and the Order's Звʼязковий may correct them while its Скриптор may not (chromium only: it signs in as shared seeded accounts).
- Check the third-probe notebook (PROBE-02, `third-probe.spec.ts`, helpers in `support/third-probe.ts`).
  Each test opens a kurin of its own with a гурток, its впорядник and a youth born sixteen years ago, so
  no seeded probe or project is touched:
  - the youth sees «До закриття третьої проби» counting down and «Що вже є» at «0 з 5»; writes the
    mentors, the reflection and the camp in «Ментори, рефлексія й табір»; attaches the посвідка as a
    picture; adds a project after the template with a picture in «Опис і моя роль» (one project, though
    the picture saves it quietly first), and renames it with the picture kept;
  - the впорядник of the гурток gives «Дати дозвіл», confirms the reflection and has «Змінити етапи»;
    the youth rewrites the project and «Дозвіл є» stays;
  - nobody signs their own notebook: the youth gets 403 on approval and stages, and a впорядник on
    their own third probe writes it but has no «Дати дозвіл» or «Змінити етапи» and gets 403;
  - a впорядник of another гурток only reads: no «Додати проєкт», «Редагувати» or upload, and 403 on
    writing a project or approving it.

## Seeded Users

Default credentials come from the backend development `DataSeeder`:

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@projectk.com` | `Admin@12345` |
| Manager | `manager1@projectk.com` | `User@12345` |
| Mentor | `mentor1@projectk.com` | `User@12345` |
| Member | `g1member1@projectk.com` | `User@12345` |

Override with environment variables when needed:

- `E2E_ADMIN_EMAIL`, `E2E_ADMIN_PASSWORD`
- `E2E_MANAGER_EMAIL`, `E2E_MANAGER_PASSWORD`
- `E2E_MENTOR_EMAIL`, `E2E_MENTOR_PASSWORD`
- `E2E_MEMBER_EMAIL`, `E2E_MEMBER_PASSWORD`
- `E2E_DEFAULT_PASSWORD`

## Local Run

Start backend on the URL used by `src/environments/environment.ts`:

```powershell
dotnet run --project Backend\ProjectK.Backend\ProjectK.API\ProjectK.API.csproj
```

Then run from `Frontend/projectk-frontend`:

```powershell
npm run e2e
```

For isolated E2E reset, run backend with the E2E environment:

```powershell
$env:ASPNETCORE_ENVIRONMENT='E2E'
dotnet run --project Backend\ProjectK.Backend\ProjectK.API\ProjectK.API.csproj
```

Then enable reset in Playwright:

```powershell
$env:E2E_RESET_ENABLED='true'
$env:E2E_RESET_TOKEN='local-e2e-reset-token'
$env:PLAYWRIGHT_API_URL='http://127.0.0.1:5205/api'
npm run e2e
```

By default Playwright starts Angular on `http://127.0.0.1:4200`.
If Angular is already running:

```powershell
$env:PLAYWRIGHT_START_FRONTEND='false'
npm run e2e
```

Override frontend base URL:

```powershell
$env:PLAYWRIGHT_BASE_URL='http://127.0.0.1:4200'
npm run e2e
```

## Backend Reset Endpoint

The stable Stage 8 flow uses:

- `ASPNETCORE_ENVIRONMENT=E2E`;
- `appsettings.E2E.json` with a separate DB, blob container/prefix and disabled orphan cleanup;
- a reset endpoint such as `POST /api/test/e2e/reset`, available only in `E2E` and protected by a local secret header.

That reset endpoint runs before auth setup when `E2E_RESET_ENABLED=true`, so CRUD tests can create/update/delete entities without leaking state across runs.
