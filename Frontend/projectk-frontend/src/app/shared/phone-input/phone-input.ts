import { ChangeDetectionStrategy, Component, computed, effect, forwardRef, input, signal } from '@angular/core';
import { AbstractControl, ControlValueAccessor, FormsModule, NG_VALIDATORS, NG_VALUE_ACCESSOR, ValidationErrors, Validator } from '@angular/forms';
import { CountryCode } from 'libphonenumber-js';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import {
  DEFAULT_PHONE_COUNTRY,
  formatAsYouType,
  PHONE_COUNTRIES,
  phoneLengthIssue,
  splitPhone,
  toStoredPhone
} from '../functions/phone.function';

/**
 * A phone number with its country: the country picks the calling code (+420), the box takes the
 * rest as people write it. The form gets the number as it is stored, +420777123456.
 */
@Component({
  selector: 'app-phone-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, SelectModule, InputTextModule],
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PhoneInputComponent), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => PhoneInputComponent), multi: true }
  ],
  template: `
    <div class="phone-input">
      <p-select
        class="phone-input__country"
        [options]="countries"
        optionLabel="label"
        optionValue="code"
        [filter]="true"
        filterBy="label,dialCode"
        [ngModel]="country()"
        (ngModelChange)="setCountry($event)"
        [disabled]="disabled()"
        [ariaLabel]="'Країна номера'"
        appendTo="body">
        <ng-template #selectedItem let-selected>
          @if (selected) { <span><span class="phone-input__code">{{ selected.code }}</span> {{ selected.dialCode }}</span> }
        </ng-template>
        <ng-template #item let-option>
          <span><span class="phone-input__code">{{ option.code }}</span> {{ option.label }} <span class="phone-input__dial">{{ option.dialCode }}</span></span>
        </ng-template>
      </p-select>
      <input
        pInputText
        class="phone-input__number"
        type="tel"
        autocomplete="tel-national"
        [id]="inputId()"
        [ngModel]="national()"
        (ngModelChange)="setNational($event)"
        (blur)="touched()"
        [disabled]="disabled()"
        [attr.aria-invalid]="invalid() || !!shownIssue()"
        [attr.aria-describedby]="shownIssue() ? inputId() + '-issue' : null" />
    </div>
    @if (shownIssue(); as issue) {
      <small class="phone-input__issue" [id]="inputId() + '-issue'">{{ issue === 'long' ? 'Задовгий номер.' : 'Закороткий номер.' }}</small>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 0.25rem; }
    .phone-input__issue { color: var(--p-red-600); font-size: var(--lil-text-caption); }
    .phone-input { display: flex; gap: 0.5rem; width: 100%; max-width: 24rem; }
    .phone-input__country { flex: 0 0 8.5rem; }
    .phone-input__number { flex: 1 1 auto; min-width: 0; }
    .phone-input__dial { color: var(--p-text-muted-color); margin-left: 0.25rem; }
    .phone-input__code { display: inline-block; min-width: 1.75rem; margin-right: 0.25rem; color: var(--p-text-muted-color); font-size: var(--lil-text-caption); font-weight: 700; }
  `
})
export class PhoneInputComponent implements ControlValueAccessor, Validator {
  /** The country a new number starts in, as the край's (CZ for cz). */
  readonly defaultCountry = input<CountryCode>(DEFAULT_PHONE_COUNTRY);
  readonly inputId = input<string>('phoneNumber');
  readonly invalid = input(false);

  readonly countries = PHONE_COUNTRIES;
  readonly country = signal<CountryCode>(DEFAULT_PHONE_COUNTRY);
  readonly national = signal('');
  readonly disabled = signal(false);
  private readonly left = signal(false);

  /**
   * The length the field complains about: too long at once, too short only once the person has
   * left the box, so it does not nag while the number is still being typed.
   */
  readonly shownIssue = computed(() => {
    const issue = phoneLengthIssue(toStoredPhone(this.country(), this.national()));
    return issue === 'long' || (issue === 'short' && this.left()) ? issue : null;
  });

  private written = false;
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    // Until a number is there, the field follows the край it is filled in for.
    effect(() => {
      const fallback = this.defaultCountry();
      if (!this.national()) {
        this.country.set(fallback);
      }
    });
  }

  writeValue(value: string | null | undefined): void {
    const { country, national } = splitPhone(value, this.defaultCountry());
    this.written = !!national;
    this.country.set(country);
    this.national.set(national);
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  validate(control: AbstractControl): ValidationErrors | null {
    const issue = phoneLengthIssue(control.value);
    return issue ? { phone: issue } : null;
  }

  setCountry(country: CountryCode): void {
    this.country.set(country ?? this.defaultCountry());
    this.emit();
  }

  setNational(value: string): void {
    this.national.set(value ?? '');
    this.written = true;
    this.emit();
  }

  /** Spaces go in once the number is typed, so they never fight the caret. */
  touched(): void {
    this.national.update(national => formatAsYouType(this.country(), national));
    this.left.set(true);
    this.onTouched();
  }

  private emit(): void {
    if (this.written) {
      this.onChange(toStoredPhone(this.country(), this.national()));
    }
  }
}
