import { defineCollection } from 'astro:content';
import { z } from 'astro/zod';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';
import { PAGE_STATUS_KEYS } from './doc-status';

export const collections = {
	docs: defineCollection({
		loader: docsLoader(),
		schema: docsSchema({
			extend: z.object({
				/** Мітка сторінки: `new`, `updated`, `deprecated` чи `removal` (див. `src/doc-status.ts`). */
				status: z.enum(PAGE_STATUS_KEYS as [string, ...string[]]).optional(),
				/** Реліз, з якого мітка стоїть, наприклад '1.4': за ним її знімають на наступному. */
				since: z.string().optional(),
			}),
		}),
	}),
};
