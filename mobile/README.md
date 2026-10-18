# Лілейка Mobile

Ionic 9 + Angular 22 (zoneless) + Capacitor 8. План: [PLAN.md](PLAN.md).

Статус: Фаза 0 пройдена (див. PLAN.md §17). Зараз основний шлях — PWA: той самий код у браузері,
з Material-виглядом на Android і iOS-виглядом на iPhone. Нативні збірки Capacitor чекають.

## Вимоги

- Node.js **22.22.3+** або **24.15+** (Angular CLI 22 нижчі версії не запускає).
- Android Studio (з Android SDK 36 і емулятором), JDK 21 (іде з Android Studio).

## Команди

```bash
npm ci
npm start                 # ng serve на http://localhost:4200
npm test                  # vitest
npm run build             # прод-збірка у www/
npm run sync              # build + cap sync (копіює www/ у native-проєкти)
npm run e2e               # прод-збірка + PWA-смоук у Playwright (iPhone/WebKit і Pixel/Chromium)
```

## PWA

PWA живе на тому ж домені, що й веб, під шляхом `/m/` (наприклад
`lileyka.app/m/`): той самий origin у CORS, ті самі cookie і той самий `/env.js`
з адресою API, тож self-host працює без окремого домену. Збирає її `npm run build:pwa`
(`--base-href /m/`); у хмарі її кладе в сайт `angular.yml`, у self-host — `Frontend/projectk-frontend/Dockerfile`
(nginx: `docker/nginx/projectk-frontend.conf`).

Прод-збірка містить маніфест, іконки і service worker (`ngsw-config.json`). Service worker
вмикається лише в прод-збірці і лише в браузері, у нативних оболонках він вимкнений.
Перевірити локально: `npm run build:pwa && node scripts/serve-www.mjs`, далі http://127.0.0.1:4300/m/.

Перед першим `npm run e2e` постав браузери: `npm run e2e:install`. Скріни лягають у
`test-results/pwa-screens/`.

У браузері режим Ionic можна примусити: `http://localhost:4200/?ionic:mode=ios` або `?ionic:mode=md`.

## Android-емулятор з live reload (Windows)

Два термінали в `mobile/`:

```powershell
# 1. dev-сервер
npm start

# 2. емулятор має бути запущений (Android Studio → Device Manager)
npx cap run android -l --host 10.0.2.2 --port 4200
```

`10.0.2.2` — це `localhost` хост-машини зсередини емулятора. `cap run -l` сам підставляє
`server.url` і cleartext, а на Ctrl+C повертає конфіг назад. Збережи файл у `src/` — екран
на емуляторі оновиться.

Відкрити проєкт в Android Studio: `npx cap open android`.

Dev-збірка (`npm start`) ходить на бекенд `http://10.0.2.2:5205/api`, прод — у хмару.
