---
title: Архітектура в діаграмах
description: Чотири рівні одним поглядом — клієнт і сервер, шлях запиту, шари й модулі моноліту, прод проти локальних заглушок.
sidebar:
  order: 2
---

Лілейка — **модульний моноліт**: один процес `.NET`, одна база, один образ на всі середовища, але
всередині девʼять модулів із контрактами між ними, які колись можна винести окремо. Словами це
описано в [Архітектурі](/dev/core/architecture/); тут ті самі речі намальовані. Діаграми
підлаштовуються під ширину екрана і тему сайту.

## 1. Клієнт і сервер

Браузер тримає Angular-застосунок і ходить до одного API. Усе, що API потрібно ззовні, стоїть
праворуч: база, сховище файлів, пошта, GitHub для звітів про проблеми, каталог вмілостей і проб,
сервіс статусу для щоденного пінгу, логи.

```mermaid
flowchart TB
    SPA["Браузер · Angular 22<br/>standalone · signals"]
    PROXY["Cloudflare або nginx<br/>TLS · адреса відвідувача"]

    subgraph api["Моноліт ProjectK.API · .NET 10"]
        direction TB
        HTTP["HTTP · /api/*<br/>JWT · політики · ResourceAuthorize"]
        AUTH["AuthModule"]
        USERS["UsersModule"]
        KURIN["KurinModule"]
        PB["ProbesAndBadges<br/>Module"]
        INFRA["Infrastructure<br/>Module"]
        DUES["DuesModule"]
        SCORE["ScoreModule"]
        KRAI["KraiModule"]
        ME["MeModule"]
        HTTP --> AUTH & USERS & KURIN & PB & INFRA
        HTTP --> DUES & SCORE & KRAI & ME
    end

    subgraph ext["Зовнішні служби"]
        direction LR
        DB[("SQL Server")]
        BLOB[("Blob Storage")]
        MAIL["Resend"]
        GH["GitHub Issues"]
        CAT["Сервіс каталогу<br/>GET /v1/catalog"]
        STATUS["Сервіс статусу<br/>POST /v1/ping · GET /health"]
        LOGS["Serilog · App Insights · Telegram"]
    end

    SPA -- "HTTPS · access-токен у заголовку<br/>refresh у httpOnly-cookie" --> PROXY
    PROXY --> HTTP
    KURIN ~~~ DB
    api --> DB & BLOB & MAIL & GH & CAT
    api -. "щоденний пінг" .-> STATUS
    STATUS -. "перевірка /health" .-> PROXY
    api -.-> LOGS
```

Фронтенд і сайт довідки — окрема статика зі своїм деплоєм; API нічого не рендерить.

## 2. Шлях запиту

Що відбувається з одним `PUT`, поки він стане відповіддю. Два кроки авторизації розділені
навмисно: політика каже, чи людина взагалі такого рівня, `ResourceAuthorize` — чи саме цей
обʼєкт у її сфері.

```mermaid
sequenceDiagram
    autonumber
    participant B as Браузер
    participant M as Middleware
    participant A as Авторизація
    participant C as Контролер
    participant H as MediatR · хендлер
    participant R as Репозиторії · IUnitOfWork
    participant DB as SQL Server

    B->>M: HTTP-запит з access-токеном
    M->>M: Serilog · forwarded headers · rate limiting
    M->>A: JWT перевірено, сесія — рядок у UserRefreshTokens
    A->>A: політика AuthorizationPolicies.*
    A->>A: ResourceAuthorize → IResourceAccessService (Own · OwnGroups · KurinWide)
    A->>C: 403, якщо будь-який крок відмовив
    C->>H: _mediator.Send(команда)
    H->>H: FluentValidation → доменне рішення
    H->>R: читання й зміни через Common-інтерфейси
    R->>DB: SaveChangesAsync
    DB-->>R: ок
    R-->>H: сутності
    H-->>C: ServiceResult<T>
    C-->>B: ToActionResult → JSON або { error, message }
```

Контролер не приймає рішень: усе, що він робить, — `_mediator.Send(...)` і переклад
`ServiceResult<T>` у HTTP в одному місці, `ServiceResultExtensions.ToActionResult`.

## 3. Шари й модулі всередині моноліту

Чотири проєкти з односторонніми залежностями. `BusinessLogic` та `Infrastructure` не знають одне
про одного: інтерфейс живе в `Common`, реалізація — в `Infrastructure`. Модулі не читають таблиць
одне одного, а питають через контракти в `Common/Interfaces/Modules/`; `ProjectK.Architecture.Tests`
читає IL і валить збірку, якщо хтось перетнув межу.

