import { TemplateRef } from '@angular/core';
import { reconcileOrder } from './tile-order.function';
import { TileDefinition } from './tile-board.models';

function def(key: string, defaultOrder: number, pinned = false): TileDefinition {
  return {
    key,
    span: 'half',
    pinned,
    defaultOrder,
    label: key,
    template: {} as TemplateRef<unknown>
  };
}

describe('reconcileOrder', () => {
  const defs: TileDefinition[] = [
    def('profile', 0, true),
    def('skills', 1),
    def('probes', 2),
    def('awards', 3)
  ];

  it('returns default order (pinned first) when nothing is saved', () => {
    const result = reconcileOrder([], defs).map(d => d.key);
    expect(result).toEqual(['profile', 'skills', 'probes', 'awards']);
  });

  it('applies a saved order', () => {
    const result = reconcileOrder(['probes', 'skills', 'awards', 'profile'], defs).map(d => d.key);
    expect(result).toEqual(['profile', 'probes', 'skills', 'awards']);
  });

  it('drops unknown / removed keys from the saved order', () => {
    const result = reconcileOrder(['skills', 'ghost-tile', 'probes'], defs).map(d => d.key);
    expect(result).not.toContain('ghost-tile');
    expect(result).toEqual(['profile', 'skills', 'probes', 'awards']);
  });

  it('appends newly added tiles at their default slot', () => {
    const result = reconcileOrder(['profile', 'probes', 'skills'], defs).map(d => d.key);
    expect(result).toContain('awards');
    expect(result[result.length - 1]).toBe('awards');
  });

  it('de-duplicates repeated keys in the saved order', () => {
    const result = reconcileOrder(['skills', 'skills', 'probes'], defs).map(d => d.key);
    expect(result.filter(k => k === 'skills')).toHaveSize(1);
  });

  it('keeps pinned tiles at the top even if saved order buries them', () => {
    const twoPinned: TileDefinition[] = [
      def('a', 0, true),
      def('b', 1, true),
      def('c', 2),
      def('d', 3)
    ];
    const result = reconcileOrder(['c', 'd', 'b', 'a'], twoPinned).map(d => d.key);
    expect(result.slice(0, 2)).toEqual(['a', 'b']);
  });
});
