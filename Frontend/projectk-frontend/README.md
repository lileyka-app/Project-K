# Лілейка — фронтенд

Angular 22 (standalone-компоненти, signals), `@openng/optimus-ui` (PrimeNG-сумісний форк) і Tailwind 4.
Мапа системи — у кореневому [ARCHITECTURE.md](../../ARCHITECTURE.md), конвенції коду — у
[CONTRIBUTING.md](../../CONTRIBUTING.md), візуальна система — у [BRANDBOOK.md](../../BRANDBOOK.md).

## Запуск

Потрібен Node 22.22.3 або новіший. Системна Node часто старша за це, тому версія береться через fnm:

```bash
fnm use 22
npm ci
npm start
```

Застосунок відкривається на http://localhost:4200 і ходить в API за адресою з `environment.ts`
(`http://localhost:5205/api`). У Docker-образі адреса підставляється при старті контейнера через
`env.js` (`PROJECTK_API_URL`), тому один образ працює для будь-якого хоста.

## Перевірка

```bash
npm run lint
npm test
npm run build -- --configuration production
```

Тести йдуть на Vitest у jsdom, браузер не потрібен; `npm run test-c` додає покриття. Бейслайн
кількості тестів і попереджень лінту та особливості Vitest — у `CONTRIBUTING.md` → «Перевірка».

## Оточення

`src/environments/` тримає по файлу на конфігурацію збірки (`development`, `staging`, `production`,
`tailscale`); `apiUrl` у кожному — лише запасне значення на випадок, коли `env.js` не підставлено.
Повний стек будь-якого оточення піднімається з кореня репозиторію: `./scripts/dev.sh up <env>`.
