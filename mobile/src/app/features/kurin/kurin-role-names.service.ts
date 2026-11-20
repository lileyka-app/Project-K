import { Injectable, effect, inject } from '@angular/core';
import { AuthService } from '../../auth/auth.service';
import { KurinApi } from './kurin.service';
import { actInKurin } from './kurin.labels';

/**
 * Follows the kurin the person acts in, from the first screen on: makes it the current one for role
 * names and reads it once, so a kurin's own names («Скриптор») are there wherever a role is shown,
 * not only after the kurin tab was opened.
 */
@Injectable({ providedIn: 'root' })
export class KurinRoleNames {
  private readonly auth = inject(AuthService);
  private readonly kurins = inject(KurinApi);

  constructor() {
    effect(() => {
      const kurinKey = this.auth.user()?.kurinKey ?? null;
      actInKurin(kurinKey);
      if (kurinKey) {
        // A failed read leaves the usual names; the kurin tab reads it again anyway.
        this.kurins.kurin(kurinKey).catch(() => undefined);
      }
    });
  }
}
