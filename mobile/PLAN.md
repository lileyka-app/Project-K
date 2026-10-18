# Лілейка Mobile — план для передачі між сесіями

> Статус: **лише планування. Код не написано.** Документ призначений для `mobile/PLAN.md` у репо `lileyka-app/Project-K` (поза `docs/`, щоб не потрапляв на публічний сайт Starlight).
> Дата складання: 2026-10-10. База аналізу: HEAD `4f363805fd4a1738af990e5913ebd310fdf8ed56`.
> Позначки: ✅ перевірено по коду/документації, ⚠️ гіпотеза — перевірити спайком.

---

## 1. Контекст і мета

- Власник: Rost, .NET-інженер. Основна машина — **Windows**, **Mac немає**. Є **iPhone 16**.
- Мета: мобільний **додаток-компаньйон Лілейки**. Перший обсяг — **юнак** може виконати всі основні дії; далі поступове розширення до повної підтримки (лідери тощо).
- Потрібні **iOS і Android**, «дуже зручно і приємний UI», **максимально native-вигляд**, який можна витиснути з Ionic.
- Тестування: реальний **iPhone**; **Android на емулюваторі під Windows**. Фінальні/релізні збірки можуть іти в **CI**; dev/валідаційні — бажано без Mac (xtool-подібні тули).
- Ліцензія репо: Lileyka Source-Available License (некомерційна). Назва в UI — «Лілейка», «ProjectK» лише в коді/версії.

## 2. Рішення користувача (зафіксовані)

| # | Рішення |
|---|---|
| D1 | Окремий Ionic-застосунок у `mobile/` (в тому ж моно-репо) |
| D2 | Платформо-адаптивні режими: iOS-вигляд на iPhone, Material на Android |
| D3 | Шрифти/типографіка за брендбуком (Manrope; Neucha — лише лого і заголовки порожніх станів) |
| D4 | MVP-вкладки/екрани — спланувати детально (розділ 7) |
| D5 | Папки `mobile/ios`, `mobile/android` — ок |
| D6 | Адаптація токенів/auth на бекенді — «не проблема» |
| D7 | Фінальна збірка в CI допустима; dev-перевірки бажано швидкими тулами |

**Принцип дизайну:** *native behaviour, brand skin.* Платформа визначає навігацію, переходи, жести, пікери/шити, гаптику, safe areas. Бренд визначає кольори, шрифт, форму, порожні стани, тексти.

## 3. Огляд інструментів (результати дослідження)

- **xtool** (xtool-org/xtool, MIT): заміна Xcode на Linux/WSL/macOS; SwiftPM → iOS app, підпис, встановлення. Потрібен Xcode.xip (ліцензія Apple забороняє Xcode/SDK на не-Apple залізі — сіра зона). Без вивантаження в App Store на старті. Для `xtool setup` потрібен Team Key (App Manager).
- **xcross** (arxdeus/xcross): Flutter iOS **debug JIT** + hot reload з Windows/Linux; release/AOT неможливий без macOS (`gen_snapshot`). Для Ionic/Capacitor не підходить.
- **omarchy-apple-dev** (joshuaswarren, MIT): glue навколо xtool для нативного SwiftUI на Omarchy; `ship.sh` → .ipa + TestFlight через ASC API key (платний акаунт). Лише USB; без storyboard/xib; матриця Swift/Xcode SDK (FINDINGS.md).
- **Висновок:** жоден з них не прибирає потреби в **macOS CI-раннері** для гарантованого, коректного App Store-релізу Capacitor-додатку. xtool-лінія для dev — це **гейтований спайк (S4)**, не залежність.
- **Capacitor 8** ✅: iOS за замовчуванням SPM (`CapApp-SPM`), плагіни мають підтримувати SPM; конфіг `ios.path`/`android.path`, `server.url`, `server.cleartext` (Android блокує cleartext з API 28), `server.androidScheme` (default `https`), `server.iosScheme` (default `capacitor`); live reload: `cap run --live-reload`. Хмарні iOS-білдери (платні): Capgo Build, Capawesome Cloud, Odevio.
- **Ionic 9** ✅ (реліз 2026-08-19): Angular 18–22, zoneless за замовчуванням, імпорти з `@ionic/angular` (не `/standalone`), міграція `npx @ionic/migrate`. Ionic 8 — Angular ≤20. Angular 22 вимагає TypeScript 6.0 (web вже на 6.0.3).

## 4. Архітектура

### 4.1 Розкладка репо

```
Project-K/
  Backend/ Frontend/ docs/ site/ ...
  mobile/                  ← корінь Angular 22 + Ionic 9 + Capacitor 8
    src/ (app, environments, theme, assets)
    ios/                   ← capacitor ios (SPM)
    android/
    capacitor.config.ts
    PLAN.md                ← цей файл
```

`mobile/` — корінь проєкту, тож `ios/` і `android/` лежать поруч із `src/`, `ios.path` не потрібен.

### 4.2 Що ділимо з вебом

- Лише **чисті** файли: `*.dto.ts`, `*.enum.ts`, `*.function.ts`, `agenda.ts`, `me.dto.ts`, `greeting.function`, `day-label.function` тощо — через path alias (напр. `@lil/shared/*` → `../Frontend/projectk-frontend/src/app/...`).
- ⚠️ Перевірити, що ці файли не тягнуть UI-залежностей (Optimus/Tailwind/PrimeNG). `me.dto.ts` імпортує `scoreModule/models/score.enums` та `kurinModule/models/agenda` — це ✅ чисті типи.
- Тонкі мобільні API-сервіси (власні), без Optimus UI / Tailwind.
- Альтернатива, якщо alias болить: окремий пакет `shared/` з перенесенням (більший рефакторинг веба) — не в v1.

### 4.3 Мережа/конфіг

- Веб бере `apiUrl` з runtime `env.js` (`__PROJECTK_CONFIG__`); self-host — повноправний кейс. У мобілці — **вибір сервера** (екран 0): за замовчуванням хмарний, «Свій сервер» → валідація через `GET /health` (повертає версію).
- Збереження: токени/сервер — у **Keychain/Keystore** (Capacitor secure-storage плагін із SPM-підтримкою — ⚠️ вибрати і перевірити).
- Інтерсептор: Bearer access token, 401 → refresh (див. 8), помилки `{error, message}` → код у український текст; **ніколи** не показувати `HttpErrorResponse.message`.
- Контекст куреня: `POST api/auth/kurin-scope` перевидає токен.
- Фото: завжди даунскейл (API приймає ≤6 МБ на запит): `resizeToWidth`, `onlyScaleDown`, jpeg, `isImageTooLarge`.
- Zoneless: веб ще на zone.js, мобілка — zoneless (signals) ✅ узгоджено з Angular 22/Ionic 9.

## 5. Тема і бренд (BRANDBOOK v1.2)

Токени → Ionic CSS-змінні (`--ion-color-primary` + `-rgb/-shade/-tint/-contrast`, `--ion-background-color`, `--ion-text-color`, `--ion-border-color`, `--ion-toolbar-*`, `--ion-tab-bar-*`).

| Токен | Light | Dark |
|---|---|---|
| green (primary) | `#0E6E4E` | `#34C48D` |
| terracotta (one-spot) | `#D9762F` | `#EE9A5A` |
| ink | `#101413` | — |
| muted | `#6E7873` | — |
| line | `#E4E8E6` | — |
| surface | `#F7F9F8` | `#161B19` |
| paper | `#FFFFFF` | ground `#0D1110` |

Правила:
- Terracotta — «one-spot rule»: ніколи на кнопках/лінках/success. Непрочитане = primary-крапка, не terracotta.
- Шрифт: **Manrope** (variable woff2 локально: cyrillic/latin/latin-ext, ~112 kB; копія з `public/assets/fonts`). **Neucha** лише лого і заголовки порожніх станів.
- Шкала: h1 28px/800, body 15px, caption 13px, micro 11px uppercase.
- Рамки завжди 1px `--lil-line`; радіуси 8 (інтерактив) / 12 (картки); **без тіней**, окрім модалок/шитів/дропдаунів; анімація ≤200ms; hover = bg→surface (на таче не покладатись).
- Іконки лише PrimeIcons або Lucide (`lucide-static`), без емодзі і намальованих SVG. ⚠️ Перевірити `ion-icon` + Lucide (font/SVG). Дефолтні Ionicons у tab bar замінити.
- Порожні стани обов'язкові: заголовок Neucha без крапки + один пояснювальний рядок + одна дія. Каталог — BRANDBOOK §6, включно з офлайн «Звʼязку немає» і холодним стартом «Сервер ще прокидається».
- Одна primary-кнопка на екран. Тексти «ти», кнопки в інфінітиві, без «будь ласка/упс». Тости прості («Збережено»).
- Статуси задач: Todo=secondary(сірий) / InProgress=warn(terracotta) / Done=info(зелений) — джерело істини `agenda-status.config.ts`. Події календаря — приглушений синій `--lil-agenda-event-*`. RSVP: Going=зелений / Maybe=terracotta / NotGoing=сірий. Категорія — крапка з mix-кольорами `--lil-agenda-cat-{bg|line|dot}-mix`.
- Темна тема: `data-theme='dark'` + системна за замовчуванням; перемикач Системна/Світла/Темна.
- Захищене: табличка номера куреня (червоний `#b30003`/`#ff0005`, Arial) і `scouts-main.png` — не чіпати.
- Іконка застосунку: білий знак на зеленому `#0E6E4E` із теракотовою центральною пелюсткою.
- Конфлікти Ionic ↔ бренд, які треба явно перекрити: md-elevation (тіні), iOS large title vs h1 28px, дефолтні Ionicons, радіуси кнопок.
- Режими Ionic: `mode: 'ios'` на iPhone, `'md'` на Android (автовибір платформою). У браузері перемикання для перевірки — ⚠️ параметр `?ionic:mode=`.

## 6. Телефонні уроки (CONTRIBUTING «Телефон»)

Без tooltip/hover-only; рядок `(click)` + реальний лінк; оверлеї; фото даунскейлити; помилки через коди; використовувати патерни `UserActionService` (спінер/захист від подвійного кліку) та `RequestFeedbackInterceptor` (`requestFeedback('silent'|'errors',[handled])`) — у мобілці реалізувати еквіваленти.

## 7. Детальний план екранів MVP («юнак»)

