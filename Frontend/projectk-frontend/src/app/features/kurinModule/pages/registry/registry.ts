import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, filter, of } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { MembershipService } from '../../services/membership-service/membership.service';
import { KURIN_BRANCH_LABELS, KurinBranch } from '../../models/enums/kurin-branch.enum';
import { RegistryModeLink, RegistryModes } from './registry-modes';

/**
 * «Реєстр» — the kurin's people, one page: склад, переходи, архів, імпорт. This is the frame: the
 * kurin's line, the modes the person has and the one open now. Each mode is its own route, so a
 * mode can be linked to, and leaving one ends what it holds — the archive's danger zone with it.
 */
@Component({
  selector: 'app-registry',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './registry.html',
  styleUrl: './registry.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegistryComponent {
  private readonly auth = inject(AuthService);
  private readonly kurins = inject(KurinService);
  private readonly memberships = inject(MembershipService);
  private readonly registryModes = inject(RegistryModes);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly modes = signal<RegistryModeLink[]>([]);
  readonly waiting = signal(0);
  private readonly kurinNumber = signal<number | null>(null);
  private readonly branch = signal<KurinBranch | null>(null);
  private readonly kurinLoaded = signal(false);

  readonly kurinLine = computed(() => {
    if (!this.kurinLoaded()) {
      return '';
    }
    const number = this.kurinNumber();
    const branch = this.branch();
    const name = number ? `${number} курінь` : 'Підготовчий курінь';
    return branch ? `${name} · ${KURIN_BRANCH_LABELS[branch]}` : name;
  });

  constructor() {
    let kurinKey: string | null = null;
    this.auth.getAuthState().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(state => {
      this.modes.set(this.registryModes.available());
      if (!state?.kurinKey || state.kurinKey === kurinKey) {
        return;
      }

      kurinKey = state.kurinKey;
      this.loadKurin(kurinKey);
      this.loadWaiting(kurinKey);
    });

    // The waiting count goes out once a request is answered; re-read it whenever the mode changes.
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.modes.set(this.registryModes.available());
      if (kurinKey) {
        this.loadWaiting(kurinKey);
      }
    });
  }

  private loadKurin(kurinKey: string): void {
    this.kurins.getByKey(kurinKey).subscribe({
      next: kurin => {
        this.kurinNumber.set(kurin.number);
        this.branch.set(kurin.branch ?? KurinBranch.UPYu);
        this.kurinLoaded.set(true);
        // Import depends on the branch, which is known only now.
        this.modes.set(this.registryModes.available());
      },
      error: () => this.kurinLoaded.set(false)
    });
  }

  private loadWaiting(kurinKey: string): void {
    if (!this.registryModes.allows('transfers')) {
      this.waiting.set(0);
      return;
    }
    this.memberships.waitingRequests(kurinKey).pipe(catchError(() => of(0))).subscribe(count => this.waiting.set(count));
  }
}
