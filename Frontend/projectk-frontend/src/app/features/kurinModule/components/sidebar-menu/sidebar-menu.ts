import { Component, inject, OnChanges, SimpleChanges, ChangeDetectionStrategy, model, input, signal } from '@angular/core';
import { DrawerModule } from '@openng/optimus-ui/drawer';
import { ButtonModule } from '@openng/optimus-ui/button';
import { PanelMenuModule } from '@openng/optimus-ui/panelmenu';
import { MenuItem } from '@openng/optimus-ui/api';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { MenuModule } from '@openng/optimus-ui/menu';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { catchError, combineLatest, defer, filter, map, Observable, of, startWith, switchMap, tap } from 'rxjs';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { AsyncPipe } from '@angular/common';
import { environment } from '../../../../../environments/environment';
import { trimTrailingSlashes } from '../../../../shared/functions/trim-trailing-slashes.function';
import { AccountRoleTag, GENERIC_MEMBER_TAG, signedInRoleTag } from '../../functions/account-role-tag.function';
import { AccountSwitcherComponent } from '../../../authModule/components/account-switcher/account-switcher';
import { InstallOnPhoneDialogComponent } from '../../../systemModule/components/install-on-phone-dialog/install-on-phone-dialog';
import { InstallAppService } from '../../../systemModule/services/install-app-service/install-app.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { hasYouthProgram } from '../../models/enums/kurin-branch.enum';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { DuesGroupLinkDto } from '../../../duesModule/models/group-dues.dto';
import { ReportProblemDialogComponent } from '../../../systemModule/components/report-problem-dialog/report-problem-dialog';
import { displayCodeName } from '../../../../shared/functions/release-code-name.function';
import { WaitlistAttentionService } from '../../../adminModule/services/waitlist-attention/waitlist-attention.service';