**Таби (5): Головна · Календар [Події | Задачі] · Поступ (лише якщо `hasYouthProgram`) · Сповіщення · Ще.**

### 7.0 Auth (без табів)
1. Вибір сервера (хмара за замовчуванням / «Свій сервер» з валідацією `/health`).
2. Логін; помилки код→текст (як `LOGIN_ERROR_TEXT` у `features/authModule/pages/login/login.ts`).
3. Крок MFA-коду (`mfaToken`); еквівалент `mfaTrust` (7 днів) — device-bound (розділ 8).
4. Forgot-password / активація — через веб у in-app browser, потім universal links.
5. Банер холодного старту; біометрія — пізніше. **Реєстрацію (`join`) у v1 не робимо.**

### 7.1 Головна (`GET api/me`)
- Large title-привітання (`greeting.function`, `day-label.function`), підрядок: дата · ступінь · гурток.
- Картки: **Найближче** (14 днів, спершу ті, що чекають відповіді; інлайн-сегмент «Іду/Можливо/Не йду» + гаптика); **Мої задачі** (swipe Почати/Зроблено, прострочені червоним, посилання «Дошка»); **Проба** (прогрес + 3 наступні пункти); **Вмілості** (три полиці: На перевірці/У роботі/Підтверджені); **Точкування** (сума, джерела, місце X з N); **Вкладка** (сплачено — зелений / борг — червоний / надлишок «+», квартал, ставка).
- Прибрати з Головної плитки «Мій профіль»/«Мої курені» (переїжджають у «Ще», аватар у хедері). «Справи» (лідерські) — фаза 3.
- Збережене розташування плиток: `/user/me/layouts` (по `boardKey`) — спершу read-only, поважати приховані.
- Pull-to-refresh, скелетони, останній снапшот офлайн; підпис куреня на рядках, якщо куренів >1 (`MyKurinRefDto.namedAfter`).

### 7.2 Календар → Події
- Тижнева стрічка + список по днях; `ion-datetime` з `highlightedDates` для стрибка.
- Перемикач «Графіки гуртків» (бліді рядки, без RSVP).
- Рядок: крапка категорії (mix-кольори), локація, чіп RSVP.
- Деталі — `ion-modal` з breakpoints ~[0, .5, .92]: RSVP (з приміткою для серій), посилання на мапу. Додати в календар пристрою і створення подій — пізніше.

### 7.3 Календар → Задачі
- Фільтр-чіпи Зробити / В процесі / Зроблено (замість канбану), «Моє» за замовчуванням увімкнено, пошук, сортування (action sheet), infinite scroll («Завантажити ще»).
- Тег статусу за брендом; PerMember-прогрес «5 з 8».
- Дії гейтяться `canChangeStatus` (swipe + деталь-шит). Архів у MVP немає.

### 7.4 Поступ
- Сегменти **Проба | Вмілості**.
- Проба: хедер прогресу, тег статусу, акордеон за `sectionCode`, пункти read-only з хто/коли (повний список потребує окремого endpoint — `MyProbeDto.nextPoints` лише найближчі). Підписують лише ментори.
- Вмілості: три полиці з картинкою/статусом/датою перевірки; відхилена показує коментар. «Подати вмілість» через пошуковий каталог — фаза 2b. Порожній стан напр. «Вмілостей ще немає».

### 7.5 Сповіщення
- Список по днях; непрочитане = primary-крапка; swipe «прочитано»; тап → мапінг на мобільний маршрут (`AppNotificationType`, 12 типів; поле `route` — це веб-маршрут, потрібна таблиця відповідності; fallback — in-app browser); бейдж на табі.
- Push — фаза 3 (device-token endpoint, APNs-ключ, FCM).

### 7.6 Ще
Профіль (read-only картка учасника) · Мої курені (перемикач, «ЗАРАЗ», `POST api/auth/kurin-scope`) · Вкладка · Точкування (+ таблиця групи) · Акаунт і безпека (імʼя/email/пароль/MFA/сесії; MVP — через in-app browser для MFA) · Вигляд (Системна/Світла/Темна) · Сервер · **Про Лілейку** (вимога ліцензії, без змін, з атрибуцією) · Конфіденційність · Повідомити про проблему (існуючий feedback endpoint) · Версія · Вийти.

## 8. Зміни на бекенді (Backend, під ворота тестів)

1. **Мобільний refresh-потік**: зараз refresh — httpOnly cookie `refreshToken` (`Path=/api/auth`, SameSite=None/Lax залежно від HTTPS, `RefreshTokenCookie.cs`). Для мобілки — токен у тілі відповіді за opt-in заголовком, зберігання в Keychain/Keystore.
2. **CORS**: зараз один origin (`EnvCorsOrigin` + `AllowCredentials`, Program.cs ~L150). Потрібен allow-list (origins застосунку `capacitor://localhost`, `https://localhost`, dev-origins). ⚠️ Поведінка cookie у WKWebView з `capacitor://localhost` не перевірена — тому non-cookie шлях.
3. **Device-bound MFA trust** (аналог `mfaTrust` cookie, 7 днів, security-stamp bound).
4. **Push device registration** (фаза 3).
5. **Universal/app links** association-файли (лише хмарний домен).
6. **Тестовий акаунт для рев'ю в магазинах** (у Production демо-акаунтів немає ✅ ARCHITECTURE.md).
7. Нові ендпоїнти додавати в `AuthorizationBaselineMatrixTests`, відповідати `ProjectK.Architecture.Tests`; вертикальні слайси, іменування `…Command/Query/Handler/Validator`.
8. Повний список пунктів проби для юнака (read-only) — окремий endpoint.

## 9. Матриця тестування

| Ціль | Спосіб |
|---|---|
| Ітерація UI | Chrome DevTools, обидва режими Ionic |
| iPhone без нативної збірки | Safari по Tailscale (той самий WebKit; плагіни/status bar не перевіряються); патерн `ng serve --configuration tailscale --host 0.0.0.0` уже є у вебі |
| Android | Емулятор на Windows: `cap run android --live-reload`, `server.url=http://10.0.2.2:4200`, `server.cleartext=true`, бекенд `http://10.0.2.2:5205/api`, CORS має дозволяти dev-origins ⚠️ (віртуалізація Windows — перевірити) |
| iPhone нативно (гарантовано) | CI macOS → TestFlight |
| iPhone нативно (швидко) | Спайк S4: xtool/WSL2 (usbipd-win для USB ⚠️) |

## 10. Фази і гейти

**Фаза 0 — спайки (кожен має критерій pass/fail):**
- **S1** Ionic 9 + Angular 22 + Capacitor 8 hello у `mobile/` (zoneless) + Android-емулятор + live reload. *Pass:* екран на емуляторі, HMR працює.
- **S2** iPhone Safari по Tailscale. *Pass:* UI відкривається на iPhone, ios-режим коректний.
- **S3** CI macOS → TestFlight порожньої оболонки. *Pass:* білд в TestFlight з GitHub Actions. Перевірити ліміти/вартість macOS-раннерів.
- **S4** (ризиковий, опційний) xtool/WSL2 оболонка на iPhone. Проблеми: Capacitor-шаблон має Main/LaunchScreen storyboard, xtool їх не підтримує → програмний `CAPBridgeViewController` + `UILaunchScreen`; лише SPM-плагіни; Xcode.xip ліцензійна сіра зона. *Fail ≠ блокер.*

**Фаза 1 — фундамент:** маппінг теми, shell з табами, логін + вибір сервера + secure storage, shared alias, мобільні зміни auth на бекенді, CI.

**Фаза 2 — MVP «юнак»:** 2a Головна / Календар+Задачі / Сповіщення / Ще; 2b Поступ (з поданням вмілості); 2c безпека акаунта.

**Фаза 3 — розширення:** push (APNs/FCM), біометрія, камера/фото, офлайн-кеш, лідерські функції (аркуш відвідуваності — природний телефонний кейс; плитка «Справи»; підпис проб; модерація вмілостей; створення подій).

## 11. CI/CD

