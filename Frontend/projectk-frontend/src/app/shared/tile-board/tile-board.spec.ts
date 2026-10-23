import { Component, ChangeDetectionStrategy } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TileBoardComponent } from './tile-board';
import { TileDefDirective } from './tile-def.directive';

@Component({
  imports: [TileBoardComponent, TileDefDirective],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <app-tile-board boardKey="member-card">
      <ng-template [appTileDef]="{ key: 'profile', span: 'full', pinned: true, label: 'Профіль' }">
        <div class="tile-content-profile">PROFILE CONTENT</div>
      </ng-template>
      <ng-template [appTileDef]="{ key: 'skills', span: 'half', label: 'Вмілості' }">
        <div class="tile-content-skills">SKILLS CONTENT</div>
      </ng-template>
    </app-tile-board>
  `
})
class HostComponent {}

describe('TileBoardComponent (projection)', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => localStorage.clear());

  it('renders each declared tile as a slot', () => {
    const slots = fixture.nativeElement.querySelectorAll('.tile-board > .tile-slot');
    expect(slots).toHaveLength(2);
  });

  it('projects the actual tile content (guards against ng-template double-wrap)', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.tile-content-profile')?.textContent).toContain('PROFILE CONTENT');
    expect(el.querySelector('.tile-content-skills')?.textContent).toContain('SKILLS CONTENT');
  });

  it('places the pinned tile first', () => {
    const slots = fixture.nativeElement.querySelectorAll('.tile-board > .tile-slot');
    expect(slots[0].textContent).toContain('PROFILE CONTENT');
  });

  it('wraps projected content in a body element that is interactive by default', () => {
    const bodies = fixture.nativeElement.querySelectorAll('.tile-slot .tile-slot__body');
    expect(bodies).toHaveLength(2);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.tile-slot__body .tile-content-skills')).toBeTruthy();
    expect(getComputedStyle(bodies[0]).pointerEvents).not.toBe('none');
  });

  it('freezes tile-body interaction in edit mode (guards the encapsulation bug)', () => {
    const el = fixture.nativeElement as HTMLElement;
    const toggle = Array.from(el.querySelectorAll('button')).find(b =>
      b.textContent?.includes('Налаштувати вигляд')
    ) as HTMLButtonElement;
    toggle.click();
    fixture.detectChanges();

    const body = el.querySelector('.tile-slot .tile-slot__body') as HTMLElement;
    expect(getComputedStyle(body).pointerEvents).toBe('none');
  });
});
