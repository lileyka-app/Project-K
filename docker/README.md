# Контейнери для розробки

Будь-яке середовище — `dev`, `e2e`, `demo`, `selfhost`, `tailscale`, `staging`, `prod` — можна підняти
локально в контейнерах, вибираючи його однією змінною. Образи збираються самому або беруться
готові. Локальні образи **лише тегуються і ніколи не пушаться** в реєстр.

## Як це працює

- **Образ .NET API не залежить від середовища.** Один зібраний образ запускається як будь-яке
  середовище; його вибирає `ASPNETCORE_ENVIRONMENT`, який підтягує відповідний
  `appsettings.<Env>.json`. Рядки підключення, CORS і JWT приходять зі змінних середовища.
- **Збірка Angular запікається на середовище** (build arg `NG_CONFIG`: `development` / `staging` /
  `tailscale` / `production`), але кілька значень підмінюються на старті через `env.js`: адреса API
  (`PROJECTK_API_URL`), назва середовища в бейджі сайдбару (`PROJECTK_ENVIRONMENT_NAME`) і корінь
  сайту довідки (`PROJECTK_DOCS_URL`): «Довідка»
  на вітальній веде на нього, а в сайдбарі — на його сторінку `/user/start/what-is/`. Дев-контейнери
  типово дивляться на dev-деплой `docs-dev.lileyka.app`, реліз і self-host — на
  production `docs.lileyka.app`.
- **SQL Server + Azurite — спільна інфраструктура.** Вони запускаються один раз
  (`compose.tools.yml`) у мережі `projectk-dev-net`. Стек кожного середовища підключається до неї і
  працює зі **своєю базою** на тому ж сервері (`projectK_dev`, `projectK_e2e`, …): інструменти
  спільні, дані ізольовані.

```
docker/
  compose.tools.yml         спільні SQL + Azurite (запускаються раз)
  compose.app.yml           параметризовані API + Web (на середовище)
  compose.dev.override.yml  hot-reload (dotnet watch / ng serve)
  env/
    dev.env  e2e.env  demo.env  selfhost.env  tailscale.env   (у git, локальні дефолти)
    staging.env.example  prod.env.example           (скопіювати → заповнити секрети)
  nginx/                    конфіг nginx, запечений у веб-образ
    projectk-frontend.conf  40-projectk-env.sh
  selfhost/                 артефакти self-host (пакуються в bundle релізу)
    compose.yml             self-host зі збіркою з джерел
    compose.bundle.yml      self-host з готових образів (GHCR)
    .env.example            шаблон .env для self-host
```

