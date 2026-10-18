// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import mermaid from 'astro-mermaid';
import docsEditor from './src/integrations/docs-editor.mjs';

// Сайт Лілейки: візитка + довідка. Довідка не пишеться тут — вона живе в `docs/user/` у корені
// репозиторію і копіюється сюди скриптом `scripts/sync-docs.mjs` перед dev і build, тож одна
// правда і для сайту, і для тих, хто читає markdown на GitHub.
export default defineConfig({
	site: 'https://docs.lileyka.app',
	integrations: [
		// Лише під `astro dev`: сторінка /editor — джерело зліва, сторінка справа.
		docsEditor(),
		// Діаграми в довідці: ```mermaid у markdown рендериться в браузері й перемикає тему разом
		// із сайтом. Має стояти перед starlight, інакше блок забере підсвітка коду.
		mermaid({
			theme: 'base',
			autoTheme: false,
			enableLog: false,
			// Кольори обох тем накладає brand.css; тут лише шрифт і відступи.
			mermaidConfig: {
				fontFamily: 'Manrope, system-ui, sans-serif',
				flowchart: { curve: 'basis', padding: 12, nodeSpacing: 28, rankSpacing: 40 },
				sequence: { actorMargin: 24, messageMargin: 28, mirrorActors: false },
				themeVariables: { fontFamily: 'Manrope, system-ui, sans-serif', fontSize: '14px' },
			},
		}),
		starlight({
			title: 'Лілейка',
			description: 'Система для куреня: реєстр, проби й вмілості, календар і планування в одному місці.',
			defaultLocale: 'root',
			locales: {
				root: { label: 'Українська', lang: 'uk' },
			},
			logo: {
				light: './src/assets/lileyka-mark.svg',
				dark: './src/assets/lileyka-mark-dark.svg',
				alt: 'Лілейка',
			},
			favicon: '/favicon.svg',
			customCss: ['./src/styles/brand.css'],
			// Під dev «Правити» веде на джерело в репозиторії: у /editor і у VS Code. Зібраний сайт — як був.
			components: { EditLink: './src/components/EditLink.astro' },
			// Two trees in one sidebar: the user one open, the developer one folded until asked for.
			// The middleware only keeps prev/next from stepping across the border between them.
			routeMiddleware: './src/route-middleware.ts',
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/lileyka-app/Project-K' },
			],
			sidebar: [
				{
					label: 'Для користувача',
					items: [
						{ label: 'Початок', items: [{ autogenerate: { directory: 'user/start' } }] },
						{ label: 'За роллю', items: [{ autogenerate: { directory: 'user/roles' } }] },
						{ label: 'Функції', items: [{ autogenerate: { directory: 'user/features' } }] },
						{ label: 'Як зробити', items: [{ autogenerate: { directory: 'user/howto' } }] },
						{ slug: 'user/account' },
						{ slug: 'user/report-problem' },
						{ slug: 'user/self-host' },
						{ slug: 'user/about' },
						{ slug: 'user/privacy' },
					],
				},
				{
					label: 'Для розробника',
					collapsed: true,
					items: [
						{ slug: 'dev' },
						{ label: 'Основи', items: [{ autogenerate: { directory: 'dev/core' } }] },
						{ label: 'Гайди', items: [{ autogenerate: { directory: 'dev/guides' } }] },
						{ label: 'Довідник', items: [{ autogenerate: { directory: 'dev/reference' } }] },
						{ label: 'Експлуатація', items: [{ autogenerate: { directory: 'dev/operations' } }] },
						{ label: 'Self-host', items: [{ autogenerate: { directory: 'dev/self-host' } }] },
						{ label: 'Історія', collapsed: true, items: [{ autogenerate: { directory: 'dev/history' } }] },
						{ label: 'DevLog', collapsed: true, items: [{ autogenerate: { directory: 'dev/devlog' } }] },
					],
				},
			],
			credits: false,
			head: [
				{
					tag: 'script',
					attrs: { src: '/scripts/diagram-viewer.js', defer: true },
				},
				{
					tag: 'link',
					attrs: {
						rel: 'preload',
						href: '/fonts/manrope-var-cyrillic.woff2',
						as: 'font',
						type: 'font/woff2',
						crossorigin: 'anonymous',
					},
				},
			],
		}),
	],
});
