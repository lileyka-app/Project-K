import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** A tag of the kurin, as `GET api/kurin/{kurinKey}/tags` lists it. */
export interface KurinTagDto {
  kurinTagKey: string;
  name: string;
  colorHex: string;
}

/** The kurin's tags as the viewer may use them; empty when the kurin has tags off. */
export interface KurinTagsDto {
  enabled: boolean;
  canManage: boolean;
  maxPerMember: number;
  tags: KurinTagDto[];
}

/** The tags behind these keys, by name; keys the list does not know are left out. */
export function resolveTags(all: KurinTagsDto | null, keys: readonly string[] | null | undefined): KurinTagDto[] {
  if (!all?.enabled || !keys?.length) return [];
  const byKey = new Map(all.tags.map(tag => [tag.kurinTagKey, tag]));
  return keys
    .map(key => byKey.get(key))
    .filter((tag): tag is KurinTagDto => !!tag)
    .sort((a, b) => a.name.localeCompare(b.name, 'uk'));
}

/**
 * A person's tags as a row of chips, the same as on the web card: the colour is a dot, never a fill,
 * and the text stays the theme's ink (`BRANDBOOK.md` §2).
 */
@Component({
  selector: 'app-kurin-tags',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    ul { display: flex; flex-wrap: wrap; gap: 6px; justify-content: center; list-style: none; margin: 6px 0 0; padding: 0; }
    li {
      align-items: center; border: 1px solid var(--lk-line); border-radius: 8px; color: var(--lk-ink-soft);
      display: inline-flex; font-size: 13px; font-weight: 600; gap: 6px; padding: 2px 8px; white-space: nowrap;
    }
    .dot { border-radius: 50%; height: 8px; width: 8px; }
  `,
  template: `
    @if (shown().length) {
      <ul aria-label="Теги" data-testid="member-tags">
        @for (tag of shown(); track tag.kurinTagKey) {
          <li><span class="dot" [style.background]="tag.colorHex" aria-hidden="true"></span>{{ tag.name }}</li>
        }
      </ul>
    }
  `,
})
export class KurinTags {
  readonly all = input<KurinTagsDto | null>(null);
  readonly keys = input<readonly string[] | null | undefined>([]);

  protected readonly shown = computed(() => resolveTags(this.all(), this.keys()));
}
