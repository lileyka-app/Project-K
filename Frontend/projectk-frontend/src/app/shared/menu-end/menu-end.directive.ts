import { Directive, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Menu } from '@openng/optimus-ui/menu';

/**
 * A popup menu under a button at the right of a header opens leftwards: its right edge under the
 * button's right edge, instead of starting at the button and running off past it.
 */
@Directive({ selector: 'p-menu[appMenuEnd]' })
export class MenuEndDirective {
  private readonly menu = inject(Menu);

  constructor() {
    this.menu.onShow.pipe(takeUntilDestroyed()).subscribe(() => this.align());
  }

  private align(): void {
    const container = this.menu.container as HTMLElement | null;
    const target = this.menu.target as HTMLElement | null;
    if (!container || !target) {
      return;
    }

    const right = target.getBoundingClientRect().right + window.scrollX;
    container.style.left = `${Math.max(window.scrollX + 8, right - container.offsetWidth)}px`;
  }
}
