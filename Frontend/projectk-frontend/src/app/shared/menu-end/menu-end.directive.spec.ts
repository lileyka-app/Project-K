import { EventEmitter } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Menu } from '@openng/optimus-ui/menu';
import { MenuEndDirective } from './menu-end.directive';

describe('MenuEndDirective', () => {
  it('ставить правий край меню під правий край кнопки', () => {
    const container = document.createElement('div');
    Object.defineProperty(container, 'offsetWidth', { value: 200 });
    const target = document.createElement('button');
    target.getBoundingClientRect = () => ({ right: 900 }) as DOMRect;
    const menu = { onShow: new EventEmitter<unknown>(), container, target };

    TestBed.configureTestingModule({ providers: [{ provide: Menu, useValue: menu }] });
    TestBed.runInInjectionContext(() => new MenuEndDirective());
    menu.onShow.emit({});

    expect(container.style.left).toBe(`${900 + window.scrollX - 200}px`);
  });
});
