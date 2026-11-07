import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { KurinService } from '../services/kurin-service/kurin.service';
import { KurinDto } from '../models/kurin.dto';

/** Можливості, які курінь вмикає й вимикає в налаштуваннях. */
export type KurinFeature = 'score';

const IS_ON: Record<KurinFeature, (kurin: KurinDto) => boolean> = {
  score: kurin => kurin.scoreEnabled ?? true
};

/**
 * Сторінки можливості, яку курінь вимкнув, недосяжні й за прямим посиланням: воно веде на «Тут такого
 * немає», а дані лишаються, поки курінь не ввімкне її знову.
 *
 * Курінь не прочитався — пропускаємо: сторінка й сама спитає сервер.
 */
export const kurinFeatureGuard = (feature: KurinFeature, kurinKeyParam = 'kurinKey'): CanActivateFn => {
  return route => {
    const kurinService = inject(KurinService);
    const router = inject(Router);
    const kurinKey = route.paramMap.get(kurinKeyParam);
    if (!kurinKey) {
      return true;
    }

    return kurinService.getByKey(kurinKey).pipe(
      map(kurin => IS_ON[feature](kurin) ? true : router.createUrlTree(['/feature-off', feature])),
      catchError(() => of(true))
    );
  };
};
