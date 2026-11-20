import { TestBed } from '@angular/core/testing';
import { ThirdProbeCountdownComponent, dayWord } from './third-probe-countdown';

describe('ThirdProbeCountdownComponent', () => {
  afterEach(() => vi.useRealTimers());

  it('counts down to the 18th birthday, every second', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2028, 5, 1, 21, 58, 30));
    const fixture = TestBed.createComponent(ThirdProbeCountdownComponent);
    fixture.componentRef.setInput('dateOfBirth', '2010-06-03');
    fixture.detectChanges();

    const units = () => [...(fixture.nativeElement as HTMLElement).querySelectorAll('.countdown__unit')]
      .map(unit => unit.textContent!.replace(/\s+/g, ''));
    expect(units()).toEqual(['1день', '02год', '01хв', '30с']);

    vi.advanceTimersByTime(1000);
    fixture.detectChanges();
    expect(units()[3]).toBe('29с');
  });

  it('shows nothing to someone who may not see the birth date', () => {
    const fixture = TestBed.createComponent(ThirdProbeCountdownComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="third-probe-countdown"]')).toBeNull();
  });

  it('says days the Ukrainian way', () => {
    expect([1, 2, 5, 11, 21, 22, 25, 111, 581].map(dayWord)).toEqual(['день', 'дні', 'днів', 'днів', 'день', 'дні', 'днів', 'днів', 'день']);
  });
});
