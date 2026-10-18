---
title: Автентифікація й доступ
description: Вхід, сесії, MFA, онбординг через список очікування і як перевіряються права.
sidebar:
  order: 2
---

Огляд для розробника. Модель прав (діловодства → ролі → дозволи) описана в
[Архітектурі](/dev/core/architecture/), тут — маршрути й механіка.

## Вхід і сесії

`AuthController` (`AuthModule`), JWT Bearer з refresh-сесіями.

- `POST /register/kurin` — адмін створює курінь разом з першим акаунтом Звʼязкового;
  `POST /register` — акаунт для людини, яка вже є в курені.
- `POST /login` — вхід. Якщо в акаунта є другий фактор і пристрій ще не довірений, відповідь несе
  `requiresMfa` і короткоживучий `mfaToken` замість токенів.
- `POST /mfa/login-verify` — другий крок: код з автентифікатора або резервний код разом з
  `mfaToken`. Успіх ставить cookie `mfaTrust` (`Security:MfaTrustDays`, 7 днів), і наступні входи з
  цього пристрою минають другий крок, поки не зміниться security stamp акаунта.
- `POST /refresh` · `POST /logout` — оновлення й завершення сесії. Сесія — рядок у
  `UserRefreshTokens`; вхід додає рядок, вихід завершує один, зміна пароля чи MFA завершує всі.
  Вихід не чіпає `mfaTrust`.
- `POST /kurin-scope` — вибір активного куреня; токен доступу несе ролі саме для нього.
  `GET /kurin-scope/options` — з яких куренів можна обирати (усі активні членства).
  `GET /kurin-scope/options` — з яких куренів можна обирати (усі активні членства).
- `POST /check-access` — серверна перевірка доступу до конкретного ресурсу, якою користується
  фронтенд, щоб ховати кнопки за тим самим рішенням, що й ендпоінт.

## Онбординг: список очікування → запрошення → активація

`OnboardingController`. Потік: заявка → модерація → запрошення → активація.

- `POST /waitlist` — заявка (зараз лише від Звʼязкового куреня). Кожен адміністратор отримує
  лист і сповіщення в застосунку; у сайдбарі та на кнопці «Заявки» зʼявляється крапка, поки є що
  розглянути. `GET /waitlist` — список для адміна;
- `GET /krais` — коди й назви країв для форми, без входу (лише це: кількість куренів бачить
  адмін у `GET api/krai`). Заявка несе `kraiCode`; без нього обовʼязковий вільний текст
  `regionOrCountry`, як надсилали старі форми;
- `POST /waitlist/{key}/approve` — схвалити: створює курінь і акаунт, надсилає запрошення.
  Необовʼязкове тіло `{ "kraiCode": "pl" }` замінює край із заявки; без жодного курінь
  відкривається в `Krai:Default`. Зайнятість числа перевіряється в тому краї, де курінь
  відкриється;
  `POST /waitlist/{key}/reject` — відхилити; `POST /waitlist/{key}/resend-invitation` — надіслати
  ще раз; `POST /invitation/resend` — те саме для будь-якого неактивованого акаунта; `POST /invitation/resend` — те саме для будь-якого неактивованого акаунта;
- `GET /invitation/{token}/validate` — перевірити токен;
- `POST /activate` — задати пароль. Відповідь — `LoginUserResponse` з refresh-cookie: людина
  одразу в застосунку;
- `POST /password-reset/request` · `POST /password-reset/reset` — відновлення пароля. Запит також
  надсилає нове запрошення, якщо акаунт ще не активований.

Листи йдуть через `IEmailService` — див. [Пошта](/dev/guides/email/). Сутності: `WaitlistEntry`,
`Invitation`.

## MFA

TOTP. `GET /mfa/setup` (секрет і QR) → `POST /mfa/enable` (код) → `POST /mfa/recovery-codes`
(резервні коди, за поточним паролем). `GET /mfa/status` каже, чи увімкнено і чи вимагається.

**Примус для проводу:** `PrivilegedMfaEnforcementMiddleware` + `MfaEnforcementPolicy` вимагають
другий фактор для адміністратора і Звʼязкового на `Production` і `Staging`; `Security:EnforcePrivilegedMFA`
керує цим на self-host. Ключ `E2E:BypassPrivilegedMfa` читається лише на тестових тирах. Гейт не ходить
у базу: access-токен несе клейм `amr` (`mfa` / `pwd`, `AccessTokenClaims`), записаний при видачі —
`login`, `refresh`, активація. Увімкнення MFA (`mfa/enable`) обриває всі сесії й одразу віддає нову
(`Tokens` у відповіді + cookie), тож старий токен «лише пароль» не доживає до відмови; вимкнення теж
обриває всі сесії, і токен без другого фактора живе щонайбільше `Jwt:ExpiresInMinutes`. Фронтенд:
`mfa-setup-dialog` (обовʼязковий діалог після входу), `account-settings`.

## Дозволи на бекенді

- `[Authorize(Policy = …)]` — грубі політики (`RequireUser`, `RequireAdmin`, `RequireKurinManagement`…).
- `[ResourceAuthorize(ResourceType, ResourceAction, "route:key")]` — дозвіл на конкретний ресурс:
  `RolePermissionMap` перекладає ролі з діловодств у дозволи зі скоупом (`KurinWide`, `OwnGroups`, `Own`),
  а `AccessContextResolver` визначає, в якому курені людина стоїть і які діловодства тримає.
- Активне закріплення впорядника за гуртком (`MentorAssignment`) трактується як діловодство впорядника в КВ.
- Обидві перевірки покриті тестом `AuthorizationBaselineMatrixTests`: кожна дія контролера має
  бути в матриці, інакше тест падає.

## Guard-и (Angular)

- `authGuard` — вимагає автентифікації;
- `kurinAccessGuard('kurin' | 'panel')` — активний курінь у скоупі, або адмін-панель;
- `capabilityGuard('admin' | 'kurinManagement' | 'groupLeadership')` — можливість за роллю, ті
  самі поняття, що й політики на бекенді;
- `leadershipAccessGuard` — сторінка складу проводу: курінний бачить провід куреня, гуртковий —
  свого гуртка, КВ не редагується тут узагалі (`PermissionService.canSetupLeadership`);
- `EntityGuard` — доступ до конкретної сутності через `check-access`, **fail-closed**: помилка
  сервера означає «заборонено»;
- `setupGuard` — майстер первинного налаштування;
- `publicAuthRedirectGuard` — вже автентифікованих з публічних сторінок веде додому.

Принцип: guard-и ховають, бекенд вирішує. Жодна перевірка на фронті не є останньою.

## Локальні дев-інструменти

На `Development`, `E2E`, `Tailscale` є `DevToolsController` (`api/dev/impersonate`,
`api/dev/impersonate/member`, `api/dev/return`) і перемикач ролей на правому краю екрана: адмін
заходить за діловодство у курені або як людина з відкритої картки і повертається квитком. Квиток
живе 12 годин; прострочений фронт викидає й позичає роль на поточній сесії, замість того щоб падати
на кожному перемиканні. На інших тирах контролер знімається з моделі
(`DevOnlyControllerFeatureProvider`).
