---
title: Пошта
description: Як система надсилає листи, які є провайдери й де що налаштовується.
sidebar:
  order: 3
---

Абстракція — інтерфейс `IEmailService` у `ProjectK.Common`:

- `SendEmailAsync(to, subject, body)` — довільний HTML-лист;
- `SendInvitationEmailAsync(to, token)` — запрошення з посиланням `{BaseUrl}/activate/{token}`;
- `SendPasswordResetEmailAsync(to, token)` — відновлення пароля з посиланням на `/reset-password`;
- `SendEmailChangeConfirmationEmailAsync(to, currentEmail, confirmationUrl)` — підтвердження нової
  адреси акаунта;
- `SendWaitlistSubmittedEmailAsync(to, applicantName, claimedKurin)` — адміністраторам про нову
  заявку куреня; разом із листом іде сповіщення в застосунку (`INotificationService`).

## Провайдери

Провайдер обирається ключем `Email:Provider` у `appsettings.{Environment}.json`:

- **`Mock`** — значення за замовчуванням (`MockEmailService`). Нічого не надсилає, пише лист у лог
  разом із токеном — так локально можна пройти активацію без пошти.
- **`Resend`** — робочий провайдер (`ResendEmailService`) через офіційний SDK. Використовується на
  staging і в проді.

Реєстрація в DI — `ProjectK.Infrastructure/DependencyInjection.cs`, метод `AddEmail`: усе, що не
`Resend`, стає `MockEmailService`, тож self-host без ключа не падає на першому листі.

## Шаблон листа

Кожен лист — запис `Letter` (заголовок, абзаци, кнопка), який `ResendEmailService` збирає в одну
рамку `Frame`: банер `assets/images/email-banner.png` (з домену фронтенду), заголовок, текст, зелена
кнопка і той самий лінк текстом для клієнтів, що ріжуть кнопки, примітка про теку «Спам», футер.
Табличний макет, усі стилі інлайном, `<meta charset>` у голові — інакше поштові клієнти показують
кракозябри. Поруч із HTML іде текстова частина (`Letter.PlainText()`) і заголовок `Reply-To` —
обидва потрібні, щоб лист не потрапляв у спам. Тексти українською.

Банер рендериться з `public/assets/lileyka-banner-1080x288.svg` скриптом
`Frontend/projectk-frontend/scripts/render-email-banner.mjs` (Playwright, 1200×320).

## Налаштування (`EmailSettings`)

Секція `Email` в `appsettings`:

- `Provider` — `Mock` / `Resend`; `ApiKey` — ключ Resend, у секретах середовища, не в репозиторії;
- `FromName`, `FromEmail` — відправник («Лілейка», `projectk-system@mail.lileyka.app` на
  верифікованому піддомені `mail.lileyka.app`);
- `ReplyTo` — адреса для відповідей (`hello@…`), щоб на системний лист можна було відповісти;
- `RedirectAllTo` — якщо задано, **кожен** лист іде на цю адресу, а справжній одержувач лишається в
  темі й у заголовку `X-Original-To`. На staging стоїть `delivered@resend.dev`: пошта проходить
  через Resend по-справжньому, але нікому не приходить;
- `BaseUrl` — адреса фронтенду для посилань у листах, для банера і для QR-коду «Встановити на
  телефон» (`GET api/phone-app/install-code` кодує `{BaseUrl}/m/install`). У Docker це
  `PROJECTK_PUBLIC_URL`; хибна адреса ламає разом і листи, і цей код.

## Де використовується

- запрошення — `ProvisionMemberAccountCommandHandler` (новий учасник з акаунтом, імпорт складу),
  `ApproveWaitlistEntryCommandHandler`, `ResendInvitationCommandHandler`;
- відновлення пароля — `RequestPasswordResetCommandHandler`;
- зміна адреси акаунта — `UpdateAccountProfileCommandHandler`;
- нова заявка куреня — `SubmitWaitlistRegistrationCommandHandler`, лист кожному адміністратору.

:::caution
На dev-стеку з `Provider: Resend` і недійсним ключем створення учасника з акаунтом падає на
відправці листа, хоча акаунт і запрошення вже записані. Для локальних перевірок став `Mock`.
:::
