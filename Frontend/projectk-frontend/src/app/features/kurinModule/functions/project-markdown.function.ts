import { DeltaAttributes, DeltaOp } from '../../systemModule/functions/quill-delta-to-markdown.function';

/** A run of text inside a line, or a picture. */
export interface MarkdownRun {
  text: string;
  bold?: boolean;
  italic?: boolean;
  strike?: boolean;
  code?: boolean;
  /** Only http(s) and mailto survive; anything else stays text. */
  link?: string;
  /** The picture's address, already resolved from its key. */
  image?: string;
}

export type MarkdownBlock =
  | { kind: 'paragraph'; runs: MarkdownRun[] }
  | { kind: 'heading'; level: number; runs: MarkdownRun[] }
  | { kind: 'item'; list: 'ordered' | 'bullet' | 'checked' | 'unchecked'; indent: number; ordinal: number; runs: MarkdownRun[] }
  | { kind: 'quote'; runs: MarkdownRun[] }
  | { kind: 'code'; text: string };

/**
 * Reads back the Markdown that `quillDeltaToMarkdown` writes, and only that: headings, lists,
 * quotes, code, bold, italic, strike, links and pictures. The page renders the blocks with its
 * own template, so nothing written by a person ever reaches `innerHTML`.
 *
 * @param resolveImage turns a picture's target into an address to show, or null to drop it.
 */
export function parseProjectMarkdown(markdown: string, resolveImage: (target: string) => string | null): MarkdownBlock[] {
  const blocks: MarkdownBlock[] = [];
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  let code: string[] | null = null;

  for (const line of lines) {
    if (line.trim() === '```') {
      if (code) {
        blocks.push({ kind: 'code', text: code.join('\n') });
        code = null;
      } else {
        code = [];
      }
      continue;
    }

    if (code) {
      code.push(line);
      continue;
    }

    blocks.push(lineBlock(line, blocks[blocks.length - 1], resolveImage));
  }

  if (code) {
    blocks.push({ kind: 'code', text: code.join('\n') });
  }

  return blocks;
}

/** The blocks as a Quill delta, so a saved description opens in the editor as it was written. */
export function markdownBlocksToDelta(blocks: MarkdownBlock[]): DeltaOp[] {
  const ops: DeltaOp[] = [];

  for (const block of blocks) {
    if (block.kind === 'code') {
      for (const line of block.text.split('\n')) {
        if (line) {
          ops.push({ insert: line });
        }
        ops.push({ insert: '\n', attributes: { 'code-block': true } });
      }
      continue;
    }

    for (const run of block.runs) {
      ops.push(runOp(run));
    }
    ops.push(lineEnd(block));
  }

  return ops;
}

/** Whether there is anything to show: blank lines alone are not a description. */
export function hasContent(blocks: MarkdownBlock[]): boolean {
  return blocks.some(block => block.kind === 'code' || block.runs.some(run => run.image || run.text.trim()));
}