Оркестратор — `scripts/dev.sh` / `scripts/dev.ps1`, з тонкими обгортками `./dev.sh` / `./dev.ps1` у
корені репозиторію. Див. [довідник команд](#довідник-команд).

## NuGet-токен (ніколи не комітити)

Збірка образу **API** відновлює приватні NuGet-пакети з GitHub, для чого потрібен токен GitHub. Він
читається зі **змінної хоста `NUGET_AUTH_TOKEN`** — compose-файли беруть її автоматично
(`${PROJECTK_NUGET_AUTH_TOKEN:-${NUGET_AUTH_TOKEN:-}}`).

**Не клади токен у `docker/env/*.env`** — ці файли в git. Задай його в шелі:

```powershell
# PowerShell — поточна сесія
$env:NUGET_AUTH_TOKEN = "ghp_твій_токен"
# …або назавжди для користувача (один раз)
setx NUGET_AUTH_TOKEN "ghp_твій_токен"   # після цього перевідкрий шел
```

```bash
# bash — додай у ~/.bashrc / ~/.profile
export NUGET_AUTH_TOKEN="ghp_твій_токен"
```

Немає токена? Не збирай, а витягни готовий образ (див. [два способи запуску](#два-способи-запуску)).

## Покроково

Передумови: **Docker Desktop запущений**, і `NUGET_AUTH_TOKEN` у шелі, якщо збираєш локально.

Все йде через оркестратор. З кореня репозиторію — `./dev.ps1` (PowerShell) або `./dev.sh`
(bash / Git Bash); вони прокидають у `scripts/dev.*`, які можна викликати і напряму. Команди
однакові й працюють з будь-якої теки.

З **кореня репозиторію**:

```powershell
# 1. Спільна інфраструктура, раз на сесію (SQL + Azurite у projectk-dev-net)
./dev.ps1 tools up

# 2. Зібрати й запустити середовище. Перший запуск довгий (restore + ng build);
#    API сам застосовує міграції EF і засіює демо-дані на старті.
./dev.ps1 up dev --build         # web http://localhost:4200, API http://localhost:5205/api

# 3. Відкрий застосунок. Swagger для dev — http://localhost:5205/swagger.

# 4. Щодня
./dev.ps1 logs dev               # логи (додай назву сервісу, щоб звузити)
./dev.ps1 ps dev                 # стан контейнерів
./dev.ps1 down dev               # зупинити (дані лишаються в томах tools)
./dev.ps1 down dev -v            # зупинити + видалити томи цього середовища
```

Інші середовища — та сама команда з іншою назвою; порти унікальні, тож можна тримати кілька
паралельно (див. [матрицю](#матриця-середовищ)):

```powershell
./dev.ps1 up selfhost --build    # web http://localhost:8080 — майстер /setup при першому вході
./dev.ps1 up e2e --build         # web http://localhost:4201 (окрема база projectK_e2e)
```

bash — те саме з `./dev.sh`:

```bash
./dev.sh tools up
./dev.sh up dev --build
./dev.sh down dev
```

Демо-облікові записи середовища `dev` (лише сід розробки, не прод): `admin@projectk.com` /
`Admin@12345` і `demo0…demoN@projectk.com` / `User@12345`, де `demo0` — Звʼязковий демо-куреня.
У `dev.env` пошта — `Resend` з порожнім ключем; для локальних сценаріїв з листами постав
`Email__Provider=Mock`.

## Довідник команд

Два рівнозначні входи, з будь-якої теки репозиторію:

- **`./dev.ps1 <команда>`** (PowerShell) / **`./dev.sh <команда>`** (bash / Git Bash) — обгортки в
  корені. Рекомендовано.
- **`./scripts/dev.ps1 <команда>`** / **`./scripts/dev.sh <команда>`** — самі скрипти.

`<env>` — одне з: `dev`, `e2e`, `demo`, `selfhost`, `tailscale`, `staging`, `prod`.

Довідка (`site/`, Astro) піднімається поруч зі стеком як сервіс `projectk-docs`, коли в env-файлі
задано `PROJECTK_DOCS_PORT` (у `tailscale.env` — 4230). Образ збирається з `site/Dockerfile` з
кореня репозиторію, тож у нього потрапляє поточний стан `docs/` і кореневих документів.

`demo` — публічний демо-курінь: ті самі демо-дані, що й у `dev`, вхід без пароля кнопками на
сторінці входу (`POST api/demo/login`, контролер існує лише в цьому середовищі), щоночі о 03:00 UTC
база повертається до засіяного стану (`DemoResetBackgroundService`). Хоститься окремо від проду.

### Спільна інфраструктура — раз на сесію

| Команда | Що робить |
|---------|-----------|
| `tools up`   | Запускає SQL Server + Azurite у мережі `projectk-dev-net` |
| `tools down` | Зупиняє (томи лишаються; `-v` — видалити) |
| `tools logs` | Логи інфраструктури |
| `tools ps`   | Контейнери інфраструктури |

### Стек середовища — API + Web

| Команда | Що робить |
|---------|-----------|
| `up <env>`              | Запустити API + Web з наявних образів |
| `up <env> --build`      | Спершу зібрати образи, потім запустити |
| `up <env> --pull`       | Витягнути образи (`--pull always`), потім запустити |
| `watch <env>`           | **Hot-reload** (bind-mount + `dotnet watch` / `ng serve`); у форграунді, Ctrl-C зупиняє |
| `build <env>`           | Лише зібрати образи |
| `pull <env>`            | Витягнути теги `PROJECTK_API_IMAGE` / `PROJECTK_WEB_IMAGE` |
| `down <env>`            | Зупинити стек (дані лишаються в спільній інфраструктурі) |
| `down <env> -v`         | Зупинити й видалити томи середовища |
| `logs <env> [service]`  | Логи; можна один сервіс (`projectk-api` / `projectk-web`) |
| `ps <env>`              | Контейнери середовища |
| `--help`                | Підказка |

Нюанси:

- `up` і `watch` самі піднімають інфраструктуру й мережу `projectk-dev-net`, якщо вони ще не
  запущені — `tools up` наперед не обовʼязковий.
- Передавай **лише** перелічені опції; `--build` з `watch` не поєднуй (hot-reload працює на базових
  образах SDK/Node, а не на зібраних).
- Кожне середовище — окремий compose-проєкт (`projectk-<env>`) з унікальними портами, тож кілька
  можуть працювати одночасно.

## Матриця середовищ

| env       | ASPNETCORE_ENVIRONMENT | NG_CONFIG   | Web  | API  | База                |
|-----------|------------------------|-------------|------|------|---------------------|
| dev       | Development            | development | 4200 | 5205 | projectK_dev        |
| e2e       | E2E                    | development | 4201 | 5206 | projectK_e2e        |
| demo      | Demo                   | production  | 4220 | 5255 | projectK_demo       |
| selfhost  | SelfHost               | production  | 8080 | 5215 | projectK_selfhost   |
| tailscale | Tailscale              | tailscale   | 4210 | 5225 | projectK_tailscale  |
| staging   | Staging                | staging     | 8090 | 5235 | projectK_staging *  |
| prod      | Production             | production  | 8095 | 5245 | projectK_prod *     |

\* `staging` / `prod` є лише як `*.env.example`. Скопіюй у `docker/env/<env>.env` (у .gitignore) і
постав справжні секрети. Дефолти вказують на локальну інфраструктуру для перевірки конфігу;
для хмари підміни рядки підключення і JWT.

## Два способи запуску

**Зібрати локально.** Потрібен `NUGET_AUTH_TOKEN` у шелі
(див. [NuGet-токен](#nuget-токен-ніколи-не-комітити)), далі `./dev.ps1 up <env> --build`.

**Готовий образ.** Постав `PROJECTK_API_IMAGE` / `PROJECTK_WEB_IMAGE` в env-файлі на конкретний тег
(наприклад `ghcr.io/lileyka-app/projectk-api:1.0`), далі `./dev.ps1 pull <env>` і
`./dev.ps1 up <env>` — ні збірки, ні токена.

## Телефонна PWA (`/m/`)

Веб-образ (`Frontend/projectk-frontend/Dockerfile`) збирає і мобільну PWA з `mobile/` і віддає її тим
самим nginx за адресою `<web>/m/`. Тож будь-яке середовище, підняте з `--build`, уже має PWA поруч із
вебом, з тим самим API (`PROJECTK_API_URL` з `env.js`) і тією самою сесією: вхід у вебі — вхід у PWA.

```powershell
./dev.ps1 up dev --build         # PWA на цьому компі: http://localhost:4200/m/
./dev.ps1 up tailscale --build   # з будь-якого пристрою в tailnet: http://100.64.66.7:4210/m/
```

- На комп'ютері зручно дивитися в DevTools у режимі пристрою (iPhone чи Pixel): Ionic вибирає вигляд
  iOS чи Material за пристроєм; `?ionic:mode=ios` або `?ionic:mode=md` у адресі примушує один із них.
- Для `tailscale` відкривай саме адресу tailnet (як і веб): CORS пускає одне джерело.
- `watch` (hot-reload) PWA не віддає: там `ng serve` лише вебу. Для PWA — `up <env> --build`.
- Звичайний http у tailnet — це не «безпечний контекст», тому service worker не реєструється: PWA
  працює як сторінка, але без офлайну, а Android не запропонує «Встановити» (на iPhone «Поділитися →
  На початковий екран» працює). Повна поведінка — на `localhost` або через HTTPS.

## Hot-reload

`./dev.ps1 watch dev` монтує джерела і запускає `dotnet watch` + `ng serve` у контейнерах — правки
підхоплюються на льоту. Працює у форграунді (Ctrl-C зупиняє). `--build` з `watch` **не** передавай.

> Нативний цикл `scripts/start-local.*` (без контейнерів) теж працює і лишається найшвидшим, якщо
> .NET SDK і Node стоять на хості.

## Якщо щось не так

- **`Cannot connect to the Docker daemon`** — Docker Desktop не запущений.
- **Збірка API падає на restore / `401 Unauthorized`** — `NUGET_AUTH_TOKEN` не заданий у тому шелі,
  з якого запущено скрипт, або токен протух. Перевір [NuGet-токен](#nuget-токен-ніколи-не-комітити)
  або візьми готовий образ.
- **Web віддає 502 одразу після старту** — API ще стартує / мігрує; веб-контейнер чекає його
  healthcheck, дай кілька секунд.
- **Скинути базу середовища** — `./dev.ps1 down <env> -v`, потім `up`. Стерти спільні SQL/Azurite
  повністю: `./dev.ps1 tools down`, потім `docker volume rm projectk-sql-data projectk-azurite-data`.
