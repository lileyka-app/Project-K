import { mkdirSync, writeFileSync } from 'node:fs';
import { expect, type Page } from '@playwright/test';

/** One thing drawn where it should not be: outside the screen, past its card or row, or cut. */
export interface LayoutProblem {
  kind: 'off-screen' | 'past-card' | 'past-row' | 'cut-text';
  what: string;
  text: string;
  box: string;
  container?: string;
}

/**
 * Looks over what the screen shows now (the visible page and any open sheet, menu or dialog) for
 * text, icons and pictures that run off the screen, out of their card or inset list, out of their
 * row, or are cut without an ellipsis. Every screenshot runs it, so each screen the tests visit is
 * checked on iPhone and Android alike.
 */
export function layoutProblems(page: Page): Promise<LayoutProblem[]> {
  return page.evaluate(() => {
    const problems: LayoutProblem[] = [];
    const vw = document.documentElement.clientWidth;
    const TARGETS = 'img, ion-icon, svg, ion-badge, ion-note, ion-toggle, ion-checkbox, ion-radio, ion-chip, ion-avatar';
    const name = (el: Element) => {
      const cls = [...el.classList].filter((c) => !c.startsWith('ion-') && !c.startsWith('sc-') && !c.startsWith('md') && !c.startsWith('ios') && c !== 'hydrated').slice(0, 2);
      const id = (el as HTMLElement).dataset?.['testid'];
      const classes = cls.length ? `.${cls.join('.')}` : '';
      const testId = id ? `[${id}]` : '';
      return `${el.tagName.toLowerCase()}${classes}${testId}`;
    };
    const where = (el: Element) => {
      const page = el.closest('.ion-page, ion-modal, ion-action-sheet, ion-menu, ion-alert, ion-popover');
      return page ? name(page) : 'app';
    };
    const box = (r: DOMRect) => `${Math.round(r.left)},${Math.round(r.top)} ${Math.round(r.width)}×${Math.round(r.height)}`;
    const shown = (el: Element) => {
      const r = el.getBoundingClientRect();
      if (r.width <= 1 || r.height <= 1) return false;
      if (r.bottom <= 0 || r.top >= innerHeight) return false; // scrolled away: a later shot sees it
      return (el as HTMLElement).checkVisibility?.({ opacityProperty: true, visibilityProperty: true }) ?? true;
    };
    const skip = (el: Element) =>
      !!el.closest(
        'ion-segment, ion-skeleton-text, ion-refresher, .sr-only, ion-datetime, ion-picker, [data-layout-free], ion-toast, ion-tab-bar',
      );
    const scrollsSideways = (el: Element) => {
      for (let node = el.parentElement; node; node = node.parentElement) {
        const ox = getComputedStyle(node).overflowX;
        if ((ox === 'auto' || ox === 'scroll') && node.scrollWidth > node.clientWidth + 1) return true;
      }
      return false;
    };
    // Where the element's own text is drawn (a label's box is often wider than its words); the box
    // itself for icons, pictures and controls.
    const inkOf = (el: Element): DOMRect => {
      const texts = [...el.childNodes].filter((n) => n.nodeType === Node.TEXT_NODE && (n.textContent ?? '').trim());
      if (!texts.length || el.matches(TARGETS)) return el.getBoundingClientRect();
      const range = document.createRange();
      range.setStartBefore(texts[0]);
      range.setEndAfter(texts.at(-1) ?? texts[0]);
      const ink = range.getBoundingClientRect();
      // A form field's value is not laid out as text: its box stands for it.
      return ink.width > 0 ? clipped(el, ink) : el.getBoundingClientRect();
    };
    // Up the composed tree, so a slotted label meets the shadow parts that hold it (ion-title's).
    const parentOf = (node: Element): Element | null =>
      node.assignedSlot ?? node.parentElement ?? ((node.getRootNode() as ShadowRoot).host || null);
    // Text cut by a box that hides its overflow (an ellipsis, a title) only shows up to that box.
    const clipped = (el: Element, ink: DOMRect): DOMRect => {
      let { left, right } = ink;
      for (let node: Element | null = el; node && node !== document.body; node = parentOf(node)) {
        const ox = getComputedStyle(node).overflowX;
        if (ox !== 'hidden' && ox !== 'clip') continue;
        const c = node.getBoundingClientRect();
        left = Math.max(left, c.left);
        right = Math.min(right, c.right);
      }
      return new DOMRect(left, ink.top, Math.max(0, right - left), ink.height);
    };
    const textOf = (el: Element) => (el.textContent ?? '').replace(/\s+/g, ' ').trim().slice(0, 60);
    const outsideBox = (el: Element, r: DOMRect, what: string): LayoutProblem | null => {
      // A rounded card or inset list: nothing may touch or cross its edge.
      const card = el.closest('ion-card, ion-list.list-inset, .lk-card');
      if (card && card !== el) {
        const c = card.getBoundingClientRect();
        if (r.left < c.left + 4 || r.right > c.right - 4) {
          return { kind: 'past-card', what, text: textOf(el), box: box(r), container: `${name(card)} ${box(c)}` };
        }
      }
      const row = el.closest('ion-item');
      if (row && row !== el) {
        const c = row.getBoundingClientRect();
        if (r.left < c.left - 1 || r.right > c.right + 1 || r.bottom > c.bottom + 1) {
          return { kind: 'past-row', what, text: textOf(el), box: box(r), container: `${name(row)} ${box(c)}` };
        }
      }
      return null;
    };
    // Text wider than its box: it spills over or is clipped without an ellipsis.
    const cutText = (el: Element, r: DOMRect, what: string): LayoutProblem | null => {
      const style = getComputedStyle(el);
      const control = el.matches('ion-icon, ion-toggle, ion-checkbox, ion-radio, ion-select, ion-input, ion-textarea');
      if (el.scrollWidth > el.clientWidth + 1 && el.clientWidth > 0 && style.textOverflow !== 'ellipsis' && !control) {
        const own = [...el.childNodes].some((n) => n.nodeType === Node.TEXT_NODE && (n.textContent ?? '').trim());
        if (own && !scrollsSideways(el) && style.overflowX !== 'auto' && style.overflowX !== 'scroll') {
          return { kind: 'cut-text', what, text: textOf(el), box: `${box(r)} needs ${el.scrollWidth}px` };
        }
      }
      return null;
    };
    const problemOf = (el: Element): LayoutProblem | null => {
      if (skip(el) || !shown(el)) return null;
      const tag = el.tagName.toLowerCase();
      if (tag === 'script' || tag === 'style' || tag === 'title') return null;
      const r = inkOf(el);
      const what = `${name(el)} in ${where(el)}`;
      if ((r.left < -1 || r.right > vw + 1) && !scrollsSideways(el)) {
        return { kind: 'off-screen', what, text: textOf(el), box: box(r) };
      }
      return outsideBox(el, r, what) ?? cutText(el, r, what);
    };

    // What carries text of its own, plus icons, pictures and controls.
    const targets = new Set<Element>(document.querySelectorAll(TARGETS));
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if ((node.textContent ?? '').trim() && node.parentElement) targets.add(node.parentElement);
    }

    for (const el of targets) {
      const problem = problemOf(el);
      if (problem) problems.push(problem);
    }
    return problems;
  });
}

/** Fails the test on a layout problem; with LAYOUT_REPORT=1 only writes them down for a survey. */
export async function checkLayout(page: Page, project: string, name: string): Promise<void> {
  // A page still sliding in (iOS pushes from the right) is off-screen for a moment: let it land.
  let problems = await layoutProblems(page);
  for (let wait = 0; problems.length && wait < 6; wait++) {
    await page.waitForTimeout(250);
    problems = await layoutProblems(page);
  }
  if (process.env['LAYOUT_REPORT']) {
    if (!problems.length) return;
    mkdirSync('test-results/layout', { recursive: true });
    writeFileSync(`test-results/layout/${project}-${name}.json`, JSON.stringify(problems, null, 2));
    return;
  }
  expect(problems, `layout of ${name}`).toEqual([]);
}
