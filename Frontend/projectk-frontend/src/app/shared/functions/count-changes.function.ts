/**
 * How many entries differ between what was loaded and what is on screen now, key by key. A key
 * present on one side only counts as a change; values are compared by their JSON.
 */
export function countChanges(before: object | null | undefined, now: object | null | undefined): number {
  if (!before || !now) {
    return 0;
  }

  const a = before as Record<string, unknown>;
  const b = now as Record<string, unknown>;
  const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
  let changed = 0;
  for (const key of keys) {
    if (JSON.stringify(a[key] ?? null) !== JSON.stringify(b[key] ?? null)) {
      changed++;
    }
  }
  return changed;
}

/** «1 незбережена зміна», «3 незбережені зміни», «5 незбережених змін»; empty for none. */
export function unsavedChangesLabel(count: number): string {
  if (count <= 0) {
    return '';
  }

  const tens = count % 100;
  const ones = count % 10;
  if (ones === 1 && tens !== 11) {
    return `${count} незбережена зміна`;
  }
  if (ones >= 2 && ones <= 4 && (tens < 12 || tens > 14)) {
    return `${count} незбережені зміни`;
  }
  return `${count} незбережених змін`;
}
