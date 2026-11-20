import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { PageTab } from '../../../../shared/page-tabs/page-tabs';

/**
 * Pages that are one place in the menu and tabs at the top, like the «Реєстр»: Календар and
 * Планування; the kurin's вкладка and its гуртки's; the kurin's settings and the account's. The menu
 * and every page of a group ask here, so a tab is offered only where its page would open.
 */
@Injectable({ providedIn: 'root' })
export class PageGroups {
  private readonly permissions = inject(PermissionService);
  private readonly duesService = inject(DuesService);

  calendar(kurinKey: string): PageTab[] {
    return [
      { label: 'Календар', link: `/calendar/${kurinKey}`, testId: 'tab-calendar' },
      { label: 'Планування', link: `/planning/${kurinKey}`, testId: 'tab-planning' }
    ];
  }

  /** The kurin's table for everyone, the КВ's book and the settings for who may open them; the period goes along. */
  score(kurinKey: string): PageTab[] {
    const base = `/kurin/${kurinKey}/score`;
    return [
      { label: 'Таблиця', link: base, exact: true, carriesQuery: true, testId: 'tab-score-table' },
      ...(this.permissions.canSeePrivateScore()
        ? [{ label: 'Книга КВ', link: `${base}/private`, exact: true, carriesQuery: true, testId: 'tab-score-private' }]
        : []),
      ...(this.permissions.canManageScore()
        ? [{ label: 'Налаштування', link: `${base}/settings`, exact: true, testId: 'tab-score-settings' }]
        : [])
    ];
  }

  /** The kurin's box for who keeps it, then each гурток's box the person keeps, by name. */
  dues(kurinKey: string): Observable<PageTab[]> {
    const kurin: PageTab[] = this.permissions.canSeeKurinDues()
      ? [{ label: 'Курінь', link: `/kurin/${kurinKey}/dues`, testId: 'tab-dues-kurin' }]
      : [];
    if (!this.permissions.canSeeGroupDues()) {
      return of(kurin);
    }

    return this.duesService.getReadableGroups(kurinKey).pipe(
      map(groups => [
        ...kurin,
        ...groups.map(group => ({ label: group.groupName, link: `/group/${group.groupKey}/dues`, testId: `tab-dues-${group.groupKey}` }))
      ]),
      catchError(() => of(kurin))
    );
  }

  /** The kurin's settings for who may read them, then the account's, which everyone has. */
  settings(kurinKey: string | null): PageTab[] {
    return [
      ...(kurinKey && this.permissions.canReadKurinSettings()
        ? [{ label: 'Курінь', link: `/kurin/${kurinKey}/settings`, testId: 'tab-settings-kurin' }]
        : []),
      { label: 'Акаунт', link: '/settings/account', testId: 'tab-settings-account' }
    ];
  }
}
