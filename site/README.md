# Сайт Лілейки

Візитка і довідка на [Astro](https://astro.build) + [Starlight](https://starlight.astro.build).

- Довідка **не пишеться тут**: джерело — `../docs/user/*.md` (формат Starlight: frontmatter з `title`,
  картинки поруч у `images/`). Скрипт `scripts/sync-docs.mjs` копіює їх у `src/content/docs/user/`
  перед `build`, а в `npm run dev` ще й стежить за `docs/` і кореневими документами: збережений
  файл зʼявляється на сторінці за мить. Ця тека генерується і не комітиться.
- Головна сторінка — `src/content/docs/index.mdx`.
- Стиль — `src/styles/brand.css`, токени з `BRANDBOOK.md` і `lileyka-theme.css` застосунку.

```bash
npm install
npm run dev      # http://localhost:4321, з живим оновленням з docs/
npm run build    # dist/
```

## Правити довідку

Лише під `npm run dev`; на зібраний сайт нічого з цього не потрапляє.

- **`/editor`** — джерело сторінки ліворуч, сама сторінка праворуч. Зберігається само через ~0,7 с
  після паузи в наборі (або `Ctrl+S`), і права половина оновлюється сама. Курсор у тексті прокручує
  сторінку до свого розділу. Подвійний клік по слову на сторінці виділяє його в тексті. Якщо файл
  змінили деінде (VS Code, git), редактор не перезапише його мовчки, а спитає, що взяти. Межу між
  половинами можна тягнути.
- **Внизу кожної сторінки** — «Правити поруч зі сторінкою» (відкриває `/editor` на ній) і «Відкрити
  у VS Code».

- **Рев'ю змін** — змінене відносно `origin/main` (тобто того, що вже на сайті) стоїть на теплому
  тлі з «Як було», прибране закреслене, а **`/review`** перелічує всі нові й змінені сторінки з
  позначкою «вичитано». Інша база — `DOCS_REVIEW_BASE=<гілка чи коміт>`, вимкнути — `DOCS_REVIEW=0`,
  увімкнути в збірці для превʼю — `DOCS_REVIEW=1 npm run build`. Мітки сторінок (`status:` у
  frontmatter) і що з них іде в прод — `src/doc-status.ts`; правила — CONTRIBUTING, «Сайт і довідка».

Обидва ведуть на справжнє джерело (`docs/user/…`, `ARCHITECTURE.md` тощо), а не на копію в
`src/content/docs`. Яка сторінка з якого файлу — `scripts/doc-sources.mjs`; редактор і API —
`src/integrations/`.

## Демо

`/demo/` — статична збірка застосунку (`ng build --configuration demo`), у якій замість API працює
`DemoApiInterceptor` із записаними відповідями. Фікстури лежать у
`Frontend/projectk-frontend/public/assets/demo/*.json` і перезаписуються з docker-стеку `demo`
командою `node scripts/record-demo-fixtures.mjs` (з теки фронтенду). Збірка сайту разом із демо —
`bash scripts/build-site.sh` з кореня репозиторію; результат у `dist/`. Для Cloudflare Pages: build
command `bash scripts/build-site.sh`, output `site/dist`, версія Node з `.node-version` у корені
(без змінної `NODE_VERSION`); `public/_redirects`
веде глибокі посилання `/demo/*` на оболонку застосунку.