- `mobile.yml` за зразком наявних path-filtered workflow (`angular.yml` — Frontend/**, Node 22, `sed`-ін'єкція версії): ubuntu-job — lint/test/web build + Android AAB; macOS-job — iOS archive + fastlane/TestFlight на release або `workflow_dispatch`.
- Секрети: ASC API key (`.p8`), Android keystore.
- Версія: плейсхолдери `v0.0.0-dev` / `LocalDevelopment` у `mobile/src/environments/environment*.ts` (як у вебі). Розширити composite action `.github/actions/release-info` (VERSION/CODENAME; regex тегів `^[0-9]+\.[0-9]+(\.[0-9]+)?([-.][0-9A-Za-z.-]+)?$`): iOS-версія має бути **числовою** (теги на кшталт `v0.20.0-beta` не підходять) + build number з run number.
- Розглянути path-фільтри так, щоб зміни спільних DTO у вебі тригерили mobile build.
- PR-гейту немає (один мейнтейнер, CODEOWNERS `* @iamavasya`).

## 12. Ризики і пастки

1. Cookie-refresh + один origin у CORS vs Capacitor origins → non-cookie шлях (розділ 8).
2. `apiUrl` runtime-конфіг → потрібен вибір сервера.
3. Нечислові версії тегів vs iOS.
4. Storyboard-и vs xtool (S4).
5. Плагіни лише з SPM.
6. App Review: потрібен робочий тестовий акаунт; користувачі-неповнолітні → target audience / age rating, privacy policy URL, правила видалення акаунта при in-app реєстрації (тому без `join` у v1).
7. MFA QR на тому ж телефоні → ручний ключ / `otpauth://`; у MVP — in-app browser.
8. `route` у сповіщеннях — веб-маршрути → мапінг.
9. Ionic 9 новий (відставання екосистеми).
10. Конфлікти бренду з Ionic (тіні, large title, Ionicons).
11. Ліцензія: сторінка «Про Лілейку» з атрибуцією **має лишатись у кожній працюючій копії**; назва «Лілейка» зафіксована; сторонні не можуть модифікувати.
12. Xcode.xip-ліцензія (S4).

## 13. Відкриті питання

- Чи показувати «Поступ» не-юнакам (зараз ховаємо без `hasYouthProgram`)?
- Чи поважати збережений layout плиток (read-only) у v1?
- Обсяг push у фазі 3 (які типи сповіщень).
- Target audience / age rating у сторах.
- Оформлення Apple Developer акаунта (платний) і Google Play.
- Вибір secure-storage плагіна з SPM-підтримкою.

## 14. Що перевірено, а що ні

✅ Перевірено: Capacitor/Ionic можливості (док), код і структура репо, auth-cookie поведінка, CORS, CI, BRANDBOOK, CONTRIBUTING, форма `api/me`, моделі agenda/notification.
✅ Перевірено спайками (див. §17): Angular 22 zoneless + Ionic 9 + Capacitor 8 збирається і працює на Android та iOS-симуляторі; `10.0.2.2`+cleartext у Capacitor 8 (live reload і dev-API на емуляторі).
⚠️ Не перевірено: WKWebView-cookie з `capacitor://localhost`; WSL2+usbipd з iPhone; ATS при http; `ion-icon`+Lucide; `?ionic:mode=`; віртуалізація Android-емулятора на Windows; чистота shared DTO від UI-імпортів; `auth.interceptor.ts` прочитано не повністю.

## 15. Список для читання наступній сесії

- `ARCHITECTURE.md`, `BRANDBOOK.md` (особливо §2 токени, §6 порожні стани), `CONTRIBUTING.md` (розділ «Телефон»), `LICENSE`.
- `Frontend/projectk-frontend/`: `package.json`, `angular.json`, `src/environments/environment*.ts`, `src/lileyka-theme.css`, `src/app/lileyka-preset.ts`, `src/app/app.routes.ts`, `public/assets/fonts/*`.
- `features/dashboardModule/models/me.dto.ts`, `services/me.service.ts`, `functions/greeting.function.ts`, `day-label.function.ts`.
- `features/kurinModule/models/agenda.ts`, `agenda-status.config.ts`, `services/{agenda-service,probes-and-badges,client-cache,membership-service}`.
- `features/notificationsModule/models/app-notification.model.ts`.
- `features/authModule/pages/login/login.ts`, `services/auth.interceptor.ts` (дочитати).
- `shared/tile-board/tile-layout.service.ts`, `shared/empty-state`.
- Backend: `ProjectK.API/Program.cs` (CORS), `Helpers/RefreshTokenCookie.cs`, `MfaTrustCookie.cs`, `AuthorizationBaselineMatrixTests`, `ProjectK.Architecture.Tests`.
- `.github/actions/release-info/action.yml`, `.github/workflows/{angular,dotnet,site,e2e,selfhost-docker}.yml`.
- `docs/user/features/*` (dashboard, calendar-and-tasks, probes-and-badges, notifications, dues, score, member-card), `docs/user/roles/*`, `docs/user/account.md`.

## 16. Перший крок наступної сесії

Не починати з коду фіч. Спершу S1 (+S2) у `mobile/`; після них узгодити з користувачем результат і лише тоді Фаза 1. Пристроєві перевірки (iPhone, емулятор) виконує користувач; у середовищі агента доступ до репо лише на читання — зміни віддавати патчем/файлами.

## 17. Результати Фази 0 (2026-10-10)

Каркас у `mobile/`, PR #114 (draft, у `dev` не мерджиться до рішення користувача). Перевірки ганяє `.github/workflows/mobile.yml` на кожен PR, що чіпає `mobile/**`; скріни в артефактах `mobile-screens` і `ios-screens`.

- **S1 — ✅ pass.** Ionic 9 + Angular 22 (zoneless, без zone.js) + Capacitor 8. На емуляторі Android (API 35, GitHub Actions + KVM) `scripts/emulator-smoke.sh` перевіряє: рендер, режим `md`, платформа `android`, лічильник (сигнали), вкладка «Ще», темна тема за системою, live reload з `10.0.2.2:4200` (правка `home.ts` долітає на емулятор) і dev-API `http://10.0.2.2:5205/api`. Cleartext дозволено лише для `10.0.2.2`/`localhost` через `network_security_config.xml`.
- **S2 — ✅ pass (замінено).** Замість iPhone по Tailscale: iOS-симулятор на macOS-раннері. Нативна збірка Capacitor (SPM, без підпису) встановлюється і запускається (режим Ionic `ios`, платформа Capacitor `ios`, темна тема за системою); Safari на симуляторі відкриває dev-сервер (Головна, Ще) і сам вмикає режим `ios` без `?ionic:mode=`. Перевірка на справжньому iPhone по Tailscale лишається за користувачем.
- **S3 — ⏸ заблоковано.** Потрібні Apple Developer акаунт, App Store Connect API key і сертифікати в секретах репо. macOS-раннер для симулятора вже працює (~10 хв на прогін).
- **S4 — ⏸ заблоковано.** Потрібні iPhone і Windows/WSL2 користувача.

Що з'ясувалося по дорозі:
- Angular CLI 22 вимагає Node ≥22.22.3 або ≥24.15.
- Sass `@import` застарів, теми підключаються через `@use`.
- SonarCloud (quality gate) вимагає: `npm ci --ignore-scripts`, без `npx` у workflow, закріплені SHA сторонніх actions, `Package.resolved` для SPM, вузький `FileProvider`, `allowBackup=false`, явний `usesCleartextTraffic=false`.

Наступний крок: користувач дивиться скріни і PR, вирішує по S3/S4, далі Фаза 1.

## 18. Поворот на PWA (2026-10-10)

Рішення користувача: поки нативні збірки чекають (S3/S4 потребують комп'ютера), основний шлях —
максимально «нативна» PWA з того самого `mobile/`. Ionic сам обирає вигляд і в браузері: `ios` на
iPhone/iPad, `md` (Material) на Android.

Зроблено:
- маніфест, іконки Лілейки (any, maskable, apple-touch), iOS meta-теги, `theme-color` за системною темою;
- Angular service worker лише в браузерній прод-збірці (у нативних оболонках вимкнений);
- картка встановлення: кнопка «Встановити» на Android (`beforeinstallprompt`), підказка
  «Поділитися → На початковий екран» на iPhone;
- haptics на вебі через `navigator.vibrate` (Android), на iPhone без вібрації;
- PWA-смоук у Playwright (`npm run e2e`, job `pwa` у CI): маніфест та іконки, `ios`/`md`, таби,
  темна тема, запуск з головного екрана, офлайн-перезавантаження (Chromium).

Відкрито:
- хостинг PWA: вирішено — той самий домен, шлях `/m/` (CORS, cookie і `env.js` спільні з вебом; self-host
  без окремого домену). Хмара: `angular.yml` збирає PWA в `dist/.../m`, маршрути в `staticwebapp.config.json`;
  self-host: стадія `mobile` у web-Dockerfile і `location /m/` в nginx. Для фото пізніше треба дозволити
  `camera` у `Permissions-Policy` для `/m/`;
- вхід у PWA (2026-10-10): екран входу (email + пароль, другий крок з кодом або кодом відновлення),
  вихід у «Ще». Бекенд без змін: PWA на тому ж origin, тому бере ту саму refresh-cookie і той самий
  `authState` у localStorage, що й веб (вхід/вихід в одному діє в іншому в тому самому браузері).
  Офлайн сесія не скидається. Перевірено в CI: мок API в `pwa` і справжній API в job `pwa-real-api`
  (e2e-стек). Нативні оболонки поки без входу: їм потрібен refresh-токен у тілі (§8). На preview SWA
  вхід не працює: staging API дозволяє в CORS лише основний origin SWA. Ще не зроблено: обов'язкове
  налаштування MFA для привілейованих акаунтів (у вебі це діалог після входу);
- Головна, профіль і двофакторка (2026-10-10): Головна на `api/me` (найближчі події з RSVP, мої задачі з
  «Почати/Зроблено», проба, вмілості, точкування, вкладка; pull-to-refresh), профіль у «Ще» (картка
  учасника, read-only), обовʼязкове увімкнення двофакторки для проводу й адмінів (сервер каже через
  `auth/mfa/status`; без цього вкладки не відкриваються, лише «Вийти»), необовʼязкове для решти з «Ще».
  Технічна інформація оболонки переїхала в «Ще → Про застосунок». У CI справжній API проходить вхід,
  Головну, профіль і увімкнення двофакторки з кодом TOTP, обчисленим тестом;
- push (APNs через Web Push на iOS 16.4+ лише для встановленої PWA) — зроблено для PWA, див. §21.

**Тема Лілейки поверх Ionic (2026-10-10).** `src/theme/lileyka.scss`, поки під класом `.lk`, і довідник
`/m/ui` (без входу, працює і на прев'ю): Ionic як є поруч із Лілейкою, `?ionic:mode=ios|md` перемикає
платформу. Як у вебі (однаково на iOS і Android): кнопки, вкладки (сегмент = лоток вкладок дошки),
поля, мітки, картка, прогрес. Нативна форма з брендовим кольором, шрифтом Manrope і лініями:
перемикачі, прапорці, радіо, списки («Ще»), шапка, нижнє меню. Розгортання на весь застосунок —
перенести правила з `.lk` на корінь після рішення Роста.
Після першого огляду Роста схрещено: обидві платформи — вкладки й поля з вебу (крапки пароля
системні), мітки/картка/шапка/нижнє меню з розмірами й радіусами Ionic у кольорах бренду, перемикачі
й прогрес як в Ionic, іконки шапки темні. iPhone — кнопки Ionic як є, список «Ще» з розмірами iOS.
Android — кнопки з вебу в розмірах Material (36px, 14/500), список «Ще» як меню вебу. Сегмент на
обох платформах у висоті Ionic (iOS 32px, Android 48px).
Розгорнуто на весь застосунок (2026-10-10): `.lk` стоїть на `ion-app` (разом з тостами), крім `/ui`, де
його має лише колонка Лілейки. Сторінка — тло surface під картками paper; поля входу й двофакторки —
поля Лілейки. Liquid Glass на iPhone (рішення Роста, 2026-10-10): Ionic 9.0.7 власного Liquid Glass не має (запит
ionic-framework#30466 без відповіді), тож узято теми rdlabo `@rdlabo/ionic-theme-ios26` і
`@rdlabo/ionic-theme-ios27` (MIT, Ionic 8.8.1+/9). `styles.scss` вантажить iOS 27, де Safari вже має
`overflow-anchor`, інакше iOS 26 (є `text-wrap: pretty`), на старших iOS звичайний Ionic; темна тема
системна; переходи сторінок і поповерів з iOS 27 (`app.config.ts`). Тема чіпає лише режим `ios`.
Вимога теми: у `ion-list inset` пункти загорнуті в `ion-item-group`, заголовок поза групою. Лілейка на
iPhone тепер лише кольори, Manrope, тло (`--lk-ios-ground`, на крок темніше за surface) і поля; вся
«шкіра» вебу (сегмент-лоток, шапка й таб-бар з лінією, картки й меню з рамкою) лишилась тільки на Android.
Довідник на iPhone порівнює «скло iOS як є» зі склом у Лілейці. Локальні знімки iPhone робляться в
Chromium, тобто це гілка iOS 27; справжній Safari 26 бере гілку iOS 26.
Скло й HIG по всьому iPhone (2026-10-10, прохання Роста «усі елементи Liquid Glass + анімації + відступи з
доків Apple»). Анімації: лінза скла, що їде за пальцем, на таб-барі й усіх сегментах
(`src/app/ui/glass.ts` над `registerTabBarEffect`/`registerSegmentEffect` rdlabo; на Android нічого не
робить), таб-бар по центру (`tab-bar-position-center`). Метрики з Apple HIG і WWDC25 (HIG для iOS майже
не дає пікселів; решта — типові значення UIKit): ціль дотику ≥44pt, поля 16pt, плитки іконок у
списках 29pt з радіусом ~7pt і 16pt до назви, шрифти за таблицею HIG (великий заголовок 34 bold,
підзаголовок 15/20, Title 2 22/28), панелі без лінії (лише м'яке розмиття краю). «Ще» зроблено як
«Параметри» iOS: рядок акаунта з аватаром і імʼям, плитки іконок, «Вийти» окремо по центру з
підтвердженням через action sheet (руйнівна дія червона, є «Скасувати»). Заголовки груп — `ion-label`
у `ion-list-header` (так їх малює тема), кнопки в `ion-item` явно `size="default"` (Ionic інакше робить їх
малими). Відкрите: точний радіус груп iOS 26 і розмір іконок таб-бару Apple не публікує — звірити з
UI Kit з Apple Design Resources, коли Рост буде за компʼютером.

## 19. Інвентар веб-додатку для перенесення (2026-10-10)

Повний перелік сторінок і фіч основного вебу (`Frontend/projectk-frontend`), щоб переносити в PWA пункт за
пунктом. Знято з коду (маршрути в [app.routes.ts](../Frontend/projectk-frontend/src/app/app.routes.ts), меню в
[sidebar-menu.ts](../Frontend/projectk-frontend/src/app/features/kurinModule/components/sidebar-menu/sidebar-menu.ts), права в
[permission.service.ts](../Frontend/projectk-frontend/src/app/features/authModule/services/permission-service/permission.service.ts)); веб не запускався.

**Ролі.** **Ю** — юнак (без проводових прав: читає курінь, своє редагує). **П** — провід (Виховник/гуртковий,
Звʼязковий, курінний, скарбник, суддя; конкретна роль вказана). **А** — адмін системи (поза куренем).
Більшість кнопок усередині сторінок вебу гейтиться прапорцями з відповіді API (`canEdit`, `canChangeStatus`,
`viewer.canKeep` …), тож PWA має читати ті самі прапорці, а не вигадувати свої перевірки.

**Стан у PWA.** ✅ є · ◐ частково · ○ немає (план §7 або пізніше) · ✗ свідомо не переносимо в мобілку.

### 19.1 Оболонка і навігація

1. **Верхня панель** — [toolbar-header](../Frontend/projectk-frontend/src/app/features/kurinModule/components/toolbar-header/toolbar-header.html):
   бургер-меню, хлібні крихти, перемикач куреня, «До адміністрації» (А), тема, дзвіночок, «Вийти». Ю/П/А.
   PWA ◐: таб-бар + «Ще», вихід є; хлібних крихт не треба ✗.
2. **Бокове меню** — [sidebar-menu.ts](../Frontend/projectk-frontend/src/app/features/kurinModule/components/sidebar-menu/sidebar-menu.ts): пункти за
   правами (Головна, Мій профіль, Курінь, Реєстр, Імпорт, Календар, Задачі, Планування, Точкування, Модерація
   вмілостей, Вкладка гуртка/куреня, Налаштування куреня, Адміністрація, Акаунт, Довідка, Повідомити про
   проблему, Про Лілейку). PWA ◐: «Ще» має профіль, двофакторку, про застосунок, вихід.
3. **Перемикач куреня** — [kurin-switcher](../Frontend/projectk-frontend/src/app/features/kurinModule/components/kurin-switcher/) (лише якщо куренів >1;
   `auth/kurin-scope/options`, `POST auth/kurin-scope`). Ю/П. PWA ✅ (перемикач у шапці й «Мої курені», §20).
4. **Банер холодного старту** — [cold-start-banner](../Frontend/projectk-frontend/src/app/features/systemModule/components/cold-start-banner/),
   [health-banner.service](../Frontend/projectk-frontend/src/app/features/systemModule/services/health-banner-service/). Усі. PWA ✅ (§20).
5. **Тема світла/темна** — [theme.service](../Frontend/projectk-frontend/src/app/features/systemModule/services/theme-service/). Усі. PWA ✅ («Ще → Вигляд», §20).
6. **Плиткові дошки з власним розкладом** — [tile-board](../Frontend/projectk-frontend/src/app/shared/tile-board/tile-board.ts): «Налаштувати
   вигляд», перетягування, сховати/повернути плитку, скинути; `GET/PUT/DELETE user/me/layouts/{boardKey}`.
   Дошки: `dashboard`, `member-card`, `kurin-panel`, `group-panel`. Усі. PWA ◐ (Головна поважає приховані на
   вебі плитки дошки `dashboard` і її порядок, лише читання; решта дошок і редагування — ні, §20).
7. **Перемикач ролей для розробки** — [dev-role-switcher](../Frontend/projectk-frontend/src/app/features/systemModule/components/dev-role-switcher/)
   (не прод, А). PWA ✗.

### 19.2 Вхід, онбординг, акаунт

8. **Вхід** — [login](../Frontend/projectk-frontend/src/app/features/authModule/pages/login/login.html): email+пароль, крок з кодом або кодом відновлення,
   посилання на відновлення і заявку; у демо-режимі панель «Демо-курінь» (Звʼязковий/Впорядник/Юнак). Усі.
   PWA ✅ (без демо-панелі ○).
9. **Вихід** — [logout](../Frontend/projectk-frontend/src/app/features/authModule/pages/logout/logout.ts). Усі. PWA ✅.
10. **Обовʼязкова двофакторка** — [mfa-enforcer.service](../Frontend/projectk-frontend/src/app/features/authModule/services/mfa-enforcer-service/),
    [mfa-setup-dialog](../Frontend/projectk-frontend/src/app/features/authModule/components/mfa-setup-dialog/mfa-setup-dialog.html): QR/секрет, код,
    коди відновлення. Для А і Звʼязкового (сервер каже через `auth/mfa/status`). PWA ✅.
11. **Налаштування акаунта** — [account-settings](../Frontend/projectk-frontend/src/app/features/authModule/pages/account-settings/account-settings.html):
    контакти (email з підтвердженням, телефон), зміна пароля, MFA (увімкнути; привілейовані — «Скинути»,
    решта — «Вимкнути»; оновити коди відновлення). Ю/П/А. PWA ◐ (§20: усе, крім підтвердження email — у вебі).
12. **Відновлення пароля** — [forgot-password](../Frontend/projectk-frontend/src/app/features/authModule/pages/onboarding/forgot-password/forgot-password.ts),
    [reset-password](../Frontend/projectk-frontend/src/app/features/authModule/pages/onboarding/reset-password/reset-password.ts). Усі. PWA ○ (§7.0:
    через веб).
13. **Заявка на приєднання** — [waitlist-registration](../Frontend/projectk-frontend/src/app/features/authModule/pages/onboarding/waitlist-registration/waitlist-registration.ts)
    (`/join`). Гості. PWA ✗ у v1 (§7.0).
14. **Активація акаунта** — [account-activation](../Frontend/projectk-frontend/src/app/features/authModule/pages/onboarding/account-activation/account-activation.ts)
    (`/activate/:token`, пароль → вхід). Гості. PWA ○ (через веб, потім universal links).
15. **Початкове налаштування системи** — [setup](../Frontend/projectk-frontend/src/app/features/authModule/pages/setup/setup.html) (лише перший запуск).
    А. PWA ✗.
16. **Немає доступу / Тут такого немає** — [forbidden](../Frontend/projectk-frontend/src/app/features/authModule/pages/forbidden/forbidden.html). Усі. PWA ○.

### 19.3 Головна

17. **Головна (дашборд)** — [dashboard](../Frontend/projectk-frontend/src/app/features/dashboardModule/pages/dashboard/dashboard.html),
    [me.service](../Frontend/projectk-frontend/src/app/features/dashboardModule/services/me.service.ts) (`api/me/*`); показується, коли є картка учасника,
    інакше вітальна сторінка ([dashboard-match.guard](../Frontend/projectk-frontend/src/app/features/dashboardModule/guards/dashboard-match.guard.ts)).
    Плитки:
    - 17.1 **Найближче** (14 днів, RSVP інлайн) — [upcoming-events-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/upcoming-events-tile/). Ю/П. PWA ✅.
    - 17.2 **Мої задачі** (Почати/Зроблено) — [my-tasks-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-tasks-tile/). Ю/П. PWA ✅.
    - 17.3 **Проба** — [my-probe-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-probe-tile/) (лише УПЮ). Ю. PWA ✅.
    - 17.4 **Вмілості** — [my-skills-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-skills-tile/) (лише УПЮ). Ю. PWA ✅.
    - 17.5 **Точкування** — [my-score-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-score-tile/). Ю. PWA ✅.
    - 17.6 **Вкладка** (свій баланс, пільга) — [my-dues-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-dues-tile/). Ю. PWA ✅.
    - 17.7 **Справи** (вмілості на перевірку, передачі, записи на перевірку, події без присутності) —
      [my-duties-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-duties-tile/). П. PWA ✅ (§20).
    - 17.8 **Мій профіль** — [my-profile-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-profile-tile/). Усі. PWA ✅ (у «Ще»).
    - 17.9 **Мої курені** (з перемиканням скоупу) — [my-kurins-tile](../Frontend/projectk-frontend/src/app/features/dashboardModule/components/my-kurins-tile/). Ю/П. PWA ✅ («Ще → Мої курені», §20).

### 19.4 Календар, задачі, планування

18. **Календар** — [agenda-calendar](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/agenda-calendar/agenda-calendar.html) (`/calendar/:kurinKey`),
    [agenda.service](../Frontend/projectk-frontend/src/app/features/kurinModule/services/agenda-service/agenda.service.ts): місяць/тиждень/день,
    «Графіки гуртків» (усі); «Нова подія», виділення діапазону, перетягування/розтяг (П: `canManageAgenda` +
    `item.canEdit`; серія зсувається цілком). Ю — перегляд і RSVP. PWA ✅ (§20: місяць/тиждень/день/список; без виділення діапазону й перетягування).
19. **Подія/задача (діалог)** — [agenda-item-dialog](../Frontend/projectk-frontend/src/app/features/kurinModule/components/agenda-item-dialog/agenda-item-dialog.html):
    перегляд (усі); RSVP Йду/Можливо/Не йду з лічильниками й списком (усі, для збережених подій); форма
    (П): тип, назва, опис, місце, група подій, весь день, повторення (тиждень/місяць/рік, інтервал, дні, до
    дати), цілі ([agenda-assign-select](../Frontend/projectk-frontend/src/app/features/kurinModule/components/agenda-assign-select/)), режим виконання
    (спільно/будь-хто/кожен), видалення; кнопка «Точкування» → аркуш присутності (`canScore`). PWA ✅ (§20,
    сторінки замість діалогу).
20. **Виконання задачі** — [agenda-progress](../Frontend/projectk-frontend/src/app/features/kurinModule/components/agenda-progress/): «Твоя частина»,
    статус по цілях «X з N», чекбокси кожного (за прапорцями `canChangeStatus`). Ю/П. PWA ✅ (§20).
21. **Задачі (дошка)** — [agenda-board](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/agenda-board/agenda-board.html) (`/tasks/:kurinKey`):
    колонки Зробити/В процесі/Зроблено, пошук, ціль, сортування, «Моє», «Завантажити ще», перетягування між
    колонками (`canChangeStatus`); «Нова задача» (П); редагувати/в архів/видалити (`canEdit`); архів з
    поверненням і видаленням назавжди (`canEdit`). Ю — перегляд, свої статуси. PWA ✅ (§20, без перетягування).
22. **Групи подій (категорії)** — [agenda-category-manager](../Frontend/projectk-frontend/src/app/features/kurinModule/components/agenda-category-manager/)
    на сторінці налаштувань куреня. П (Звʼязковий)/А. PWA ✗ (адмінка лишається у вебі).
23. **Політика архіву задач** — [agenda-archive-policy](../Frontend/projectk-frontend/src/app/features/kurinModule/components/agenda-archive-policy/). П (Звʼязковий)/А. PWA ✗.
24. **Планування таборів** — [planning-list.ts](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/planning-list/planning-list.ts)
    (`/planning/:kurinKey`), [planning-detail.ts](../Frontend/projectk-frontend/src/app/features/kurinModule/components/planning-detail/planning-detail.ts):
    список сесій, графік зайнятості, оптимальні дати, «Перенести в календар»; видалення (`session.canDelete`).
    Ю/П читають. PWA ✅ (§20, «Планування таборів»: графік — смуга днів, що гортається вбік).
25. **Нове планування** — [create-planning.ts](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/create-planning/create-planning.ts):
    назва, тривалість, вікно пошуку, учасники КВ з вагою голосу і зайнятими датами. П (`canCreatePlanning`).
    PWA ✅ (§20).

### 19.5 Курінь, гуртки, учасники

26. **Курінь** — [kurin-panel](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/kurin-panel/kurin-panel.html) (`/kurin`): шапка (номер,
    імені, гілка, станиця/край, опис); гуртки (відкрити — усі; створити/змінити/видалити через
    [manage-panel](../Frontend/projectk-frontend/src/app/features/kurinModule/components/manage-panel/) — Звʼязковий); PDF-звіт і редагування профілю
    куреня (Звʼязковий); список учасників ([member-list](../Frontend/projectk-frontend/src/app/features/kurinModule/components/member-list/): пошук,
    сортування, ролі, верифікація); «Додати учасника куреня», «Прийняти за кодом»
    ([join-by-code-dialog](../Frontend/projectk-frontend/src/app/features/kurinModule/components/join-by-code-dialog/)) — Звʼязковий. Ю — усе read-only.
    PWA ◐ (огляд, учасники, «Новий гурток» з сильветкою, «Новий учасник» і «Прийняти за кодом» — Звʼязковий, «Редагувати»: дані куреня й PDF-звіт).
27. **КВ (курінна виховна)** — [kv-panel](../Frontend/projectk-frontend/src/app/features/kurinModule/components/kv-panel/): Звʼязковий і впорядники з
    гуртками, архів (усі); додати впорядника, призначити гуртки, передати Звʼязкового (Звʼязковий/А). PWA ✅
    (`/tabs/kurin/kv`, §20).
28. **Провід (панель)** — [leadership-panel](../Frontend/projectk-frontend/src/app/features/kurinModule/components/leadership/leadership-panel/): ролі
    зараз і архів (усі); шестерня → форма (`canSetupLeadership`). Курінь і гурток. PWA ✅ (`/tabs/kurin/leadership`,
    `/tabs/kurin/group/:g/leadership`, §20).
29. **Провід (форма)** — [leadership](../Frontend/projectk-frontend/src/app/features/kurinModule/components/leadership/leadership/leadership.html)
    (`/leadership/...`): учасник на кожну роль, кілька на роль, архів. П (курінний — свій курінь,
    Виховник/гуртковий — свій гурток, Звʼязковий — усе). PWA ✅ (аркуш для куреня й гуртка; КВ — лише передача
    Звʼязкового з п. 27, як у вебі).
30. **Гурток** — [group-panel](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/group-panel/group-panel.html) (`/group/:groupKey`): силует,
    опис, провід гуртка, учасники таблицею або картками ([mini-member-card](../Frontend/projectk-frontend/src/app/features/kurinModule/components/mini-member-card/)),
    дні народження на 30 днів ([upcoming-birthdays-tile](../Frontend/projectk-frontend/src/app/features/kurinModule/components/upcoming-birthdays-tile/));
    меню «Редагувати»: опис, додати учасника, Виховники, силует з обрізанням (П за `check-access`). Ю — read-only.
    PWA ✅ («Редагувати»: опис і сильветка за `check-access`, назва й видалення — Звʼязковий; «Новий учасник» за `check-access` Create;
    «Налаштувати провід» гуртка; «Додати з куреня», перевести/прибрати, «Виховники» — Звʼязковий).
31. **Картка учасника** — [member-card](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/member-card/member-card.html) (`/member/:memberKey`),
    дошка з плитками:
    - 31.1 Профіль (фото, верифікація, попередження, контакти, школа, адреса; «Редагувати» — своя картка або П). PWA ✅ (редагування — п. 32).
    - 31.2 Здобуті вмілості, діалог «Усі вмілості», «Додати вмілість» з каталогу (своя картка/П), підтвердити/зняти (П-рецензент) — [skill-mini-card](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/member-card/components/skill-mini-card/). PWA ✅ (§20: каталог, подання, підтвердити/зняти).
    - 31.3 Проба (прогрес, «Деталі»). PWA ✅ (§20, сторінка проби — п. 33).
    - 31.4 Нагороди: додати/змінити/видалити (своя картка/П), підтвердити (П) — [member-awards-tile](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/member-card/components/member-awards-tile/), [member-awards-dialog](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/member-card/components/member-awards-dialog/). PWA ✅ (§20).
    - 31.5 Впорядництво (ролі КВ, гуртки). PWA ✅.
    - 31.6 Вкладка учасника (баланс, рахунки, 3 останні записи; своя або скарбник). PWA ✅ (на картці, коли сервер її показує).
    - 31.7 Членства: поточні/минулі курені, перемкнути курінь; «Гурток» (перевести) і «Вивести» — Звʼязковий — [member-memberships-tile](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/member-card/components/member-memberships-tile/). PWA ◐ (поточні й «Раніше», «Змінити гурток» і «Вивести з куреня» з підтвердженням — Звʼязковий, лише в своєму курені; без перемикання куреня звідси).
    Ю — своя картка повністю (крім модерації), чужа — read-only без вкладки.
32. **Редагування/новий учасник** — [upsert-member](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/upsert-member/upsert-member.html) (4 маршрути):
    імʼя, телефон, дата народження, email (лише без акаунта), повторне запрошення, фото з обрізанням, дати
    ступенів УПЮ/УСП/УПС; верифікація профілю (П-рецензент), попередження 3 рівнів (П), «Видалити профіль»
    (Звʼязковий/А). Ю — лише свій профіль без email. PWA ✅ (редагування і створення як у вебі).
33. **Проба (сторінка)** — [member-probe-page](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/member-probe-page/member-probe-page.html):
    розділи-акордеон, хто/коли підписав; «Підписати», «Скасувати підпис», «Здати і закрити пробу» (П-рецензент).
    Ю — read-only. PWA ✅ (§20).
34. **Модерація вмілостей** — [skills-review-page](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/skills-review-page/skills-review-page.html):
    черга, «Підтвердити»/«Відхилити» з коментарем. П (Виховник, Звʼязковий), лише УПЮ. PWA ✅ (§20).
35. **Реєстр** — [registry](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/registry/registry.html): склад рядками, вибір колонок,
    вивантаження в Excel, колишні з «Повернути», чисельність за ступенями. П (Виховник, Звʼязковий)/А. PWA ✗
    (таблиця під десктоп).
36. **Імпорт складу** — [import](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/import/import.html): `.xlsx`, мапінг колонок, пробний
    прогін, імпорт. Звʼязковий/А. PWA ✗.
37. **Налаштування куреня** — [kurin-settings](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/kurin-settings/kurin-settings.html):
    верифікація профілів, групи подій (п. 22), архів задач (п. 23). Звʼязковий/А. PWA ✗.

### 19.6 Вкладка (внески)

38. **Вкладка гуртка** — [group-dues](../Frontend/projectk-frontend/src/app/features/duesModule/pages/group-dues/group-dues.html) (`/group/:groupKey/dues`),
    [dues.service](../Frontend/projectk-frontend/src/app/features/duesModule/services/dues-service/dues.service.ts): каса, ставки, сітка по кварталах,
    колишні, історія з фільтром; ставка гуртка, «Записати операцію»
    ([dues-entry-dialog](../Frontend/projectk-frontend/src/app/features/duesModule/pages/group-dues/components/dues-entry-dialog/)), пільга, правка
    неперевірених (`viewer.canKeep`); «Перевірено» (`canVerify`); ставки куреня
    ([kurin-rate-dialog](../Frontend/projectk-frontend/src/app/features/duesModule/components/kurin-rate-dialog/), `canSetKurinRates`). П (Виховник,
    гуртковий, скарбник гуртка). Ю — немає (свій баланс — п. 17.6). PWA ✅ (§20; «Ставки куреня» тим, кому
    `canSetKurinRates`, — §20, 2026-10-11).
39. **Вкладка куреня** — [kurin-dues](../Frontend/projectk-frontend/src/app/features/duesModule/pages/kurin-dues/kurin-dues.html): каса куреня, гуртки,
    квартали, передачі від гуртків («Отримано»), операції, ставки. П (курінний скарбник, Звʼязковий)/А. PWA ✅
    (`/tabs/kurin/dues`: Каса / Гуртки з кварталами / Передачі / Історія; «Меню», «Курінь» і «Справи» ведуть сюди).

### 19.7 Точкування

40. **Точкування куреня** — [kurin-score](../Frontend/projectk-frontend/src/app/features/scoreModule/pages/kurin-score/kurin-score.html) (`/kurin/:kurinKey/score`),
    [score.service](../Frontend/projectk-frontend/src/app/features/scoreModule/services/score-service/score.service.ts): період
    ([score-period-select](../Frontend/projectk-frontend/src/app/features/scoreModule/components/score-period-select/)), рейтинг гуртків; кнопки «Точкування
    КВ» (`canSeePrivateScore`) і «Налаштування» (`viewer.canManage`). Ю/П. PWA ◐ (§20: таблиця за періодами; без
    «Точкування КВ» і «Налаштування» — п. 43, 44).
41. **Точкування гуртка** — [group-score](../Frontend/projectk-frontend/src/app/features/scoreModule/pages/group-score/group-score.html): юнаки за джерелами,
    «Дано вручну»; «Записати бал» ([score-entry-dialog](../Frontend/projectk-frontend/src/app/features/scoreModule/components/score-entry-dialog/)) і
    правка (`canScore`). Ю — read-only, якщо сервер пускає. PWA ✅ (§20).
42. **Аркуш присутності події** — [attendance-sheet](../Frontend/projectk-frontend/src/app/features/scoreModule/pages/attendance-sheet/attendance-sheet.html):
    пошук, фільтр гуртка, «Відповіли/Призначені/Решта», відмітити присутність, «Усі, хто відповів, були»,
    бал особі/гуртку, ставка події. П (суддя, гуртковий, курінний, Виховник, Звʼязковий). PWA ✅ (§20).
43. **Налаштування точкування** — [kurin-score-settings](../Frontend/projectk-frontend/src/app/features/scoreModule/pages/kurin-score-settings/kurin-score-settings.html):
    алгоритм, ставки присутності, автоправила, позиції, етапи. П (суддя куреня, Звʼязковий)/А. PWA ✗.
44. **Книга КВ** — [private-score](../Frontend/projectk-frontend/src/app/features/scoreModule/pages/private-score/private-score.html): бали КВ за критеріями,
    записи, критерії. Звʼязковий, впорядники/А. PWA ✗/пізніше.

### 19.8 Сповіщення

45. **Дзвіночок** — [notification-bell](../Frontend/projectk-frontend/src/app/features/notificationsModule/components/notification-bell/),
    [notification.service](../Frontend/projectk-frontend/src/app/features/notificationsModule/services/notification-service/notification.service.ts):
    останні 10, лічильник, «Позначити всі як прочитані», тап → прочитано і перехід за `route`; без опитування й
    push. Усі. PWA ✅ (§20; лічильник оновлюється, поки застосунок відкритий; push на телефон — §21: кожне нове сповіщення
    дзвіночка приходить і пушем, тап відкриває той самий екран, що й тап у дзвіночку). Типи і веб-маршрути (для таблиці відповідності):
    `MemberProfileVerified`, `MemberProfileChangedAfterVerification`, `MemberAwardSubmitted`,
    `MemberAwardReviewed`, `MemberWarningAssigned`, `MemberSkillReviewed` → `/member/{key}`;
    `MemberSkillSubmittedForReview` → `/kurin/{k}/review/skills`; `AgendaItemAssigned/Updated/Deleted/StatusChanged`
    → `/tasks/{k}` (задача) або `/calendar/{k}` (подія); `WaitlistEntrySubmitted` → `/waitlist` (А; у фронтовому
    типі відсутній); `LeadershipChanged` — у переліку є, але ніде не надсилається.

### 19.9 Адміністрування (адмін поза куренем)

46. **Адміністрація** — [admin-panel](../Frontend/projectk-frontend/src/app/features/kurinModule/pages/admin-panel/admin-panel.html) (`/panel`): курені
    (відкрити, створити, змінити, видалити), «Заявки» з крапкою, «Користувачі». А. PWA ✗.
47. **Користувачі** — [users-list](../Frontend/projectk-frontend/src/app/features/adminModule/pages/users-list/users-list.html): пошук, роль, блокування,
    видалення. А. PWA ✗.
48. **Заявки** — [waitlist-management.ts](../Frontend/projectk-frontend/src/app/features/adminModule/pages/waitlist-management/waitlist-management.ts):
    схвалити (запрошення), відхилити з приміткою, повторне запрошення. А. PWA ✅ («Меню → Заявки» з крапкою,
    поки щось чекає; сповіщення `WaitlistEntrySubmitted` веде туди, §20).
49. **Системні налаштування** — [system-settings](../Frontend/projectk-frontend/src/app/features/adminModule/pages/system-settings/system-settings.html):
    обовʼязкова MFA для привілейованих. А. PWA ✗.

### 19.10 Інформаційні сторінки і зворотний звʼязок

50. **Вітальна сторінка** — [welcome-page](../Frontend/projectk-frontend/src/app/features/systemModule/pages/welcome-page/welcome-page.html). Гості. PWA ✗
    (PWA відкривається одразу на вхід).
51. **Про Лілейку** — [about-page](../Frontend/projectk-frontend/src/app/features/systemModule/pages/about-page/about-page.html): ідея, історія релізів,
    версії фронту й API. Усі. PWA ✅ (§20).
52. **Конфіденційність** — [privacy-page](../Frontend/projectk-frontend/src/app/features/systemModule/pages/privacy-page/privacy-page.html). Усі. PWA ✅ (§20).
53. **Повідомити про проблему** — [report-problem-dialog](../Frontend/projectk-frontend/src/app/features/systemModule/components/report-problem-dialog/report-problem-dialog.html),
    [feedback.service](../Frontend/projectk-frontend/src/app/features/systemModule/services/feedback-service/): опис, кроки, очікуване, скріншоти,
    `POST feedback/problems`. Усі. PWA ✅ (§20).
54. **Довідка** — зовнішній сайт документації (`docsUrl`), пункт меню. Усі. PWA ✅ (посилання в «Ще»).

### 19.11 Що з цього випливає для порядку перенесення

- **Юнак (MVP, §7):** 3, 5, 11, 18–21, 31 (своя картка), 33 (read-only), 40, 45, 52–54. Решта юнацького вже є.
- **Провід, телефонні сценарії (фаза 3):** 17.7 Справи, 42 аркуш присутності, 34 модерація вмілостей,
  33 підпис пункту проби, 38 записати операцію у вкладку, створення події/задачі (19), 26/30 перегляд складу.
- **Лишається у вебі:** 7, 15, 22, 23, 35–37, 43, 46, 47, 49 (налаштування, таблиці й адмінка під десктоп).

## 20. Перша повна PWA (2026-10-10)

Прохання Роста «великим прогоном першу повну реалізацію». Узято все, що §19 позначає для мобілки (юнак
і телефонні сценарії проводу); пункти «лишається у вебі» (7, 15, 22, 23, 25, 29, 35–37, 39, 43, 44,
46–49) не переносились (29 перенесено пізніше, див. «КВ і провід куреня» нижче). Вкладки за HIG (≤5): Головна, Календар, Задачі, Курінь, Ще. Код:
`src/app/core/` (Api з українськими текстами помилок, Loaded, Toasts), `src/app/features/<область>/`,
e2e на кожну область (`e2e/<область>.pw.ts`) поверх спільного stand-in API `e2e/support/mock-api.ts`.

- **Головна:** дзвіночок з лічильником → Сповіщення (перехід у відповідний екран, «прочитати всі»);
  «Справи» проводу (вмілості, операції вкладки, присутність); рядки подій і задач відкривають деталі.
- **Календар:** місяць з крапками й список дня, тиждень (сім днів списком, шеврон відкриває день), день
  (сітка годин, «весь день» зверху, смуга тижня), «Список» на 60 днів, «Графіки гуртків»; вибраний вигляд
  памʼятається на пристрої; деталі події з
  RSVP, лічильниками й списком відповідей; «Точкування» → аркуш присутності; форма події/задачі для
  проводу (повторення, «Для кого», режим виконання), редагування й видалення за `canEdit`.
- **Задачі:** Зробити / В процесі / Зроблено, пошук, «Моє», сортування, цілі, «Завантажити ще»; статус
  тапом або свайпом; деталі з «Твоя частина» і «X з N». Архів (як у вебі, бачать усі): пошук, правила
  куреня, «Повернути на дошку» й «Видалити назавжди» за `canEdit` (рядок, свайп або сама задача). Без
  перетягування.
- **Курінь:** огляд (шапка, точкування, гуртки, КВ, провід), учасники з пошуком і сортуванням; гурток
  (провід, дні народження, учасники, точкування, вкладка); картка учасника (вмілості з каталогу,
  перевірка вмілостей, нагороди, ролі, вкладка, членства, своє редагування); сторінка проби з підписом
  пунктів і закриттям. «Профіль» у «Ще» — власна картка.
- **Точкування:** таблиця куреня за періодами, гурток (юнаки за джерелами, «Дано вручну», «Записати
  бал»), аркуш присутності (тап — відмітка одразу, «Усі, хто відповів, були», бали особі/гуртку, ставка).
- **Провід:** перевірка вмілостей (підтвердити/відхилити з приміткою), вкладка гуртка (каса, юнаки за
  кварталом, історія, «Записати операцію», «Перевірено», пільга, ставка гуртка).
- **Ще:** Акаунт (контакти, пароль, двофакторка: вимкнути/скинути, нові коди), Вигляд (Системна /
  Світла / Темна: темні палітри Ionic і rdlabo тепер у класовій формі), Мої курені (перемикання
  скоупу), Довідка, Повідомити про проблему (зі скриншотами), Конфіденційність, Про застосунок (зміст
  вебової сторінки + версія API); банер холодного старту.

Права — ті самі прапорці API, що у вебі. Не зроблено: фото й силуети за токеном (показуються ініціали),
опитування сповіщень (пуш — §21), підтвердження email (у вебі), «Немає доступу» як окремий маршрут. Початковий
бандл 1,9 МБ (≈295 кБ gzip): rdlabo імпортує `@ionic/core/components`, тож усі використані компоненти
Ionic потрапляють у main; бюджет піднято до 2 МБ.

**Учасники й гуртки для проводу (2026-10-11).** Редагування картки як у вебі (`profile-edit.sheet.ts`): фото з
квадратним обрізанням на canvas (JPEG ≤1024px, якість 85, ліміт 5 МБ як `ImageUploadRules`) і «Очистити фото»;
email — лише без активованого акаунта або адміну (`canEditEmail`), «Надіслати лист ще раз» для неактивованого;
ступені УПЮ/УСП/УПС з датами — провід (сервер бере їх лише від `IsLeadership`, тож юнакові їх не показуємо, хоча
веб показує); верифікація профілю — рецензент на чужій картці, коли курінь її вмикає; перестороги 3 рівнів
(наступний після попереднього, знімає лише автор) — рецензент; «Видалити профіль» — Звʼязковий/А. Гуртки
(`group-edit.sheet.ts`): «Новий гурток» на вкладці «Курінь» і «Редагувати» на сторінці гуртка; сильветка обрізається
і обробляється як у вебі (коло 512px, «Чорно-біле», «Обводка», «Без обробки»). Обрізання — `image-crop.sheet.ts`
(тягнути, зводити пальці, повзунок). Е2Е: `e2e/member-edit.pw.ts`, `e2e/group-manage.pw.ts`.

**Люди в гуртку і новий учасник (2026-10-11).** «Новий учасник» — та сама форма `profile-edit.sheet.ts` у режимі
створення (як `upsert-member` без ключа): імʼя, по батькові, прізвище, email (обовʼязковий: на нього йде лист-запрошення,
акаунт створюється завжди, `createUserAccount=true`), телефон, дата народження (≥6 років, як `minAge`), фото з обрізанням,
ступені для проводу; після збереження відкривається картка нового учасника, а «лист не пішов» показується тостом.
Вхід: рядок «Новий учасник» у «Курінь → Учасники» (Звʼязковий/А, `POST member/kurins/{k}/members`) і в списку учасників
гуртка (за `check-access` group Create, `POST member` з `groupKey`). На сторінці гуртка: «Налаштувати провід» (ролі
гуртка, по одній людині з учасників гуртка або «Не обсаджено», `POST/PUT leadership`; права як `canSetupLeadership('group')`),
і для Звʼязкового — «Виховники» (кандидати з КВ, призначити/зняти), «Додати з куреня» (кілька людей одразу, ті, хто без
гуртка, першими) і дії рядка учасника «⋯»/свайп: «Перевести до іншого гуртка» і «Прибрати з гуртка»
(`PUT kurin/{k}/memberships/{m}/group`; людина лишається в курені). «Вивести з куреня» — на картці учасника, архів проводу — на сторінці «Провід» (обидва нижче).
Е2Е: `e2e/member-new.pw.ts`, `e2e/group-people.pw.ts`.

**Ставки куреня у вкладці гуртка, дані й звіт куреня (2026-10-11).** На вкладці гуртка в «Ставках» рядок
«Ставки куреня» для тих, кому сервер дає `canSetKurinRates` (у вебі — кнопка в шапці сторінки): той самий аркуш,
що на вкладці куреня (`DuesKurinRateForm`), пише в `PUT kurin/{kurinKey}/dues/rate` і перечитує касу гуртка. На
вкладці «Курінь» у картці куреня «Редагувати» для Звʼязкового й адміна (веб `canManageGroups`) — меню вебу:
«Редагування даних куреня» (`kurin-edit.sheet.ts`: імені, станиця, край, опис з лімітами API; `PUT kurin/{key}`
отримує весь запис назад, як у вебі; незмінену форму не шлемо — API на порожнє оновлення відповідає помилкою) і
«Експорт звіту куреня» (`GET kurin/{key}/report/pdf?timeZone=…`). Готовий PDF на телефоні з системним «Поділитися»
(iOS 15+, Android) відкриває ще один аркуш «Поділитися… / Зберегти файл»: share sheet відкривається лише з
свіжого дотику, а звіт сервер складає кілька секунд; деінде файл завантажується, як у вебі (`core/file-share.ts`).
Е2Е: `e2e/leader.pw.ts` (ставки куреня), `e2e/kurin-profile.pw.ts`.

**Планування таборів (2026-10-11).** Під «Календарем» (`/tabs/calendar/planning…`): рядок «Планування таборів» під
календарем і «Меню → Планування» (раніше відкривав веб) ведуть у застосунок. Список сесій (вікно, тривалість, люди,
«Розраховано»/«Нове»; видалення свайпом за `canDelete`) читають усі в курені, як у вебі. Сесія: вікно пошуку, оптимальні
дні з тими, хто тоді зайнятий (за правилом розвʼязувача), «Перенести в календар» (подія на весь курінь, як у вебі; кнопка
лише з правом створювати події — `canManageAgenda`, ціль «курінь» судить API), графік зайнятості й учасники з вагою голосу
та зайнятими днями, «Видалити планування» за `canDelete`. Графік вебу (Chart.js) на телефоні — смуга: стовпчик на день
вікна, імена закріплені ліворуч, дні гортаються вбік, табір і його дні підсвічені крізь усі рядки, відкривається на
днях табору. «Новий табір» (`canCreatePlanning`, інакше «Немає доступу»): назва, тривалість −/+ (1–30), вікно «Від/До»,
КВ куреня з «Важливістю голосу» і періодами зайнятості (обидва дні включно). Свідомі відмінності від вебу: кінець
інтервалу сервер читає як виключний (табір з 5-го на 10 днів закінчується опівночі 15-го, тож показуємо «5–14 липня»
і так само переносимо в календар — all-day кінець включний); період зайнятості «до 15-го» йде як північ 16-го (веб шле
північ 15-го і 15-те для розвʼязувача вільне); вікно за замовчуванням — найближче літо (у вебі — літо поточного року,
навіть восени); вікно, коротше за табір, не відправляється. Видалення API відповідає текстом, тож `Api.deleteText`.
Е2Е: `e2e/planning.pw.ts`.

**Членство, Заявки й розклад Головної (2026-10-11).** «Прийняти за кодом» (`join-by-code.sheet.ts`, веб join-by-code-dialog)
у «Курінь → Учасники» поруч із «Новий учасник» для Звʼязкового/А (веб `canManageGroups`): два кроки, як у вебі — код
(`GET kurin/{k}/memberships/candidate`, одна відповідь і на хибний, і на нічий код), картка людини, потім «Прийняти»
(`POST kurin/{k}/memberships` з `publicId`, `kind: Youth`); «вже в цьому курені» показується, але не приймається.
На картці учасника в «Членствах» під поточним членством свого куреня — «Змінити гурток» (аркуш з гуртками й «Поза
гуртком», `PUT …/memberships/{m}/group`) і «Вивести з куреня» з підтвердженням тексту вебу (`DELETE kurin/{k}/memberships/{m}`);
членство переходить у «Раніше». Колишніх із «Повернути» веб показує лише в Реєстрі (п. 35), тож у PWA їх немає.
Головна читає `GET user/me/layouts` (окремого читання однієї дошки API не має) і показує плитки в порядку дошки
`dashboard` без прихованих (ключі вебу: `my-duties`, `upcoming-events`, `my-tasks`, `my-probe`, `my-skills`, `my-score`,
`my-dues`; невідому розкладу плитку ставить на її типове місце, як `reconcileOrder`); стартує з кешу вебу
`tile-layout:dashboard` у localStorage (спільний origin), тож прихована плитка не блимає; без розкладу або без кешу
при збої — усе. «Заявки» (`features/admin/waitlist.page.ts`) — для системного адміна поза куренем, як пункти адмінки
в меню вебу: «Чекають рішення» / «Схвалені» / «Розглянуті» (статуси українською замість англійських назв вебу), тап —
аркуш «Схвалити й надіслати запрошення» / «Відхилити» (причина в алерті, тіло — JSON-рядок) або «Надіслати запрошення
ще раз»; крапка на «Заявки» в меню, поки щось чекає; сповіщення `/waitlist` відкриває екран. Е2Е: `e2e/membership.pw.ts`,
`e2e/waitlist.pw.ts`, `e2e/home-layout.pw.ts`.

**Встановлення з вебу (2026-10-11).** Люди відкривають веб на телефоні й не знають, що є застосунок; у сторах його
не буде. Три шматки. (1) `/m/install` (`pages/install.ts`, без входу): за UA і прапорцями (`pwa/install-guide.ts`, юніт-тести)
показує «вже встановлено»; кнопку «Встановити» в одне натискання там, де Chromium дав `beforeinstallprompt` (подію ловить
`main.ts` ще до старту Angular), і «Готово!» після; інакше, після ~2,5 с очікування, кроки з картинками для свого браузера
(`pwa/install-steps.ts`): Safari («Поділитися» або «⋯» на Safari 26 → «На початковий екран» → «Додати», плюс «увійди ще
раз» — у застосунку з екрана iOS окреме сховище), Chrome/Edge/Firefox на iOS, меню Chrome/Samsung на Android, компʼютер
(значок в адресному рядку, «Додати в Dock»); у вбудованих браузерах (Telegram, Instagram, Facebook, TikTok, Viber…, і будь-який
`; wv)`/WKWebView) — «Відкрий у Safari/Chrome», «Скопіювати посилання» і на Android `intent://` «Відкрити в Chrome». Картинки —
SVG у `public/install-art/` (не `public/install/`: nginx віддав би теку замість сторінки /install) (світла/темна за системою, як і сам інтерфейс, який вони показують; копії в `docs/user/images/install/`);
справжній скриншот замінює файл у таблиці `pwa/install-art.ts`. (2) На вході — «Встановити застосунок» або «Як встановити на
телефон»; картка Головної памʼятає «Не зараз» 14 днів, на iPhone веде на `/install`. (3) Веб: власний `manifest.webmanifest`
(той самий `id`, що в PWA — `./` розвʼязується від origin `start_url`, тобто `/`; `start_url /m/`, `scope /`, іконки з `/m/icons/`),
тож Chrome на Android ставить PWA просто з вебу; плашка «Лілейка на телефоні» (телефон за media query або UA, не в
standalone, «Не зараз» на 14 днів) — «Встановити» (діалог Chrome або `/m/install`) і «Відкрити мобільну версію» (той самий
екран у PWA, де очевидно). Пункт «Встановити на телефон» у бічному меню вебу. Довідка: `docs/user/howto/install-app.md`.
Телефон на вебі тепер **переадресовується** в PWA (прохання Роста: люди знають лише адресу вебу): `main.ts` вебу ще до
Angular (`mobile-redirect.function.ts`, юніт-тести) — телефон за media query `(max-width: 767px) and (pointer: coarse)` або UA
(Android-планшети без «Mobile» лишаються на вебі) → `location.replace` на відповідний екран PWA (з query) або `/m/`. Не
переадресовуються: `/m`, `/activate`, `/reset-password`, `/forgot-password`, `/join`, `/setup`, `/logout`, `/privacy`, `/about`;
`?web=1` — одне завантаження на вебі (так PWA відкриває свої вебові пункти меню, `webOnlyPage`); `lk.preferWeb=1` у localStorage —
вибір «Повна версія» (рядок у «Меню» і посилання на `/install`; іде як `?full=1`, бо в iOS застосунок з екрана має окреме
сховище). На вебі тоді рядок «Це повна версія сайту. Мобільна версія», що знімає вибір; плашка встановлення лишилась лише на
випадок, коли переадресації не було. У e2e вебу проєкт `mobile-chrome` іде з `lk.preferWeb` (і `auth.setup` зберігає його в стани
ролей), щоб і далі перевіряти сам веб на телефоні.
Е2Е: `e2e/install.pw.ts` (PWA), `Frontend/projectk-frontend/e2e/phone-redirect.spec.ts` (веб, без API).

## 21. Push на телефон (Web Push, 2026-10-11)

Те, що падає в дзвіночок (пункт 45), тепер приходить і на телефон, коли застосунок закритий. iPhone — лише з
Лілейки на початковому екрані (iOS 16.4+), Android Chrome — завжди. Нативних APNs/FCM немає: це стандартний
Web Push (RFC 8030) з VAPID (RFC 8292) і шифруванням `aes128gcm` (RFC 8291), усе на вбудованій криптографії .NET.

- **PWA:** у «Меню» рядок «Сповіщення на телефон» (поруч із «Вигляд»; у вебі відповідника немає). Стани: перемикач
  увімк./вимк.; «Сповіщення заборонені… в налаштуваннях телефону» після відмови; на iPhone у вкладці Safari —
  «Спершу додай Лілейку на початковий екран»; «Цей браузер не вміє показувати сповіщення»; без ключа на сервері
  рядка немає. Увімкнення питає дозвіл (з самого дотику — так вимагає iOS), підписується через `SwPush` з
  VAPID-ключем сервера і реєструє пристрій; вимкнення знімає підписку з обох боків. На кожному старті з входом
  (`TabsPage`) підписка надсилається знову (endpoint може змінитися; підписка під старим ключем робиться
  заново). «Вийти» спершу забуває цей пристрій на сервері й у браузері, щоб наступний на цьому телефоні не
  отримував чужих пушів. Код: `src/app/pwa/push.ts` (стани, юніт-тести), `push.service.ts`, рядок у `nav/nav-menu.ts`.
- **Показ і тап:** пуш — у форматі повідомлень Angular service worker
  (`{notification: {title, body, icon, tag, data: {onActionClick: {default: {operation: 'navigateLastFocusedOrOpen', url}}}}}`),
  тож ngsw сам показує його і на тап відкриває (або переводить відкритий застосунок на) `url` під `/m/`.
  Відповідність типів і маршрутів — та сама, що в `features/leader/notification-route.ts`, на сервері в
  `PushRoutes` (учасник → картка, вмілості на перевірку → черга, якщо це курінь, у якому застосунок, інакше
  дзвіночок; задача/подія → сторінка, видалена → список; решта → дзвіночок `/m/tabs/home/notifications`).
  `tag` — ключ дедуплікації (або тип), тож система замінює, а не складає стосом.
- **Бекенд:** таблиця `WebPushSubscriptions` (endpoint унікальний, ключі `p256dh`/`auth`, user agent; каскадно з
  акаунтом), міграція `AddWebPushSubscriptions` (написана вручну в стилі попередніх — у цьому середовищі немає
  `dotnet`; CI перевірить, що знімок моделі збігся). Ендпойнти: `GET api/notifications/push/public-key`
  (`publicKey: null`, коли push вимкнено), `POST api/notifications/push/subscriptions` (upsert за endpoint;
  дозволені лише хости push-сервісів: FCM, Mozilla, Apple, WNS — сервер потім сам туди POST-ить),
  `DELETE api/notifications/push/subscriptions?endpoint=…`. `NotificationService.NotifyAsync` після успішного
  збереження **нового** сповіщення кладе його в чергу (`IPushQueue`, канал у памʼяті, 1000, найстаріші
  відкидаються); повтор, що злився з непрочитаним за ключем дедуплікації, телефон не будить.
  `WebPushBackgroundService` відправляє у власному DI-scope; 404/410 — пристрій забувається, інші помилки лише
  в лог (без повторів). Пуш живе добу (`TTL`), VAPID-JWT — 12 год.
- **Налаштування (Ростові для проду, лише назви):** `WebPush:PublicKey`, `WebPush:PrivateKey`, `WebPush:Subject`
  (в App Service — `WebPush__PublicKey`, `WebPush__PrivateKey`, `WebPush__Subject`; у docker —
  `PROJECTK_WEBPUSH_PUBLIC_KEY` / `_PRIVATE_KEY` / `_SUBJECT`). Пара P-256 у base64url (публічний — 65 байт
  нестиснутий, приватний — 32 байти), напр. з `npx web-push generate-vapid-keys`; Subject — `mailto:` або `https:`
  (типово `mailto:projectk-system@…`). Без обох ключів push вимкнено. Зміна пари відʼєднує старі підписки
  (застосунок перепідпишеться на наступному старті). У `docker/env/e2e.env` — **лише тестова** одноразова пара для
  e2e-стека; більше ніде приватного ключа в репозиторії немає.
- **Перевірка:** юніт-тести бекенду — шифрування відтворює приклад з RFC 8291 і розшифровується ключем пристрою,
  JWT VAPID читається й перевіряється публічним ключем, розсилка на всі пристрої з видаленням 404/410, черга лише
  для нових сповіщень, upsert/видалення підписок, фільтр endpoint. Е2Е `e2e/push.pw.ts` (обидва проєкти) з
  підробленими в сторінці service worker / PushManager / Notification (Playwright не має справжнього push-сервісу,
  а WebKit — Push API): увімк./вимк., повторна реєстрація після перезавантаження, «заборонено», «додай на
  екран» (iPhone), «не вміє» (Android), без ключа рядка немає, вихід забуває пристрій. Справжній API —
  `e2e/real-push.pw.ts` у job `pwa-real-api`: ключ віддається, підписка 200 (і повторно), чужий хост 400,
  відписка, перемикач у «Меню» проти справжнього API.
- **Відкрито:** справжня доставка на телефон перевіряється лише вручну на проді/стейджингу після ключів (у CI
  push-сервіси недосяжні); Android показує стандартну іконку в рядку стану (монохромного `badge` ще немає);
  черга в памʼяті — пуші, що не встигли піти до перезапуску API, губляться (сповіщення в дзвіночку лишаються).
**КВ і провід куреня (2026-10-11).** На вкладці «Курінь» у секції «КВ» рядок «Архів впорядників» (Звʼязковому й адміну —
«Впорядники й архів») веде на `/tabs/kurin/kv` (`kv.page.ts`, веб kv-panel): Звʼязковий і впорядники з гуртками, архів
знятих призначень з періодом (усі). Звʼязковий/А (веб `canManageMentors`): «Додати впорядника» й олівець на рядку —
аркуш `kv-mentor.sheet.ts` (учасник з акаунтом з `mentor-candidates`, гуртки галочками; `POST/DELETE group/{g}/mentors/{u}`
лише для змінених), «Передати роль Звʼязкового» — вибір, підтвердження, `GET leadership/type/kv/{k}` і `PUT` того самого
проводу КВ з новим Звʼязковим (решта КВ лишається; без проводу КВ — `POST`). Права походять з ролей, тож хто віддав
роль (не адмін) одразу бере новий токен для того самого куреня (`auth/kurin-scope`) і втрачає керування на екрані; веб
лише оновлює токен. У секції «Провід куреня» — «Архів проводу» (`/tabs/kurin/leadership`, `leadership.page.ts`: «Зараз» і
«Архів» з періодами, пошук в архіві від 9 записів, шестерня для тих, хто може садити провід) і «Налаштувати провід»
(веб `canSetupLeadership('kurin')`: Звʼязковий, А, курінний). Форма — спільний для куреня й гуртка аркуш
`leadership.sheet.ts` (колишній `group-leadership.sheet.ts`; правила в `leadership-seats.ts`): ролі з
LEADERSHIP_ROLE_MAP, учасник на роль з куреня чи гуртка або «Не обсаджено», кілька людей на роль там, де веб дозволяє
(Впорядник, Інструктор), перемикач «Архів». Курінному рядок «Курінний» показано без вибору: сервер (AssignableOffices)
не дає йому садити себе. Та сама сторінка проводу є й для гуртка (`/tabs/kurin/group/:g/leadership`, рядок «Архів
проводу» на сторінці гуртка). Е2Е: `e2e/kv-leadership.pw.ts`.
