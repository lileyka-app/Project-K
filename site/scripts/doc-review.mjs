// Рев'ю довідки: що на сторінці змінилося відносно того, що вже на сайті. Порівнюємо з базою —
// гілкою, з якої Cloudflare Pages збирає довідку (`origin/main`), або з `DOCS_REVIEW_BASE`. Працює
// лише там, де ввімкнено рев'ю (`astro dev`, або збірка з `DOCS_REVIEW=1`); зібраний сайт про нього
// не знає.
//
// Тут лише git і порівняння тексту. Підсвітку блоків на сторінці робить `src/integrations/review-plugin.mjs`,
// мітки в меню — `route-middleware.ts`, перелік змін — сторінка `/review`.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { dirname, extname, posix, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderRootDocBody, repo, rootDocs } from './doc-sources.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const publicReview = resolve(here, '../public/_review');

/** Прапорець, який ставить інтеграція `docs-review`: решта коду питає лише його. */
export const isReviewActive = () => process.env.DOCS_REVIEW_ACTIVE === '1';

export const reviewBaseName = () => process.env.DOCS_REVIEW_BASE || 'origin/main';

function git(args, encoding = 'utf8') {
	return execFileSync('git', args, { cwd: repo, encoding, stdio: ['ignore', 'pipe', 'ignore'], maxBuffer: 64 * 1024 * 1024 });
}

let baseCommit;
/** Коміт бази, прочитаний раз: поки сервер живе, кеш нижче за ним і тримається. */
export function reviewBase() {
	if (baseCommit === undefined) {
		try {
			baseCommit = git(['rev-parse', '--verify', `${reviewBaseName()}^{commit}`]).trim();
		} catch {
			console.warn(`doc-review: базу ${reviewBaseName()} не знайдено — усе буде «нове»`);
			baseCommit = null;
		}
	}
	return baseCommit;
}

const baseFiles = new Map();
/** Файл у базі як текст (без BOM, з `\n`), або null, коли його там немає. */
export function baseText(source) {
	if (!baseFiles.has(source)) {
		let text = null;
		const commit = reviewBase();
		if (commit) {
			try {
				text = git(['show', `${commit}:${source}`]).replace(/^﻿/, '').replaceAll('\r\n', '\n');
			} catch {
				text = null;
			}
		}
		baseFiles.set(source, text);
	}
	return baseFiles.get(source);
}

export function currentText(source) {
	const absolute = resolve(repo, source);
	return existsSync(absolute) ? readFileSync(absolute, 'utf8').replace(/^﻿/, '').replaceAll('\r\n', '\n') : null;
}

const FRONTMATTER = /^---\n[\s\S]*?\n---\n?/;

/**
 * Тіло сторінки так, як його бачить сайт: без frontmatter, а кореневий документ — з тими самими
 * правками, що робить `sync-docs` (без `# H1`, з приміткою «Джерело», з переписаними посиланнями).
 */
export function pageBody(source, text) {
	if (text === null) {
		return null;
	}
	const rootDoc = rootDocs.find((doc) => doc.from === source);
	return rootDoc ? renderRootDocBody(rootDoc, text).body : text.replace(FRONTMATTER, '');
}

const blobs = new Map();
function baseBlob(path) {
	const key = `${reviewBase()}:${path}`;
	if (!blobs.has(key)) {
		let sha = null;
		if (reviewBase()) {
			try {
				sha = git(['rev-parse', '--verify', '--quiet', key]).trim() || null;
			} catch {
				sha = null;
			}
		}
		blobs.set(key, sha);
	}
	return blobs.get(key);
}

const workingBlobs = new Map();
function workingBlob(path) {
	const absolute = resolve(repo, path);
	if (!existsSync(absolute)) {
		return null;
	}
	const stamp = statSync(absolute).mtimeMs;
	const cached = workingBlobs.get(path);
	if (cached?.stamp === stamp) {
		return cached.sha;
	}
	// Through git, not a raw hash: `text=auto` turns an SVG's LF into CRLF on checkout, and only
	// git's own clean filter makes the working file hash like its blob again.
	const sha = git(['hash-object', '--', path]).trim();
	workingBlobs.set(path, { stamp, sha });
	return sha;
}

