// Збирає довідку для Starlight з одного джерела правди — репозиторію — у `src/content/docs/`.
//
// Дві гілки:
//  - «Для користувача»: `docs/user/**` копіюється як є (уже у форматі Starlight).
//  - «Для розробника»: `docs/dev/**` копіюється як є, а кореневі документи репозиторію
//    (ARCHITECTURE, CONTRIBUTING, BRANDBOOK, SECURITY, docs/self-host …) підтягуються з
//    frontmatter, згенерованим із їхнього `# H1`, і з переписаними посиланнями між собою.
//    Так на GitHub лишається звичний markdown, а сайт не тримає копій.
//
// Тека `src/content/docs/{user,dev}` генерується і не комітиться. Запуск: перед `build`, а з
// `--watch` — замість `astro dev`: скрипт синхронізує, стежить за `docs/` і кореневими документами
// і сам піднімає `astro dev`, тож збережений файл у `docs/user/` зʼявляється на сторінці одразу.
import { spawn } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, statSync, watch, writeFileSync } from 'node:fs';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { linkMap, repo, rootDocs } from './doc-sources.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const content = resolve(here, '../src/content/docs');

function copyTree(from, to) {
	const source = resolve(repo, from);
	if (!existsSync(source)) {
		return 0;
	}
	cpSync(source, resolve(content, to), {
		recursive: true,
		filter: (path) => !/\.(tmp|bak)$/i.test(path),
	});
	return 1;
}

function escapeYaml(text) {
	return `'${text.replaceAll("'", "''")}'`;
}

function rewriteLinks(markdown) {
	return markdown.replace(/\]\(([^)\s#]+)(#[^)]*)?\)/g, (match, target, hash = '') => {
		const key = target.replace(/^\.\//, '');
		const mapped = linkMap.get(key);
		return mapped ? `](${mapped}${hash})` : match;
	});
}

function importRootDoc({ from, to, order, description }) {
	const source = resolve(repo, from);
	if (!existsSync(source)) {
		console.warn(`sync-docs: пропущено ${from} — файла немає`);
		return 0;
	}

	let markdown = readFileSync(source, 'utf8').replace(/^﻿/, '').replaceAll('\r\n', '\n');
	let title = to.split('/').pop().replace(/\.md$/, '');
	const heading = markdown.match(/^# (.+)$/m);
	if (heading) {
		title = heading[1].trim();
		markdown = markdown.replace(heading[0], '').replace(/^\n+/, '');
	}

	const frontmatter = [
		'---',
		`title: ${escapeYaml(title)}`,
		`description: ${escapeYaml(description)}`,
		'sidebar:',
		`  order: ${order}`,
		'---',
		'',
		`:::note[Джерело]`,
		`Ця сторінка збирається з [\`${from}\`](https://github.com/lileyka-app/Project-K/blob/main/${from}) у репозиторії. Правити треба там.`,
		':::',
		'',
	].join('\n');

	const target = resolve(content, to);
	mkdirSync(dirname(target), { recursive: true });
	writeFileSync(target, frontmatter + rewriteLinks(markdown));
	return 1;
}

function syncAll() {
	for (const branch of ['user', 'dev']) {
		rmSync(resolve(content, branch), { recursive: true, force: true });
	}

	const copied = copyTree('docs/user', 'user') + copyTree('docs/dev', 'dev');
	const imported = rootDocs.map(importRootDoc).reduce((sum, n) => sum + n, 0);
	console.log(`sync-docs: скопійовано тек — ${copied}, зібрано кореневих документів — ${imported}`);
}

/**
 * One changed file → one copied file. A root document is re-imported; anything under docs/user
 * or docs/dev is copied (or removed) at the same relative path; the rest is left alone.
 */
function syncOne(repoRelative) {
	const rootDoc = rootDocs.find((doc) => doc.from === repoRelative);
	if (rootDoc) {
		importRootDoc(rootDoc);
		return `зібрано ${repoRelative}`;
	}

	const match = /^docs\/(user|dev)\/(.+)$/.exec(repoRelative);
	if (!match || /\.(tmp|bak)$/i.test(repoRelative)) {
		return null;
	}

	const source = resolve(repo, repoRelative);
	const target = resolve(content, match[1], match[2]);
	if (existsSync(source)) {
		mkdirSync(dirname(target), { recursive: true });
		// Windows reports the folder, not the file, for some edits: then the folder is copied whole.
		cpSync(source, target, { recursive: statSync(source).isDirectory(), filter: (path) => !/\.(tmp|bak)$/i.test(path) });
		return `скопійовано ${repoRelative}`;
	}
	rmSync(target, { recursive: true, force: true });
	return `прибрано ${repoRelative}`;
}

/** Watches docs/ and the root documents; changes land in src/content/docs within a moment. */
function watchDocs() {
	const pending = new Map();
	let timer = null;
	const flush = () => {
		timer = null;
		for (const path of pending.keys()) {
			try {
				const result = syncOne(path);
				if (result) {
					console.log(`sync-docs: ${result}`);
				}
			} catch (error) {
				// A half-written file or a race with the editor must not take astro dev down with it.
				console.warn(`sync-docs: не вдалося синхронізувати ${path}: ${error.message}`);
			}
		}
		pending.clear();
	};
	const noticed = (base, filename) => {
		if (!filename) {
			return;
		}
		const repoRelative = relative(repo, resolve(base, filename.toString())).split(sep).join('/');
		pending.set(repoRelative, true);
		clearTimeout(timer);
		timer = setTimeout(flush, 150);
	};

	const docsDir = resolve(repo, 'docs');
	watch(docsDir, { recursive: true }, (_event, filename) => noticed(docsDir, filename));
	for (const dir of new Set(rootDocs.map((doc) => dirname(resolve(repo, doc.from))))) {
		watch(dir, (_event, filename) => noticed(dir, filename));
	}
	console.log('sync-docs: стежу за docs/ і кореневими документами');
}

syncAll();

if (process.argv.includes('--watch')) {
	watchDocs();
	const astro = spawn(
		process.execPath,
		[resolve(here, '../node_modules/astro/bin/astro.mjs'), 'dev', ...process.argv.slice(process.argv.indexOf('--watch') + 1)],
		{ stdio: 'inherit', cwd: resolve(here, '..') }
	);
	astro.on('exit', (code) => process.exit(code ?? 0));
}