function lineBlock(line: string, previous: MarkdownBlock | undefined, resolveImage: (target: string) => string | null): MarkdownBlock {
  const heading = /^(#{1,6}) (.*)$/.exec(line);
  if (heading) {
    return { kind: 'heading', level: heading[1].length, runs: parseInline(heading[2], resolveImage) };
  }

  const item = /^((?: {2})*)(?:(\d+)\.|-( \[[ x]\])?) (.*)$/.exec(line);
  if (item) {
    const indent = item[1].length / 2;
    const list = listKind(item[2], item[3]);
    const ordinal = list === 'ordered' && previous?.kind === 'item' && previous.list === 'ordered' ? previous.ordinal + 1 : 1;
    return { kind: 'item', list, indent, ordinal, runs: parseInline(item[4], resolveImage) };
  }

  if (line.startsWith('> ')) {
    return { kind: 'quote', runs: parseInline(line.slice(2), resolveImage) };
  }

  return { kind: 'paragraph', runs: parseInline(line, resolveImage) };
}

type InlineKind = 'image' | 'link' | 'bold' | 'strike' | 'code' | 'italic';

// One simple pattern per mark, the earliest match wins: a single alternation of them all was
// super-linear on a long line, and a description may run to twenty thousand characters.
const INLINE_PATTERNS: readonly [InlineKind, RegExp][] = [
  ['image', /!\[([^[\]]*)\]\(([^)\s]*)\)/],
  ['link', /\[([^[\]]+)\]\(([^)\s]+)\)/],
  ['bold', /\*\*([^*]+)\*\*/],
  ['strike', /~~([^~]+)~~/],
  ['code', /`([^`]+)`/],
  ['italic', /(?<![\p{L}\p{N}])_([^_]+)_(?![\p{L}\p{N}])/u]
];

function firstMark(text: string): { kind: InlineKind; match: RegExpExecArray } | null {
  let first: { kind: InlineKind; match: RegExpExecArray } | null = null;
  for (const [kind, pattern] of INLINE_PATTERNS) {
    const match = pattern.exec(text);
    if (match && (!first || match.index < first.match.index)) {
      first = { kind, match };
    }
  }
  return first;
}

function parseInline(text: string, resolveImage: (target: string) => string | null, inherited: Omit<MarkdownRun, 'text'> = {}): MarkdownRun[] {
  const runs: MarkdownRun[] = [];
  let rest = text;

  while (rest) {
    const mark = firstMark(rest);
    if (!mark) {
      runs.push({ ...inherited, text: rest });
      break;
    }

    if (mark.match.index > 0) {
      runs.push({ ...inherited, text: rest.slice(0, mark.match.index) });
    }
    runs.push(...markRuns(mark.kind, mark.match, resolveImage, inherited));
    rest = rest.slice(mark.match.index + mark.match[0].length);
  }

  return runs;
}

function markRuns(
  kind: InlineKind,
  match: RegExpExecArray,
  resolveImage: (target: string) => string | null,
  inherited: Omit<MarkdownRun, 'text'>
): MarkdownRun[] {
  switch (kind) {
    case 'image': {
      const src = resolveImage(match[2]);
      return src ? [{ text: match[1], image: src }] : [];
    }
    case 'link': {
      const link = /^(https?:\/\/|mailto:)/i.test(match[2]) ? match[2] : undefined;
      return parseInline(match[1], resolveImage, link ? { ...inherited, link } : inherited);
    }
    case 'code':
      return [{ ...inherited, text: match[1], code: true }];
    default:
      return parseInline(match[1], resolveImage, { ...inherited, [kind]: true });
  }
}

function listKind(numbered: string | undefined, box: string | undefined): 'ordered' | 'bullet' | 'checked' | 'unchecked' {
  if (numbered) {
    return 'ordered';
  }
  if (!box) {
    return 'bullet';
  }
  return box === ' [x]' ? 'checked' : 'unchecked';
}

function runOp(run: MarkdownRun): DeltaOp {
  if (run.image) {
    return { insert: { image: run.image } };
  }

  const attributes: DeltaAttributes = {};
  if (run.bold) attributes.bold = true;
  if (run.italic) attributes.italic = true;
  if (run.strike) attributes.strike = true;
  if (run.code) attributes.code = true;
  if (run.link) attributes.link = run.link;
  return Object.keys(attributes).length ? { insert: run.text, attributes } : { insert: run.text };
}

function lineEnd(block: Exclude<MarkdownBlock, { kind: 'code' }>): DeltaOp {
  switch (block.kind) {
    case 'heading':
      return { insert: '\n', attributes: { header: block.level } };
    case 'item':
      return { insert: '\n', attributes: block.indent ? { list: block.list, indent: block.indent } : { list: block.list } };
    case 'quote':
      return { insert: '\n', attributes: { blockquote: true } };
    default:
      return { insert: '\n' };
  }
}
