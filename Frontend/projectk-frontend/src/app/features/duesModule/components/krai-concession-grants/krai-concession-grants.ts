import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import { TagModule } from '@openng/optimus-ui/tag';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { quarterKey, quarterLabel } from '../../functions/dues-format.function';
import { QuarterOption } from '../../functions/from-quarter-options.function';
import { DuesConcessionGrantDto, KraiConcessionDto, describeConcession } from '../../models/concession.dto';
import { QuarterDto } from '../../models/group-dues.dto';
import { DuesService } from '../../services/dues-service/dues.service';

/** How far ahead an end can be picked: three пластові роки. */
const END_QUARTERS_AHEAD = 12;

/**
 * Пільги краю однієї людини в гуртку: які надано, з якого й до якого кварталу і чому, та форма,
 * щоб надати ще одну. Кожна дія зберігається одразу; з усіх пільг людини діє найвигідніша.
 */
@Component({
  selector: 'app-krai-concession-grants',
  imports: [FormsModule, ButtonModule, InputTextModule, SelectModule, TagModule],
  templateUrl: './krai-concession-grants.html',
  styleUrl: './krai-concession-grants.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KraiConcessionGrantsComponent {
  private readonly dues = inject(DuesService);
  private readonly confirmation = inject(ConfirmationService);

  readonly groupKey = input.required<string>();
  readonly membershipKey = input.required<string>();
  readonly grants = input<DuesConcessionGrantDto[]>([]);
  readonly concessions = input<KraiConcessionDto[]>([]);
  readonly fromOptions = input<QuarterOption[]>([]);
  readonly currentQuarter = input.required<QuarterDto>();
  readonly money = input.required<(amount: number) => string>();

  /** Something was saved; the page reloads the box. */
  readonly changed = output<void>();

  readonly concessionKey = signal<string | null>(null);
  readonly fromQuarter = signal<string>('');
  readonly toQuarter = signal<string | null>(null);
  readonly reason = signal('');
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly editingKey = signal<string | null>(null);
  readonly editTo = signal<string | null>(null);
  readonly editReason = signal('');

  readonly concessionOptions = computed(() => this.concessions().map(c => ({
    value: c.key,
    label: c.name,
    rule: describeConcession(c, this.money())
  })));

  readonly toOptions = computed(() => endOptions(this.fromOptions().find(o => o.value === this.fromQuarter())?.quarter ?? this.currentQuarter()));

  readonly canGrant = computed(() => !!this.concessionKey() && !!this.fromQuarter() && !this.saving());

  describe(grant: DuesConcessionGrantDto): string {
    return describeConcession(grant, this.money());
  }

  period(grant: DuesConcessionGrantDto): string {
    const from = quarterLabel(grant.fromQuarter);
    return grant.toQuarter ? `${from} — ${quarterLabel(grant.toQuarter)}` : `з ${from}, без кінця`;
  }

  startGranting(): void {
    if (!this.fromQuarter()) {
      this.fromQuarter.set(quarterKey(this.currentQuarter()));
    }
  }

  grant(): void {
    const from = this.fromOptions().find(o => o.value === this.fromQuarter())?.quarter;
    const key = this.concessionKey();
    if (!from || !key) {
      return;
    }
    const to = this.toOptions().find(o => o.value === this.toQuarter())?.quarter ?? null;
    this.run(this.dues.grantConcession(this.groupKey(), this.membershipKey(), {
      concessionKey: key,
      fromQuarter: from,
      toQuarter: to,
      reason: this.reason().trim() || null
    }), () => {
      this.concessionKey.set(null);
      this.toQuarter.set(null);
      this.reason.set('');
    });
  }

  edit(grant: DuesConcessionGrantDto): void {
    this.editingKey.set(grant.grantKey);
    this.editTo.set(grant.toQuarter ? quarterKey(grant.toQuarter) : null);
    this.editReason.set(grant.reason ?? '');
  }

  editOptions(grant: DuesConcessionGrantDto): QuarterOption[] {
    return endOptions(grant.fromQuarter);
  }

  saveEdit(grant: DuesConcessionGrantDto): void {
    const to = this.editOptions(grant).find(o => o.value === this.editTo())?.quarter ?? null;
    this.run(this.dues.updateConcessionGrant(this.groupKey(), grant.grantKey, {
      toQuarter: to,
      reason: this.editReason().trim() || null
    }), () => this.editingKey.set(null));
  }

  remove(grant: DuesConcessionGrantDto): void {
    this.confirmation.confirm({
      header: 'Видалити пільгу?',
      message: `«${grant.name}» зникне з усіх кварталів, які вона знижувала. Щоб пільга просто перестала діяти, задай їй останній квартал.`,
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => this.run(this.dues.deleteConcessionGrant(this.groupKey(), grant.grantKey), () => this.editingKey.set(null))
    });
  }

  private run(request: ReturnType<DuesService['grantConcession']>, done: () => void): void {
    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        done();
        this.changed.emit();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(serverMessage(error) ?? failureDetail(error, 'Не вдалося зберегти пільгу. Спробуй ще раз.'));
      }
    });
  }
}

/** The quarter it starts in and the ones after it, for picking an end. */
function endOptions(from: QuarterDto): QuarterOption[] {
  const options: QuarterOption[] = [];
  let quarter = from;
  for (let i = 0; i < END_QUARTERS_AHEAD; i++) {
    options.push({ label: quarterLabel(quarter), value: quarterKey(quarter), quarter });
    quarter = quarter.number === 4 ? { year: quarter.year + 1, number: 1 } : { year: quarter.year, number: quarter.number + 1 };
  }
  return options;
}

/** Сервер пояснює українською, чому не надав пільгу (400) чи що вона вже діє (409). */
function serverMessage(error: unknown): string | null {
  if (error instanceof HttpErrorResponse && (error.status === 400 || error.status === 409)) {
    const message = (error.error as { message?: unknown } | null)?.message;
    return typeof message === 'string' && message ? message : null;
  }
  return null;
}
