import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit } from '@angular/core';
import { KurinTagService } from '../../services/kurin-tag-service/kurin-tag.service';

/**
 * Теги людини рядком плашок. Колір тегу — лише крапка: як і категорія події (`BRANDBOOK.md` §2),
 * він не стає заливкою, а текст лишається чорнилом теми. Порожньо, коли тегів немає або вони вимкнені.
 */
@Component({
  selector: 'app-kurin-tag-chips',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (tags().length) {
      <ul class="kurin-tags" [class.kurin-tags--compact]="compact()" aria-label="Теги">
        @for (tag of tags(); track tag.kurinTagKey) {
          <li class="kurin-tag">
            <span class="kurin-tag__dot" [style.background]="tag.colorHex" aria-hidden="true"></span>{{ tag.name }}
          </li>
        }
      </ul>
    }
  `,
  styleUrl: './kurin-tag-chips.css'
})
export class KurinTagChipsComponent implements OnInit {
  private readonly tagService = inject(KurinTagService);

  readonly keys = input<readonly string[] | null | undefined>([]);
  /** Дрібніше, для рядка таблиці. */
  readonly compact = input(false);

  protected readonly tags = computed(() => this.tagService.enabled() ? this.tagService.resolve(this.keys()) : []);

  ngOnInit(): void {
    this.tagService.ensureCurrent();
  }
}
