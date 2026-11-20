// Мітки сторінок довідки. Ставить їх frontmatter сторінки (`status:`), а в режимі рев'ю
// «Нове» й «Оновлено» ставляться й самі — за порівнянням з базою (`scripts/doc-review.mjs`).
//
// `prod` вирішує, чи мітка доходить до зібраного сайту. Що лише для рев'ю — видно тільки під
// `astro dev` чи у збірці з `DOCS_REVIEW=1`. Кольори — класи `doc-status-*` у `brand.css`.

export type PageStatus = 'new' | 'updated' | 'deprecated' | 'removal';

export interface PageStatusInfo {
	label: string;
	prod: boolean;
	/** Банер над сторінкою — для того, що читачеві треба знати ще до тексту. */
	banner?: string;
}

export const PAGE_STATUSES: Record<PageStatus, PageStatusInfo> = {
	new: { label: 'Нове', prod: true },
	updated: { label: 'Оновлено', prod: false },
	deprecated: { label: 'Застаріле', prod: true, banner: 'Ця сторінка застаріла: описане тут відходить.' },
	removal: { label: 'Буде прибрано', prod: false, banner: 'Цю сторінку приберуть із довідки.' },
};

export const PAGE_STATUS_KEYS = Object.keys(PAGE_STATUSES) as PageStatus[];
