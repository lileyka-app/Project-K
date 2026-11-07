import { Component, inject, ChangeDetectionStrategy } from '@angular/core';

import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { KraiOption, KurinNumberRange, OnboardingService, WaitlistRegistration } from '../../../services/onboarding-service/onboarding.service';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { SelectModule } from '@openng/optimus-ui/select';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { MessageService } from '@openng/optimus-ui/api';
import { RouterLink } from '@angular/router';
import { PhoneInputComponent } from '../../../../../shared/phone-input/phone-input';

/** Улад, у якому курінь має число. */
type Ulad = 'UPYu' | 'USP' | 'UPS';

@Component({
  selector: 'app-waitlist-registration',
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    CheckboxModule,
    DatePickerModule,
    PhoneInputComponent,
    ButtonModule,
    CardModule,
    MessageModule,
    SelectModule,
    SelectButtonModule,
    RouterLink
],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <main class="flex justify-center items-center min-h-screen p-4">
      <p-card
        header="Приєднатися до списку очікування"
        subheader="Залиште заявку, щоб отримати доступ до Лілейки"
        [style]="{ width: 'min(100%, 480px)' }"
      >
        @if (submitted) {
          <div class="flex flex-col gap-4">
            <p-message severity="success" styleClass="w-full"
              text="Дякуємо! Заявку надіслано. Ми переглянемо її і напишемо на вказану пошту." />
            <p-message severity="info" styleClass="w-full"
              text="Наші листи часом потрапляють у «Спам» — якщо відповіді не видно, загляньте туди." />
          </div>
        } @else {
        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-2">
              <label for="firstName">Ім'я</label>
              <input pInputText id="firstName" formControlName="firstName" autocomplete="given-name" />
            </div>
            <div class="flex flex-col gap-2">
              <label for="lastName">Прізвище</label>
              <input pInputText id="lastName" formControlName="lastName" autocomplete="family-name" />
            </div>
          </div>

          <div class="flex flex-col gap-2">
            <label for="email">Email</label>
            <input pInputText id="email" type="email" formControlName="email" autocomplete="email" />
          </div>

          <div class="flex flex-col gap-2">
            <label for="phone">Номер телефону</label>
            <app-phone-input inputId="phone" formControlName="phoneNumber" />
          </div>

          <div class="flex flex-col gap-2">
            <label for="dob">Дата народження</label>
            <p-datePicker id="dob"
              formControlName="dateOfBirth"
              [showIcon]="true"
              dateFormat="dd.mm.yy"
              [maxDate]="maxDate"
             />
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-2">
              <label for="stanytsia">Станиця</label>
              <input pInputText id="stanytsia" formControlName="stanytsia" maxlength="120" autocomplete="address-level2" />
            </div>
            @if (kraisUnavailable) {
              <div class="flex flex-col gap-2">
                <label for="regionOrCountry">Край</label>
                <input pInputText id="regionOrCountry" formControlName="regionOrCountry" maxlength="120" autocomplete="country-name" />
              </div>
            } @else {
              <div class="flex flex-col gap-2">
                <label for="krai">Край</label>
                <p-select
                  inputId="krai"
                  formControlName="kraiCode"
                  [options]="krais"
                  optionLabel="name"
                  optionValue="code"
                  [filter]="true"
                  filterBy="name"
                  placeholder="Оберіть край"
                  emptyFilterMessage="Такого краю немає"
                  appendTo="body"
                  styleClass="w-full" />
              </div>
            }
          </div>

          <div class="flex items-center gap-2 mt-2">
            <p-checkbox formControlName="isKurinLeaderCandidate" [binary]="true" inputId="leader" />
            <label for="leader">Я є зв'язковим куреня</label>
          </div>

          @if (!form.get('isKurinLeaderCandidate')?.value) {
            <p-message severity="info" styleClass="w-full"
              text="Наразі ми приймаємо лише курені. Заявки окремих пластунів опрацюємо пізніше." />
          }

          @if (form.get('isKurinLeaderCandidate')?.value) {
            <div class="flex flex-col gap-2">
              <label for="ulad">Улад куреня</label>
              <p-selectbutton
                inputId="ulad"
                formControlName="ulady"
                [options]="uladOptions"
                optionLabel="label"
                optionValue="value"
                [multiple]="true" />
              <small class="text-muted-color">Курінь УСП і УПС водночас — це один курінь зі своїм числом у кожному уладі.</small>
            </div>
          }

          @if (form.get('isKurinLeaderCandidate')?.value && isSenior) {
            <div class="flex flex-col gap-3">
              @if (inUlad('USP')) {
                <div class="flex flex-col gap-2">
                  <label for="usp-number">Число куреня в УСП</label>
                  <input pInputText id="usp-number" formControlName="uspNumber" inputmode="numeric" (input)="onDigitsInput($event, 'uspNumber')" />
                </div>
              }
              @if (inUlad('UPS')) {
                <div class="flex flex-col gap-2">
                  <label for="ups-number">Число куреня в УПС</label>
                  <input pInputText id="ups-number" formControlName="upsNumber" inputmode="numeric" (input)="onDigitsInput($event, 'upsNumber')" />
                </div>
              }
            </div>
          }

          @if (form.get('isKurinLeaderCandidate')?.value && form.hasError('noUlad')) {
            <small class="text-muted-color">Обери улад куреня.</small>
          }

          @if (form.get('isKurinLeaderCandidate')?.value && inUlad('UPYu')) {
            <div class="flex items-center gap-2">
              <p-checkbox formControlName="isPreparatoryKurin" [binary]="true" inputId="preparatory" />
              <label for="preparatory">Мій курінь є підготовчим (ще без числа)</label>
            </div>

            @if (!form.get('isPreparatoryKurin')?.value) {
              <div class="flex flex-col gap-2">
                <label for="kurin">Число куреня</label>
                <input
                  pInputText
                  id="kurin"
                  formControlName="claimedKurinNameOrNumber"
                  inputmode="numeric"
                  pattern="[0-9]*"
                  (input)="onKurinNumberInput($event)"
                />
                @if (kurinNumberOutOfRange) {
                  <small class="text-red-500">Число куреня від {{ numberRange.min }} до {{ numberRange.max }}.</small>
                }
              </div>
            } @else {
              <p-message severity="info" styleClass="w-full"
                text="Число куреню можна буде вписати пізніше, у його налаштуваннях." />
            }
          }

          <p-button label="Подати заявку"
            type="submit"
            [disabled]="form.invalid || loading"
            [loading]="loading"
            styleClass="w-full"
           />
          <p class="m-0 text-center text-xs text-muted-color">
            Подаючи заявку, ти погоджуєшся з
            <a routerLink="/privacy" class="text-primary no-underline">політикою конфіденційності</a>.
          </p>
        </form>
        }

        <ng-template pTemplate="footer">
          <div class="text-center text-sm text-muted-color">
            Вже маєте доступ?
            <a routerLink="/login" class="font-semibold text-primary no-underline">Увійти</a>
          </div>
        </ng-template>
      </p-card>
    </main>
  `
})
export class WaitlistRegistrationComponent {
  form: FormGroup;
  loading = false;
  submitted = false;
  maxDate = new Date();
  /** The server says the real range; this is its default until it answers. */
  numberRange: KurinNumberRange = { min: 1, max: 200 };
  krais: KraiOption[] = [];
  /** The list did not load: the applicant names the край in words, as the form used to ask. */
  kraisUnavailable = false;
  readonly uladOptions = [
    { label: 'УПЮ', value: 'UPYu' },
    { label: 'УСП', value: 'USP' },
    { label: 'УПС', value: 'UPS' }
  ];

  // Only a Зв'язковий may apply for now: the kurin is what gets onboarded, and a person on
  // their own has nowhere to be placed. The box is required, so the button stays disabled
  // and the notice explains why, instead of letting a lone пластун submit and hear nothing.
  private readonly fb = inject(FormBuilder);
  private readonly onboardingService = inject(OnboardingService);
  private readonly messageService = inject(MessageService);

  constructor() {
    this.form = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phoneNumber: ['', Validators.required],
      dateOfBirth: [null, Validators.required],
      stanytsia: ['', [Validators.required, Validators.maxLength(120)]],
      kraiCode: [null as string | null, Validators.required],
      regionOrCountry: [{ value: '', disabled: true }, [Validators.required, Validators.maxLength(120)]],
      isKurinLeaderCandidate: [false, Validators.requiredTrue],
      isPreparatoryKurin: [false],
      claimedKurinNameOrNumber: ['', this.kurinNumberValidators()],
      ulady: [['UPYu'] as Ulad[]],
      uspNumber: [{ value: '', disabled: true }, this.kurinNumberValidators()],
      upsNumber: [{ value: '', disabled: true }, this.kurinNumberValidators()]
    }, { validators: (control: AbstractControl) => this.uladChosen(control) });

    // УПЮ stands alone; УСП and УПС go together, each with its own number.
    this.form.get('ulady')!.valueChanges.subscribe((ulady: Ulad[] | null) => this.pickUlady(ulady ?? []));

    // A підготовчий курінь has no number yet, so the field steps aside rather than stay required.
    this.form.get('isPreparatoryKurin')!.valueChanges.subscribe(() => this.syncNumberFields());

    this.onboardingService.getKurinNumberRange().subscribe({
      next: range => {
        this.numberRange = range;
        for (const name of ['claimedKurinNameOrNumber', 'uspNumber', 'upsNumber']) {
          const number = this.form.get(name)!;
          number.setValidators(this.kurinNumberValidators());
          number.updateValueAndValidity();
        }
      },
      // The default range stands in; the server still checks the real one.
      error: () => undefined
    });

    this.onboardingService.getKrais().subscribe({
      next: krais => {
        this.krais = krais;
        const krai = this.form.get('kraiCode')!;
        // Most applicants are in the default край, so it comes picked.
        if (!krai.value) {
          krai.setValue(krais.find(k => k.isDefault)?.code ?? null);
        }
      },
      error: () => {
        this.kraisUnavailable = true;
        this.form.get('kraiCode')!.disable();
        this.form.get('regionOrCountry')!.enable();
      }
    });
  }

  get isSenior(): boolean {
    return this.inUlad('USP') || this.inUlad('UPS');
  }

  inUlad(ulad: Ulad): boolean {
    return ((this.form.get('ulady')!.value as Ulad[] | null) ?? []).includes(ulad);
  }

  /** Picking УПЮ drops УСП and УПС, and the other way round: a kurin is never both. */
  private pickUlady(ulady: Ulad[]): void {
    const previous = this.lastUlady;
    const added = ulady.filter(u => !previous.includes(u));
    let next = ulady;
    if (added.includes('UPYu')) {
      next = ['UPYu'];
    } else if (added.some(u => u !== 'UPYu')) {
      next = ulady.filter(u => u !== 'UPYu');
    }
    this.lastUlady = next;
    if (next.length !== ulady.length) {
      this.form.get('ulady')!.setValue(next, { emitEvent: false });
    }
    this.syncNumberFields();
  }

  private lastUlady: Ulad[] = ['UPYu'];

  /** Each number field is on only while it means something, so a hidden one never blocks the form. */
  private syncNumberFields(): void {
    const preparatory = !!this.form.get('isPreparatoryKurin')!.value;
    this.toggle('claimedKurinNameOrNumber', this.inUlad('UPYu') && !preparatory);
    this.toggle('uspNumber', this.inUlad('USP'));
    this.toggle('upsNumber', this.inUlad('UPS'));
    this.form.updateValueAndValidity();
  }

  private toggle(name: string, on: boolean): void {
    const control = this.form.get(name)!;
    if (on && control.disabled) {
      control.enable({ emitEvent: false });
    } else if (!on && control.enabled) {
      control.reset('', { emitEvent: false });
      control.disable({ emitEvent: false });
    }
  }

  private uladChosen(control: AbstractControl): ValidationErrors | null {
    const value = control.value as { isKurinLeaderCandidate?: boolean; ulady?: Ulad[] | null };
    return value.isKurinLeaderCandidate && !value.ulady?.length ? { noUlad: true } : null;
  }

  get kurinNumberOutOfRange(): boolean {
    const number = this.form.get('claimedKurinNameOrNumber')!;
    return number.dirty && (number.hasError('min') || number.hasError('max'));
  }

  private kurinNumberValidators() {
    return [
      Validators.required,
      Validators.pattern(/^\d+$/),
      Validators.min(this.numberRange.min),
      Validators.max(this.numberRange.max)
    ];
  }

  onKurinNumberInput(event: Event): void {
    this.onDigitsInput(event, 'claimedKurinNameOrNumber');
  }

  onDigitsInput(event: Event, controlName: string): void {
    const input = event.target as HTMLInputElement;
    const digitsOnly = input.value.replace(/\D/g, '');

    if (input.value !== digitsOnly) {
      input.value = digitsOnly;
      this.form.get(controlName)?.setValue(digitsOnly, { emitEvent: false });
    }
  }

  /** What the server takes: a senior kurin's numbers per улад, or the one УПЮ number. */
  private registration(): WaitlistRegistration {
    const { ulady: _ulady, uspNumber, upsNumber, ...rest } = this.form.value;
    if (!this.isSenior) {
      return rest;
    }
    return {
      ...rest,
      claimedKurinNameOrNumber: null,
      isPreparatoryKurin: false,
      uspNumber: uspNumber ? Number(uspNumber) : null,
      upsNumber: upsNumber ? Number(upsNumber) : null
    };
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.loading = true;
    this.onboardingService.submitWaitlist(this.registration()).subscribe({
      next: () => {
        this.submitted = true;
        this.loading = false;
        this.messageService.add({
          severity: 'success',
          summary: 'Заявку надіслано',
          detail: 'Відповідь прийде на пошту. Перевірте й теку «Спам».'
        });
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Помилка',
          detail: err.error?.message || 'Не вдалося надіслати заявку'
        });
        this.loading = false;
      }
    });
  }
}
