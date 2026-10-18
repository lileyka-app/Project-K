# Спостережуваність у проді

API надсилає телеметрію ASP.NET Core в ресурс Application Insights, заданий змінною
`APPLICATIONINSIGHTS_CONNECTION_STRING`. Той самий ресурс отримує трейси Serilog через
`Serilog__WriteTo__1__Args__connectionString`.

Обидва значення в проді мають бути посиланнями на Key Vault в App Service. Рядок підключення чи
instrumentation key не потрапляє ні в репозиторій, ні в запити KQL.

## Перші перевірки після деплою

Зроби запит на `/health`, зачекай хвилину на інжест і виконай:

```kusto
requests
| where timestamp > ago(30m)
| summarize Requests=count(), Failed=countif(success == false), P95=percentile(duration, 95)
```

Окрім `traces`, API має давати `requests` і `dependencies`. Необроблена помилка запиту зʼявляється в
`exceptions`.

## Корисні запити

### Невдалі запити

```kusto
requests
| where timestamp > ago(24h)
| where success == false or toint(resultCode) >= 400
| project timestamp, resultCode, duration, name, url, operation_Id
| order by timestamp desc
```

### Винятки

```kusto
exceptions
| where timestamp > ago(24h)
| project timestamp, type, outerMessage, problemId, operation_Id
| order by timestamp desc
```

### Відновлення пароля і запрошення

```kusto
union isfuzzy=true
(
    requests
    | where timestamp > ago(7d)
    | where url has_any ("password-reset", "invitation/resend", "activate")
    | project timestamp, itemType="request", name, resultCode, success, operation_Id
),
(
    traces
    | where timestamp > ago(7d)
    | where message has_any ("PasswordReset", "Invitation", "RequestPasswordReset")
    | project timestamp, itemType="trace", name="", resultCode="", success="", operation_Id, message
)
| order by timestamp desc
```

### Аудит безпеки

Безпекові події (`Auth.*`, `Mfa.*`, `Dev.Impersonate`, призупинення акаунтів) пишуться через
`IActivityLogger` як структуровані трейси:

```kusto
traces
| where timestamp > ago(7d)
| where customDimensions.Action startswith "Auth." or customDimensions.Action startswith "Mfa."
| project timestamp, customDimensions.Action, customDimensions.ActorUserId, customDimensions.TargetUserId, message
| order by timestamp desc
```

### Один запит цілком

Візьми `operation_Id` із запиту, трейсу чи винятку і подивись увесь ланцюжок:

```kusto
union isfuzzy=true requests, dependencies, exceptions, traces
| where operation_Id == "<operation-id>"
| project timestamp, itemType, name, target, resultCode, success, message, type
| order by timestamp asc
```

## Доступ

У ресурсу Application Insights має бути увімкнений мережевий доступ до запитів. Людині, що
дивиться логи, потрібна щонайменше роль `Monitoring Reader` на ресурсі або його групі. Керована
ідентичність API до цього не стосується: їй потрібен лише доступ до Key Vault для посилань на
рядок підключення.

## Health-ендпоінт

`GET /health` відповідає без автентифікації; фронтенд опитує його і показує банер «сервер
прокидається», поки App Service холодний. Той самий ендпоінт використовує healthcheck контейнера в
self-host. Базу він не опитує: каже, що процес обслуговує запити, а не що здорове все за ним.

```json
{"status":"ready","version":"v1.2.1","codeName":null,"utc":"…","api":{"windowMinutes":15,"requests":42,"p50Ms":3.1,"p95Ms":18.4}}
```

- `version` і `codeName` — з `ReleaseInfo`; `codeName` — `null`, коли збірка без кодової назви.
- `api` — власний час API на справжніх запитах за останні 15 хвилин: кількість, медіана й 95-й
  перцентиль у мілісекундах. Його рахує `RequestTimingMiddleware` — перший у конвеєрі, тож час
  охоплює весь middleware після нього, — і тримає `RequestTimings`: лише в памʼяті, не більше
  5000 замірів. `/health`, корінь `/`, `OPTIONS` (CORS-preflight) і `HEAD` не враховуються, бо
  відповідають за мікросекунди й тягнули б медіану до нуля. Без жодного запиту `p50Ms` і
  `p95Ms` — `null`.

Саме `api` малює сторінка статусу (`docs/dev/operations/status-page.md`): раніше вона показувала
час власного запиту проби, тобто мережу від колокації Cloudflare (150–670 мс), а не сервер.

## Телеметрія

Раз на добу `TelemetryBackgroundService` надсилає анонімний пінг на `Telemetry:Endpoint`
(`/v1/ping` сервісу статусу): випадковий id інсталяції (ключ `Telemetry__InstanceId` у
`SystemSettings`), версію, тип середовища, коди країв, кількість куренів, членів і активних
користувачів за 7 і 30 днів, кількість запитів і відповідей 5xx від попереднього пінгу, години
роботи процесу. Перший пінг — через `Telemetry:InitialDelayMinutes` (5) після старту, далі кожні
`Telemetry:IntervalHours` (24). Невдалий пінг лише пишеться в лог.

`Telemetry:Enabled` увімкнений у `Production` і `SelfHost` (на self-host — `PROJECTK_TELEMETRY`),
в усіх інших середовищах вимкнений. Вмикає лише `true`, `1`, `yes` чи `on`; будь-яке інше значення —
вимкнено. Коли пінг вимкнений, служба навіть не реєструється.

Запити рахує `TelemetryCountingMiddleware`: він стоїть зовні обробника винятків, тож запит, що
закінчився його 500, рахується як помилка; `/health`, який проба контейнера смикає кожні 15
секунд, не рахується. Лічильники живуть у памʼяті й обнуляються з кожним пінгом.

Рядки в лозі API:

| Рядок | Коли |
|---|---|
| `Telemetry: an anonymous daily ping goes to {Endpoint}; Telemetry:Enabled=false turns it off.` | на старті служби |
| `Telemetry ping sent.` | пінг дійшов |
| `Lilejka {Latest} is out; this installation runs {Version}.` | пінг дійшов, а у відповіді новіший реліз (warning) |
| `Telemetry ping answered HTTP {Status}.` | сервіс відповів не 2xx (warning) |
| `Telemetry ping failed; the next one goes in {Hours} h.` | мережа, тайм-аут, виняток (warning) |

## Прогрів після старту

`/health` не чіпає ні MediatR, ні AutoMapper, ні EF, тож інстанс міг би день відповідати пробам і
лишатися холодним: перший дашборд після деплою на одному ядрі B1 відкривався 13 с. Тому
`StartupWarmUpService`, щойно сервер почав слухати, компілює мапінги AutoMapper, піднімає MediatR і
проганяє запити дашборда з порожнім ключем (EF кешує скомпільований запит за формою, а не за
значеннями). Вмикає його `Startup:WarmUp`; без ключа він працює всюди, крім `Development`,
`Testing` і `E2E`. У лозі — `Warm-up finished in {ElapsedMs} ms.` або warning
`Warm-up failed after …`; невдалий прогрів коштує лише швидкості першого запиту.

## Рівні логів

У `Production` і `Staging` Serilog притишує `Microsoft.EntityFrameworkCore` і
`Serilog.AspNetCore.RequestLoggingMiddleware` до `Warning` (`Serilog:MinimumLevel:Override` в
`appsettings.{Production,Staging}.json`), щоб кожен SQL-запит і кожен HTTP-запит не ставав окремим
трейсом. Помилки EF і запити, що впали, лишаються в `traces`, а `requests` і `dependencies`
Application Insights збирає сам, незалежно від Serilog.
