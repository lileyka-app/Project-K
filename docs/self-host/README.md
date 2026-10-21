# Self-host Лілейки

Лілейку можна підняти двома шляхами:

- локально з клонованого репозиторію — для проби і розробки;
- Docker self-host з готових образів GHCR через bundle релізу GitHub, без git і без збірки.

В обох випадках фронтенд (Angular) і бекенд (ASP.NET Core API) — окремі процеси; файли зберігає
Azurite, сумісне з Azure Blob сховище.

## Локально з клонованого репозиторію

Потрібні git, .NET SDK, Node.js, npm, SQL Server і Azurite на машині.

Windows:

```powershell
./scripts/doctor.ps1
./scripts/start-local.ps1
```

Linux/macOS:

```bash
chmod +x ./scripts/*.sh
./scripts/doctor.sh
./scripts/start-local.sh
```

Адреси:

- фронтенд: http://localhost:4200
- API: http://localhost:5205
- Swagger: http://localhost:5205/swagger
- Azurite: http://127.0.0.1:10000/devstoreaccount1

Зупинити: `./scripts/stop-local.ps1` або `./scripts/stop-local.sh`. Логи — в `.tmp/local-run/logs`.

Альтернатива без встановлення SDK — контейнерний стек із `docker/`: `./dev.sh up dev --build`
(див. `docker/README.md`).

## Docker self-host без git

Для сервера чи машини, де є лише Docker.

Образи публікуються в GitHub Container Registry:

- `ghcr.io/lileyka-app/projectk-api:<версія>`
- `ghcr.io/lileyka-app/projectk-web:<версія>`

Найпростіший шлях — bundle з GitHub Releases: `projectk-<версія>-docker-selfhost.zip` або `.tar.gz`.
Всередині `docker-compose.yml`, `.env.example` і ця документація. Бінарників там немає і нічого не
збирається — Docker тягне готові образи.

Windows:

```powershell
Expand-Archive projectk-<версія>-docker-selfhost.zip
cd projectk-<версія>-docker-selfhost
copy .env.example .env
notepad .env
docker compose up -d
```

Linux/macOS:

```bash
tar -xzf projectk-<версія>-docker-selfhost.tar.gz
cd projectk-<версія>-docker-selfhost
cp .env.example .env
nano .env
docker compose up -d
```

Три значення треба задати до першого запуску:

- `PROJECTK_VERSION` — версія образів, яку ставиш, наприклад `1.1.9` (без неї compose не
  запуститься; у `.env.example` стоїть версія цього bundle);
- `PROJECTK_JWT_KEY` — щонайменше 32 випадкові символи (`openssl rand -base64 48`), з прикладним
  API не стартує;
- `PROJECTK_SQL_PASSWORD` — і той самий пароль усередині `PROJECTK_DB_CONNECTION_STRING`.

Перший запуск триває кілька хвилин: SQL Server готує базу. Готово, коли всі чотири контейнери
в `docker compose ps` мають статус `running (healthy)`.

Відкрий http://localhost:8080. Перший візит веде на майстер налаштування, який створює
адміністратора. До цього жодного акаунта немає і нічого не засіюється.

Bundle публікує один порт — веб. API, SQL Server і сховище лишаються в мережі compose: nginx у
`projectk-web` проксить `/api` до API і `/blob` до сховища, тому `PROJECTK_API_URL` — відносний
`/api`, і ззовні більше нічого не має бути видно.

Перевірка здоровʼя API (сам API назовні не опублікований):

```bash
docker compose exec projectk-api curl -fsS http://localhost:8080/health
```

Відповідь здорового API — `"status":"ready"` і версія, яку ти поставив:

```json
{"status":"ready","version":"v1.1.9","codeName":null,"utc":"…","api":{"windowMinutes":15,"requests":42,"p50Ms":3.1,"p95Ms":18.4}}
```

`codeName` — кодова назва збірки, `null`, якщо її немає (з 1.0 релізи без назв). `api` — скільки
часу сам API витратив на справжні запити за останні 15 хвилин: їх кількість, медіана й 95-й
перцентиль у мілісекундах. Самі `/health`, корінь `/` і CORS-preflight сюди не входять; без жодного
запиту `p50Ms` і `p95Ms` — `null`. База під час цієї перевірки не опитується.

## Перед виходом в інтернет

Bundle слухає звичайний HTTP і розрахований на реверс-проксі, який термінує TLS (Caddy, nginx,
Traefik або проксі хостера). Далі:

1. У `.env` постав публічну адресу: `PROJECTK_PUBLIC_URL`, `PROJECTK_CORS_ORIGIN` і хост у
   `PROJECTK_BLOB_PUBLIC_BASE_URL` стають `https://твій.домен`. `PROJECTK_API_URL` лишається `/api`.
2. Спрямуй реверс-проксі на `127.0.0.1:8080` (або що в `PROJECTK_WEB_PORT`) і передавай адресу
   відвідувача; nginx у `projectk-web` віддає її API як `X-Real-IP` — за нею обмежуються спроби входу.
3. SQL Server і Azurite не публікуй. Якщо база потрібна з хоста для бекапу — додай
   `docker-compose.override.yml`, який публікує `projectk-sql` лише на `127.0.0.1:1433`.
4. Фото лежать у закритому контейнері, API видає на них підписані посилання на кілька годин. При
   старті API сам вимикає публічний доступ до контейнера, якщо він лишився від старої версії.
   Скриншоти з повідомлень про проблеми — виняток: посилання на них діє рік, бо воно вбудоване
   в issue на GitHub.
5. Зроби копію обох томів до перших реальних даних і перед кожним оновленням —
   [backup-restore.md](backup-restore.md).
6. Увімкни двофакторну автентифікацію адміністратору і кожному Звʼязковому. У налаштуваннях
   системи її можна зробити обовʼязковою для привілейованих акаунтів (на self-host вона типово
   вимкнена, `Security__EnforcePrivilegedMFA`).

## Мобільна версія і сповіщення на телефон

Мобільна версія входить у той самий образ `projectk-web` і відкривається на `/m/`. Встановити її
на телефон можна лише з HTTPS-адреси; QR-код «Встановити на телефон» у вебі веде на
`PROJECTK_PUBLIC_URL` + `/m/install`, тож адреса в `.env` має бути публічною.

Сповіщення на телефон потребують пари ключів VAPID. Без них сповіщення лишаються всередині
застосунку, а пункту «Сповіщення на телефон» у «Меню» немає. Згенеруй власну пару (потрібен лише
Node.js, на будь-якій машині):

```bash
npx web-push generate-vapid-keys
```

і впиши в `.env`:

```dotenv
PROJECTK_WEBPUSH_PUBLIC_KEY=<Public Key>
PROJECTK_WEBPUSH_PRIVATE_KEY=<Private Key>
PROJECTK_WEBPUSH_SUBJECT=mailto:admin@твій-домен.org
```

Приватний ключ — секрет. Якщо його змінити, телефони, які вже підписались, перестануть отримувати
сповіщення, поки люди не ввімкнуть їх знову. Після зміни `.env` — `docker compose up -d`.

## Час

Повторювані події розгортаються за місцевим часом стіни, тож година сходин не зсувається на
переході між літнім і зимовим часом. Часовий пояс, як і валюту вкладки, задає край: адміністратор
відкриває «Краї» → «Валюта й час» і вписує IANA-ідентифікатор (наприклад `Europe/Prague`). Курені
краю живуть за його поясом.

`PROJECTK_TIME_ZONE` у `.env` (`Agenda__TimeZone`, типово `Europe/Kyiv`) — лише запасний пояс для
краю, у профілі якого пояс не заданий.

## Числа куренів

Число куреня приймається від 1 до 200. Нуль і менші лишаються для власних куренів команди
(наприклад, тестового). Коли станиця переросте двісті куренів, верхню межу піднімає
`PROJECTK_KURIN_NUMBER_MAX` у `.env`. Підготовчий курінь числа не має, доки його не задасть у
налаштуваннях.

Число унікальне в межах краю: у двох краях може бути свій курінь 12. Новий курінь потрапляє до
краю з `PROJECTK_DEFAULT_KRAI` (типово `ua`), а адміністратор може перевести його до іншого краю
в списку куренів.

## Пошта

Без поштового провайдера запрошення й відновлення пароля нікуди не підуть — у логах API буде
лише текст листа (`PROJECTK_EMAIL_PROVIDER=Mock`, так стоїть у `.env.example`). Для живої
інсталяції задай у `.env` Resend:

```dotenv
PROJECTK_EMAIL_PROVIDER=Resend
PROJECTK_EMAIL_API_KEY=re_…
PROJECTK_EMAIL_FROM=lileyka@mail.твій-домен.org
PROJECTK_EMAIL_FROM_NAME=Лілейка
PROJECTK_EMAIL_REPLY_TO=hello@твій-домен.org
```

Адреса відправника має бути на домені, який ти підтвердив у Resend. `PROJECTK_EMAIL_REPLY_TO` —
необовʼязкова скринька, куди люди зможуть відповісти на лист. Після зміни `.env`:
`docker compose up -d` (контейнер API перезапуститься з новими значеннями).