@Component({
  selector: 'app-sidebar-menu',
  imports: [DrawerModule, ButtonModule, PanelMenuModule, MenuModule, AsyncPipe, RouterLink, ReportProblemDialogComponent, AccountSwitcherComponent, InstallOnPhoneDialogComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './sidebar-menu.html',
})
export class SidebarMenuComponent implements OnChanges {
  private readonly router = inject(Router);
  private readonly permissionService = inject(PermissionService);
  private readonly kurinService = inject(KurinService);
  private readonly waitlistAttention = inject(WaitlistAttentionService);
  private readonly duesService = inject(DuesService);
  private readonly installApp = inject(InstallAppService);
  readonly visible = model(false);
  /** «Повідомити про проблему» lives beside the menu so it can be opened from any page. */
  readonly reportVisible = signal(false);
  readonly installOnPhoneVisible = signal(false);
  readonly state$ = input<Observable<AuthState | null>>(of(null));
  items$: Observable<MenuItem[]> = of([]);
  email$: Observable<string | null> = of(null);
  roleTag$: Observable<AccountRoleTag> = of(GENERIC_MEMBER_TAG);

  kurinKey: string | null = null;

  // e.g. 0.15.0-beta.pre-4 "Liberty Queen Ant" | Self-Host Environment, or just
  // 1.0 | Production Environment for a release published without a code name.
  readonly versionLabel: string = (() => {
    const code = displayCodeName(environment.codeName);
    const quoted = code === null ? '' : ` "${code}"`;
    return `${environment.version}${quoted} | ${environment.envName} Environment`;
  })();

  // defer, so the seed URL is read when something subscribes rather than when this field
  // is initialised — the menu is built before the first navigation settles.
  private readonly currentUrl$: Observable<string> = defer(() => this.router.events.pipe(
    filter(event => event instanceof NavigationEnd),
    map(() => this.router.url),
    startWith(this.router.url)
  ));

  ngOnChanges(changes: SimpleChanges) {
    if (changes['state$']) {
      this.items$ = combineLatest([this.state$(), this.currentUrl$]).pipe(
        // An administrator outside a kurin is the one who decides on applications; the count is
        // re-read on every navigation so the dot goes out right after the decision.
        tap(([state]) => {
          if (this.permissionService.isAdmin() && !state?.kurinKey) {
            this.waitlistAttention.refresh();
          }
        }),
        switchMap(([state, url]) => combineLatest([
          this.youthProgram$(state?.kurinKey ?? null),
          this.waitlistAttention.pending$,
          this.duesGroups$(state?.kurinKey ?? null)
        ]).pipe(
          map(([isYouthKurin, pendingWaitlist, duesGroups]) => this.markCurrent(this.buildItems(state, isYouthKurin, pendingWaitlist, duesGroups), url))
        ))
      );
      this.email$ = this.state$().pipe(
        map(state => state?.email ?? null)
      );
      this.roleTag$ = this.state$().pipe(
        map(state => this.currentRoleTag(state))
      );
    }
  }
  
  /**
   * Flags the item whose routerLink best matches the current URL. PanelMenu only marks
   * headers it expands, and every item here is a leaf, so the current page would never
   * be highlighted without this. The longest match wins, otherwise "/kurin" would light
   * up alongside "/kurin/<key>/settings".
   */
  private markCurrent(items: MenuItem[], url: string): MenuItem[] {
    const path = url.split(/[?#]/)[0];
    let best: MenuItem | null = null;
    let bestLength = 0;

    for (const item of items) {
      const link = this.toPath(item.routerLink);
      if (!link || item.disabled) {
        continue;
      }
      if ((path === link || path.startsWith(`${link}/`)) && link.length > bestLength) {
        best = item;
        bestLength = link.length;
      }
    }

    return items.map(item => item === best
      ? { ...item, styleClass: [item.styleClass, 'lil-menu-item--current'].filter(Boolean).join(' ') }
      : item);
  }

  private toPath(routerLink: unknown): string | null {
    if (typeof routerLink === 'string') {
      return routerLink;
    }
    if (!Array.isArray(routerLink) || routerLink.length === 0) {
      return null;
    }
    return `/${routerLink.map(String).join('/').replace(/^\/+/, '')}`;
  }

  /**
   * Чи веде цей курінь юнацький вишкіл. Курінь читається через кеш, який і так скидається при зміні
   * скоупу, тож перемикання куреня саме собою приносить правильну відповідь.
   */
  private youthProgram$(kurinKey: string | null): Observable<boolean> {
    if (!kurinKey) {
      return of(true);
    }

    return this.kurinService.getByKey(kurinKey).pipe(
      map(kurin => hasYouthProgram(kurin.branch)),
      // Не змогли прочитати курінь — не привід ховати пункт меню, який людина має право бачити.
      catchError(() => of(true))
    );
  }

  /**
   * The гуртки whose box this person keeps, for the «Вкладка гуртка» item. Asked only of those whose
   * grants say they keep one; a failed read hides the item rather than breaking the menu.
   */
  private duesGroups$(kurinKey: string | null): Observable<DuesGroupLinkDto[]> {
    if (!kurinKey || !this.permissionService.canSeeGroupDues()) {
      return of([]);
    }

    return this.duesService.getReadableGroups(kurinKey).pipe(catchError(() => of([])));
  }

  private buildItems(state: AuthState | null, isYouthKurin = true, pendingWaitlist = 0, duesGroups: DuesGroupLinkDto[] = []): MenuItem[] {
    const kurinKey = state?.kurinKey ?? null;
    const memberKey = state?.memberKey ?? null;
    const isAdmin = this.permissionService.isAdmin();

    return [
      ...(memberKey ? this.personalItems(memberKey) : []),
      ...(kurinKey ? this.kurinItems(kurinKey, isYouthKurin, duesGroups) : []),
      ...(isAdmin && !kurinKey ? this.adminItems(pendingWaitlist) : []),
      ...this.commonItems()
    ];
  }

  private navItem(item: MenuItem & { routerLink: unknown[] }): MenuItem {
    return {
      ...item,
      command: () => {
        this.close();
        this.router.navigate(item.routerLink);
      }
    };
  }

  private personalItems(memberKey: string): MenuItem[] {
    return [
      this.navItem({ label: 'Головна', icon: 'pi pi-home', routerLink: ['/'] }),
      this.navItem({ label: 'Мій профіль', icon: 'pi pi-user', routerLink: ['/member', memberKey] })
    ];
  }

  private kurinItems(kurinKey: string, isYouthKurin: boolean, duesGroups: DuesGroupLinkDto[]): MenuItem[] {
    const canSeeRegistry = this.permissionService.isAdmin()
      || this.permissionService.canManageWholeKurin()
      || this.permissionService.canLeadGroups();
    const canManageKurinSettings = this.permissionService.canManageKurinSettings();
    const disabled = !kurinKey;
    const items: MenuItem[] = [
      this.navItem({ label: 'Курінь', icon: 'pi pi-flag', routerLink: ['/kurin'], disabled })
    ];

    // Реєстр — суцільний склад куреня рядками, з контактами. Це інструмент проводу, тож
    // юнакові його не показуємо; сторінка так само закрита capabilityGuard.
    if (canSeeRegistry) {
      items.push(this.navItem({ label: 'Реєстр', icon: 'pi pi-table', routerLink: ['/kurin/registry'], disabled }));
    }

    // Імпорт заводить склад цілого куреня — це дія Зв'язкового, не гурткового.
    if (canManageKurinSettings) {
      items.push(this.navItem({ label: 'Імпорт складу', icon: 'pi pi-upload', routerLink: ['/kurin/import'], disabled }));
    }

    // Календар і Задачі бачить кожен у курені — учасник бачить призначене йому,
    // провід керує. Створення обмежене на рівні сторінки/бекенду (canManageAgenda).
    // Таблицю гуртків бачить кожен у курені; хто ставить бали, вирішує сторінка.
    items.push(
      this.navItem({ label: 'Календар', icon: 'pi pi-calendar', routerLink: ['/calendar', kurinKey] }),
      this.navItem({ label: 'Задачі', icon: 'pi pi-check-square', routerLink: ['/tasks', kurinKey] }),
      this.navItem({ label: 'Планування', icon: 'pi pi-clock', routerLink: ['/planning', kurinKey] }),
      this.navItem({ label: 'Точкування', icon: 'pi pi-trophy', routerLink: ['/kurin', kurinKey, 'score'] })
    );

    // Гуртки та «Всі учасники» ще не реалізовані — повернути сюди, коли зʼявляться
    // сторінки, разом із іконками pi-sitemap і pi-address-book.

    if (this.permissionService.canReviewSkills() && isYouthKurin) {
      items.push(this.navItem({ label: 'Модерація вмілостей', icon: 'pi pi-verified', routerLink: ['/kurin', kurinKey, 'review', 'skills'] }));
    }

    items.push(...this.duesGroupItems(duesGroups));

    if (this.permissionService.canSeeKurinDues()) {
      items.push(this.navItem({ label: 'Вкладка куреня', icon: 'pi pi-wallet', routerLink: ['/kurin', kurinKey, 'dues'] }));
    }

    if (canManageKurinSettings) {
      items.push(this.navItem({ label: 'Налаштування куреня', icon: 'pi pi-cog', routerLink: ['/kurin', kurinKey, 'settings'] }));
    }

    return items;
  }

  /** One гурток — straight to its box; several — a fold with one entry each. */
  private duesGroupItems(duesGroups: DuesGroupLinkDto[]): MenuItem[] {
    if (duesGroups.length === 1) {
      const [group] = duesGroups;
      return [this.navItem({ label: 'Вкладка гуртка', icon: 'pi pi-wallet', routerLink: ['/group', group.groupKey, 'dues'] })];
    }
    if (duesGroups.length > 1) {
      return [{
        label: 'Вкладка гуртків',
        icon: 'pi pi-wallet',
        items: duesGroups.map(group => this.navItem({ label: group.groupName, routerLink: ['/group', group.groupKey, 'dues'] }))
      }];
    }
    return [];
  }

  private adminItems(pendingWaitlist: number): MenuItem[] {
    return [
      this.navItem({
        label: 'Адміністрація',
        icon: 'pi pi-lock',
        routerLink: ['/panel'],
        // The dot, not a number: «there is something to decide» is all the sidebar has to say.
        styleClass: pendingWaitlist > 0 ? 'lil-menu-item--attention' : undefined
      }),
      this.navItem({ label: 'Користувачі', icon: 'pi pi-users', routerLink: ['/users'] }),
      this.navItem({ label: 'Краї', icon: 'pi pi-map', routerLink: ['/krai'] }),
      this.navItem({ label: 'Системні налаштування', icon: 'pi pi-sliders-h', routerLink: ['/system-settings'] })
    ];
  }

  private commonItems(): MenuItem[] {
    return [
      this.navItem({ label: 'Налаштування акаунта', icon: 'pi pi-shield', routerLink: ['/settings/account'] }),
      // The guide is another site (PROJECTK_DOCS_URL is its root), so it opens in a new tab and never
      // takes the «current» mark: markCurrent only knows routerLinks. The welcome page sends people
      // to the root; from inside the app they already know what Лілейка is, so straight to the guide.
      {
        label: 'Довідка',
        icon: 'pi pi-book',
        url: `${trimTrailingSlashes(environment.docsUrl)}/user/start/what-is/`,
        target: '_blank',
        command: () => this.close()
      },
      ...this.installItems(),
      {
        label: 'Повідомити про проблему',
        icon: 'icon-bug',
        command: () => {
          this.close();
          this.reportVisible.set(true);
        }
      },
      this.navItem({ label: 'Про Лілейку', icon: 'pi pi-info-circle', routerLink: ['/about'] })
    ];
  }

  /**
   * The phone app is the PWA under /m/ on this origin: a page load, not a route of this app.
   * Its /install page offers the one-tap install or shows the steps for the phone at hand. A
   * computer gets a code for the phone instead: /m/ there read as «install it on this computer».
   * The demo on the docs site has no phone app next to it.
   */
  private installItems(): MenuItem[] {
    if (!this.installApp.available) {
      return [];
    }
    if (this.installApp.onPhone) {
      return [{ label: 'Встановити на телефон', icon: 'pi pi-mobile', url: '/m/install', command: () => this.close() }];
    }
    return [{
      label: 'Встановити на телефон',
      icon: 'pi pi-mobile',
      command: () => {
        this.close();
        this.installOnPhoneVisible.set(true);
      }
    }];
  }

  close() {
    this.visible.set(false);
  }

  /**
   * What the viewer is called in the footer: the office first, so a Зв'язковий reads "Зв'язковий"
   * rather than the tier "Провід куреня", coloured by the member list's rule. The tiers are the
   * fallback for accounts that hold no office.
   */
  private currentRoleTag(state: AuthState | null): AccountRoleTag {
    return signedInRoleTag(this.permissionService, state?.roles ?? []);
  }
}
