import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { parseDateOnlyString, toDateOnlyString } from '../../../../../kurinModule/functions/to-date-only-string.function';
import {
  DUES_ENTRY_KIND_LABELS,
  DUES_PAYMENT_METHOD_LABELS,
  DUES_STANDING_LABELS,
  DuesEntryKind,
  DuesPaymentMethod,
  GROUP_DUES_KINDS,
  PERSONAL_DUES_KINDS
} from '../../../../models/dues.enums';
import { DuesAccountDto, DuesEntryDto, DuesPersonDto, UpsertDuesEntryRequest } from '../../../../models/group-dues.dto';
import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE, currencySymbol, money } from '../../../../functions/dues-format.function';

/** The other side of an exchange: cash goes to the card, the card to cash. */
export function oppositeMethod(method: DuesPaymentMethod): DuesPaymentMethod {
  return method === DuesPaymentMethod.Cash ? DuesPaymentMethod.Card : DuesPaymentMethod.Cash;
}

/**
 * One operation of a box, new or edited — a гурток's (with people to name) or the kurin's (without).
 * The dialog only gathers and checks; the page sends it and reloads, because every operation moves
 * balances the page already shows.
 */
@Component({
  selector: 'app-dues-entry-dialog',
  imports: [ReactiveFormsModule, DialogModule, ButtonModule, SelectModule, SelectButtonModule, DatePickerModule, InputTextModule, TextareaModule],
  templateUrl: './dues-entry-dialog.html',
  styleUrl: './dues-entry-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DuesEntryDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly visible = model(false);
  /** The kinds this box holds; a гурток's by default. */
  readonly kinds = input<readonly DuesEntryKind[]>(GROUP_DUES_KINDS);
  /** Whose вкладка a personal operation may be about. Empty for the kurin's box. */
  readonly accounts = input<DuesAccountDto[]>([]);
  /** Who may have collected the money. */
  readonly people = input<DuesPersonDto[]>([]);
  readonly personPlaceholder = input('Обери юнака');
  readonly entry = input<DuesEntryDto | null>(null);
  readonly saving = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly currency = input(DEFAULT_CURRENCY);
  readonly numberLocale = input(DEFAULT_NUMBER_LOCALE);
  readonly currencySymbol = computed(() => currencySymbol(this.currency()));
  readonly save = output<UpsertDuesEntryRequest>();

  readonly today = new Date();

  readonly kindOptions = computed(() => this.kinds().map(value => ({ value, label: DUES_ENTRY_KIND_LABELS[value] })));
  readonly methodOptions = [DuesPaymentMethod.Cash, DuesPaymentMethod.Card]
    .map(value => ({ value, label: DUES_PAYMENT_METHOD_LABELS[value] }));

  readonly form = this.fb.group({
    kind: this.fb.nonNullable.control<DuesEntryKind>(DuesEntryKind.Contribution, Validators.required),
    method: this.fb.nonNullable.control<DuesPaymentMethod>(DuesPaymentMethod.Cash, Validators.required),
    amount: this.fb.control<number | null>(null, Validators.required),
    occurredOn: this.fb.control<Date | null>(new Date(), Validators.required),
    membershipKey: this.fb.control<string | null>(null),
    collectedByMemberKey: this.fb.control<string | null>(null),
    note: this.fb.control<string>('', Validators.maxLength(500))
  }, { validators: control => DuesEntryDialogComponent.checkShape(control) });

  readonly kind = signal<DuesEntryKind>(DuesEntryKind.Contribution);
  readonly method = signal<DuesPaymentMethod>(DuesPaymentMethod.Cash);
  readonly person = signal<string | null>(null);

  readonly isPersonal = computed(() => PERSONAL_DUES_KINDS.has(this.kind()));
  readonly isExchange = computed(() => this.kind() === DuesEntryKind.Exchange);
  readonly isCorrection = computed(() => this.kind() === DuesEntryKind.Correction);

  /**
   * Ready amounts for a person's вкладка: one quarter at their rate, a year ahead (four quarters
   * less what they already have over), and their debt. A surplus pays the quarters as they come.
   */
  readonly quickAmounts = computed(() => {
    const account = this.accounts().find(a => a.membershipKey === this.person());
    const rate = account?.rateNow ?? 0;
    if (this.kind() !== DuesEntryKind.Contribution || !account || rate <= 0) {
      return [];
    }
    const label = (name: string, amount: number) => `${name} · ${money(amount, this.currency(), this.numberLocale())}`;
    const year = Math.max(0, 4 * rate - account.balance);
    return [
      { label: label('Квартал', rate), amount: rate },
      { label: label('Рік', year), amount: year },
      ...(account.balance < 0 ? [{ label: label('Борг', -account.balance), amount: -account.balance }] : [])
    ];
  });
  readonly exchangeHint = computed(() =>
    `З ${DUES_PAYMENT_METHOD_LABELS[this.method()].toLowerCase()} на ${DUES_PAYMENT_METHOD_LABELS[oppositeMethod(this.method())].toLowerCase()}`);

  /** Youth of the гурток first; those who moved or left come after, named for what they are. */
  readonly personOptions = computed(() => this.accounts().map(account => {
    const standing = DUES_STANDING_LABELS[account.standing];
    return { value: account.membershipKey, label: standing ? `${account.fullName} · ${standing}` : account.fullName };
  }));

  readonly collectorOptions = computed(() => this.people().map(person => ({ value: person.memberKey, label: person.fullName })));

  readonly title = computed(() => this.entry() ? 'Змінити операцію' : 'Записати операцію');

  constructor() {
    this.form.controls.kind.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(kind => this.kind.set(kind));
    this.form.controls.method.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(method => this.method.set(method));
    this.form.controls.membershipKey.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(key => this.person.set(key));

    effect(() => {
      if (this.visible()) {
        this.reset(this.entry());
      }
    });
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    const personal = PERSONAL_DUES_KINDS.has(value.kind);
    this.save.emit({
      kind: value.kind,
      method: value.method,
      counterMethod: value.kind === DuesEntryKind.Exchange ? oppositeMethod(value.method) : null,
      amount: Number(value.amount),
      occurredOn: toDateOnlyString(value.occurredOn)!,
      membershipKey: personal ? value.membershipKey : null,
      collectedByMemberKey: value.collectedByMemberKey,
      note: value.note?.trim() || null
    });
  }

  useAmount(amount: number): void {
    this.form.controls.amount.setValue(amount);
    this.form.controls.amount.markAsDirty();
  }

  close(): void {
    this.visible.set(false);
  }

  showError(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.dirty || control.touched);
  }

  get amountError(): string | null {
    const amount = this.form.controls.amount;
    if (!amount.touched && !amount.dirty) {
      return null;
    }
    if (amount.hasError('required')) {
      return 'Сума потрібна.';
    }
    return this.form.hasError('amountSign') ? 'Сума має бути більшою за нуль.' : null;
  }

  get personError(): string | null {
    return this.form.hasError('personRequired') && this.form.controls.membershipKey.touched ? 'Чия це вкладка?' : null;
  }

  private reset(entry: DuesEntryDto | null): void {
    this.form.reset({
      kind: entry?.kind ?? this.kinds()[0] ?? DuesEntryKind.Contribution,
      method: entry?.method ?? DuesPaymentMethod.Cash,
      amount: entry?.amount ?? null,
      occurredOn: entry ? parseDateOnlyString(entry.occurredOn) : new Date(),
      membershipKey: entry?.membershipKey ?? null,
      collectedByMemberKey: entry?.collectedByMemberKey ?? null,
      note: entry?.note ?? ''
    });
    this.kind.set(this.form.controls.kind.value);
    this.method.set(this.form.controls.method.value);
    this.person.set(this.form.controls.membershipKey.value);
  }

  /** The rules that need more than one field: a personal kind names a person, only a correction goes below zero. */
  private static checkShape(control: AbstractControl): ValidationErrors | null {
    const kind = control.get('kind')?.value as DuesEntryKind;
    const amount = Number(control.get('amount')?.value);
    const errors: ValidationErrors = {};

    if (PERSONAL_DUES_KINDS.has(kind) && !control.get('membershipKey')?.value) {
      errors['personRequired'] = true;
    }

    if (Number.isFinite(amount) && (kind === DuesEntryKind.Correction ? amount === 0 : amount <= 0)) {
      errors['amountSign'] = true;
    }

    return Object.keys(errors).length ? errors : null;
  }
}
