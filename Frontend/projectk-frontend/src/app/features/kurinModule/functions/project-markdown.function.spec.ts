import { DeltaOp, quillDeltaToMarkdown } from '../../systemModule/functions/quill-delta-to-markdown.function';
import { hasContent, markdownBlocksToDelta, parseProjectMarkdown } from './project-markdown.function';

const KEY = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';
const URL = 'https://blob/probe-project-images/a.jpg?sig';
const resolve = (target: string) => (target === `project-image:${KEY}` ? URL : null);
const toKey = (src: string) => (src === URL ? `project-image:${KEY}` : null);

describe('parseProjectMarkdown', () => {
  it('reads headings, lists, quotes and code', () => {
    const blocks = parseProjectMarkdown('## План\n1. Зібрати\n2. Піти\n- [x] Намет\n> Цитата\n```\nx = 1\n```', resolve);

    expect(blocks.map(block => block.kind)).toEqual(['heading', 'item', 'item', 'item', 'quote', 'code']);
    expect(blocks[2]).toMatchObject({ list: 'ordered', ordinal: 2 });
    expect(blocks[3]).toMatchObject({ list: 'checked' });
    expect(blocks[5]).toEqual({ kind: 'code', text: 'x = 1' });
  });

  it('reads inline formatting without touching snake_case', () => {
    const [paragraph] = parseProjectMarkdown('**Мета**: _зійти_ на ~~Говерлу~~ `file_name` з [Пластом](https://plast.org.ua)', resolve);

    expect(paragraph.kind === 'paragraph' && paragraph.runs).toEqual([
      { text: 'Мета', bold: true },
      { text: ': ' },
      { text: 'зійти', italic: true },
      { text: ' на ' },
      { text: 'Говерлу', strike: true },
      { text: ' ' },
      { text: 'file_name', code: true },
      { text: ' з ' },
      { text: 'Пластом', link: 'https://plast.org.ua' }
    ]);
  });

  it('keeps a link that is not http or mail as plain text', () => {
    const [paragraph] = parseProjectMarkdown('[тиць](javascript:alert(1))', resolve);

    expect(paragraph.kind === 'paragraph' && paragraph.runs.every(run => !run.link)).toBe(true);
  });

  it('shows the project\'s own pictures and drops any other', () => {
    const blocks = parseProjectMarkdown(`![зображення](project-image:${KEY})\n![x](https://evil.example/a.png)`, resolve);

    expect(blocks[0].kind === 'paragraph' && blocks[0].runs).toEqual([{ text: 'зображення', image: URL }]);
    expect(blocks[1].kind === 'paragraph' && blocks[1].runs).toEqual([]);
  });

  it('calls only blank lines empty', () => {
    expect(hasContent(parseProjectMarkdown('\n\n', resolve))).toBe(false);
    expect(hasContent(parseProjectMarkdown(`![](project-image:${KEY})`, resolve))).toBe(true);
  });
});

describe('a description round trip', () => {
  it('comes back from the editor as the same Markdown', () => {
    const markdown = `## План\n**Мета**: _зійти_ на Говерлу\n\n1. Зібрати\n  - Намет\n> Не забути воду\n![зображення](project-image:${KEY})`;

    const delta = markdownBlocksToDelta(parseProjectMarkdown(markdown, resolve));

    expect(quillDeltaToMarkdown(delta, { imageAlt: 'зображення', imageTarget: toKey })).toBe(markdown);
  });

  it('leaves out a picture the editor holds but the project does not know', () => {
    const ops: DeltaOp[] = [{ insert: 'Текст\n' }, { insert: { image: 'https://elsewhere/x.png' } }, { insert: '\n' }];

    expect(quillDeltaToMarkdown(ops, { imageAlt: 'зображення', imageTarget: toKey })).toBe('Текст');
  });
});
