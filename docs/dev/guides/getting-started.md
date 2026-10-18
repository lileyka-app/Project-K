---
title: Як запустити проєкт
description: Два шляхи підняти Лілейку локально — контейнери одним скриптом або повний ручний запуск.
sidebar:
  order: 1
---

Є два шляхи: швидкий через Docker і повний ручний. Для щоденної роботи достатньо першого.

## Швидкий старт — Docker

Проєкт повністю контейнеризований (`docker/`). Оркестратор — `scripts/dev.sh` (Git Bash на Windows теж
підходить); повна довідка — `docker/README.md`.

```bash
# 1. Спільний tooling (SQL Server + Azurite) — один раз
./scripts/dev.sh tools up

# 2. Зібрати й підняти dev-стек (API + Web)
./scripts/dev.sh up dev --build

# або з hot-reload (dotnet watch / ng serve)
./scripts/dev.sh watch dev

# Логи / статус / зупинка
./scripts/dev.sh logs dev
./scripts/dev.sh ps dev
./scripts/dev.sh down dev
```

Середовище обирається одним аргументом: `dev | e2e | demo | selfhost | tailscale | staging | prod`. Образ .NET
не залежить від середовища (воно задається через `ASPNETCORE_ENVIRONMENT`); SQL і Azurite спільні,
кожне середовище має власну базу на тому самому сервері. Dev-стек відповідає на
`http://localhost:4200` (web) і `http://localhost:5205` (API), e2e — на `4201` / `5206`.

:::note
Збірка образу API тягне приватні NuGet-пакети з GitHub — потрібна змінна середовища
`NUGET_AUTH_TOKEN` з персональним токеном. У репозиторій вона не потрапляє.
:::

Демо-дані сіються лише в `Development` і `E2E`: адміністратор `admin@projectk.com`, учасники
`demo0@projectk.com` … `demoN@projectk.com`. Паролі — у `DataSeeder`. У `Production` жодного
акаунта не сіється: перший адміністратор створюється майстром налаштування при першому запуску.

Демо-курінь (`DemoDataSeeder`) — це не порожній каркас, а курінь, у якому вже прожили кілька років:
три гуртки зі своїм проводом, КВ зі Звʼязковим (`demo0`), трьома впорядниками й інструктором,
двоє колишніх у реєстрі, ступені з датами, проби з підписаними точками на різних стадіях, вмілості
підтверджені й у черзі на розгляд, відзначення, перестороги, календар зі сходинами, таборами й
заходами (з відповідями «йду / не йду»), дошка задач і два сеанси планування табору. Дати рахуються
від сьогодні, тож минуле й майбутнє в календарі є завжди; випадковість із фіксованим зерном — на
кожній машині ті самі люди. Скид перед сідом стирає курінь 1 разом з агендою — свої правки в ньому
не переживуть перезапуск.

## Ручний запуск (без Docker)

### 1. Клонувати

```bash
git clone https://github.com/lileyka-app/Project-K.git
```

У корені — `Backend/`, `Frontend/`, `site/`, `docker/`, `docs/`.

### 2. SQL Server

Підійде **SQL Server 2022 Developer** з [сайту Microsoft](https://www.microsoft.com/uk-ua/sql-server/sql-server-downloads),
базова інсталяція з усім за замовчуванням. Express не рекомендується — бракує частини можливостей.
Для роботи з базою зручний [SSMS](https://learn.microsoft.com/en-us/ssms/install/install); при
підключенні до локального сервера постав «Trust server certificate».

### 3. Інструменти

- **.NET SDK 10.0**. Для бекенду зручна Visual Studio 2022 з модулями *ASP.NET and web development*,
  *Azure development*, *Data storage and processing*; VS Code теж підходить.
- **Node.js ≥ 22.22.3** (Angular 22 вимагає саме таку мінімальну версію). Найпростіше через
  [fnm](https://github.com/Schniz/fnm): `fnm install 22 && fnm use 22`.
- Angular CLI і Azurite глобально:

```bash
npm install -g @angular/cli azurite
```

### 4. Запуск

**Фронтенд**

```bash
cd Frontend/projectk-frontend
npm install
npm run start        # http://localhost:4200
```

**Azurite** — локальна емуляція Azure Blob Storage. Окрема тека поруч із репозиторієм:

```bash
azurite --location ./azurite --debug ./azurite/debug.log --skipApiVersionCheck
```

**Бекенд**

1. Додай змінну середовища користувача `NUGET_AUTH_TOKEN` (персональний токен GitHub з правом
   `read:packages`), перезапусти IDE.
2. Відкрий `Backend/ProjectK.Backend/ProjectK.Backend.sln`, профіль `https`, F5.
3. Міграції застосовуються автоматично на старті. Якщо SQL Server і Azurite працюють, у браузері
   відкриється Swagger.

## Далі

- [Технологічний стек](/dev/reference/tech-stack/)
- [Конвенції](/dev/core/contributing/) — прочитати до першого коміту
- [Архітектура](/dev/core/architecture/)
