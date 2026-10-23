import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PREVIEW_FRAME_MS, WelcomePageComponent } from './welcome-page';

describe('WelcomePageComponent', () => {
  let fixture: ComponentFixture<WelcomePageComponent>;
  let component: WelcomePageComponent;

  const create = () => {
    fixture = TestBed.createComponent(WelcomePageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  beforeEach(async () => {
    vi.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [WelcomePageComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  afterEach(() => vi.useRealTimers());

  it('advances the preview one frame at a time and wraps around', () => {
    create();
    expect(component.active()).toBe(0);

    vi.advanceTimersByTime(PREVIEW_FRAME_MS);
    expect(component.active()).toBe(1);

    vi.advanceTimersByTime(PREVIEW_FRAME_MS * (component.screens.length - 1));
    expect(component.active()).toBe(0);
  });

  it('lets a chosen frame stay up for a full turn', () => {
    create();
    vi.advanceTimersByTime(PREVIEW_FRAME_MS / 2);
    component.show(2);

    vi.advanceTimersByTime(PREVIEW_FRAME_MS - 100);
    expect(component.active()).toBe(2);

    vi.advanceTimersByTime(200);
    expect(component.active()).toBe(0);
  });

  // The frames are pictures inside a window frame, and nothing inside it catches the pointer,
  // so a tester cannot mistake a screenshot for the app.
  it('frames every screen and shows exactly one', () => {
    create();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.preview-window__bar')).not.toBeNull();
    expect(el.querySelector('a.preview-window')).toBeNull();
    expect(el.querySelectorAll('.preview-window__frame')).toHaveLength(component.screens.length);
    expect(el.querySelectorAll('.preview-window__frame--active')).toHaveLength(1);
  });

  it('lists calendars before the documentation that is still to come', () => {
    create();
    const headings = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.feature-card h3'))
      .map(h => h.textContent?.trim());

    expect(headings).toEqual(['Реєстр куреня', 'Календарі', 'Документація']);
    const tags = (fixture.nativeElement as HTMLElement).querySelectorAll('.feature-card p-tag');
    expect(tags).toHaveLength(1);
  });
});
