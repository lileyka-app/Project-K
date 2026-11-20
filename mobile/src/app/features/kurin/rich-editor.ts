import { AfterViewInit, Component, ElementRef, OnDestroy, ViewEncapsulation, input, output, viewChild } from '@angular/core';
import Quill from 'quill';
import { DeltaOp, quillDeltaToMarkdown } from './delta-markdown';
import { MarkdownBlock, markdownBlocksToDelta } from './project-markdown';

export const IMAGE_MIME_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

/**
 * A text with formatting on the phone (web uses p-editor around the same Quill). It starts from the
 * stored Markdown's blocks and gives back Markdown the way the web does, so a text written on one
 * opens unchanged on the other. Pictures — from the toolbar, a paste or the camera roll — are handed
 * to the page through `imagePicked`; the page uploads them and calls `insertImage` with the link.
 */
@Component({
  selector: 'app-rich-editor',
  encapsulation: ViewEncapsulation.None,
  styles: `
    app-rich-editor { display: block; }
    app-rich-editor .ql-toolbar.ql-snow {
      border: 1px solid var(--lk-line);
      border-radius: 8px 8px 0 0;
      font-family: inherit;
      padding: 4px;
    }
    app-rich-editor .ql-container.ql-snow {
      border: 1px solid var(--lk-line);
      border-top: 0;
      border-radius: 0 0 8px 8px;
      font-family: inherit;
      font-size: 16px;
      color: var(--lk-ink);
    }
    app-rich-editor .ql-editor { min-height: 7rem; line-height: 1.5; }
    app-rich-editor .ql-editor.ql-blank::before { color: var(--lk-faint); font-style: normal; }
    app-rich-editor .ql-editor img { max-width: 100%; border-radius: 8px; }
    app-rich-editor .ql-snow .ql-stroke { stroke: var(--lk-ink); }
    app-rich-editor .ql-snow .ql-fill { fill: var(--lk-ink); }
    app-rich-editor .ql-snow .ql-picker { color: var(--lk-ink); }
    app-rich-editor .ql-snow button { width: 32px; height: 32px; }
  `,
  template: `<div #host></div>`,
})
export class RichEditor implements AfterViewInit, OnDestroy {
  /** What the text holds now, read from Markdown with the pictures' links resolved. */
  readonly initial = input<MarkdownBlock[]>([]);
  readonly placeholder = input('');
  /** The résumé takes no pictures. */
  readonly withImages = input(true);
  readonly imagePicked = output<File>();

  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private quill: Quill | null = null;

  ngAfterViewInit(): void {
    const toolbar: unknown[] = [[{ header: 2 }, 'bold', 'italic'], [{ list: 'ordered' }, { list: 'bullet' }], ['link']];
    if (this.withImages()) (toolbar[2] as string[]).push('image');

    this.quill = new Quill(this.host().nativeElement, {
      theme: 'snow',
      placeholder: this.placeholder(),
      formats: ['bold', 'italic', 'strike', 'code', 'list', 'link', 'header', 'blockquote', 'indent', ...(this.withImages() ? ['image'] : [])],
      modules: {
        toolbar: { container: toolbar, handlers: { image: () => this.pickImage() } },
        uploader: {
          mimetypes: this.withImages() ? IMAGE_MIME_TYPES : [],
          handler: (_range: unknown, files: File[]) => files.forEach((file) => this.imagePicked.emit(file)),
        },
      },
    });
    this.quill.setContents(markdownBlocksToDelta(this.initial()) as never, 'api');
  }

  ngOnDestroy(): void {
    this.quill = null;
  }

  /** The text as Markdown; `imageTarget` turns a picture's link into what is stored, or drops it. */
  markdown(imageTarget: (src: string) => string | null = () => null): string {
    if (!this.quill) return '';
    return quillDeltaToMarkdown(this.ops(), { imageAlt: 'зображення', imageTarget });
  }

  /** How many pictures the text shows now. */
  imageCount(): number {
    return this.ops().filter((op) => typeof op.insert !== 'string' && op.insert.image).length;
  }

  /** Puts an uploaded picture on its own line where the cursor is. */
  insertImage(url: string): void {
    const quill = this.quill;
    if (!quill) return;
    let at = quill.getSelection(true)?.index ?? quill.getLength() - 1;
    if (at > 0) {
      quill.insertText(at, '\n', 'user');
      at += 1;
    }
    quill.insertEmbed(at, 'image', url, 'user');
    quill.insertText(at + 1, '\n', 'user');
    quill.setSelection(at + 2, 0, 'api');
  }

  private ops(): DeltaOp[] {
    return (this.quill?.getContents().ops ?? []) as DeltaOp[];
  }

  private pickImage(): void {
    const picker = document.createElement('input');
    picker.type = 'file';
    picker.accept = IMAGE_MIME_TYPES.join(',');
    picker.addEventListener('change', () => {
      const file = picker.files?.[0];
      if (file) this.imagePicked.emit(file);
    });
    picker.click();
  }
}
