/**
 * The shape Quill hands back from `getContents()`: text runs with inline attributes, and a
 * newline that carries the block attributes of the line it closes.
 */
export interface DeltaOp {
  insert: string | { image?: string };
  attributes?: DeltaAttributes;
}

export interface DeltaAttributes {
  bold?: boolean;
  italic?: boolean;
  underline?: boolean;
  strike?: boolean;
  code?: boolean;
  link?: string;
  list?: 'ordered' | 'bullet' | 'checked' | 'unchecked';
  header?: number;
  blockquote?: boolean;
  'code-block'?: boolean | string;
  indent?: number;
}

interface Segment {
  text: string;
  attributes: DeltaAttributes;
  isPicture?: boolean;
}

/**
 * How pictures are written. A report keeps the address the upload returned; a probe project
 * stores a key instead, since its links are signed and expire.
 */
export interface DeltaMarkdownOptions {
  /** The alt text of every picture. */
  imageAlt?: string;
  /** What goes between the parentheses for an embed, or null to leave the picture out. */
  imageTarget?: (src: string) => string | null;
}

const REPORT_PICTURES: Required<DeltaMarkdownOptions> = {
  imageAlt: 'скриншот',
  imageTarget: src => src
};

interface Line {
  segments: Segment[];
  block: DeltaAttributes;
}

/**
 * Turns a Quill delta into the Markdown GitHub renders. The report crosses the wire as Markdown,
 * not HTML: a public tracker shows Markdown as written, and the server never has to sanitise
 * markup it did not produce. Images are the embeds the upload endpoint returned.
 */
export function quillDeltaToMarkdown(ops: DeltaOp[], options: DeltaMarkdownOptions = {}): string {
  const lines = splitIntoLines(ops, { ...REPORT_PICTURES, ...options });
  const out: string[] = [];
  let ordinal = 0;
  let inCodeBlock = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const isCode = !!line.block['code-block'];

    if (isCode !== inCodeBlock) {
      out.push('```');
      inCodeBlock = isCode;
    }

    if (isCode) {
      out.push(plainText(line.segments));
      continue;
    }

    const text = inlineMarkdown(line.segments);
    const previous = lines[i - 1];
    ordinal = line.block.list === 'ordered' && previous?.block.list === 'ordered' ? ordinal + 1 : 1;

    out.push(blockMarkdown(line.block, text, ordinal));
  }

  if (inCodeBlock) {
    out.push('```');
  }

  return out.join('\n').replace(/\n{3,}/g, '\n\n').trim();
}

/** A delta is empty when nothing but Quill's trailing newline is in it. */
export function isDeltaBlank(ops: DeltaOp[]): boolean {
  return quillDeltaToMarkdown(ops).length === 0;
}

function splitIntoLines(ops: DeltaOp[], pictures: Required<DeltaMarkdownOptions>): Line[] {
  const lines: Line[] = [];
  let current: Segment[] = [];

  for (const op of ops) {
    if (typeof op.insert !== 'string') {
      const picture = pictureSegment(op.insert.image, pictures);
      if (picture) {
        current.push(picture);
      }
      continue;
    }

    const parts = op.insert.split('\n');
    for (let p = 0; p < parts.length; p++) {
      if (parts[p]) {
        current.push({ text: parts[p], attributes: op.attributes ?? {} });
      }

      const closesLine = p < parts.length - 1;
      if (closesLine) {
        lines.push({ segments: current, block: op.attributes ?? {} });
        current = [];
      }
    }
  }

  if (current.length) {
    lines.push({ segments: current, block: {} });
  }

  return lines;
}

/**
 * A stored picture becomes a Markdown image. A data: URL is the editor's own inlining of a file,
 * megabytes of base64 that no tracker wants and the description limit refuses, so it is dropped.
 */
function pictureSegment(image: string | undefined, pictures: Required<DeltaMarkdownOptions>): Segment | null {
  if (!image || image.startsWith('data:')) {
    return null;
  }
  const target = pictures.imageTarget(image);
  return target ? { text: `![${pictures.imageAlt}](${target})`, attributes: {}, isPicture: true } : null;
}

function plainText(segments: Segment[]): string {
  return segments.map(s => s.text).join('');
}

function inlineMarkdown(segments: Segment[]): string {
  return segments.map(({ text, attributes, isPicture }) => {
    if (isPicture) {
      return text;
    }

    let wrapped = text;
    if (attributes.code) {
      wrapped = `\`${wrapped}\``;
    }
    if (attributes.bold) {
      wrapped = `**${wrapped}**`;
    }
    if (attributes.italic) {
      wrapped = `_${wrapped}_`;
    }
    if (attributes.strike) {
      wrapped = `~~${wrapped}~~`;
    }
    if (attributes.link) {
      wrapped = `[${wrapped}](${attributes.link})`;
    }
    return wrapped;
  }).join('');
}

function blockMarkdown(block: DeltaAttributes, text: string, ordinal: number): string {
  const indent = '  '.repeat(block.indent ?? 0);

  if (block.header) {
    return `${'#'.repeat(Math.min(block.header, 6))} ${text}`;
  }
  if (block.list === 'ordered') {
    return `${indent}${ordinal}. ${text}`;
  }
  if (block.list === 'bullet') {
    return `${indent}- ${text}`;
  }
  if (block.list === 'checked') {
    return `${indent}- [x] ${text}`;
  }
  if (block.list === 'unchecked') {
    return `${indent}- [ ] ${text}`;
  }
  if (block.blockquote) {
    return `> ${text}`;
  }
  return text;
}
