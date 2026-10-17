# Оновлення self-host-інсталяції

Для інсталяцій, поставлених із Docker self-host bundle релізу.

## Перед оновленням

1. Прочитай нотатки до релізу на [GitHub Releases](https://github.com/lileyka-app/Project-K/releases):
   там сказано, чи є нові міграції і нові змінні `.env`.
2. Зроби [копії бази і файлів](backup-restore.md).
3. Збережи поточний `.env`.
4. Перевір, що томи на місці:

```bash
docker volume ls --filter name=projectk
```

Дані живуть в іменованих томах Docker; оновлення образів їх не чіпає.

Ніколи не виконуй це, якщо не хочеш видалити всі дані інсталяції:

```bash
docker compose down -v
```

## Оновлення в тій самій теці bundle

У `.env` постав цільову версію:

```text
PROJECTK_VERSION=<версія>
```

Витягни нові образи з GHCR і перезапусти:

```bash
docker compose pull
docker compose up -d
```

API застосовує міграції EF Core на старті. Великі рефакторинги (як 0.20.0 з переходом на
членства) роблять це довше за звичайне — дай API хвилину і перевір `docker compose logs -f projectk-api`.

## Теги образів

Compose тягне образи за `PROJECTK_VERSION`:

```text
ghcr.io/lileyka-app/projectk-api:${PROJECTK_VERSION}
ghcr.io/lileyka-app/projectk-web:${PROJECTK_VERSION}
```

До переїзду в організацію `lileyka-app` образи лежали в `ghcr.io/iamavasya/…`. Якщо в твоєму
`compose.yml` досі стара адреса, заміни `iamavasya` на `lileyka-app`. Нові версії ще якийсь час
дублюються й за старою адресою, але з часом ця копія зникне.

Для живих інсталяцій — явна версія (наприклад `1.1.9`). Тег `beta` рухається до найновішої бети і
годиться лише тим, хто свідомо хоче йти за нею.

## Оновлення з нового bundle

1. Завантаж новий `projectk-<версія>-docker-selfhost`.
2. Розпакуй у нову теку.
3. Скопіюй туди свій `.env`.
4. Переглянь `.env.example` — чи не зʼявились нові змінні. У 1.1.x додались, зокрема,
   `PROJECTK_EMAIL_*` (пошта), `PROJECTK_TIME_ZONE`, `PROJECTK_KURIN_NUMBER_MAX` і
   `PROJECTK_WEBPUSH_*` (сповіщення на телефон) — див. [Self-host](README.md).
5. Виконай:

```bash
docker compose pull
docker compose up -d
```

Bundle використовує явні назви томів `projectk-sql-data` і `projectk-azurite-data`, тож дані
лишаються привʼязаними до інсталяції навіть при зміні назви теки.

## Якщо інсталяція старіша за 0.14.2-beta

Ранні bundle давали томам назви з префіксом теки, наприклад
`projectk-0.14.1-beta-docker-selfhost_projectk-sql-data`. Перед запуском нової версії з нової теки
перевір реальні назви томів (`docker volume ls --filter name=projectk`). Якщо дані в томах із
префіксом — зроби копію зі старої теки і віднови її в нові томи `projectk-sql-data` та
`projectk-azurite-data`.

## Перевір після оновлення

1. `docker compose ps` — усі чотири контейнери `running (healthy)`.
2. Здоровʼя API і його версія:

   ```bash
   docker compose exec projectk-api curl -fsS http://localhost:8080/health
   ```

   У відповіді `"status":"ready"` і нова версія.
3. Увійди в Лілейку й відкрий «Про Лілейку» внизу бічного меню: там версія застосунку й API.
4. Якщо щось не так — `docker compose logs -f projectk-api`.

## Відкат

Відкат безпечний лише коли схема бази сумісна з попередньою версією. Після невдалої міграції чи
великих змін — відновлюйся з копії, а не відкочуй образ.