```mermaid
flowchart TB
    subgraph API["ProjectK.API"]
        direction LR
        CTRL["Контролери по модулях<br/>Auth · Users · Kurin · ProbesAndBadges · Infrastructure<br/>Dues · Score · Krai · Me"]
        POL["AuthorizationPolicies<br/>ResourceAuthorize"]
        DI["Program.cs · DI · Serilog<br/>middleware · прогрів після старту"]
    end

    subgraph BL["ProjectK.BusinessLogic"]
        direction LR
        MODS["Modules/*<br/>команди · запити · хендлери · валідатори"]
        ACCESS["AgendaAccess · RolePermissionMap<br/>доменні правила доступу"]
    end

    subgraph COMMON["ProjectK.Common"]
        direction LR
        ENT["Сутності<br/>Member · Membership · Kurin · Group · AgendaItem …"]
        IFACE["Інтерфейси<br/>IUnitOfWork · IEmailService · IBlobStorageService …"]
        CONTRACTS["Контракти між модулями<br/>IMemberDirectory · IMembershipDirectory<br/>IOfficeDirectory · IMemberProgressDirectory<br/>IDuesDirectory · IKraiProfileProvider"]
    end

    subgraph INFRA["ProjectK.Infrastructure"]
        direction LR
        DBC["DbContext · міграції · репозиторії"]
        SVC["Сервіси<br/>Resend · Blob · QuestPDF · GitHub · JWT"]
        BG["Фонові служби<br/>аудит · перестороги · осиротілі фото · архів задач<br/>каталог · телеметрія · сповіщення · демо-скидання"]
    end

    EXT["Зовнішнє<br/>пакет ProjectK.Optimization · API каталогу · сервіс статусу"]

    API --> BL
    API --> INFRA
    BL --> COMMON
    INFRA --> COMMON
    BL --> EXT
```

Модулі однакові по обидва боки межі: `BusinessLogic/Modules/AuthModule` і
`API/Controllers/AuthModule` — про одне й те саме. Мембер від початку будувався так, щоб його
можна було винести в окремий сервіс: контракт замість репозиторію, ключ замість навігації.

## 4. Прод і локальні заглушки

Один образ, середовище обирається через `ASPNETCORE_ENVIRONMENT`, а зовнішні служби — через
конфігурацію. Вибір робиться один раз на старті, тож локальний стек чи self-host без ключів не
падає в момент, коли треба надіслати лист або створити issue.

```mermaid
flowchart LR
    subgraph local["Development · E2E · SelfHost без ключів"]
        direction TB
        ME["MockEmailService<br/>лист іде в лог"]
        AZU["Azurite<br/>UseDevelopmentStorage=true"]
        LOGR["LogProblemReporter<br/>звіт іде в лог"]
        FILE["Файл і консоль"]
        SEED["Демо-курінь і акаунти<br/>DevToolsController"]
    end

    subgraph ifaces["Інтерфейси в Common"]
        direction TB
        IE["IEmailService"]
        IB["IBlobStorageService"]
        IP["IProblemReporter"]
        IL["Serilog"]
        IS["Сідер і дев-інструменти"]
    end

    subgraph prod["Production · Staging"]
        direction TB
        RE["ResendEmailService<br/>Email:Provider = Resend"]
        AZ["Azure Blob Storage<br/>ConnectionStrings:BlobStorage"]
        GHR["GitHubProblemReporter<br/>Feedback:GitHub:Token"]
        AI["Application Insights<br/>Telegram-сінк для алертів"]
        NOSEED["Без демо-даних<br/>без DevToolsController"]
    end

    ME --- IE --> RE
    AZU --- IB --> AZ
    LOGR --- IP --> GHR
    FILE --- IL --> AI
    SEED --- IS --> NOSEED
```

| Що перемикає | Ключ | Прод | Локально |
|---|---|---|---|
| Пошта | `Email:Provider` | `Resend` (`Email:ApiKey`) | будь-що інше → `MockEmailService` |
| Файли | `ConnectionStrings:BlobStorage` | рядок Azure | порожньо → `UseDevelopmentStorage=true` (Azurite) |
| Звіти про проблеми | `Feedback:GitHub:Token` | є → GitHub Issues | нема → лог |
| Демо-дані | `ASPNETCORE_ENVIRONMENT` | `Production`, `Staging`, `SelfHost`, `Tailscale`: нічого | `Development`, `E2E`, `Demo`: демо-курінь, стирається при старті (`Demo` — ще й щоночі) |
| Дев-контролери | `ASPNETCORE_ENVIRONMENT` | знімаються з моделі | `Development`, `E2E`, `Tailscale` |
| Телеметрія | `Telemetry:Enabled` | `Production`, `SelfHost`: щоденний пінг сервісу статусу (`PROJECTK_TELEMETRY=false` вимикає на self-host) | вимкнена, служба не реєструється |
| Прогрів | `Startup:WarmUp` | без ключа ввімкнений: після старту компілює мапінги й запити дашборда | `Development`, `Testing`, `E2E`: вимкнений |

`SelfHost` живе між двома колонками: заглушки вмикаються не середовищем, а відсутністю ключа, тож
станиця може поставити систему без пошти й GitHub і додати їх пізніше, не міняючи образ.
