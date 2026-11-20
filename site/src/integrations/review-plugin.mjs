// Підсвітка змін на сторінці довідки — mdast-плагін Sätteri, лише в режимі рев'ю
// (див. `docs-review.mjs`).
//
// Сторінку розбито на блоки верхнього рівня (абзац, список, таблиця, заголовок, код, примітка) і
// порівняно з тією ж сторінкою в базі. Змінений чи доданий блок обгортається `.doc-review-changed`;
// у зміненого під ним розгортається «Як було» зі старим текстом. Прибраний блок стоїть закресленим
// на своєму місці. Картинка, файл якої змінився, робить свій блок зміненим, навіть коли текст той
// самий. Нову сторінку не підсвічуємо: на ній лише мітка «Нове».
//
// Старі блоки рендеряться окремо в HTML (`markdownToHtml`) і стають сирими `html`-вузлами: у змісті
// сторінки вони не зʼявляються, а картинки в них ведуть на копії з бази в `public/_review`.
import { markdownToHtml, markdownToMdast } from 'satteri';
import { sourceOfContentPath } from '../../scripts/doc-sources.mjs';
import {
	baseImageUrl,
	baseText,
	imageChange,
	imagePath,
	isReviewActive,
	pageBody,
	workingImageUrl,
} from '../../scripts/doc-review.mjs';

const FEATURES = { gfm: true, directive: true };
const SKIPPED = new Set(['yaml', 'toml', 'definition']);

function contentPathOf(fileURL) {
	const path = fileURL ? decodeURIComponent(fileURL.pathname ?? String(fileURL)) : '';
	const match = /src\/content\/docs\/(.+)$/.exec(path.replace(/\\/g, '/'));
	return match ? match[1] : null;
}

function slice(text, node) {
	const start = node.position?.start?.offset;
	const end = node.position?.end?.offset;
	return start === undefined || end === undefined ? '' : text.slice(start, end);
}

const normalize = (text) => text.replace(/\s+/g, ' ').trim();

const IMAGE = /!\[([^\]]*)\]\(([^)\s]+)([^)]*)\)/g;

const LIST_MARKER = /^\s*(?:[-*+]|\d+[.)])\s+(?:\[[ xX]\]\s+)?/;

/**
 * Одиниці порівняння: блоки верхнього рівня, а список — по пунктах, щоб одна правлена позиція не
 * підсвічувала весь список.
 */
function units(root, text) {
	return root.children
		.filter((node) => !SKIPPED.has(node.type))
		.flatMap((node) =>
			node.type === 'list'
				? node.children.map((item) => ({ node: item, item: true, text: slice(text, item) }))
				: [{ node, item: false, text: slice(text, node) }]
		);
}

/** Longest common subsequence over block keys → операції по порядку: same / add / remove. */
function diff(before, after) {
	const cols = after.length + 1;
	const table = new Uint32Array((before.length + 1) * cols);
	for (let i = before.length - 1; i >= 0; i--) {
		for (let j = after.length - 1; j >= 0; j--) {
			table[i * cols + j] =
				before[i].key === after[j].key ? table[(i + 1) * cols + j + 1] + 1 : Math.max(table[(i + 1) * cols + j], table[i * cols + j + 1]);
		}
	}
	const ops = [];
	let i = 0;
	let j = 0;
	while (i < before.length && j < after.length) {
		if (before[i].key === after[j].key) {
			ops.push({ op: 'same', after: after[j++] });
			i++;
		} else if (table[(i + 1) * cols + j] >= table[i * cols + j + 1]) {
			ops.push({ op: 'remove', before: before[i++] });
		} else {
			ops.push({ op: 'add', after: after[j++] });
		}
	}
	while (i < before.length) ops.push({ op: 'remove', before: before[i++] });
	while (j < after.length) ops.push({ op: 'add', after: after[j++] });
	return ops;
}

/**
 * Старі блоки як HTML: заголовки — жирним рядком (щоб не дати другий якір), картинки — з бази.
 * Картинку, якої в базі нема, показує теперішня копія — так «Як було» хоч не розсипається.
 */
function oldHtml(blocks, source) {
	const markdown = blocks
		.map((block) => (block.item ? block.text.replace(LIST_MARKER, '').replace(/^ {2,3}/gm, '') : block.text))
		.join('\n\n')
		.replace(/^#{1,6}\s+(.+)$/gm, '**$1**')
		.replace(IMAGE, (match, alt, url, rest) => {
			const path = imagePath(source, url);
			const shown = path && (baseImageUrl(path) ?? workingImageUrl(path));
			return shown ? `![${alt}](${shown}${rest})` : match;
		});
	return markdownToHtml(markdown, { features: FEATURES }).html;
}

const html = (value) => ({ type: 'html', value });

function reviewPlugin(source, before) {
	return {
		name: 'lileyka-docs-review',
		options: { position: true },
		before(root, context) {
			const text = context.source;
			const blocks = units(root, text).map((unit) => {
				const pictureChanged = [...unit.text.matchAll(IMAGE)].some(([, , url]) => imageChange(imagePath(source, url)) === 'changed');
				// A changed picture under the same text must not match its old block.
				return { ...unit, key: `${normalize(unit.text)}${pictureChanged ? '\u0000picture' : ''}` };
			});
			const oldRoot = markdownToMdast(before, { features: FEATURES, position: true });
			const oldBlocks = units(oldRoot, before).map((unit) => ({ ...unit, key: normalize(unit.text) }));

			let removed = [];
			// Next to a list item the removed text has to be an <li> itself, or the list breaks.
			const removedBlock = (anchor) => {
				const tag = anchor.item ? 'li' : 'div';
				const block = html(`<${tag} class="doc-review-removed"><p class="doc-review-label">Прибрано</p>${oldHtml(removed, source)}</${tag}>`);
				removed = [];
				return block;
			};
			for (const step of diff(oldBlocks, blocks)) {
				if (step.op === 'remove') {
					removed.push(step.before);
					continue;
				}
				const { node, item } = step.after;
				if (step.op === 'same') {
					if (removed.length) {
						context.insertBefore(node, removedBlock(step.after));
					}
					continue;
				}
				// Removed blocks right before an added one read as one edit: the old text under «Як було».
				const was = removed.length
					? html(`<details class="doc-review-was"><summary>Як було</summary>${oldHtml(removed, source)}</details>`)
					: null;
				const kind = removed.length ? 'doc-review-edited' : 'doc-review-added';
				removed = [];
				if (item) {
					context.setProperty(node, 'data', { ...(node.data ?? {}), hProperties: { className: ['doc-review-changed', kind] } });
					if (was) {
						context.appendChild(node, was);
					}
				} else {
					context.replaceNode(node, [html(`<div class="doc-review-changed ${kind}">`), node, ...(was ? [was] : []), html('</div>')]);
				}
			}
			if (removed.length && blocks.length) {
				context.insertAfter(blocks.at(-1).node, removedBlock(blocks.at(-1)));
			}
		},
	};
}

/**
 * Запис для `mdastPlugins`: фабрика, що вирішує на кожен документ. Не сторінка довідки, нова
 * сторінка чи вимкнене рев'ю — плагін для цього документа не вмикається зовсім.
 */
export function reviewMdastPlugin(factory) {
	if (!isReviewActive()) {
		return null;
	}
	const contentPath = contentPathOf(factory.fileURL);
	const source = contentPath && sourceOfContentPath(contentPath);
	if (!source) {
		return null;
	}
	const before = pageBody(source, baseText(source));
	return before === null ? null : reviewPlugin(source, before);
}
