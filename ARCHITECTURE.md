# Архітектура ProjectK

Мапа системи: з чого вона складається, куди йде запит і де що шукати. Правила («як писати новий
код») — у [CONTRIBUTING.md](CONTRIBUTING.md); цей файл описує, **що вже є**.

---

## Що це за система

Застосунок для управління пластовим куренем: членство, провід (діловодства), реєстр складу, календар і
планування, проби та вмілості, онбординг нових людей.

Ключова відмінність від типової рольової системи: **доступ визначається діловодством, яке людина
тримає**, а не роллю акаунта. Акаунт знає лише, адміністратор це чи ні. Усе решта — «чи може ця
людина редагувати цього члена» — виводиться з діловодства. Тому `LeadershipController` змінює не довідник,
а права.

---

## Складові

```
Frontend/projectk-frontend     Angular 22, standalone, signals, optimus-ui 2 + Tailwind
Backend/ProjectK.Backend       .NET 10, ASP.NET Core, EF Core 10, MediatR
docker/                        одна збірка образу на всі середовища
scripts/                       dev.sh / dev.ps1 — підняти будь-яке середовище локально
```

`ProjectK.Optimization` (закритий двигун підбору дати табору в плануванні — `CampDateProblem`) живе
як зовнішній пакет, не як проєкт у солюшені. Каталог вмілостей, проб і відзначень Лілейка бере із
сервісу каталогу по API (`GET /v1/catalog`, секція `Catalog` в `appsettings`). Каталоги **читаються,
а не редагуються** через застосунок — тому `BadgesCatalogController` і `ProbesCatalogController` не
мають операцій запису.

---

## Бекенд: чотири проєкти

```
ProjectK.Common          → (нічого)
ProjectK.Infrastructure  → Common
ProjectK.BusinessLogic   → Common
ProjectK.API             → Common, BusinessLogic, Infrastructure
```

`Infrastructure` і `BusinessLogic` не знають одне про одного. Коли бізнес-логіці потрібен
зовнішній світ, інтерфейс оголошується в `Common`, а реалізується в `Infrastructure`:
`IKurinReportRepository`, `ISpreadsheetWriter`, `ISpreadsheetReader`, `IAppUserRepository`, `IUnitOfWork`.

Що лежить у кожному проєкті (шляхи — від `Backend/ProjectK.Backend/`):

| Проєкт | Теки |
|---|---|
| `ProjectK.Common` | `Entities/`, `Contracts/`, `Dtos/`, `Records/`, `Events/`, `Enums/`, `Rules/`, `Options/` — і нічого більше |
| `ProjectK.BusinessLogic` | `Modules/<Module>/` (зрізи), `Behaviors/` (конвеєр MediatR), `Services/` (кеш і публікація подій — спільне для всіх модулів), `DependencyInjection.cs` |
| `ProjectK.Infrastructure` | `DbContexts/`, `Persistence/` (`Configurations/<Module>/`, `Interceptors/`), `Repositories/<Module>/`, `UnitOfWork/`, `Stores/`, `Services/<Concept>/`, `BackgroundServices/`, `Logging/`, `Seeding/`, `Migrations/`, `DependencyInjection.cs` |
| `ProjectK.API` | `Controllers/<Module>/`, `Middleware/`, `Filters/`, `Authorization/`, `Cookies/`, `Extensions/`, `Hosting/`, `Serialization/`, `Swagger/`, `Uploads/`, `Models/Requests/`, `Program.cs` |

Кожен шар реєструє своє: `AddInfrastructure()` і `AddBusinessLogic()`; `AddProjectDependencies` в API
додає лише хостове (`HttpContextAccessor`, `TimeProvider`, `ICurrentUserContext`) і кличе обидва.

### Common: спершу вид, потім модуль

`Common/<Вид>/<Module>/`, простір імен — `ProjectK.Common.<Вид>.<Module>`; спільне для всіх модулів —
у підтеці `Shared/`. Один публічний тип — один файл.

| Вид | Що там | Приклади |
|---|---|---|
| `Entities/` | таблиці | `Member`, `Membership`, `KurinAuditEvent`; базовий клас — `Shared/Entity` |
| `Contracts/` | інтерфейси: репозиторії, фасади UoW, `I…Directory` для інших модулів, `I…Catalog`, зовнішні системи; винятки репозиторію | `IMemberDirectory`, `IUnitOfWork`, `IEmailService`, `StaleRowException` |
| `Dtos/` | усе, що бачить клієнт: відповідь `…Dto`, запит `…Request` (у підтеці `Requests/`) | `GroupDto`, `LoginUserDto`, `KraiProfileDocument` |
| `Records/` | внутрішні значення, які клієнту не серіалізуються: відповіді контрактів між модулями, рядки читання репозиторіїв | `ServiceResult<T>`, `ResourceAccessDecision`, `AgendaViewerItem`, `MemberListRow`, `MemberFieldMask` |
| `Events/` | доменні події — те, що вже сталося | `MembershipLeft`, `MembershipMovedToGroup`, `AccountActivated` |
| `Enums/` | перелічення | `AccessScope`, `ResultType`, `AuditArea` |
| `Rules/` | static-класи: правила (`…Rules`), підписи й сталі рядки (`…Names`), розширення | `RolePermissionRules`, `KurinCapabilityRules`, `PlastLevelNames`, `PhoneNumberRules` |
| `Options/` | налаштування з `SectionName` | `LoginThrottleOptions`, `CatalogOptions`, `TelemetryOptions` |

