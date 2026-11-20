import { defineRouteMiddleware } from '@astrojs/starlight/route-data';
import type { StarlightRouteData } from '@astrojs/starlight/route-data';
import { getCollection } from 'astro:content';
import { sourceOfContentPath } from '../scripts/doc-sources.mjs';
import { isReviewActive, pageReview } from '../scripts/doc-review.mjs';
import { PAGE_STATUSES, type PageStatus } from './doc-status';

// Two audiences share one sidebar: «Для користувача» open, «Для розробника» folded. What must not
// happen is prev/next walking a reader from the last user page into the developer tree, or back.
const isDevPath = (path: string) => path.startsWith('/dev/') || path === '/dev';

export interface ShownStatus {
	status: PageStatus;
	/** Мітка з frontmatter (а не порахована рев'ю) — її бачить і прод, коли `prod`. */
	declared: boolean;
	review: ReturnType<typeof pageReview>;
}

const contentPathOf = (filePath: string | undefined) =>
	filePath?.replace(/\\/g, '/').replace(/^.*?src\/content\/docs\//, '') ?? null;

/**
 * Яку мітку показати сторінці. Мітка з frontmatter має перевагу; у режимі рев'ю, коли її нема,
 * ставиться «Нове» чи «Оновлено» за порівнянням з базою. У проді — лише мітки з `prod: true`.
 */
function shownStatus(entry: { filePath?: string; data: { status?: string } }): ShownStatus | null {
	const review = isReviewActive() ? reviewOf(entry.filePath) : null;
	const declared = entry.data.status as PageStatus | undefined;
	if (declared && (PAGE_STATUSES[declared].prod || isReviewActive())) {
		return { status: declared, declared: true, review };
	}
	return review ? { status: review.kind as PageStatus, declared: false, review } : null;
}

function reviewOf(filePath: string | undefined) {
	const contentPath = contentPathOf(filePath);
	const source = contentPath ? sourceOfContentPath(contentPath) : null;
	return source ? pageReview(source) : null;
}

const badgeOf = (shown: ShownStatus) => ({
	text: PAGE_STATUSES[shown.status].label,
	variant: 'default' as const,
	class: `doc-status doc-status-${shown.status}`,
});

type SidebarEntry = StarlightRouteData['sidebar'][number];

function withBadges(entries: SidebarEntry[], byHref: Map<string, ShownStatus>): SidebarEntry[] {
	return entries.map((entry) => {
		if (entry.type === 'group') {
			return { ...entry, entries: withBadges(entry.entries, byHref) };
		}
		const shown = byHref.get(entry.href);
		return shown && !entry.badge ? { ...entry, badge: badgeOf(shown) } : entry;
	});
}

export const onRequest = defineRouteMiddleware(async (context) => {
	const route = context.locals.starlightRoute;
	const onDevPage = isDevPath(context.url.pathname);

	const { prev, next } = route.pagination;
	route.pagination = {
		prev: prev && isDevPath(prev.href) === onDevPage ? prev : undefined,
		next: next && isDevPath(next.href) === onDevPage ? next : undefined,
	};

	const byHref = new Map<string, ShownStatus>();
	for (const entry of await getCollection('docs')) {
		const shown = shownStatus(entry);
		if (shown) {
			byHref.set(`/${entry.id}/`, shown);
		}
	}
	route.sidebar = withBadges(route.sidebar, byHref);

	const shown = shownStatus(route.entry);
	(context.locals as { docStatus?: ShownStatus | null }).docStatus = shown;
	const banner = shown && PAGE_STATUSES[shown.status].banner;
	if (banner && !route.entry.data.banner) {
		route.entry.data.banner = { content: banner };
	}
});
