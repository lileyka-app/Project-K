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

const releaseParts = (release: string) => release.split('.').map((part) => Number.parseInt(part, 10) || 0);

/** `'1.10'` новіший за `'1.9'`: порівнює номери, а не рядки. */
export function compareReleases(a: string, b: string): number {
	const [left, right] = [releaseParts(a), releaseParts(b)];
	for (let i = 0; i < Math.max(left.length, right.length); i++) {
		const diff = (left[i] ?? 0) - (right[i] ?? 0);
		if (diff !== 0) {
			return diff;
		}
	}
	return 0;
}

/**
 * Найновіший `since` серед сторінок — реліз, чиї сторінки ще «Нові». Версію руками не піднімаємо
 * (CONTRIBUTING, «Релізи»): щойно зʼявляються сторінки з новішим `since`, мітки попереднього
 * релізу знімаються самі.
 */
export function newestRelease(sinces: Iterable<string | undefined>): string | null {
	let newest: string | null = null;
	for (const since of sinces) {
		if (since && (!newest || compareReleases(since, newest) > 0)) {
			newest = since;
		}
	}
	return newest;
}

/** «Нове» з `since` старшого релізу вже не нове; без `since` мітка стоїть, доки її не знімуть. */
export const isExpiredNew = (status: string | undefined, since: string | undefined, newest: string | null) =>
	status === 'new' && !!since && !!newest && compareReleases(since, newest) < 0;