/** Шлях картинки в репозиторії за посиланням зі сторінки, або null для зовнішніх адрес. */
export function imagePath(source, url) {
	if (!url || /^[a-z]+:|^\/\/|^\//i.test(url)) {
		return null;
	}
	return posix.normalize(posix.join(posix.dirname(source), decodeURI(url.split(/[?#]/)[0])));
}

/** 'new' — картинки в базі нема; 'changed' — файл інший; null — та сама. */
export function imageChange(path) {
	if (!path) {
		return null;
	}
	const before = baseBlob(path);
	if (!before) {
		return 'new';
	}
	return before === workingBlob(path) ? null : 'changed';
}

/**
 * Картинка з бази як файл у `public/_review/`, щоб «Як було» показало стару. Повертає адресу на
 * сайті, або null, коли в базі картинки немає.
 */
export function baseImageUrl(path) {
	const sha = baseBlob(path);
	if (!sha) {
		return null;
	}
	const name = `${sha}${extname(path).toLowerCase()}`;
	const target = resolve(publicReview, name);
	if (!existsSync(target)) {
		mkdirSync(publicReview, { recursive: true });
		writeFileSync(target, git(['cat-file', 'blob', sha], 'buffer'));
	}
	return `/_review/${name}`;
}

/** Теперішня картинка як файл у `public/_review/` — для блоків «Як було», що її показують. */
export function workingImageUrl(path) {
	const sha = workingBlob(path);
	if (!sha) {
		return null;
	}
	const name = `${sha}${extname(path).toLowerCase()}`;
	const target = resolve(publicReview, name);
	if (!existsSync(target)) {
		mkdirSync(publicReview, { recursive: true });
		writeFileSync(target, readFileSync(resolve(repo, path)));
	}
	return `/_review/${name}`;
}

const IMAGE_REF = /!\[[^\]]*\]\(([^)\s]+)[^)]*\)/g;

function lineDiff(before, after) {
	const a = before.split('\n').map((line) => line.trim()).filter(Boolean);
	const b = after.split('\n').map((line) => line.trim()).filter(Boolean);
	const rows = a.length + 1;
	const cols = b.length + 1;
	const table = new Uint16Array(rows * cols);
	for (let i = a.length - 1; i >= 0; i--) {
		for (let j = b.length - 1; j >= 0; j--) {
			table[i * cols + j] = a[i] === b[j] ? table[(i + 1) * cols + j + 1] + 1 : Math.max(table[(i + 1) * cols + j], table[i * cols + j + 1]);
		}
	}
	const common = table[0];
	return { added: b.length - common, removed: a.length - common };
}

const statuses = new Map();
/**
 * Що сталося зі сторінкою відносно бази: `new`, `updated` або null. Для `updated` — скільки рядків
 * додано й прибрано та скільки картинок змінилося. Frontmatter не рахується: мітка чи опис ще не
 * зміна сторінки.
 */
export function pageReview(source) {
	const current = currentText(source);
	if (current === null) {
		return null;
	}
	const cached = statuses.get(source);
	const stamp = statSync(resolve(repo, source)).mtimeMs;
	if (cached?.stamp === stamp) {
		return cached.review;
	}

	const before = pageBody(source, baseText(source));
	const after = pageBody(source, current);
	let review = null;
	if (before === null) {
		review = { kind: 'new', added: 0, removed: 0, images: 0 };
	} else {
		const images = [...after.matchAll(IMAGE_REF)].filter(([, url]) => imageChange(imagePath(source, url)) === 'changed').length;
		const { added, removed } = before === after ? { added: 0, removed: 0 } : lineDiff(before, after);
		review = added || removed || images ? { kind: 'updated', added, removed, images } : null;
	}
	statuses.set(source, { stamp, review });
	return review;
}

/** Відбиток тексту сторінки — щоб позначка «вичитано» на /review скидалася після правки. */
export function contentStamp(source) {
	return createHash('sha1').update(currentText(source) ?? '').digest('hex').slice(0, 12);
}
