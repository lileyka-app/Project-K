// Редактор довідки поруч зі сторінкою — лише для `astro dev`. Зібраний сайт про нього не знає:
// і сторінка `/editor`, і `/__docs/*` додаються тільки тоді, коли Astro запущено командою `dev`.
//
// `/editor` — звичайний HTML (`editor.html`), а не маршрут Astro: на сторінках Astro в dev сидить
// клієнт Vite, і кожне збереження перезавантажувало б сам редактор разом із курсором.
//
// Читає й пише справжні джерела (`docs/user/…`, `docs/dev/…`, кореневі документи), а не копії в
// `src/content/docs`. Записаний файл підхоплює `sync-docs --watch`, і сторінка в правій половині
// оновлюється сама — той самий шлях, що й після збереження у VS Code.
import { readFileSync, statSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, sep } from 'node:path';
import { isEditableSource, listSources, repo } from '../../scripts/doc-sources.mjs';

const API = '/__docs/';
const EDITOR_PAGE = fileURLToPath(new URL('./editor.html', import.meta.url));

function sendEditor(res) {
	// Читається щоразу: правка самого редактора видна після простого оновлення вкладки.
	const html = readFileSync(EDITOR_PAGE, 'utf8').replace('__REPO_PREFIX__', `${repo.replaceAll(sep, '/')}/`);
	res.statusCode = 200;
	res.setHeader('Content-Type', 'text/html; charset=utf-8');
	res.setHeader('Cache-Control', 'no-store');
	res.end(html);
}

function send(res, status, body) {
	const json = typeof body === 'string' ? JSON.stringify({ message: body }) : JSON.stringify(body);
	res.statusCode = status;
	res.setHeader('Content-Type', 'application/json; charset=utf-8');
	res.end(json);
}

function readBody(req) {
	return new Promise((done, fail) => {
		const chunks = [];
		req.on('data', (chunk) => chunks.push(chunk));
		req.on('end', () => done(Buffer.concat(chunks).toString('utf8')));
		req.on('error', fail);
	});
}

/** Що редактор знає про файл: текст із `\n`, і як записати його назад так само, як він лежав. */
function readSource(source) {
	const absolute = resolve(repo, source);
	const raw = readFileSync(absolute, 'utf8');
	return {
		absolute,
		bom: raw.startsWith('﻿'),
		crlf: raw.includes('\r\n'),
		text: raw.replace(/^﻿/, '').replaceAll('\r\n', '\n'),
		modified: statSync(absolute).mtimeMs,
	};
}

async function handle(req, res) {
	const url = new URL(req.url, 'http://localhost');
	const route = url.pathname.slice(API.length);

	if (route === 'list' && req.method === 'GET') {
		return send(res, 200, listSources());
	}

	if (route !== 'file') {
		return send(res, 404, 'Немає такого');
	}

	const source = url.searchParams.get('source');
	if (!isEditableSource(source)) {
		return send(res, 400, 'Цей файл не є сторінкою довідки');
	}

	let current;
	try {
		current = readSource(source);
	} catch {
		return send(res, 404, `Файла ${source} немає`);
	}

	if (req.method === 'GET') {
		return send(res, 200, { source, text: current.text, modified: current.modified });
	}

	if (req.method === 'PUT') {
		// Файл змінили деінде (у VS Code, git checkout) після того, як редактор його відкрив:
		// мовчки перезаписати означало б загубити чужу правку.
		const seen = Number(req.headers['x-docs-modified']);
		const force = req.headers['x-docs-force'] === '1';
		if (!force && Number.isFinite(seen) && Math.abs(seen - current.modified) > 1) {
			return send(res, 409, { message: 'Файл змінено поза редактором', text: current.text, modified: current.modified });
		}

		let text = (await readBody(req)).replaceAll('\r\n', '\n');
		if (current.crlf) {
			text = text.replaceAll('\n', '\r\n');
		}
		writeFileSync(current.absolute, (current.bom ? '﻿' : '') + text, 'utf8');
		return send(res, 200, { source, modified: statSync(current.absolute).mtimeMs });
	}

	return send(res, 405, 'Метод не підтримується');
}

/** @returns {import('astro').AstroIntegration} */
export default function docsEditor() {
	return {
		name: 'lileyka-docs-editor',
		hooks: {
			'astro:config:setup': ({ command, updateConfig }) => {
				if (command !== 'dev') {
					return;
				}

				updateConfig({
					vite: {
						plugins: [
							{
								name: 'lileyka-docs-editor-api',
								configureServer(server) {
									server.middlewares.use((req, res, next) => {
										const path = req.url?.split('?')[0];
										if (req.method === 'GET' && (path === '/editor' || path === '/editor/')) {
											return sendEditor(res);
										}
										if (!req.url?.startsWith(API)) {
											return next();
										}
										handle(req, res).catch((error) => send(res, 500, error.message));
									});
								},
							},
						],
					},
				});
			},
		},
	};
}
