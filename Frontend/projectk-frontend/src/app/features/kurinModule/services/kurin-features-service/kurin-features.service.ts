import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinService } from '../kurin-service/kurin.service';
import { hasGroups, isSeniorBranch } from '../../models/enums/kurin-branch.enum';
import { actInKurin } from '../../functions/leadership-role-display.function';

/**
 * Чим користується курінь, у якому людина діє зараз: є в ньому гуртки чи ні, ведеться точкування.
 * Для компонентів, яким курінь не передають, а слова й блоки залежать від нього. Курінь не
 * прочитався — вважаємо, що все є: краще зайвий блок, ніж зниклий.
 */
@Injectable({ providedIn: 'root' })
export class KurinFeaturesService {
  private readonly auth = inject(AuthService);
  private readonly kurins = inject(KurinService);

  private readonly kurin = toSignal(
    this.auth.getAuthState().pipe(
      map(state => state?.kurinKey ?? null),
      distinctUntilChanged(),
      // Role names read from now on are this kurin's; KurinService remembers them as it reads it.
      tap(key => actInKurin(key)),
      switchMap(key => key ? this.kurins.getByKey(key).pipe(catchError(() => of(null))) : of(null))
    ),
    { initialValue: null }
  );

  /** The branch of the kurin the person acts in, for wording that differs by branch. */
  readonly branch = computed(() => this.kurin()?.branch ?? null);

  /** Гуртки є в усіх куренях, крім старших. */
  readonly hasGroups = computed(() => hasGroups(this.kurin()?.branch));

  readonly isSenior = computed(() => isSeniorBranch(this.kurin()?.branch));
}
