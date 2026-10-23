import { DeltaOp, isDeltaBlank, quillDeltaToMarkdown } from './quill-delta-to-markdown.function';

describe('quillDeltaToMarkdown', () => {
  it('renders inline formatting the way GitHub reads it', () => {
    const ops: DeltaOp[] = [
      { insert: 'Кнопка ' },
      { insert: 'Зберегти', attributes: { bold: true } },
      { insert: ' не ' },
      { insert: 'працює', attributes: { italic: true } },
      { insert: ', код ' },
      { insert: 'ERR_42', attributes: { code: true } },
      { insert: ' — ' },
      { insert: 'сторінка', attributes: { link: 'https://example.org/a' } },
      { insert: '\n' }
    ];

    expect(quillDeltaToMarkdown(ops))
      .toBe('Кнопка **Зберегти** не _працює_, код `ERR_42` — [сторінка](https://example.org/a)');
  });

  it('numbers ordered lists, bullets the rest, and keeps quotes and headings', () => {
    const ops: DeltaOp[] = [
      { insert: 'Кроки' }, { insert: '\n', attributes: { header: 2 } },
      { insert: 'Відкрити картку' }, { insert: '\n', attributes: { list: 'ordered' } },
      { insert: 'Натиснути' }, { insert: '\n', attributes: { list: 'ordered' } },
      { insert: 'Один' }, { insert: '\n', attributes: { list: 'bullet' } },
      { insert: 'Два' }, { insert: '\n', attributes: { list: 'bullet', indent: 1 } },
      { insert: 'Цитата' }, { insert: '\n', attributes: { blockquote: true } }
    ];

    expect(quillDeltaToMarkdown(ops)).toBe([
      '## Кроки',
      '1. Відкрити картку',
      '2. Натиснути',
      '- Один',
      '  - Два',
      '> Цитата'
    ].join('\n'));
  });

  it('fences code blocks and leaves their text alone', () => {
    const ops: DeltaOp[] = [
      { insert: 'Лог:\n' },
      { insert: 'const **x** = 1;' }, { insert: '\n', attributes: { 'code-block': true } },
      { insert: 'throw new Error();' }, { insert: '\n', attributes: { 'code-block': true } },
      { insert: 'далі\n' }
    ];

    expect(quillDeltaToMarkdown(ops)).toBe([
      'Лог:',
      '```',
      'const **x** = 1;',
      'throw new Error();',
      '```',
      'далі'
    ].join('\n'));
  });

  it('embeds images as Markdown pictures', () => {
    const ops: DeltaOp[] = [
      { insert: 'Ось:\n' },
      { insert: { image: 'https://blob/feedback-screenshots/a.png' } },
      { insert: '\n' }
    ];

    expect(quillDeltaToMarkdown(ops)).toBe('Ось:\n![скриншот](https://blob/feedback-screenshots/a.png)');
  });

  it('leaves inlined base64 pictures out of the report', () => {
    const ops: DeltaOp[] = [
      { insert: 'Ось:\n' },
      { insert: { image: 'data:image/png;base64,iVBORw0KGgo=' } },
      { insert: '\n' },
      { insert: { image: 'https://blob/feedback-screenshots/a.png' } },
      { insert: '\n' }
    ];

    expect(quillDeltaToMarkdown(ops)).toBe('Ось:\n\n![скриншот](https://blob/feedback-screenshots/a.png)');
  });

  it('treats the editor’s bare trailing newline as nothing written', () => {
    expect(isDeltaBlank([{ insert: '\n' }])).toBe(true);
    expect(isDeltaBlank([{ insert: '  \n\n' }])).toBe(true);
    expect(isDeltaBlank([{ insert: 'щось\n' }])).toBe(false);
  });
});
