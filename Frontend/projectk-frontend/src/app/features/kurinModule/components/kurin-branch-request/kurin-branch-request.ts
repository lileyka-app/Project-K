import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { MessageModule } from '@openng/optimus-ui/message';
import { MessageService } from '@openng/optimus-ui/api';
import { KurinBranch, KurinNumbering } from '../../models/enums/kurin-branch.enum';
import { KurinBranchRequestDto } from '../../models/kurin-branch-request.dto';
import { KurinBranchRequestService } from '../../services/kurin-branch-request-service/kurin-branch-request.service';
import { kurinBranchesName } from '../../../../shared/functions/kurin-label.function';
import { LocalUtcDatePipe } from '../../../../shared/pipes/local-utc-date.pipe';

/**
 * Улади й числа старшого куреня. Змінити їх сам курінь не може — число в уладі краю роздає
 * адміністратор, — тож тут він бачить, що має, і подає запит; поки запит чекає, видно й його.
 */
@Component({
  selector: 'app-kurin-branch-request',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, CheckboxModule, InputNumberModule, TextareaModule, MessageModule, LocalUtcDatePipe],
  templateUrl: './kurin-branch-request.html',
  styleUrl: './kurin-branch-request.css'
})
export class KurinBranchRequestComponent implements OnInit {
  private readonly requests = inject(KurinBranchRequestService);
  private readonly messages = inject(MessageService);

  readonly kurinKey = input.required<string>();
  readonly branches = input<KurinNumbering[]>([]);
  readonly numberRange = input<{ min: number; max: number }>({ min: 1, max: 200 });

  protected readonly open = signal<KurinBranchRequestDto | null>(null);
  protected readonly editing = signal(false);
  protected readonly sending = signal(false);
  protected readonly problem = signal<string | null>(null);

  protected inUsp = false;
  protected uspNumber: number | null = null;
  protected inUps = false;
  protected upsNumber: number | null = null;
  protected note = '';

  protected readonly current = computed(() => kurinBranchesName(this.branches()));

  ngOnInit(): void {
    this.requests.getOpen(this.kurinKey()).subscribe({
      next: open => this.open.set(open),
      error: () => this.open.set(null)
    });
  }

  protected name(branches: KurinNumbering[]): string {
    return kurinBranchesName(branches);
  }

  protected startEditing(): void {
    const usp = this.branches().find(b => b.branch === KurinBranch.USP);
    const ups = this.branches().find(b => b.branch === KurinBranch.UPS);
    this.inUsp = !!usp;
    this.uspNumber = usp?.number ?? null;
    this.inUps = !!ups;
    this.upsNumber = ups?.number ?? null;
    this.note = '';
    this.problem.set(null);
    this.editing.set(true);
  }

  protected send(): void {
    const uspNumber = this.inUsp ? this.uspNumber : null;
    const upsNumber = this.inUps ? this.upsNumber : null;
    if (uspNumber === null && upsNumber === null) {
      this.problem.set('Познач хоча б один улад і впиши число куреня в ньому.');
      return;
    }

    this.sending.set(true);
    this.problem.set(null);
    this.requests.submit(this.kurinKey(), { uspNumber, upsNumber, note: this.note.trim() || null }).subscribe({
      next: request => {
        this.open.set(request);
        this.editing.set(false);
        this.sending.set(false);
        this.messages.add({ severity: 'success', summary: 'Запит надіслано', detail: 'Адміністратор отримав лист. Рішення прийде сповіщенням.' });
      },
      error: (error: HttpErrorResponse) => {
        this.sending.set(false);
        this.problem.set(this.failure(error.error?.error));
      }
    });
  }

  private failure(code: string | undefined): string {
    switch (code) {
      case 'KurinNumberTaken':
        return 'Таке число в цьому уладі вже має інший курінь краю.';
      case 'KurinNumberOutOfRange':
        return `Число куреня від ${this.numberRange().min} до ${this.numberRange().max}.`;
      case 'NothingToChange':
        return 'Курінь уже має саме ці улади й числа.';
      case 'InvalidKurinBranches':
        return 'Познач хоча б один улад і впиши число куреня в ньому.';
      default:
        return 'Запит не надіслано. Спробуй ще раз.';
    }
  }
}