До 1.1.10 ці значення в `.env` нікуди не потрапляли: compose не передавав їх у контейнер API, і
листи лишались у логах навіть із заданим ключем.

## Зворотний звʼязок

«Повідомити про проблему» в сайдбарі застосунку відкриває issue в GitHub. Для цього в `.env`
потрібен `PROJECTK_FEEDBACK_GITHUB_TOKEN` — fine-grained токен з правом «Issues: write» на
репозиторій із `PROJECTK_FEEDBACK_GITHUB_REPOSITORY` (типово `lileyka-app/Project-K`, тобто
повідомлення йдуть авторові системи). Без токена повідомлення пишуться в лог API
(`docker compose logs projectk-api`) і нікуди не надсилаються. Скриншоти з повідомлень лежать у
теці `feedback-screenshots` того ж сховища, що й фото, а в issue йде посилання на них, чинне рік.

## Телеметрія

Раз на добу API надсилає авторові Лілейки анонімний пінг на `https://status.lileyka.app/v1/ping`. Так автор бачить, скільки інсталяцій працює, на яких версіях і з яким навантаженням. Перший пінг іде через 5 хвилин після старту. У пінгу є:

- випадковий id інсталяції (створюється при першому пінгу й лежить у таблиці `SystemSettings`);
- версія і тип (`selfhost`);
- коди країв, у яких є курені;
- кількість куренів, членів і користувачів, що входили за 7 і 30 днів;
- кількість запитів до API і відповідей 5xx від попереднього пінгу;
- скільки годин працює процес.

Імен, пошт, назв куренів, адреси сервера в пінгу немає, а IP сервіс не зберігає. У відповідь приходить номер останнього релізу, і застаріла інсталяція пише про це в лог API. Щоб вимкнути пінг, задай `PROJECTK_TELEMETRY=false` у `.env`. Пінг працює лише за значень `true`, `1`, `yes` або `on`; будь-яке інше (`false`, `0`, `no`, `off`, порожнє `PROJECTK_TELEMETRY=`) його вимикає. Якщо рядка в `.env` немає зовсім, пінг увімкнений.

## Установка прямо з образів GHCR

Bundle — підтримуваний шлях: compose-файл у ньому узгоджений з релізом. Якщо в тебе вже є своя
тека розгортання — створи `.env` за `docker/selfhost/.env.example`, скопіюй
`docker/selfhost/compose.bundle.yml` як `docker-compose.yml` і виконай:

```bash
docker compose pull
docker compose up -d
```

Перевірити доступність образів:

```bash
docker pull ghcr.io/lileyka-app/projectk-api:<версія>
docker pull ghcr.io/lileyka-app/projectk-web:<версія>
```

Тег `beta` вказує на останню бету. Для живих інсталяцій бери явну версію; `beta` — лише якщо
свідомо йдеш за найновішим.

## Контейнери

Compose-файл bundle запускає лише готові образи:

- `projectk-web` — nginx зі збіркою Angular;
- `projectk-api` — ASP.NET Core API;
- `projectk-sql` — SQL Server;
- `projectk-azurite` — емулятор сховища Azurite.

Фронтенд читає `PROJECTK_API_URL`, `PROJECTK_ENVIRONMENT_NAME` і `PROJECTK_DOCS_URL` (корінь сайту довідки; «Довідка» на вітальній веде на нього, у сайдбарі — на сторінку `/user/start/what-is/`; типово — `https://docs.lileyka.app/`) при старті
контейнера і записує в `env.js`, тож один образ працює на будь-якому домені без перезбірки.

## Томи

Compose створює іменовані томи зі стабільними назвами:

- `projectk-sql-data` — дані SQL Server;
- `projectk-azurite-data` — завантажені файли.

Контейнери можна перестворювати при оновленнях — дані лишаються в томах. Не виконуй
`docker compose down -v`, якщо не хочеш видалити всі дані. Копія обох томів — перед оновленням і
переїздом.

## Корисні команди

```bash
docker compose ps
docker compose logs -f projectk-api
docker compose logs -f projectk-web
docker compose down
docker compose pull
docker compose up -d
```

## Збірка образів локально

Bundle не потребує .NET, Node.js, npm чи NuGet-токена. Якщо збираєш образи з репозиторію сам —
для приватних NuGet-пакетів потрібен `PROJECTK_NUGET_AUTH_TOKEN` у середовищі. Він іде в збірку
секретом BuildKit і в образ не потрапляє; в `.env` його не клади.

Далі: [оновлення](update.md), [резервні копії](backup-restore.md).
