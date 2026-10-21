import { TileLayout } from './tile-board.models';

const STORAGE_PREFIX = 'tile-layout:';

/** A plain array is what older builds wrote: the order alone, nothing hidden. */
export function readStoredLayout(boardKey: string): TileLayout | null {
  try {
    const raw = localStorage.getItem(STORAGE_PREFIX + boardKey);
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    if (isKeyList(parsed)) {
      return { tileKeys: parsed, hiddenTileKeys: [] };
    }
    if (isLayout(parsed)) {
      return { tileKeys: parsed.tileKeys, hiddenTileKeys: parsed.hiddenTileKeys };
    }
    return null;
  } catch {
    return null;
  }
}

export function writeStoredLayout(boardKey: string, layout: TileLayout): void {
  try {
    localStorage.setItem(STORAGE_PREFIX + boardKey, JSON.stringify(layout));
  } catch {
    return;
  }
}

export function removeStoredLayout(boardKey: string): void {
  try {
    localStorage.removeItem(STORAGE_PREFIX + boardKey);
  } catch {
    return;
  }
}

export function clearTileLayoutStorage(): void {
  try {
    const keysToRemove: string[] = [];
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key?.startsWith(STORAGE_PREFIX)) {
        keysToRemove.push(key);
      }
    }
    keysToRemove.forEach(key => localStorage.removeItem(key));
  } catch {
    return;
  }
}

function isKeyList(value: unknown): value is string[] {
  return Array.isArray(value) && value.every(item => typeof item === 'string');
}

function isLayout(value: unknown): value is TileLayout {
  return typeof value === 'object' && value !== null
    && isKeyList((value as TileLayout).tileKeys)
    && isKeyList((value as TileLayout).hiddenTileKeys);
}
