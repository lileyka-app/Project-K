import { TestBed } from '@angular/core/testing';
import { KurinMarkComponent } from './kurin-mark';
import { KurinBranch } from '../../models/enums/kurin-branch.enum';

describe('KurinMarkComponent', () => {
  function render(inputs: Record<string, unknown>): HTMLElement {
    const fixture = TestBed.createComponent(KurinMarkComponent);
    for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('keeps the red plate for a УПЮ kurin', () => {
    const element = render({ number: 47, branch: KurinBranch.UPYu, emblemUrl: 'https://x/e.png' });
    expect(element.querySelector('app-kurin-number')).not.toBeNull();
    expect(element.querySelector('img')).toBeNull();
  });

  it('shows a senior kurin its emblem as it is, or a quiet placeholder', () => {
    expect(render({ number: 5, branch: KurinBranch.USP, emblemUrl: 'https://x/e.png' }).querySelector('img')?.getAttribute('src')).toBe('https://x/e.png');
    const empty = render({ number: 5, branch: KurinBranch.USP });
    expect(empty.querySelector('app-kurin-number')).toBeNull();
    expect(empty.querySelector('.kurin-mark__empty')).not.toBeNull();
  });
});
