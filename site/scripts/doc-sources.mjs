// Де лежить джерело кожної сторінки довідки. Одне місце для трьох споживачів: синхронізація
// (`sync-docs.mjs`), посилання «Правити» на сторінці і редактор `/editor` — усі троє мусять
// однаково знати, що `/dev/core/architecture/` — це `ARCHITECTURE.md`, а не копія в `src/content`.
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
export const repo = resolve(here, '../..');

/** Кореневі документи → сторінки розділу «Для розробника». */
export const rootDocs = [
	{ from: 'ARCHITECTURE.md', to: 'dev/core/architecture.md', order: 1, description: 'Шари, модулі, шлях запиту, авторизація, середовища.' },
	{ from: 'CONTRIBUTING.md', to: 'dev/core/contributing.md', order: 3, description: 'Конвенції, за якими пишеться новий код.' },
	{ from: 'BRANDBOOK.md', to: 'dev/core/brandbook.md', order: 4, description: 'Візуальна система: кольори, шрифти, компоненти, правила.' },
	{ from: 'SECURITY.md', to: 'dev/core/security.md', order: 5, description: 'Як повідомити про вразливість і які версії підтримуються.' },
	{ from: 'docs/quality-baseline.md', to: 'dev/core/quality-baseline.md', order: 6, description: 'Тести, лінт і те, що вважається базовою лінією.' },
	{ from: 'docs/observability.md', to: 'dev/operations/observability.md', order: 1, description: 'Логи, метрики, здоровʼя сервісу в проді.' },
	{ from: 'docs/data-retention.md', to: 'dev/operations/data-retention.md', order: 2, description: 'Що система зберігає і як довго.' },
	{ from: 'docs/self-host/README.md', to: 'dev/self-host/index.md', order: 1, description: 'Поставити Лілейку на власний сервер.' },
	{ from: 'docs/self-host/backup-restore.md', to: 'dev/self-host/backup-restore.md', order: 2, description: 'Резервні копії та відновлення.' },
	{ from: 'docs/self-host/update.md', to: 'dev/self-host/update.md', order: 3, description: 'Оновлення self-host-інсталяції.' },
];

/** Посилання між кореневими документами → адреси на сайті. */
export const linkMap = new Map([
	['ARCHITECTURE.md', '/dev/core/architecture/'],
	['CONTRIBUTING.md', '/dev/core/contributing/'],
	['BRANDBOOK.md', '/dev/core/brandbook/'],
	['SECURITY.md', '/dev/core/security/'],
	['docs/quality-baseline.md', '/dev/core/quality-baseline/'],
	['docs/observability.md', '/dev/operations/observability/'],
	['docs/data-retention.md', '/dev/operations/data-retention/'],
	['docs/self-host/README.md', '/dev/self-host/'],
	['docs/self-host/backup-restore.md', '/dev/self-host/backup-restore/'],
	['docs/self-host/update.md', '/dev/self-host/update/'],
	['README.md', '/dev/self-host/'],
	['backup-restore.md', '/dev/self-host/backup-restore/'],
	['update.md', '/dev/self-host/update/'],
]);

const PAGE_FILE = /\.mdx?$/i;

/** `user/features/dues.md` (шлях у `src/content/docs`) → адреса сторінки `/user/features/dues/`. */
export function urlOfContentPath(contentPath) {
	const withoutExt = contentPath.replace(PAGE_FILE, '');
	const path = withoutExt === 'index' || withoutExt.endsWith('/index') ? withoutExt.replace(/\/?index$/, '') : withoutExt;
	return `/${path.toLowerCase()}${path ? '/' : ''}`;
}

/** Шлях у `src/content/docs` (як `user/features/dues.md`) → джерело в репозиторії, або null. */
export function sourceOfContentPath(contentPath) {
	const normalized = contentPath.split(sep).join('/').replace(/^\/+/, '');
	const rootDoc = rootDocs.find((doc) => doc.to === normalized);
	if (rootDoc) {
		return rootDoc.from;
	}
	return /^(user|dev)\//.test(normalized) ? `docs/${normalized}` : null;
}

/** Джерело в репозиторії → шлях у `src/content/docs`, або null для того, що на сайт не йде. */
export function contentPathOfSource(source) {
	const rootDoc = rootDocs.find((doc) => doc.from === source);
	if (rootDoc) {
		return rootDoc.to;
	}
	const match = /^docs\/((?:user|dev)\/.+)$/.exec(source);
	return match && PAGE_FILE.test(source) ? match[1] : null;
}

/**
 * Чи можна редактору читати й писати цей файл: лише сторінки довідки, що йдуть на сайт.
 * Шлях порівнюється після `resolve`, тож `../` і абсолютні шляхи за межі репозиторію не пройдуть.
 */
export function isEditableSource(source) {
	if (typeof source !== 'string' || !PAGE_FILE.test(source)) {
		return false;
	}
	const absolute = resolve(repo, source);
	const back = relative(repo, absolute).split(sep).join('/');
	return back === source && contentPathOfSource(source) !== null;
}

function titleOf(absolute, fallback) {
	const text = readFileSync(absolute, 'utf8');
	const start = /^---\r?\n/m.exec(text);
	const line = start && /^title:(.*)$/m.exec(text.slice(start.index + start[0].length));
	const title = line && unquote(line[1].trim());
	if (title) {
		return title;
	}
	const heading = /^# (.+)$/m.exec(text);
	return heading ? heading[1].trim() : fallback;
}

function unquote(value) {
	const quotes = `'"`;
	let result = value;
	if (result.length > 1 && quotes.includes(result[0])) {
		result = result.slice(1);
	}
	if (result.length > 1 && quotes.includes(result.at(-1))) {
		result = result.slice(0, -1);
	}
	return result;
}

function walk(dir) {
	if (!existsSync(dir)) {
		return [];
	}
	return readdirSync(dir).flatMap((name) => {
		const path = resolve(dir, name);
		return statSync(path).isDirectory() ? walk(path) : [path];
	});
}

/** Усі сторінки довідки з джерелами: для переліку в редакторі. */
export function listSources() {
	const fromTrees = ['docs/user', 'docs/dev']
		.flatMap((tree) => walk(resolve(repo, tree)))
		.map((absolute) => relative(repo, absolute).split(sep).join('/'))
		.filter((source) => PAGE_FILE.test(source));
	const sources = [...fromTrees, ...rootDocs.map((doc) => doc.from).filter((from) => existsSync(resolve(repo, from)))];

	return sources
		.map((source) => {
			const contentPath = contentPathOfSource(source);
			return {
				source,
				url: urlOfContentPath(contentPath),
				section: contentPath.startsWith('user/') ? 'user' : 'dev',
				title: titleOf(resolve(repo, source), source),
			};
		})
		.sort((a, b) => a.section.localeCompare(b.section) * -1 || a.url.localeCompare(b.url));
}
