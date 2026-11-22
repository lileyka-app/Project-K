// Режим рев'ю довідки: мітки «Нове» / «Оновлено» за порівнянням з базою, підсвітка змінених блоків
// з «Як було» і сторінка `/review` з переліком змін.
//
// Увімкнено під `astro dev`; `DOCS_REVIEW=0` вимикає його там, `DOCS_REVIEW=1` вмикає у збірці
// (наприклад, для превʼю на рев'ю). Зібраний для проду сайт про нього не знає: ні підсвітки, ні
// `/review`, ні міток, які в мапі статусів позначено як лише для рев'ю.
import { rmSync } from 'node:fs';
import { reviewMdastPlugin, shotMdastPlugin } from './review-plugin.mjs';

export default function docsReview() {
	return {
		name: 'lileyka-docs-review',
		hooks: {
			'astro:config:setup': ({ command, config, injectRoute, logger }) => {
				const flag = process.env.DOCS_REVIEW;
				const active = flag === '1' || (command === 'dev' && flag !== '0');
				process.env.DOCS_REVIEW_ACTIVE = active ? '1' : '0';
				if (!active) {
					return;
				}
				// Astro keeps rendered pages in its content store and re-renders only a changed file:
				// a moved base or new review code would leave old highlights. Start from a clean store.
				for (const store of ['.astro/data-store.json', 'node_modules/.astro/data-store.json']) {
					rmSync(new URL(store, config.root), { force: true });
				}
				const processor = config.markdown?.processor;
				if (processor?.name !== 'satteri') {
					logger.warn(`підсвітка змін працює лише на Sätteri, а тут ${processor?.name ?? 'нічого'}: лишаються мітки й /review`);
				} else {
					// Перед Starlight: її примітки й якорі заголовків бачать уже обгорнуті блоки.
					processor.options.mdastPlugins = [shotMdastPlugin, reviewMdastPlugin, ...(processor.options.mdastPlugins ?? [])];
				}
				injectRoute({ pattern: '/review', entrypoint: new URL('./review-page.astro', import.meta.url) });
				logger.info(`рев'ю довідки ввімкнено: база ${process.env.DOCS_REVIEW_BASE || 'origin/main'}, перелік змін — /review`);
			},
		},
	};
}
