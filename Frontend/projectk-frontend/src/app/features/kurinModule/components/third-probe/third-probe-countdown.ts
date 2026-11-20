import { ChangeDetectionStrategy, Component, NgZone, computed, effect, inject, input, signal } from '@angular/core';
import { countdownMood, countdownParts, youthEndsOn } from '../../functions/third-probe.function';

const TICK_MS = 1000;

/**
 * How long is left to close the third probe: until the 18th birthday, when youth ends. It ticks
 * every second on purpose: the old УПЮ services showed the same clock, and a third-prober is meant
 * to feel it. Shown only to someone who may see the birth date; the page passes null otherwise.
 */
@Component({
  selector: 'app-third-probe-countdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (deadline(); as end) {
    <section class="countdown kurin-tile" [attr.data-mood]="mood()" data-testid="third-probe-countdown" aria-live="off">
      <div class="countdown__head">
        <p class="countdown__label">
          @if (parts().isOver) { Строк третьої проби минув } @else { До закриття третьої проби }
        </p>
        <p class="countdown__date">останній день — {{ deadlineLabel() }}, {{ age() }}-річчя</p>
      </div>
      @if (!parts().isOver) {
      <div class="countdown__digits" role="timer" [attr.aria-label]="ariaLabel()">
        <div class="countdown__unit"><span class="countdown__value">{{ parts().days }}</span><span class="countdown__name">{{ dayWord() }}</span></div>
        <div class="countdown__unit"><span class="countdown__value">{{ pad(parts().hours) }}</span><span class="countdown__name">год</span></div>
        <div class="countdown__unit"><span class="countdown__value">{{ pad(parts().minutes) }}</span><span class="countdown__name">хв</span></div>
        <div class="countdown__unit"><span class="countdown__value">{{ pad(parts().seconds) }}</span><span class="countdown__name">с</span></div>
      </div>
      } @else {
      <p class="countdown__over">Пробу, не закриту до цього дня, Лілейка далі показує як архів.</p>
      }
    </section>
    }
  `,
  styles: `
    .countdown { display: flex; flex-direction: column; gap: 0.75rem; }
    .countdown__head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 0.25rem 1rem; }
    .countdown__label { margin: 0; font-weight: 700; color: var(--p-text-color); }
    .countdown__date, .countdown__over { margin: 0; font-size: 0.85rem; color: var(--p-text-muted-color); }
    .countdown__digits { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.5rem; }
    .countdown__unit {
      display: flex; flex-direction: column; align-items: center; gap: 0.15rem;
      padding: 0.6rem 0.25rem; border: 1px solid var(--p-content-border-color); border-radius: 12px;
    }
    .countdown__value {
      font-size: clamp(1.6rem, 6vw, 2.6rem); font-weight: 800; line-height: 1.05;
      font-variant-numeric: tabular-nums; color: var(--p-primary-color);
    }
    .countdown__name { font-size: 0.75rem; color: var(--p-text-muted-color); }
    .countdown[data-mood='soon'] .countdown__value,
    .countdown[data-mood='urgent'] .countdown__value { color: var(--lil-clay-500); }
    .countdown[data-mood='urgent'] .countdown__unit { border-color: color-mix(in srgb, var(--lil-clay-500) 55%, var(--p-content-border-color)); }
  `
})
export class ThirdProbeCountdownComponent {
  /** Null when the viewer may not see the birth date: then there is no clock at all. */
  readonly dateOfBirth = input<Date | string | null>(null);
  readonly age = input(18);

  private readonly now = signal(new Date());

  readonly deadline = computed(() => youthEndsOn(this.dateOfBirth(), this.age()));
  readonly parts = computed(() => countdownParts(this.deadline() ?? this.now(), this.now()));
  readonly mood = computed(() => countdownMood(this.parts()));
  readonly deadlineLabel = computed(() => this.deadline()?.toLocaleDateString('uk-UA', { day: 'numeric', month: 'long', year: 'numeric' }) ?? '');
  readonly dayWord = computed(() => dayWord(this.parts().days));
  readonly ariaLabel = computed(() => `Лишилося ${this.parts().days} ${this.dayWord()}`);

  constructor() {
    const zone = inject(NgZone);
    // Ticks only while there is something to count, and outside the zone: a timer that never ends
    // would otherwise keep the page from ever settling (tests wait for that, so do some tools).
    effect(onCleanup => {
      if (!this.deadline() || this.parts().isOver) {
        return;
      }
      const timer = zone.runOutsideAngular(() => setInterval(() => this.now.set(new Date()), TICK_MS));
      onCleanup(() => clearInterval(timer));
    });
  }

  pad(value: number): string {
    return String(value).padStart(2, '0');
  }
}

/** «день», «дні», «днів» — as Ukrainian counts them. */
export function dayWord(days: number): string {
  const lastTwo = days % 100;
  const last = days % 10;
  if (lastTwo >= 11 && lastTwo <= 14) {
    return 'днів';
  }
  if (last === 1) {
    return 'день';
  }
  return last >= 2 && last <= 4 ? 'дні' : 'днів';
}
