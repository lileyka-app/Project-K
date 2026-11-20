import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MarkdownBlock } from '../../functions/project-markdown.function';

/**
 * Shows the blocks `parseProjectMarkdown` reads, with this template and nothing else: what a person
 * wrote never reaches `innerHTML`.
 */
@Component({
  selector: 'app-markdown-view',
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="md">
      @for (block of blocks(); track $index) {
      @switch (block.kind) {
      @case ('heading') {
      <p class="md__heading" [attr.data-level]="block.level"><ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" /></p>
      }
      @case ('item') {
      <p class="md__item" [style.padding-left.rem]="1.25 + block.indent * 1.25">
        <span class="md__marker" aria-hidden="true">
          @switch (block.list) {
          @case ('ordered') { {{ block.ordinal }}. }
          @case ('checked') { <span class="pi pi-check-square"></span> }
          @case ('unchecked') { <span class="pi pi-stop"></span> }
          @default { • }
          }
        </span>
        <ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" />
      </p>
      }
      @case ('quote') {
      <blockquote class="md__quote"><ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" /></blockquote>
      }
      @case ('code') {
      <pre class="md__code">{{ block.text }}</pre>
      }
      @default {
      <p class="md__paragraph"><ng-container *ngTemplateOutlet="runsTpl; context: { $implicit: block.runs }" /></p>
      }
      }
      }
    </div>

    <ng-template #runsTpl let-runs>
      @for (run of runs; track $index) {
      @if (run.image) {
      <img class="md__image" [src]="run.image" [alt]="run.text || 'Зображення проєкту'" loading="lazy" />
      } @else if (run.link) {
      <a [href]="run.link" target="_blank" rel="noopener noreferrer"
        [class.font-semibold]="run.bold" [class.italic]="run.italic" [class.line-through]="run.strike">{{ run.text }}</a>
      } @else if (run.code) {
      <code>{{ run.text }}</code>
      } @else {
      <span [class.font-semibold]="run.bold" [class.italic]="run.italic" [class.line-through]="run.strike">{{ run.text }}</span>
      }
      }
    </ng-template>
  `,
  styles: `
    .md { color: var(--p-text-color); font-size: 0.9375rem; line-height: 1.55; overflow-wrap: anywhere; }
    .md p { margin: 0; min-height: 0.75em; }
    .md__heading { font-size: 1.0625rem; font-weight: 700; margin-top: 0.5rem !important; }
    .md__heading[data-level='1'] { font-size: 1.1875rem; }
    .md__marker { color: var(--p-text-muted-color); display: inline-block; margin-left: -1.25rem; width: 1.25rem; }
    .md__quote { border-left: 1px solid var(--p-content-border-color); color: var(--p-text-muted-color); margin: 0.25rem 0; padding-left: 0.75rem; }
    .md__code, .md code {
      background: var(--p-content-hover-background); border: 1px solid var(--p-content-border-color); border-radius: 8px;
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.875em;
    }
    .md code { padding: 0.1em 0.4em; }
    .md__code { margin: 0.35rem 0; overflow-x: auto; padding: 0.5rem 0.75rem; white-space: pre-wrap; }
    .md a { color: var(--p-primary-color); text-decoration: underline; }
    .md__image {
      border: 1px solid var(--p-content-border-color); border-radius: 8px; display: block;
      margin: 0.5rem 0; max-height: 22rem; max-width: 100%;
    }
  `
})
export class MarkdownViewComponent {
  readonly blocks = input.required<MarkdownBlock[]>();
}
