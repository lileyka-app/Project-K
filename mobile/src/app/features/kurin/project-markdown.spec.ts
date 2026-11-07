import { hasContent, parseProjectMarkdown } from './project-markdown';

const KEY = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';
const URL = 'https://blob/probe-project-images/a.jpg?sig';
const resolve = (target: string) => (target === `project-image:${KEY}` ? URL : null);

describe('parseProjectMarkdown (phone)', () => {
  it('reads the blocks the web editor writes', () => {
    const blocks = parseProjectMarkdown('## План\n1. Зібрати\n2. Піти\n- Намет\n> Воду!', resolve);

    expect(blocks.map((block) => block.kind)).toEqual(['heading', 'item', 'item', 'item', 'quote']);
    expect(blocks[2]).toMatchObject({ list: 'ordered', ordinal: 2 });
  });

  it('shows the project\'s own pictures, drops others and unsafe links', () => {
    const [own, foreign, link] = parseProjectMarkdown(
      `![зображення](project-image:${KEY})\n![x](https://evil.example/a.png)\n[тиць](javascript:alert(1))`,
      resolve,
    );

    expect(own.kind === 'paragraph' && own.runs).toEqual([{ text: 'зображення', image: URL }]);
    expect(foreign.kind === 'paragraph' && foreign.runs).toEqual([]);
    expect(link.kind === 'paragraph' && link.runs.every((run) => !run.link)).toBe(true);
  });

  it('calls only blank lines empty', () => {
    expect(hasContent(parseProjectMarkdown('\n', resolve))).toBe(false);
    expect(hasContent(parseProjectMarkdown('**так**', resolve))).toBe(true);
  });
});

describe('a phone-written text', () => {
  it('comes back from the editor as the same Markdown the web writes', async () => {
    const { quillDeltaToMarkdown } = await import('./delta-markdown');
    const { markdownBlocksToDelta } = await import('./project-markdown');
    const toKey = (src: string) => (src === URL ? `project-image:${KEY}` : null);
    const markdown = `## План\n**Мета**: _зійти_ на Говерлу\n\n1. Зібрати\n  - Намет\n> Не забути воду\n![зображення](project-image:${KEY})`;

    const delta = markdownBlocksToDelta(parseProjectMarkdown(markdown, resolve));

    expect(quillDeltaToMarkdown(delta, { imageAlt: 'зображення', imageTarget: toKey })).toBe(markdown);
  });
});
