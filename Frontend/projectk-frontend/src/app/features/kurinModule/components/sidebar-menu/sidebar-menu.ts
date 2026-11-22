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
import { KurinBranch, hasYouthProgram } from '../../models/enums/kurin-branch.enum';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { DuesGroupLinkDto } from '../../../duesModule/models/group-dues.dto';
import { ReportProblemDialogComponent } from '../../../systemModule/components/report-problem-dialog/report-problem-dialog';
import { displayCodeName } from '../../../../shared/functions/release-code-name.function';
import { WaitlistAttentionService } from '../../../adminModule/services/waitlist-attention/waitlist-attention.service';
import { MembershipService } from '../../services/membership-service/membership.service';
import { RegistryModes } from '../../pages/registry/registry-modes';

/** Чим курінь користується — від цього залежать пункти меню. */
interface KurinMenuFeatures {
  youth: boolean;
  score: boolean;
  branch: KurinBranch | null;
}

const ALL_FEATURES: KurinMenuFeatures = { youth: true, score: true, branch: null };

/** Marks the first item of a block so the menu draws a line above it. */
function startsGroup(block: MenuItem[]): MenuItem[] {
  const [first, ...rest] = block;
  return [{ ...first, styleClass: [first.styleClass, 'lil-menu-item--group-start'].filter(Boolean).join(' ') }, ...rest];
}

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
  private readonly memberships = inject(MembershipService);
  private readonly installApp = inject(InstallAppService);
  private readonly registryModes = inject(RegistryModes);
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
          this.features$(state?.kurinKey ?? null),
          this.waitlistAttention.pending$,
          this.duesGroups$(state?.kurinKey ?? null),
          this.requestsWaiting$(state?.kurinKey ?? null)
        ]).pipe(
          map(([features, pendingWaitlist, duesGroups, requestsWaiting]) =>
            this.markCurrent(this.buildItems(state, features, pendingWaitlist, duesGroups, requestsWaiting), url))
        ))
      );
      this.email$ = this.state$().pipe(
        map(state => state?.email ?? null)
      );
      // The member's word waits on the kurin's гілка: «Учасник» first, «Член куреня» once a senior kurin is read.
      this.roleTag$ = this.state$().pipe(
        switchMap(state => this.features$(state?.kurinKey ?? null).pipe(
          startWith(ALL_FEATURES),
          map(features => this.currentRoleTag(state, features.branch))
        ))
      );
    }
  }
  
  /**
   * Flags the item whose routerLink best matches the current URL. PanelMenu only marks
   * headers it expands, and every item here is a leaf, so the current page would never
   * be highlighted without this. The longest match wins, otherwise "/kurin" would light
   * up alongside "/kurin/<key>/settings". An item that stands for a page of tabs also answers to
   * its other tabs' addresses (`state.also`).
   */
  private markCurrent(items: MenuItem[], url: string): MenuItem[] {
    const path = url.split(/[?#]/)[0];
    let best: MenuItem | null = null;
    let bestLength = 0;

    for (const item of items) {
      const own = this.toPath(item.routerLink);
      if (!own || item.disabled) {
        continue;
      }
      for (const link of [own, ...((item.state?.['also'] as string[] | undefined) ?? [])]) {
        if ((path === link || path.startsWith(`${link}/`)) && link.length > bestLength) {
          best = item;
          bestLength = link.length;
        }
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
   * Чим користується цей курінь: юнацький вишкіл (за гілкою) і точкування (перемикач у налаштуваннях).
   * Курінь читається через кеш, який і так скидається при зміні скоупу, тож перемикання куреня саме
   * собою приносить правильну відповідь.
   */
  private features$(kurinKey: string | null): Observable<KurinMenuFeatures> {
    if (!kurinKey) {
      return of(ALL_FEATURES);
    }

    return this.kurinService.getByKey(kurinKey).pipe(
      map(kurin => ({ youth: hasYouthProgram(kurin.branch), score: kurin.scoreEnabled ?? true, branch: kurin.branch ?? null })),
      // Не змогли прочитати курінь — не привід ховати пункт меню, який людина має право бачити.
      catchError(() => of(ALL_FEATURES))
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

  /**
   * Скільки запитів від інших куренів чекає відповіді Звʼязкового. Перечитується з кожним переходом,
   * тож крапка гасне одразу після відповіді.
   */
  private requestsWaiting$(kurinKey: string | null): Observable<number> {
    if (!kurinKey || !this.permissionService.canManageWholeKurin()) {
      return of(0);
    }

    return this.memberships.waitingRequests(kurinKey).pipe(catchError(() => of(0)));
  }

  private buildItems(state: AuthState | null, features: KurinMenuFeatures = ALL_FEATURES, pendingWaitlist = 0, duesGroups: DuesGroupLinkDto[] = [], requestsWaiting = 0): MenuItem[] {
    const kurinKey = state?.kurinKey ?? null;
    const memberKey = state?.memberKey ?? null;
    const isAdmin = this.permissionService.isAdmin();

    const blocks = [
      memberKey ? this.personalItems(memberKey) : [],
      ...(kurinKey ? this.kurinBlocks(kurinKey, features, duesGroups, requestsWaiting) : []),
      isAdmin && !kurinKey ? this.adminItems(pendingWaitlist) : [],
      this.commonItems(kurinKey)
    ].filter(block => block.length > 0);

    return blocks.flatMap((block, index) => index === 0 ? block : startsGroup(block));
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

  /**
   * The kurin in two blocks: who is in it and what it keeps (курінь, реєстр, вкладка, журнал), then
   * what it does (календар, задачі, точкування, вмілості).
   */
  private kurinBlocks(kurinKey: string, features: KurinMenuFeatures, duesGroups: DuesGroupLinkDto[], requestsWaiting: number): MenuItem[][] {
    const isYouthKurin = features.youth;
    const disabled = !kurinKey;
    const items: MenuItem[] = [
      this.navItem({ label: 'Курінь', icon: 'pi pi-flag', routerLink: ['/kurin'], disabled })
    ];

    // «Реєстр» — склад, переходи, архів, імпорт на одній сторінці. Пункт є, коли людина має бодай
    // один режим; крапка — є запит від іншого куреня, на який чекають відповіді.
    if (this.registryModes.available().length) {
      items.push(this.navItem({
        label: 'Реєстр',
        icon: 'pi pi-table',
        routerLink: ['/kurin/registry'],
        disabled,
        styleClass: requestsWaiting > 0 ? 'lil-menu-item--attention' : undefined
      }));
    }

    items.push(...this.duesItem(kurinKey, duesGroups));

    if (this.permissionService.canReadJournal()) {
      items.push(this.navItem({ label: 'Журнал дій', icon: 'pi pi-history', routerLink: ['/kurin', kurinKey, 'journal'] }));
    }

    // Календар і Задачі бачить кожен у курені — учасник бачить призначене йому,
    // провід керує. Створення обмежене на рівні сторінки/бекенду (canManageAgenda).
    // Таблицю гуртків бачить кожен у курені; хто ставить бали, вирішує сторінка.
    // Календар і Планування — одна сторінка з двома вкладками (PageGroups.calendar).
    const activity: MenuItem[] = [];
    activity.push(
      this.navItem({ label: 'Календар', icon: 'pi pi-calendar', routerLink: ['/calendar', kurinKey], state: { also: [`/planning/${kurinKey}`] } }),
      this.navItem({ label: 'Задачі', icon: 'pi pi-check-square', routerLink: ['/tasks', kurinKey] })
    );
    if (features.score) {
      activity.push(this.navItem({ label: 'Точкування', icon: 'pi pi-trophy', routerLink: ['/kurin', kurinKey, 'score'] }));
    }

    // Гуртки та «Всі учасники» ще не реалізовані — повернути сюди, коли зʼявляться
    // сторінки, разом із іконками pi-sitemap і pi-address-book.

    if (this.permissionService.canReviewSkills() && isYouthKurin) {
      activity.push(this.navItem({ label: 'Модерація вмілостей', icon: 'pi pi-verified', routerLink: ['/kurin', kurinKey, 'review', 'skills'] }));
    }

    return [items, activity];
  }

  /**
   * «Вкладка»: the kurin's box and the гуртків' are one page of tabs (PageGroups.dues). The item
   * opens the kurin's where the person keeps it, otherwise their first гурток's.
   */
  private duesItem(kurinKey: string, duesGroups: DuesGroupLinkDto[]): MenuItem[] {
    const links = [
      ...(this.permissionService.canSeeKurinDues() ? [`/kurin/${kurinKey}/dues`] : []),
      ...duesGroups.map(group => `/group/${group.groupKey}/dues`)
    ];
    if (links.length === 0) {
      return [];
    }

    const [first, ...also] = links;
    return [this.navItem({ label: 'Вкладка', icon: 'pi pi-wallet', routerLink: [first], state: { also } })];
  }

  private adminItems(pendingWaitlist: number): MenuItem[] {
    return [
      this.navItem({ label: 'Адміністрація', icon: 'pi pi-lock', routerLink: ['/panel'] }),
      this.navItem({
        label: 'Заявки',
        icon: 'pi pi-inbox',
        routerLink: ['/waitlist'],
        // The dot, not a number: «there is something to decide» is all the sidebar has to say.
        styleClass: pendingWaitlist > 0 ? 'lil-menu-item--attention' : undefined
      }),
      this.navItem({ label: 'Користувачі', icon: 'pi pi-users', routerLink: ['/users'] }),
      this.navItem({ label: 'Краї', icon: 'pi pi-map', routerLink: ['/krai'] }),
      this.navItem({ label: 'Системні налаштування', icon: 'pi pi-sliders-h', routerLink: ['/system-settings'] })
    ];
  }

  /**
   * «Налаштування»: the kurin's and the account's are one page of tabs (PageGroups.settings). The
   * item opens the kurin's for whoever may read them, the account's for everyone else.
   */
  private settingsItem(kurinKey: string | null): MenuItem {
    const kurinSettings = kurinKey && this.permissionService.canReadKurinSettings() ? `/kurin/${kurinKey}/settings` : null;
    return this.navItem({
      label: 'Налаштування',
      icon: 'pi pi-cog',
      routerLink: [kurinSettings ?? '/settings/account'],
      state: { also: kurinSettings ? ['/settings/account'] : [] }
    });
  }

  private commonItems(kurinKey: string | null): MenuItem[] {
    return [
      this.settingsItem(kurinKey),
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
  private currentRoleTag(state: AuthState | null, branch: KurinBranch | null): AccountRoleTag {
    return signedInRoleTag(this.permissionService, state?.roles ?? [], branch);
  }
}