Свідомий виняток — Identity: `AppUser : IdentityUser` лежить у `Common/Entities/IdentityModule`, тож
`Common` тягне `Microsoft.AspNetCore.Identity.EntityFrameworkCore`. Чому так і чому далі не
розширюється — у [CONTRIBUTING.md](CONTRIBUTING.md#шари).

### Модулі

Однакові по обидва боки межі — і в `BusinessLogic/Modules/`, і в `API/Controllers/`. Розміри — на
17 листопада 2026: області (`Features/<Area>/`), запити (`…Command` і `…Query`) і рядки `.cs` у
`BusinessLogic/Modules/<Module>/`.

| Модуль | Області · запити · рядки | Про що |
|---|---|---|
| `IdentityModule` | 16 · 55 · 6,6 тис. | вхід і сесії, MFA, онбординг і список очікування, скидання пароля, перемикання куреня; акаунти: адміністрування, призупинення, безпека й профіль власного акаунта, збережені розкладки дашборда; налаштування інстансу (`SystemSetting`), первинна настройка, демо- і дев-вхід. Тут і рушій прав: `PermissionResolver`, `AccessContextResolver`, `AccessSnapshotFactory`, `CurrentPermissions`, `ResourceAccessService` (з частинами `ResourceScopeService`, `SeniorYouthRecordService`, `SignOffRules`). «Мої обовʼязки» (`Features/Duties`) збирає з модулів через `IDutySource`. Зміна безпеки обриває всі сесії в `RefreshTokenInvalidation`, а той публікує `AccountSessionsEnded` — посилання на календар вимикає вже агенда |
| `KurinModule` | 12 · 73 · 9,3 тис. | курінь і все, що людина має в ньому. Сам курінь: картка, емблема, край, політика доступу, чи можуть люди до 18 ділитися карткою, скільки зроблені задачі лишаються на дошці й скільки живе їхній архів (`SetAgendaArchivePolicyCommand`), гуртки й редакції проб, які вони обрали, планування, прохання старшого куреня змінити гілки, теги (хто їх бачить — `SeeTags`, хто ставить — `ManageTags`, чи курінь ними користується — `KurinTagSettings`) і PDF-звіт (`KurinReportDataService`, людей і членства віддає значеннями `IKurinReportRepository`). Людина в курені: членства (прийом, вихід, перехід між гуртками), заявки й запрошення між куренями (`MembershipRequest`), діловодства та їхня історія (`Leadership`), впорядники (`MentorAssignment`). Реєстр (`Registry`, рядки `MemberListRow`), імпорт і вивантаження реєстру (`Import`) та архів куреня (`Archive`: колишні ролі, закриті членства з гуртками, розформовані гуртки, небезпечний режим). Іншим модулям відповідає `IKurinDirectory`, `IKurinTagDirectory`, `IMembershipDirectory` (хто стоїть у курені й у якому гуртку — `KurinPerson`) і `IOfficeDirectory`; засновника нового куреня ставить `IKurinFounderWriter`. Каталог помилок один — `KurinErrors` |
| `MemberModule` | 5 · 31 · 4,9 тис. | людина сама по собі: профіль і його перевірка, фото, ступені, відзначення (і закріплені на головній), перестороги, життєпис і його PDF (`Life`), своя картка й посилання на неї (`Card`). Тут живе й «хто я» — `ICurrentMember`. Про курінь модуль не знає: `MemberSummary` не несе ні куреня, ні гуртка. Те, що з куреня ще треба картці й життєпису, він читає через контракти куреня; цей борг до LIFE-03 перелічений у baseline `Member_ShouldNotNameTheKurin` |
| `AgendaModule` | 2 · 25 · 4,4 тис. | календар і задачі: події й задачі з призначенням «для кого», категорії, повтори, відповіді «буду / не буду», дошка й архів задач. Правила без стану, які читають і інші модулі, — у `Common/Rules/AgendaModule` (повтори `AgendaRecurrenceRules`, окремі дати серії `AgendaOccurrenceRules`, виконання `AgendaCompletionRules`, видимість `AgendaVisibilityRules`, пояс куреня `KurinTimeZoneRules`, адреси для сповіщень і календаря `AgendaRouteNames`), їхні значення — у `Common/Records/AgendaModule`. Сутності тримають `KurinKey` без навігації на `Kurin`. Гуртки, теги й політику архіву задач агенда читає через `IKurinDirectory` і `IKurinTagDirectory`; призначення, чия ціль зникла (людина, гурток, тег), прибирає сама з подій (`AgendaTargetCleanupEventHandler`). «Мої» задачі й події (`MyAgendaController`) і посилання на календар (`Features/CalendarFeed`, сутність `CalendarFeed`, `ICalendarFeedStore`) — теж тут. Іншим модулям відповідає `IAgendaDirectory` — лише значеннями — і `IAgendaViewerDirectory`: як людина бачить агенду в кожному своєму курені, для «моїх» сторінок і обовʼязків |
| `ProbesAndBadgesModule` | 5 · 28 · 4,3 тис. | каталоги проб, вмілостей і відзначень (лише читання); які проби склав і які вмілості здобув конкретний член; третя проба; «мій ріст» на головній (`Growth`). Вмілості, які чекають підтвердження, віддає в обовʼязки `BadgeDutyReader` |
| `NotificationsModule` | 3 · 10 · 2 тис. | сповіщення (в застосунку, на телефон і поштою), «Повідомити про проблему», код для встановлення на телефон. Нічого, крім сповіщень: сховище файлів, QR, гео-блок і телеметрія — у `Shared`, JWT і рушій прав — в Identity |
| `DuesModule` | 6 · 22 · 4,1 тис. | вкладка: ставки, нарахування, операції гурткової й курінної каси, баланс людини; у старшому курені (без гуртків) рахунок людини в касі куреня (нарахування з `GroupKey = null`), ставки за тегом (`KurinDuesRate.TagKey`, тег запамʼятовує `DuesCharge.RateTagKey`, несплачений квартал переоцінюється), там платить і провід; статті для краю й передачі просто краю; годинник кварталів і формат чисел краю (`DuesKrai`); пільги краю, надані людям, і звіт, скільки вони зняли (`GetKraiConcessionReportQuery`, віддає `KraiController`); «моя вкладка» на головній. Передачі й записи, які чекають підтвердження, віддає в обовʼязки `DuesDutyReader` |
| `ScoreModule` | 7 · 22 · 3,2 тис. | точкування: присутність на подіях, бали вручну й автоматично, таблиця гуртків, книга КВ. Події, їхні повторення й категорії бере з `IAgendaDirectory` (`AgendaEventSummary`, `AgendaEventOccurrence`, `AgendaCategorySummary`), а не з таблиць агенди; «мої бали» на головній. Події без відміченої присутності віддає в обовʼязки `AttendanceDutyReader` |
| `KraiModule` | 2 · 11 · 1,2 тис. | краї й профілі країв: які вмілості бачать курені, валюта, часовий пояс, формат чисел, поля картки, куди курені передають вкладку і які пільги дає край; ревізії профілю й відновлення давнішої; які поля прибирає зі своєї картки курінь. Додати й перейменувати край (`CreateKraiCommand`, `RenameKraiCommand`), публічний список для форми заявки (`GetKraiNamesQuery`); 28 країв засіває міграція |
| `AuditModule` | 1 · 1 · 0,3 тис. | журнал дій куреня: хто, що й коли змінив (`KurinAuditEvent`). Інші модулі пишуть у нього через `IKurinAudit` перед своїм `SaveChanges`, читають — через `IAuditUnitOfWork.KurinJournal`; таблицю бачить лише цей модуль |

У `API/Controllers/` є ще три теки без двійника в `BusinessLogic`: `DemoModule`, `DevModule` і
`TestModule` — контролери, які існують лише в окремих середовищах і беруть запити з `IdentityModule`.

У кожному шарі модуль має свою теку: `BusinessLogic/Modules/<M>`, `Common/<Вид>/<M>`,
`Infrastructure/Repositories/<M>` і `Infrastructure/Persistence/Configurations/<M>`, `API/Controllers/<M>`.
Тека є лише там, де модулю є що в неї класти. Склад модулів іде за картою з CONTRIBUTING.md
(«Карта модулів»): «моє» живе в модуля, якому воно належить, тож окремого модуля «Me» немає, а
адреси `api/me/...` віддають контролери `My…Controller` у модулях-власниках. Модулі прийшли до цього
виду в `POL-13` і `POL-23` (листопад 2026); адреси, JSON і база від цього не змінились.

**Член ≠ користувач.** Член — це людина; користувач — спосіб увійти. Одне може існувати без
іншого: людину заводять до того, як вона отримає акаунт, і не кожна його отримує.

**Людина ≠ курінь.** `Member` — це людина сама по собі: імʼя, дата народження, проби, вмілості,
рівні, відзнаки, перестороги. Де вона належить, каже окремий запис — `Membership` (курінь, гурток,
вид членства, коли прийшла й коли пішла). Тому людина може бути в кількох куренях одночасно, а вихід
із куреня чи його розпуск не стирає нічого з того, що вона здобула: історія належить їй, а курінь на
цих записах — лише штамп «де це сталося».

### Будова модуля

```
BusinessLogic/Modules/<Module>/
    Features/<Area>/
        <Verb><Thing>Command.cs   запит + валідатор + хендлер
        Get<Thing>Query.cs        запит + хендлер
        <Area>Mapping.cs          ToDto / ApplyTo цієї області
    Events/                       обробники доменних подій (…EventHandler)
    Services/                     спільне для кількох зрізів: …Service, …Reader, …Writer, <M>Access, …Trail, …Rules, …Names
    Models/                       внутрішні типи модуля, яких клієнт не бачить
    <Module>Errors.cs             коди помилок модуля
```

Область — іменник предметної області (`Group`, `Agenda`, `Life`), тек-дієслів під нею немає.
Мапінг — явний код (`ToDto`, `ApplyTo`), без бібліотек. Повні правила — у
[CONTRIBUTING.md](CONTRIBUTING.md), розділ «Що куди лягає».

### Межі модулів

Модулі не читають таблиць одне одного й не називають хендлерів, служб, мапінгів і каталогів
помилок одне одного. Чотири правила, кожне з яких перевіряє `ProjectK.Architecture.Tests`:

- **Питання — через контракт.** Усе, що один модуль питає в іншого, проходить через інтерфейс у
  `Common/Contracts/<Module>/` — рівно ті питання, які колись можна буде поставити по мережі.
  `I…Directory` **лише відповідає**: змінювати через нього нічого не можна.
- **Зміна в чужому модулі — через подію.** Модуль, що зробив дію, публікує подію з
  `Common/Events/<Module>/` через `IDomainEventPublisher`, а модуль, чиї дані міняються, обробляє її
  сам у своїй теці `Events/`. Так активація акаунта (`AccountActivated`) заводить людину в
  `MemberModule` (`AccountActivatedEventHandler`), зміна пошти й телефону акаунта (`AccountEmailChanged`,
  `AccountPhoneChanged`) доходить до картки (`AccountContactSyncEventHandler`), видалений тег
  (`KurinTagDeleted`) переоцінює вкладку в `DuesModule` (`KurinTagDeletedEventHandler`), а перехід між
  гуртками (`MembershipMovedToGroup`) записують і вкладка, і точкування. Видалений курінь
  (`KurinDeleted`) і розформований гурток (`GroupDissolved`) закривають провід і членства в
  `MemberModule` (`MembershipCleanupEventHandler`). Видалена людина (`MembersRemoved`),
  розформований гурток і знятий тег (`KurinTagRemoved`) прибирають призначення агенди, де вони були
  ціллю (`AgendaTargetCleanupEventHandler`). Ці події публікуються **до** `SaveChanges` того, хто їх
  оголосив: обробник міняє рядки на тому самому unit of work, і все комітиться одним збереженням.
  Видалення тегу оголошує дві: `KurinTagRemoved` до збереження й `KurinTagDeleted` — після, для
  переоцінки вкладки.
- **Запис у чужому модулі — контрактом запису, коли подія не годиться.** Подію обробляють без
  відповіді, тож коли дія мусить змінити чужі дані в тій самій транзакції й одразу дістати
  результат, модуль-власник відкриває `I…Writer` у `Common/Contracts/<Module>/` і виконує запис
  своїми ж правилами. Таких контрактів три (таблиця нижче), і кожен у doc-коментарі пояснює, чому
  тут не подія.
- **Ключ замість навігації.** Сутність чужого модуля не буває навігаційною властивістю: тримають
  `MemberKey`, а не `Member`. Звʼязок лишається в моделі як `HasOne<T>()` з тим самим ключем і
  правилом видалення, тож база не міняється; `Membership`, `MentorAssignment` і `Member` тримають
  курінь, гурток і акаунт лише ключами, а фільтр закріплень впорядника за розформованим гуртком
  живе в `AppDbContext.OnModelCreating`. Baseline `Entities_ShouldNotNavigateIntoAnotherModule`
  порожній.

| Контракт | Хто відповідає | На що |
|---|---|---|
| `IMemberDirectory` | мембер | хто ця людина: за ключем, публічним кодом, акаунтом чи поштою; її ступені, дата народження, підтверджені відзначення; кілька людей одним читанням (`GetByKeysAsync`, `GetLookupsAsync`); чинні перестороги, видані куренем (для точкування); хто може стояти за рядками файлу (`FindPossibleMatchesAsync`, для імпорту); старші ступені, що чекають підтвердження, у багатьох людей одним читанням (`GetWaitingLevelsAsync`). Про курінь не відповідає |
| `ICurrentMember` | мембер | хто я: людина за токеном запиту, прочитана раз на запит |
| `IMembershipDirectory` | курінь | де людина належить і належала; хто стоїть у курені й у якому гуртку (`GetPeopleInKurinAsync`, запис `KurinPerson`) і людина з її останнім членством (`FindInLatestKurinAsync`), з членством у курені, про який питають (`FindInKurinByAccountAsync`), чи в її курені УПЮ, де ведуться проби (`FindInYouthKurinAsync`); скільки людей у курені зараз (`CountCurrentInKurinAsync`, `CountCurrentInKurinsAsync` — `currentUserCount` у картці куреня) |
| `IOfficeDirectory` | курінь | які діловодства акаунт тримає в цьому курені; проводи куреня, хто в них зараз сидить і в яких сидить людина — за ключем члена (для календаря) |
| `IGroupProbeEditions` | курінь | яку редакцію проб обрав гурток |
| `IKurinDirectory` | курінь | назви гуртків куреня; скільки зроблені задачі лишаються на дошці й скільки живе архів (`KurinTaskArchivePolicy`) — для календаря |
| `IKurinTagDirectory` | курінь | чи курінь користується тегами і як далеко глядач бачить, хто їх носить; теги куреня (`GetTagsAsync`), теги однієї людини (`GetTagKeysOfMemberAsync`), хто носить кожен тег (`GetCarriersAsync`) — для календаря й точкування |
| `IKurinTagVisibility` | курінь | чиї теги бачить глядач, перемикачі тегів куреня (`KurinTagSettingsState`), чи може він ставити теги; заповнює видимі теги в рядках людей |
| `IAgendaDirectory` | календар | лише значеннями: задачі й події, які бачить глядач, зі станом для нього (`AgendaViewerItem`, `AgendaViewerOccurrence`) — для посилання на календар; категорії, подія та її повторення, відповіді на повторення (`AgendaCategorySummary`, `AgendaEventSummary`, `AgendaEventOccurrence`) — для точкування |
| `IAgendaViewerDirectory` | календар | як людина бачить агенду в кожному своєму курені (`AgendaKurinViewer`): «мої» задачі й події, обовʼязки |
| `IDutySource` | обовʼязки (Identity) | що чекає на людину з проводу в одному курені (`PendingDuty`); реалізують `BadgeDutyReader`, `DuesDutyReader`, `AttendanceDutyReader`, `PlastLevelDutyReader`, збирає `Features/Duties` |
| `IMemberProgressDirectory` | проби й вмілості | як далеко людина зайшла, підписані точки проби; що в курені підтверджено й підписано (для точкування) |
| `IProbeEditionHolds` | проби й вмілості | де в черзі проб стоїть кожен юнак (для налаштувань редакції) |
| `IBadgesCatalog`, `IProbesCatalog`, `IHonoursCatalog` | каталог | фіксовані довідники вмілостей, проб і відзначень |
| `IDuesDirectory` | вкладка | які квартали закриті без боргу (для точкування) |
| `IKraiProfileDirectory` | край | профіль краю чи куреня з усіма значеннями: вмілості, валюта, пояс, формат чисел, поля картки, вкладка (куди передають, пільги) |
| `IKurinJournalRepository` | журнал | записи журналу дій за предметами (історія на картці проби й вмілості) і сторінка журналу з фільтрами; доступний лише як `IAuditUnitOfWork.KurinJournal` |

Контракти запису:

| Контракт | Чий | Хто пише і навіщо |
|---|---|---|
| `IMemberImportWriter` | мембер | імпорт реєстру: завести нову людину й надіслати їй запрошення — ключ нової людини потрібен одразу, а весь файл лягає однією транзакцією |
| `IKurinFounderWriter` | курінь | активація акаунта засновника: прийняти його в курінь, який відкрило схвалення, і поставити Звʼязковим — до того, як активація відповість сесією з цим куренем і роллю |
| `IAgendaRsvpWriter` | календар | відповідь «буду / не буду» з головної: той самий рядок, що й з календаря, збережений у транзакції того, хто відповідає, і одразу у відповіді. Приймає ключі, а не сутність: `AnswerAsync` сам перевіряє подію, видимість і повторення й повертає `AgendaRsvpOutcome` |

Межі тримає не домовленість, а `ProjectK.Architecture.Tests`: правила читають IL, тож звернення до
чужого репозиторію чи чужої сутності валить тест навіть тоді, коли воно сховане всередині методу й
ніде не видно в сигнатурах. `Modules_ShouldNotReachIntoEachOther` (`ModuleBoundaryRules`) так само
ловить модуль, що називає чужий хендлер, службу чи мапінг, а ще читає текст: константа коду
помилки вбудовується у виклик і в IL сліду не лишає. Там, де борг ще є, він занесений у baseline
правила — тобто перелічений поіменно, а не непомічений. Обидва baseline-и межі модулів порожні;
борг мембера перед куренем (до LIFE-03) тримає окреме правило `Member_ShouldNotNameTheKurin`.
Сутності правило `Entities_ShouldNotNavigateIntoAnotherModule` читає рефлексією: навігація з однієї
теки `Common/Entities/<Module>` в іншу валить тест.

Вкладка має свій фасад `IDuesUnitOfWork`, як мембер — `IMemberUnitOfWork`: до грошових таблиць
нема дороги з `IUnitOfWork`, тож жоден інший модуль їх не прочитає. Гуртки, курені й членства вона
тримає ключами без зовнішніх ключів — історія грошей переживає видалений гурток.

Точкування влаштоване так само: фасад `IScoreUnitOfWork`, ключі без зовнішніх ключів. Правила
«лише раз» (одна присутність на людину й дату, одна тарифна позиція на подію) — унікальні індекси в
базі, а не перевірки в обробнику. Переходи між гуртками модуль записує сам із події
`MembershipMovedToGroup` (`RecordScoreGroupMoveEventHandler`), бо членство памʼятає лише теперішній
гурток.

Мембера будували так, щоб його можна було винести в окремий сервіс: контракт замість репозиторію,
подія замість виклику, ключ замість навігації. Ці правила перевіряються з 0.20.0, і саме тому в
`POL-13` мембер переїхав із `KurinModule` в окремий `MemberModule`, не зачепивши ні адрес, ні JSON,
ні бази. Окремим сервісом він від цього не став: процес і база ті самі.

---

## Шлях запиту

```
HTTP
  ↓  RequestTimingMiddleware          час запиту для /health (першим, щоб охопити весь конвеєр)
  ↓  forwarded headers → ClientIpMiddleware   справжня адреса відвідувача
  ↓  TelemetryCountingMiddleware      запити й 5xx для щоденного пінгу (зовні обробника винятків)
  ↓  Serilog request logging, обробник винятків, CORS
  ↓  автентифікація JWT (access у заголовку, refresh у httpOnly-cookie)
  ↓     сесія = рядок у UserRefreshTokens; акаунт може бути в кількох місцях одночасно
  ↓     другий фактор: пароль → короткий mfaToken (5 хв, окрема audience) → код + mfaToken;
  ↓     код без mfaToken відхиляється — другий фактор ніколи не буває єдиним
  ↓  AccessStaleHeaderMiddleware      X-Access-Stale на токен, виданий до зміни прав
  ↓  гео-блок (SecurityHardeningMiddleware → IGeoIPService), rate limiting, журнал зміни IP (SecurityActivityMiddleware)
  ↓  гейт MFA для проводу читає клейм amr з токена, не акаунт (PrivilegedMfaEnforcementMiddleware)
  ↓  політика авторизації   AuthorizationPolicies.*   — «якого рівня має бути викликач»
  ↓  ResourceAuthorize      ResourceAuthorizeFilter → IResourceAccessService — «чи саме цей об'єкт йому доступний»
  ↓  контролер API/Controllers/<Module>/<Resource>Controller: жодних рішень, лише _mediator.Send(...)
  ↓  поведінки MediatR (BusinessLogic/Behaviors), ззовні всередину:
  ↓     RequestTimingBehavior → ValidationBehavior (FluentValidation; невдача — 400 ValidationFailed,
  ↓     хендлер не викликається) → TransactionBehavior (лише для ITransactionalRequest)
  ↓  хендлер Features/<Area>/…Command.cs або …Query.cs: доменне рішення; дані — через фасади UoW
  ↓     і контракти інших модулів; людина за токеном — ICurrentMember
  ↓  <Area>Mapping: ToDto(…) для відповіді, ApplyTo(…) для зміни сутності
  ↓  IUnitOfWork.SaveChangesAsync → інтерсептори збереження (публічний код, гуртки членства)
  ↓  ServiceResult<T>; без даних — ServiceResult<Unit>; невдача — код з <Module>Errors
  ↓  ToActionResult(this) → HTTP: 200 / 201 з даними, Unit — 204, невдача — { error, message }
```

Дві сходинки авторизації розділені навмисно. Політика відповідає на питання «чи має ця людина
взагалі право на такі дії» (`RequireGroupLeadership`), а `ResourceAuthorize` — «чи цей конкретний
член / гурток / курінь у її сфері» (`AccessScope`: `Own`, `OwnGroups`, `KurinWide`).

Політика лишається лише там, де вона **вужча** за ресурсну перевірку — тобто справді щось вирішує.
Там, де вона просто повторювала мапу дозволів, її знято: дві копії правил розходяться, і вигравала
суворіша, даючи 403, якого ніщо не пояснює. `PolicyMatchesPermissionMapTests` валиться, якщо читання
запису колись стане суворішим за його зміну.

**`ServiceResult<T>` — єдина форма відповіді.** Помилка завжди `{ error, message }`: `error` —
стабільний код з каталогу модуля (`KurinErrors`, `DuesErrors`…), `message` — англійський текст для
розробника. Перетворення в HTTP живе в одному місці — `ServiceResultExtensions.ToActionResult`
(`API/Extensions`). Результат без даних — `ServiceResult<Unit>`, і він відповідає `204 No Content`
так само, як колись `ServiceResult<object>` з `null`, тож клієнт різниці не бачить. Без
`ToActionResult` відповідають лише ті, хто віддає не JSON: файл календаря (`CalendarFeedController`),
публічна картка (`PublicCardController`) і `E2ETestController`.

---

## Авторизація

```
активний курінь у токені            у якому курені людина зараз діє
        ↓ IOfficeDirectory           діловодства цього акаунта саме в цьому курені
LeadershipRole + LeadershipType      діловодство, яке людина тримає
        ↓ SystemRoleNames.ForOffice()
SystemRoleNames                      роль доступу, дзеркало діловодства
        ↓ RolePermissionRules + політика куреня (KurinCapabilityRules, Kurin.AccessPolicy)
Permission                           напр. Group:Manage:KurinWide, MemberContacts:Read:OwnGroups
```

Діловодства — джерело; ролі доступу **виводяться з них на кожен вхід і рефреш**, а не зберігаються
на акаунті. Другого списку «хто керує куренем» немає: усе виводиться з `RolePermissionRules`.

Частина прав — **налаштування куреня**: хто бачить особисті дані, хто дає перестороги й
відзначення якого рівня, хто підписує й знімає підписи проб, хто підтверджує вмілості й читає
журнал. Кожна така можливість — рядок `KurinCapabilityRules` з парою дозволу й значенням за
замовчуванням для кожної аудиторії (Звʼязковий, впорядники, інструктори, курінний, провід куреня,
гуртковий, провід гуртка, юнаки). Курінь зберігає лише відхилення, а `IPermissionResolver` складає
статичну мапу з політикою на кожен запит, тож збережена політика діє одразу. Звʼязковий закріплений
на повний доступ, щоб курінь не лишився без того, хто поверне права. Особисті дані проходять одну
маску (`MemberFieldMask`) — і на читання, і на запис.

Зміна діловодства чи політики доходить до людини без перезаходу: сервер позначає її токен
застарілим (`IAccessChangeStore` → заголовок `X-Access-Stale`), клієнт робить рефреш, а рефреш
віддає нові права разом із токенами.

Ключове тут — слово «активний». Ролі рахуються для одного куреня за раз (`IAccessContextResolver`),
бо та сама людина може бути виховником в одному курені й звичайним членом у другому. Доки ролі
писалися в Identity як глобальні, друге членство протягло б права виховника й туди — тому ролі
діловодств у сховище акаунтів більше не пишуться, і там лишились тільки `Admin` і `Member`.
Перемикання куреня (`POST api/auth/kurin-scope`) — це не фільтр списку, а перевидача токена; воно
відкрите будь-кому, хто має **активне** членство в тому курені, і лише адміністратор може стояти
поза всіма.

Один виняток з «усе в межах куреня»: **свій запис людина читає завжди**. Виведена з останнього
куреня, вона не стоїть ніде, і жоден курінний обсяг до неї не дотягується — але картка, ступені,
відзначення й проби її, а не куреня. Тож читання (`Read`, і тільки воно) власного запису проходить
і без куреня в токені: `ResourceAccessService` питає `IResourceScopeRepository.GetPersonAccountKeyAsync`,
чий це акаунт, — за членствами, закритими теж. Змінювати свій запис людина знову може, коли її
прийме курінь.

І одне обмеження в протилежний бік: **власне ніхто не підписує**. Впорядник чи Звʼязковий теж
член куреня, а право в межах свого гуртка завжди дотягується до власного запису. Тож
`ResourceAccessService` відмовляє в `SignProbe`, `UnsignProbe` і `ReviewBadge` (і в їхніх
`ProbeProgress:Update/Delete`, `BadgeProgress:Update`), коли запис — того, хто питає. Ці права
зникають і з `ReachableCapabilitiesAsync`, тож кнопок на своїй картці немає.

Юнацьке **старшого** — того, хто зараз стоїть у курені УСП чи УПС (`IResourceScopeRepository.StandsInSeniorKurinAsync`),
— закритий запис. Проби не змінює ніхто. Вмілості й відзначення УПЮ змінює лише той, хто має право на весь
курінь там, де людина стоїть зараз (Звʼязковий), і ніколи сама людина. Ступені — він і сама людина. Для проб і
вмілостей це робить сам `ResourceAccessService`; відзначення УПЮ й ступені ділять ендпоінт зі старшими
відзначеннями й рештою профілю, тож їхні хендлери питають `CheckSeniorYouthRecordAsync`. Провід колишнього
куреня сюди не доходить узагалі: перевірка обсягу тримає його в курені, де людина стоїть.

**Архів куреня** (`KurinModule/Features/Archive`: `KurinArchiveController`, `IKurinArchiveRepository`) — колишні ролі, закриті
членства з гуртками, через які вони пройшли, і розформовані гуртки. Розформований гурток не
стирається: `Group.DissolvedAtUtc` і фільтр запитів ховають його всюди, крім архіву, а провід лишається
з датами. Гуртки членства пише інтерсептор збереження (`MembershipGroupStintInterceptor`, `MembershipGroupStint`), тож
жоден шлях — прийом, перехід, вихід, переведення, імпорт — не забуде запис; історія починається з 1.4.
Хто що бачить і править, вирішують хендлери за `KurinArchive:Read/Update` (Звʼязковий — увесь курінь,
гуртковий і впорядник — свої гуртки), як у журналі; своє ніхто не править. Видаляє лише Звʼязковий і
лише в **небезпечному режимі**: повторний вхід паролем чи кодом (`IReauthentication`, ті самі перевірки
й лічильник `LoginThrottle`, що й на вході) дає квиток окремої audience `archive-danger` на 10 хвилин,
привʼязаний до акаунта, куреня й security stamp. Квиток живе лише в памʼяті сторінки, а сервер тримає
живі зони в `ArchiveDangerZoneStore`: вихід зі сторінки гасить зону одразу, і там же рахується
10 видалень на вхід. Кожен запит — один запис; кожне видалення пише в журнал знімок видаленого, а
записи розділу «Архів» журнал не прибирає через рік, як решту. Вхід у режим сповіщає інших Звʼязкових
і курінного.

Авторизація навмисно не знає про мембера: усе, що їй потрібно, вона читає через членства й діловодства за
ключем акаунта. Це не побажання, а правило `Authorization_ShouldNotKnowAboutMember` в арх-тестах.

Політики оголошені **один раз** — `AuthorizationPolicies.AddProjectPolicies()` (`API/Authorization`);
і хост, і тестові хости беруть їх звідти. Матриця `AuthorizationBaselineMatrixTests` тримає очікувану
політику для кожного ендпоінта: новий ендпоінт без запису в матриці валить тест, а
`RolePermissionRulesGoldenTests` — зліпок мапи дозволів.

Де що лежить:

| Що | Де |
|---|---|
| Чисті правила: мапа ролей на дозволи, можливості куреня, аудиторії політики, діловодства, імена claims | `Common/Rules/IdentityModule/`: `RolePermissionRules`, `KurinCapabilityRules`, `PolicyAudienceRules`, `LeadershipOfficeRules`, `SystemRoleNames`, `AccessTokenClaimNames` |
| Політика куреня, дозвіл, маска полів | `Common/Records/IdentityModule/` (`KurinAccessPolicy`, `Permission`, `CapabilityDescriptor`), `Common/Records/MemberModule/MemberFieldMask` |
| Рушій: хто викликач, які в нього права, чи доступний обʼєкт | `BusinessLogic/Modules/IdentityModule/Services/`: `AccessContextResolver`, `PermissionResolver`, `CurrentPermissions`, `AccessSnapshotFactory`, `ResourceAccessService`, `MemberFieldAccess`, `AccessChangeStore`. `ResourceAccessService` — єдина точка рішення; «де діє дозвіл» рахує `ResourceScopeService`, «своє не підписують» — `SignOffRules`, закритий запис старшого — `SeniorYouthRecordService` |
| Права всередині модуля (кого з людей і гуртків зачіпає дія) | один клас на модуль у його `Services/`: `KurinAccess`, `AgendaAccess`, `DuesAccess`, `ScoreAccess`, `ProbesAndBadgesAccess` |
| Політики й ресурсна перевірка на ендпоінті | `API/Authorization/AuthorizationPolicies`, `API/Filters/ResourceAuthorizeAttribute` і `ResourceAuthorizeFilter` |
| Хто викликач у хендлері | `ICurrentUserContext` (акаунт і курінь з токена, `API/Hosting/HttpCurrentUserContext`), `ICurrentMember` (людина за ним) |

---

## Дані та зовнішні системи

| Що | Чим | Де код |
|---|---|---|
| База | SQL Server, EF Core 10, міграції в збірці. Модель — по класу `IEntityTypeConfiguration` на сутність, `AppDbContext` бере їх зі збірки | `Infrastructure/DbContexts`, `Infrastructure/Persistence/Configurations/<Module>`, `Infrastructure/Migrations` |
| Інтерсептори збереження | правила, які мають спрацювати на кожному збереженні, хоч би хто зберігав: публічний код нового члена (`MemberPublicIdInterceptor`) і гуртки членства для архіву (`MembershipGroupStintInterceptor`). Підключає їх `AddAppDbContextInterceptors` — у хості, у фабриці для `dotnet ef` і в тестах | `Infrastructure/Persistence/Interceptors` |
| Доступ до даних | репозиторії поверх `BaseEntityRepository<T>`, по теці на модуль, лише через фасади UoW: `IUnitOfWork` і модульні `IMemberUnitOfWork`, `IDuesUnitOfWork`, `IScoreUnitOfWork`, `IAuditUnitOfWork` (`KurinAudit` пише журнал, `KurinJournal` читає). Усі пʼять реалізує один `UnitOfWork` на запит, тож зміна й запис журналу комітяться одним `SaveChanges`. Транзакцію на весь запит дає `TransactionBehavior` для `ITransactionalRequest` | `Infrastructure/Repositories/<Module>`, `Infrastructure/UnitOfWork` |
| Сховища | сесії й посилання на календар — `RefreshTokenStore`, `CalendarFeedStore`; каталог у памʼяті — `CatalogStore`. Стан у памʼяті процесу тримає модуль, якому він належить: позначки зміни прав (`AccessChangeStore`), живі зони небезпечного режиму архіву (`ArchiveDangerZoneStore`) | `Infrastructure/Stores`; `BusinessLogic/Modules/<Module>/Services` |
| Сесії | рядок на кожен вхід (`IRefreshTokenStore`); вихід обриває одну, зміна пароля, підтверджена зміна пошти й призупинення акаунта адміністратором — усі; призупинений (`OnboardingStatus.Suspended`) не проходить ні вхід, ні другий фактор, ні оновлення токена. Перемикач акаунтів тримає сесії інших акаунтів цього браузера в httpOnly-cookie `savedSessions` (`SavedSessionsCookie`, до 5, по одній на акаунт): `login?addAccount=true` відкладає поточну, `auth/accounts/switch` міняє місцями, вихід обриває всі. Зміна безпеки (`RefreshTokenInvalidation`) публікує `AccountSessionsEnded`; з неї агенда вимикає посилання на календар (`ICalendarFeedStore`, лише хеш токена) і кладе сповіщення в дзвіночок | `Infrastructure/Stores`, `API/Cookies` |
| Календар назовні | ICS-файл за секретним посиланням `GET /api/calendar/feed/{token}.ics` — анонімно, бо Google, Apple й Outlook забирають його зі своїх серверів без cookie; ліміт `CalendarFeedLimit` рахує за посиланням, не за IP. Свій серіалізатор RFC 5545 (`IcsRules`), повтори розгорнуті на сервері (`AgendaRecurrenceRules`), тож у файлі ні RRULE, ні VTIMEZONE. Вимкнене посилання 90 днів віддає подію-повідомлення замість мовчання | `AgendaModule/Features/CalendarFeed`, `API/Controllers/AgendaModule/CalendarFeedController` |
| Файли | Azure Blob Storage (локально — Azurite) | `Infrastructure/Services/Blobs` |
| Пошта | запрошення, скидання пароля, сповіщення | `Infrastructure/Services/Email` |
| PDF | QuestPDF; звіт куреня збирає `KurinReportDataService` з `IKurinReportRepository` (люди, членства й впорядники — значеннями `KurinReportSource…`) і своїх частин: шапка — `KurinReportHeaderReader`, видимі читачеві теги — `KurinReportTagReader`, хто в якому гуртку й хто кадра — `KurinReportRosterRules`, назви проб — `KurinReportProgressNames`; малює `KurinReportPdfRenderer`; життєпис — `LifeStoryPdfRenderer` | `Infrastructure/Services/Reports`, `BusinessLogic/Modules/KurinModule/Services` |
| Логи | Serilog: файл, Application Insights, Telegram-сінк для дев-алертів (події безпеки й помилки; читабельний HTML, однотипні за 5 хв — одним зведенням, черга замість відкидання) | `API/Program.cs`, `Infrastructure/Logging/TelegramDevAlerts` |
| Зворотний звʼязок | «Повідомити про проблему» → GitHub issue токеном сервера (`Feedback:GitHub`), без токена — в лог; скриншоти в `feedback-screenshots` того ж контейнера | `Infrastructure/Services/Feedback`, `API/Controllers/NotificationsModule/FeedbackController` |
| Каталог | вмілості, проби й відзначення з сервісу каталогу (`GET /v1/catalog`, секція `Catalog`); тримаються в памʼяті (`CatalogStore`), остання вдала відповідь — на диску, без мережі — копія, вбудована в збірку | `Infrastructure/Services/Catalog`, `Infrastructure/Stores/CatalogStore` |
| Країна запиту | гео-блок перед кожним запитом: `SecurityHardeningMiddleware` питає `IGeoIPService` (`Common/Contracts/Shared`); відповідь проксі (`CF-IPCountry`) береться як є, інакше адреса шукається зовні | `Infrastructure/Services/GeoIP`, `API/Middleware/SecurityHardeningMiddleware` |
| Телеметрія | раз на добу анонімний пінг сервісу статусу (`Telemetry:Endpoint`): id інсталяції, версія, коди країв, кількості, запити й 5xx; у відповідь — останній реліз. Увімкнена в `Production` і `SelfHost` (`Telemetry:Enabled`) | `Infrastructure/Services/Telemetry`, `API/Middleware/TelemetryCountingMiddleware` |

Фонові служби лежать в одній теці, `Infrastructure/BackgroundServices/`: прибирання аудиту
(`AuditCleanupBackgroundService`), закінчення строку пересторог (`MemberWarningExpiryBackgroundService`),
прибирання осиротілих фото (`OrphanPhotoCleanupBackgroundService`), архів задач — зроблені в архів і
давні архівні геть, за строками кожного куреня (`AgendaArchiveBackgroundService`), оновлення каталогу
щогодини (`CatalogSyncBackgroundService`, `Catalog:RefreshMinutes`), щоденний пінг телеметрії
(`TelemetryBackgroundService`; без `Telemetry:Enabled` не реєструється), черга сповіщень на телефон
(`WebPushBackgroundService`, лише з ключами VAPID), нічне повернення демо-бази до сіду
(`DemoResetBackgroundService`, лише `Demo`).

Окремо від них, у старті хоста, — прогрів після старту: MediatR і запити дашборда «Я» з порожнім
ключем, щоб першу людину після деплою не чекали JIT і компіляція запитів EF (`StartupWarmUpService` в
`API/Hosting`, `Startup:WarmUp`; локально й під тестами вимкнений).

---

## Фронтенд

```
src/app/features/
  authModule/           вхід, онбординг, скидання пароля, guard-и
  adminModule/          адміністрування, користувачі, заявки, краї (профіль і його ревізії)
  dashboardModule/      головна `/`: плитки з `api/me`
  kurinModule/          курінь, гуртки, члени, календар, проби та вмілості
  duesModule/           вкладка: каса гуртка, каса куреня
  scoreModule/          точкування: таблиця, гурток, аркуш присутності, налаштування, книга КВ
  notificationsModule/  інбокс
  systemModule/         налаштування, службові екрани
```

Angular 22, standalone-компоненти, signals (декораторів не лишилось). Доступ до маршрутів —
`capabilityGuard` з `RouteCapability` (`admin | kurinManagement | groupLeadership`), тобто ті самі
поняття, що й на бекенді, а не назви ролей.

Візуальна система описана в [BRANDBOOK.md](BRANDBOOK.md); іменування файлів і класів —
у [CONTRIBUTING.md](CONTRIBUTING.md#фронтенд).

---

## Середовища

Один образ, середовище обирається в рантаймі через `ASPNETCORE_ENVIRONMENT`:

| Середовище | Для чого |
|---|---|
| `Development` | локальна розробка; Swagger увімкнено; сідер заводить демо-курінь і `admin@projectk.com` |
| `E2E` | стек під Playwright; лише тут у застосунку існує `E2ETestController` з фікстурами — в інших середовищах його прибирає з моделі `E2EOnlyControllerFeatureProvider` |
| `SelfHost` | самостійне розгортання; сідер не заводить нікого |
| `Demo` | живий демо-стек: сідер заводить той самий демо-курінь, що й у `Development`, вхід без пароля в одне з трьох місць (`api/demo/login`, контролер існує лише тут), MFA обійдено, база щоночі о 03:00 UTC повертається до сіду (`DemoResetBackgroundService`) |
| `Staging` | перевірка релізу; Swagger увімкнено; демо-даних немає, дані переживають перезапуск |
| `Tailscale` | закритий доступ через tailnet — стенд для людей, не демо: сідер його не чіпає, дані переживають перезапуск |
| `Production` | прод; Swagger вимкнено, `LoadTestLoginKey` порожній; **демо-акаунтів немає** |

Майстер первинної настройки (`api/auth/setup`) відкритий у **кожному** середовищі, доки в базі
немає жодного адміністратора, і зачиняється, щойно він зʼявився. Тому середовища з сідером його
ніколи не бачать, а свіжий `Production` чи `SelfHost` отримує першого адміністратора саме тут — а
не з пароля, записаного в репозиторії, як було до 1.0.

Підняти будь-яке: `./scripts/dev.sh up <env>`. Деталі — у [docker/README.md](docker/README.md).

Демо-курінь і демо-акаунти сідер заводить (і стирає при кожному старті) **лише** в `Development`,
`E2E` і `Demo`. Демо-курінь — це люди з по батькові, адресами й школами, ступені, проби, вмілості,
відзначення, перестороги, календар зі сходинами й таборами, дошка завдань, планування, рік вкладки
(статті, пільга, оплати, борги, передачі куреню й станиці) і точкування (правила, присутність, позиції,
етапи, книжка КВ). Раніше це стосувалося й `Staging` та `Tailscale`, і стенд, який показували людям, губив усе
введене при наступному запуску. Сідер — `DemoDataSeeder` у `Infrastructure/Seeding/Demo/`, розкладений на
partial-файли за тим, що вони засівають: `DemoDataSeeder.People.cs`, `.Leadership.cs`, `.Levels.cs`,
`.Progress.cs`, `.Honours.cs`, `.Agenda.cs`, `.Planning.cs`, `.Dues.cs`, `.Score.cs`, `.Senior.cs`,
`.SeniorDues.cs`, `.LifeStory.cs`, `.Journal.cs`, спільне — `.Helpers.cs`.

**Дев-інструменти.** На `Development`, `E2E` і `Tailscale` в застосунку є `DevToolsController`
(`api/dev/impersonate`, `api/dev/impersonate/member`, `api/dev/return`) і перемикач ролей на правому
краю екрана: адміністратор заходить як той, хто тримає обране діловодство у курені на екрані (Звʼязковий,
впорядник, курінний, скарбник), як юнак без діловодства або як та людина, чия картка відкрита, і
повертається за 12-годинним квитком (`IJwtService`, окрема audience). Перемикання з однієї позиченої
ролі в іншу йде через повернення: фронт спершу віддає квиток (`api/dev/return`), потім позичає знову,
бо позичена сесія не адмін і сама позичати не може. На решті тирів `DevOnlyControllerFeatureProvider` знімає контролер з моделі, а фронт не
рендерить перемикач у production-збірці.

**Довірений пристрій для MFA.** Після вдалого другого кроку (`mfa/login-verify`) API кладе
HttpOnly-cookie `mfaTrust` (`MfaTrustCookie`, шлях `/api/auth`) з JWT-квитком окремої audience
`mfa-trust` на `Security:MfaTrustDays` (7). Квиток несе security stamp акаунта; `LoginUserCommandHandler`
пропускає другий крок, лише коли квиток чинний, виданий цьому акаунту і stamp не змінився — зміна
пароля чи скидання MFA ротує stamp і знімає довіру з усіх пристроїв. Вихід cookie не чіпає.

**Активація акаунта.** `POST onboarding/activate` відповідає `LoginUserDto` і ставить refresh-cookie:
людина, яка щойно обрала пароль, потрапляє одразу в застосунок, а не на форму входу.

**Сайт.** `site/` — візитка, довідка і статичне демо на Astro + Starlight, окрема статика на
Cloudflare Pages. Довідка не пишеться на сайті: «Для користувача» — `docs/user/*` (початок, ролі,
функції, покрокові how-to); «Для розробника» — `docs/dev/*` (основи з діаграмами архітектури,
гайди, довідник, історія, DevLog) плюс кореневі `ARCHITECTURE`, `CONTRIBUTING`, `BRANDBOOK`,
`SECURITY`, `docs/self-host/*`, які `site/scripts/sync-docs.mjs` збирає в `site/src/content/docs/`
перед `dev` і `build`, дописуючи frontmatter і переписуючи посилання між ними. Дерево розробника в
бічній панелі згорнуте, пряма адреса — `/dev/`. Діаграми — Mermaid у markdown, рендер у браузері
з кнопкою «На весь екран». Стиль — `site/src/styles/brand.css` з тими ж токенами, що й
`lileyka-theme.css`.

**Адреса відвідувача.** Рейт-ліміт входу, гео-блок і журнал зміни IP читають
`Connection.RemoteIpAddress`. Звідки він береться — `Security:ClientIp`: `Header` називає заголовок,
який пише єдиний проксі попереду (`CF-Connecting-IP` за Cloudflare у `Production`/`Staging`,
`X-Real-IP` від nginx у self-host bundle), і `ClientIpMiddleware` кладе його значення в адресу
зʼєднання одразу після `UseForwardedHeaders`; `TrustedProxies` — адреси або мережі, чий
`X-Forwarded-For` приймається. Без `Header` адреса — те, що лишив `X-Forwarded-For`, а він
приймається від будь-кого, поки список довірених проксі порожній. Запуск відмовляється від
`Jwt:Key`, коротшого за 32 символи або з шаблонним словом із прикладів (`JwtKeyRules`).

**Вхід: ліміт на адресу й на пошту.** `RateLimiting:StrictAuth` рахує запити на адресу (типово 20 за
5 хвилин): курінь, що входить разом з одного шкільного Wi-Fi, — одна адреса, тож ліміт лишає місце для
гуртка. Підбір пароля до одного акаунта зупиняє `LoginThrottle`: після `LoginThrottle:FailureLimit`
(10) неправильних паролів на одну пошту вхід на неї стоїть до кінця `LoginThrottle:Window` (15 хв) від
першої помилки, з будь-яких адрес, і відповідає 429 `TooManyAttempts`. Пошту без акаунта рахує так
само, щоб пауза не видавала, хто зареєстрований; лічильник у памʼяті процесу, перезапуск його скидає;
вдалий вхід обнуляє.

---

## Опис API

Swagger віддає повний опис поверхні в `Development` і `Staging`: `/swagger`. Кожен ендпоінт має
`<summary>` (навіщо він) і, де це неочевидно, `<remarks>` (хто може викликати, що ще станеться).
Форма помилки описана один раз — `UnifiedErrorResponsesFilter`, а не повторюється на кожній дії.

Перевірити, що документ узагалі збирається, можна без запуску застосунку:

```bash
swagger tofile --output swagger.json Backend/ProjectK.Backend/ProjectK.API/bin/Release/net10.0/ProjectK.API.dll v1
```

---

## Тести

| Проєкт | Що покриває |
|---|---|
| `ProjectK.BusinessLogic.Tests` | хендлери: доменні рішення |
| `ProjectK.Infrastructure.Tests` | репозиторії, сховища |
| `ProjectK.API.Tests` | контролери, серіалізація, **і вся авторизація** — матриця політик, ресурсні перевірки, анонімний доступ |
| `ProjectK.Architecture.Tests` | межі й розкладка: залежності шарів (`LayerRules`), імена запитів і хендлерів, а ще `sealed` на запитах, хендлерах, обробниках подій і валідаторах (`NamingRules`), чужі репозиторії й сутності мембера, події замість прямих викликів (`MemberBoundaryRules`), модулі не називають хендлерів, служб і кодів помилок одне одного, а сутності не мають навігацій у чужий модуль (`ModuleBoundaryRules`), таблиця журналу (`AuditBoundaryRules`), розкладка модулів і зрізів (`LayoutRules`), контролери (`ApiRules`), вісім видів `Common` (`CommonRules`), суфікси static-класів, служб і `Directory` (`SuffixRules`), мапінг без AutoMapper (`MappingRules`), репозиторії лише через UoW (`RepositoryRules`), коди помилок з каталогу модуля — і в хендлерах, і в контролерах (`ErrorRules`) |
| `ProjectK.LoadTests` | навантаження |
| `Frontend/.../e2e` | Playwright проти docker-стека |

Бейслайн і команди перевірки — у [CONTRIBUTING.md](CONTRIBUTING.md#перевірка).
