<picture>
  <source media="(prefers-color-scheme: dark)" srcset=".github/assets/banners/readme-dark.png">
  <img alt="Лілейка — система для пластового куреня" src=".github/assets/banners/readme-light.png">
</picture>

# Лілейка

Система для пластового куреня: склад, проби й вмілості, календар і задачі, планування табору,
звіт куреня. Працює в хмарі або на сервері станиці.

Незалежний проєкт, не офіційний ресурс НСОУ «Пласт». Зроблено пластуном для пластунів.

**[Довідка](https://docs.lileyka.app/)** ·
**[Демо](https://docs.lileyka.app/demo/)** ·
**[Приєднати курінь](https://lileyka.app/join)** ·
**[Власний сервер](https://docs.lileyka.app/user/self-host/)** ·
**[Релізи](https://github.com/lileyka-app/Project-K/releases)**

[![Release](https://img.shields.io/github/v/release/lileyka-app/Project-K)](https://github.com/lileyka-app/Project-K/releases)
[![.NET](https://github.com/lileyka-app/Project-K/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/lileyka-app/Project-K/actions/workflows/dotnet.yml)
[![Angular](https://github.com/lileyka-app/Project-K/actions/workflows/angular.yml/badge.svg?branch=main)](https://github.com/lileyka-app/Project-K/actions/workflows/angular.yml)

## Документація

Уся документація — на сайті: [для користувачів](https://docs.lileyka.app/) і
[для розробників](https://docs.lileyka.app/dev/) — архітектура, запуск, сутності,
міграції, власний сервер, DevLog. Джерела лежать у `docs/`, сайт збирається з `site/`.

## Запуск для розробки

Потрібні .NET 10 SDK, Node ≥ 22.22.3 і Docker.

```bash
./scripts/dev.sh tools up          # спільні SQL і Azurite, один раз
./scripts/dev.sh up dev --build    # застосунок на http://localhost:4200, API на 5205
```

Перед змінами: [`CONTRIBUTING.md`](CONTRIBUTING.md) — конвенції,
[`ARCHITECTURE.md`](ARCHITECTURE.md) — з чого складається система,
[`BRANDBOOK.md`](BRANDBOOK.md) §0 — перед будь-якою зміною інтерфейсу.

## Повідомити про проблему

У застосунку — кнопка **«Повідомити про проблему»** внизу бічного меню. Про вразливість —
приватно, за [`SECURITY.md`](SECURITY.md).

## Хочеш допомогти

Є ідея чи знайшов, що виправити, — спершу відкрий [issue](https://github.com/lileyka-app/Project-K/issues),
обговоримо. Далі — pull request. Форк для цього можна, для іншого — ні (див. нижче).

## Ліцензія

Код відкритий для перегляду, але це не open source. Коротко, що дозволяє [`LICENSE`](LICENSE):

- **можна** читати код, запускати Лілейку й ставити на свій сервер для куреня чи станиці —
  такою, як вона є в репозиторії й релізах, і налаштовувати через документовані параметри;
- **можна** форкати, щоб запропонувати зміни сюди через pull request;
- **обовʼязково** лишати сторінку «Про Лілейку» з автором і посиланнями — незмінною;
- **не можна** змінювати систему під себе, перевипускати, поширювати чи видавати під іншою назвою,
  переписувати на свій лад або брати код і алгоритми в інші проєкти.

Уся система, її код і алгоритми належать автору — Ростиславу Мусі. Продукт — **Лілейка**;
репозиторій, образи Docker і CI — **ProjectK**. Юридично чинний текст — англійський, у `LICENSE`.
