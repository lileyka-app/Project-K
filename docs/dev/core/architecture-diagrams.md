---
title: Архітектура в діаграмах
description: Чотири рівні одним поглядом — клієнт і сервер, шлях запиту, шари й модулі моноліту, прод проти локальних заглушок.
sidebar:
  order: 2
---

Лілейка — **модульний моноліт**: один процес `.NET`, одна база, один образ на всі середовища, але
всередині чотирнадцять модулів із контрактами між ними, які колись можна винести окремо. Словами це
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
        MEMBER["MemberModule"]
        AGENDA["AgendaModule"]
        ARCHIVE["ArchiveModule"]
        ROSTER["RosterModule"]
        PB["ProbesAndBadges<br/>Module"]
        INFRA["Infrastructure<br/>Module"]
        DUES["DuesModule"]
        SCORE["ScoreModule"]
        KRAI["KraiModule"]
        ME["MeModule"]
        AUDIT["AuditModule"]
        HTTP --> AUTH & USERS & KURIN & MEMBER & AGENDA
        HTTP --> ARCHIVE & ROSTER & PB & INFRA
        HTTP --> DUES & SCORE & KRAI & ME & AUDIT
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
    participant P as Поведінки MediatR
    participant H as Хендлер · Mapping
    participant R as Репозиторії · UoW
    participant DB as SQL Server

    B->>M: HTTP-запит з access-токеном
    M->>M: Serilog · forwarded headers · rate limiting
    M->>A: JWT перевірено, сесія — рядок у UserRefreshTokens
    A->>A: політика AuthorizationPolicies.*
    A->>A: ResourceAuthorize → IResourceAccessService (Own · OwnGroups · KurinWide)
    A->>C: 403, якщо будь-який крок відмовив
    C->>P: _mediator.Send(команда)
    P->>P: RequestTiming → Validation (400 ValidationFailed) → Transaction
    P->>H: команда пройшла валідацію
    H->>R: читання й зміни через фасади UoW і контракти модулів
    H->>H: Mapping області — ApplyTo для зміни, ToDto для відповіді
    R->>DB: SaveChangesAsync, інтерсептори збереження
    DB-->>R: ок
    R-->>H: сутності
    H-->>C: ServiceResult<T> (без даних — ServiceResult<Unit>)
    C-->>B: ToActionResult → JSON, 204 для Unit, або { error, message }
```

Контролер не приймає рішень: усе, що він робить, — `_mediator.Send(...)` і переклад
`ServiceResult<T>` у HTTP в одному місці, `ServiceResultExtensions.ToActionResult`. Код помилки
хендлер бере з каталогу свого модуля (`<Module>Errors`).

## 3. Шари й модулі всередині моноліту

Чотири проєкти з односторонніми залежностями. `BusinessLogic` та `Infrastructure` не знають одне
про одного: інтерфейс живе в `Common`, реалізація — в `Infrastructure`. Модулі не читають таблиць
одне одного, а питають через контракти в `Common/Contracts/<Module>/`, і ці контракти лише
відповідають; змінити щось у чужому модулі можна подією, яку той модуль обробить сам, а коли
потрібні одна транзакція й відповідь одразу — його контрактом запису `I…Writer`.
`ProjectK.Architecture.Tests` читає IL і валить збірку, якщо хтось перетнув межу. `Common` розкладений
спершу за видом, потім за модулем: `Entities/`, `Contracts/`, `Dtos/`, `Records/`, `Events/`, `Enums/`,
`Rules/`, `Options/`.

```mermaid
flowchart TB
    subgraph API["ProjectK.API"]
        direction LR
        CTRL["Controllers по модулях<br/>Auth · Users · Kurin · Member · Agenda · Archive · Roster<br/>ProbesAndBadges · Infrastructure · Dues · Score · Krai · Me · Audit"]
        POL["Authorization · Filters<br/>AuthorizationPolicies · ResourceAuthorize"]
        DI["Program.cs · Hosting · Middleware<br/>Cookies · прогрів після старту"]
    end

    subgraph BL["ProjectK.BusinessLogic"]
        direction LR
        MODS["Modules/*/Features/*<br/>команда чи запит з валідатором і хендлером<br/>Mapping області · ToDto · ApplyTo"]
        SVCS["Services · Events · каталоги помилок<br/>рушій прав AuthModule · Access модулів"]
        BEH["Behaviors<br/>час · валідація · транзакція"]
    end

    subgraph COMMON["ProjectK.Common"]
        direction LR
        ENT["Entities<br/>Member · Membership · Kurin · Group · AgendaItem …"]
        IFACE["Contracts<br/>IUnitOfWork · IEmailService · IPhotoService …"]
        CONTRACTS["Контракти між модулями<br/>IMemberDirectory · ICurrentMember · IMembershipDirectory<br/>IOfficeDirectory · IMemberProgressDirectory · IAgendaDirectory<br/>IKurinTagDirectory · IDuesDirectory · IKraiProfileDirectory<br/>запис: IMemberImportWriter · IKurinGroupWriter · IAgendaRsvpWriter"]
        KINDS["Dtos · Records · Events<br/>Enums · Rules · Options"]
    end

    subgraph INFRA["ProjectK.Infrastructure"]
        direction LR
        DBC["DbContext · Persistence<br/>конфігурації й інтерсептори<br/>репозиторії · UnitOfWork · Stores · міграції"]
        SVC["Services<br/>Email · Blobs · Reports · Feedback · Jwt · Catalog"]
        BG["BackgroundServices<br/>аудит · перестороги · осиротілі фото · архів задач<br/>каталог · телеметрія · сповіщення · демо-скидання"]
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
можна було винести в окремий сервіс: контракт замість репозиторію, подія замість виклику, ключ
замість навігації. Тому в листопаді 2026 колишній `KurinModule` розрізали на пʼять — `KurinModule`,
`MemberModule`, `AgendaModule`, `ArchiveModule` і `RosterModule`, — не змінивши ні адрес, ні JSON,
ні бази. Що в яку теку лягає і як зветься — таблиця «Що куди лягає» в
[Конвенціях](/dev/core/contributing/).

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
        IB["IPhotoService"]
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
| Прогрів | `Startup:WarmUp` | без ключа ввімкнений: після старту прогріває MediatR і запити дашборда | `Development`, `Testing`, `E2E`: вимкнений |

`SelfHost` живе між двома колонками: заглушки вмикаються не середовищем, а відсутністю ключа, тож
станиця може поставити систему без пошти й GitHub і додати їх пізніше, не міняючи образ.
