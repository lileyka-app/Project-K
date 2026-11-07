import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { PopoverModule } from '@openng/optimus-ui/popover';
import { TagModule } from '@openng/optimus-ui/tag';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { MessageService } from '@openng/optimus-ui/api';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinScopeOption } from '../../models/kurin-scope-option.model';
import { membershipKindLabel } from '../../models/enums/membership-kind.enum';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { kurinHeadline, kurinLabel, kurinName, kurinSubline } from '../../../../shared/functions/kurin-label.function';
import { isSeniorBranch } from '../../models/enums/kurin-branch.enum';

/**
 * Вибір куреня, в якому людина зараз діє. Зʼявляється лише тим, хто справді належить до кількох:
 * для решти вибирати нема з чого, і кнопка була б питанням без відповіді.
 *
 * Права залежать від куреня, тож перемикання — це не фільтр списку, а новий токен. Поки сервер його
 * не видав, нічого не міняється: скоуп у клеймі, а не в памʼяті вкладки.
 */
@Component({
  selector: 'app-kurin-switcher',
  imports: [ButtonModule, PopoverModule, TagModule, TooltipModule],
  templateUrl: './kurin-switcher.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './kurin-switcher.css'
})
export class KurinSwitcherComponent implements OnInit {
  protected readonly kurinLabel = kurinLabel;
  private readonly authService = inject(AuthService);
  private readonly messageService = inject(MessageService);
  private readonly router = inject(Router);

  readonly options = signal<KurinScopeOption[]>([]);
  readonly switchingTo = signal<string | null>(null);

  protected headline(option: KurinScopeOption): string {
    return kurinHeadline(this.naming(option));
  }

  protected subline(option: KurinScopeOption): string {
    return kurinSubline(this.naming(option));
  }

  protected isSenior(option: KurinScopeOption): boolean {
    return isSeniorBranch(option.branch);
  }

  private naming(option: KurinScopeOption) {
    return { number: option.kurinNumber, branch: option.branch, branches: option.branches, namedAfter: option.namedAfter };
  }
  readonly kindLabel = membershipKindLabel;

  ngOnInit(): void {
    this.authService.getKurinScopeOptions().subscribe({
      next: options => this.options.set(options),
      // Мовчки: перемикач — зручність, і його відсутність не має ламати тулбар.
      error: () => this.options.set([])
    });
  }

  get isVisible(): boolean {
    return this.options().length > 1;
  }

  get current(): KurinScopeOption | null {
    const kurinKey = this.authService.getAuthStateValue()?.kurinKey ?? null;
    return this.options().find(option => option.kurinKey === kurinKey) ?? null;
  }

  get currentLabel(): string {
    const current = this.current;
    if (!current) return 'Оберіть курінь';
    return isSeniorBranch(current.branch) ? kurinHeadline(this.naming(current)) : kurinLabel(current.kurinNumber, current.branch);
  }

  isCurrent(option: KurinScopeOption): boolean {
    return option.kurinKey === this.current?.kurinKey;
  }

  switchTo(option: KurinScopeOption, popover: { hide: () => void }): void {
    popover.hide();
    if (this.isCurrent(option) || this.switchingTo()) {
      return;
    }

    this.switchingTo.set(option.kurinKey);
    this.authService.setKurinScope(option.kurinKey).subscribe({
      next: () => {
        this.switchingTo.set(null);
        // Сторінка під нами належала попередньому куреню — його ключі тут уже нічого не відкриють.
        this.router.navigate(['/kurin']);
      },
      error: (error: unknown) => {
        this.switchingTo.set(null);
        this.messageService.add({
          severity: 'error',
          summary: 'Не вдалося перейти',
          detail: failureDetail(error, `${kurinName(option.kurinNumber, option.branch)} лишився недосяжним. Спробуй ще раз.`)
        });
      }
    });
  }
}
