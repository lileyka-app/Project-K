import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import {
  ActionSheetController,
  IonAvatar,
  IonBadge,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonNote,
  IonSpinner,
  IonToggle,
  MenuController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  book,
  bug,
  calendar,
  checkbox,
  cloudUpload,
  colorPalette,
  desktop,
  eyeOff,
  flag,
  grid,
  home,
  idCard,
  informationCircle,
  key,
  notifications,
  openOutline,
  options,
  people,
  person,
  personAdd,
  ribbon,
  settings,
  shieldCheckmark,
  time,
  trophy,
  wallet,
} from 'ionicons/icons';
import { filter, map } from 'rxjs';
import { SavedAccount } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { accountRoleTag, kurinShortLabel } from '../features/account/account.labels';
import { AppearanceService } from '../features/account/appearance.service';
import { fullVersionUrl, helpUrl, openExternal, webOnlyPage } from '../features/account/web-links';
import { hasYouthProgram, kurinAccess } from '../features/kurin/kurin.labels';
import { KurinApi } from '../features/kurin/kurin.service';
import { WaitlistApi } from '../features/admin/waitlist.service';
import { initials } from '../me/labels';
import { MemberDto } from '../me/me.models';
import { MeService } from '../me/me.service';
import { PUSH_HINTS } from '../pwa/push';
import { PushService } from '../pwa/push.service';
import { InstallService } from '../pwa/install.service';
import { Capacitor } from '@capacitor/core';
import { apiErrorText } from '../core/api';
import { Toasts } from '../core/toast';
import { KurinScopes } from './kurin-scopes.service';
import { MenuEntry, currentEntry, menuEntries } from './menu-items';

const THEME_LABELS = { system: 'Системна', light: 'Світла', dark: 'Темна' } as const;

/**
 * The web's sidebar as a list: the person on top (the web's «Мій профіль» and its footer with the
 * email), then the sidebar's items in its order, then what only the phone has (the look, the
 * kurins, privacy) and the sign-out. The «Меню» tab shows it, and so does the ☰ drawer.
 */
@Component({
  selector: 'app-nav-menu',
  imports: [IonList, IonItem, IonItemGroup, IonLabel, IonNote, IonAvatar, IonBadge, IonIcon, IonSpinner, IonToggle],
  styles: `
    .account ion-avatar {
      width: 60px;
      height: 60px;
      margin: 12px 16px 12px 0;
      background: var(--lk-primary);
      color: var(--lk-on-primary);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 24px;
      font-weight: 700;
    }
    .account h2 {
      font-size: 20px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    /* The other accounts sit under the signed-in one, a size smaller. */
    .account ion-avatar.other {
      width: 40px;
      height: 40px;
      margin: 8px 16px 8px 10px;
      font-size: 16px;
    }
    .account ion-label.saved h3,
    .account ion-label.saved p {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .account h3 {
      font-size: 16px;
      font-weight: 600;
      color: var(--lk-ink);
    }
    .current {
      --background: var(--lk-primary-50);
      font-weight: 600;
    }
    .external {
      font-size: 18px;
      color: var(--lk-faint);
    }
    /* The web's dot on the admin entry: something waits for a decision, not how much. */
    .attention {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--lk-danger);
      margin-inline-start: 8px;
    }
    .sign-out {
      text-align: center;
    }
  `,
  template: `
    @if (user(); as me) {
      <ion-list [inset]="true" class="account">
        <ion-item-group>
          <ion-item
            [button]="true"
            [detail]="true"
            [class.current]="current() === 'profile'"
            (click)="open('/tabs/more/profile')"
            data-testid="account"
          >
            <ion-avatar slot="start" aria-hidden="true">
              @if (member()?.profilePhotoUrl; as photo) {
                <img [src]="photo" alt="" />
              } @else {
                {{ monogram() }}
              }
            </ion-avatar>
            <ion-label class="ion-text-wrap">
              <h2>{{ name() || me.email }}</h2>
              <p>{{ name() ? 'Мій профіль, ' + me.email : 'Мій профіль' }}</p>
            </ion-label>
          </ion-item>
          @for (row of otherAccounts(); track row.account.userKey) {
            <ion-item
              [button]="true"
              [detail]="false"
              [disabled]="switching() !== null"
              (click)="switchTo(row.account)"
              [attr.aria-label]="'Перейти в акаунт ' + row.account.email"
              data-testid="saved-account"
            >
              <ion-avatar slot="start" class="other" aria-hidden="true">
                @if (row.account.profilePhotoUrl; as photo) {
                  <img [src]="photo" alt="" />
                } @else {
                  {{ row.monogram }}
                }
              </ion-avatar>
              <!-- One line each, cut short: a long address wrapped mid-word beside the role tag. -->
              <ion-label class="saved">
                <h3>{{ row.title }}</h3>
                @if (row.subtitle) {
                  <p>{{ row.subtitle }}</p>
                }
              </ion-label>
              @if (switching() === row.account.userKey) {
                <ion-spinner slot="end" name="crescent" />
              } @else {
                <ion-badge slot="end" [class]="'lk-tag--' + row.role.tone">{{ row.role.label }}</ion-badge>
              }
            </ion-item>
          }
          @if (me.memberKey) {
            <ion-item [button]="true" [detail]="true" (click)="openCard('check')" data-testid="show-card">
              <ion-icon class="lk-tile" slot="start" name="qr-code" aria-hidden="true" style="--lk-tile: #0e6e4e" />
              <ion-label>Показати картку</ion-label>
            </ion-item>
            <ion-item [button]="true" [detail]="true" (click)="openCard('transfer')" data-testid="transfer-code">
              <ion-icon class="lk-tile" slot="start" name="id-card" aria-hidden="true" style="--lk-tile: #8e8e93" />
              <ion-label>Мій код для переходу</ion-label>
            </ion-item>
          }
          <ion-item [button]="true" [detail]="true" (click)="openScan()" data-testid="scan">
            <ion-icon class="lk-tile" slot="start" name="scan" aria-hidden="true" style="--lk-tile: #5e5ce6" />
            <ion-label>Сканувати QR</ion-label>
          </ion-item>
          <ion-item [button]="true" [detail]="false" (click)="addAccount()" data-testid="add-account">
            <ion-icon class="lk-tile" slot="start" name="person-add" aria-hidden="true" style="--lk-tile: #8e8e93" />
            <ion-label>Додати акаунт</ion-label>
          </ion-item>
        </ion-item-group>
      </ion-list>
    }

    <ion-list [inset]="true" data-testid="web-menu">
      <ion-item-group>
        @for (entry of entries(); track entry.key) {
          <ion-item
            [button]="true"
            [detail]="!!entry.link"
            [class.current]="current() === entry.key"
            (click)="go(entry)"
            [attr.data-testid]="'menu-' + entry.key"
          >
            <ion-icon class="lk-tile" slot="start" [name]="entry.icon" aria-hidden="true" [style.--lk-tile]="entry.tile" />
            <ion-label>{{ entry.label }}</ion-label>
            @if (entry.key === 'waitlist' && waitlist.pending() > 0) {
              <span class="attention" slot="end" role="img" aria-label="Є заявки, що чекають рішення" data-testid="waitlist-dot"></span>
            }
            @if (!entry.link) {
              <ion-icon class="external" slot="end" name="open-outline" aria-hidden="true" />
            }
          </ion-item>
        }
      </ion-item-group>
    </ion-list>

    <ion-list [inset]="true">
      <ion-item-group>
        <ion-item [button]="true" [detail]="true" [class.current]="current() === 'appearance'" (click)="open('/tabs/more/appearance')">
          <ion-icon class="lk-tile" slot="start" name="color-palette" aria-hidden="true" style="--lk-tile: #5856d6" />
          <ion-label>Вигляд</ion-label>
          <ion-note slot="end">{{ themeLabel() }}</ion-note>
        </ion-item>
        @if (push.available()) {
          <ion-item data-testid="push-row" [attr.data-state]="push.state()">
            <ion-icon class="lk-tile" slot="start" name="notifications" aria-hidden="true" style="--lk-tile: #ff3b30" />
            @if (pushHint(); as hint) {
              <ion-label class="ion-text-wrap">
                Сповіщення на телефон
                <p data-testid="push-hint">{{ hint }}</p>
              </ion-label>
            } @else {
              <ion-toggle
                justify="space-between"
                [checked]="push.state() === 'on'"
                [disabled]="push.busy() || push.state() === 'loading'"
                (ionChange)="togglePush($event)"
                data-testid="push-toggle"
                >Сповіщення на телефон</ion-toggle
              >
            }
          </ion-item>
        }
        @if (scopes.options().length > 1) {
          <ion-item
            [button]="true"
            [detail]="true"
            [class.current]="current() === 'kurins'"
            (click)="open('/tabs/more/kurins')"
            data-testid="kurins-row"
          >
            <ion-icon class="lk-tile" slot="start" name="flag" aria-hidden="true" style="--lk-tile: #ff9500" />
            <ion-label>Мої курені</ion-label>
            <ion-note slot="end">{{ currentKurin() }}</ion-note>
          </ion-item>
        }
        <ion-item [button]="true" [detail]="true" [class.current]="current() === 'privacy'" (click)="open('/tabs/more/privacy')">
          <ion-icon class="lk-tile" slot="start" name="eye-off" aria-hidden="true" style="--lk-tile: #30b0c7" />
          <ion-label>Конфіденційність</ion-label>
        </ion-item>
        @if (!native) {
          <ion-item [button]="true" [detail]="false" (click)="openFullVersion()" data-testid="full-version">
            <ion-icon class="lk-tile" slot="start" name="desktop" aria-hidden="true" style="--lk-tile: #8e8e93" />
            <ion-label>Повна версія</ion-label>
            <ion-icon class="external" slot="end" name="open-outline" aria-hidden="true" />
          </ion-item>
        }
      </ion-item-group>
    </ion-list>

    @if (user()) {
      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item [button]="true" [detail]="false" [disabled]="leaving()" (click)="confirmLogout()" data-testid="sign-out">
            <ion-label class="sign-out" color="danger">Вийти</ion-label>
          </ion-item>
        </ion-item-group>
      </ion-list>
    }
  `,
})
export class NavMenu implements OnInit {
  /** In the ☰ drawer: a tap also closes it. */
  readonly drawer = input(false);

  private readonly auth = inject(AuthService);
  private readonly me = inject(MeService);
  private readonly kurinApi = inject(KurinApi);
  protected readonly waitlist = inject(WaitlistApi);
  private readonly router = inject(Router);
  private readonly menus = inject(MenuController);
  private readonly sheets = inject(ActionSheetController);
  private readonly appearance = inject(AppearanceService);
  private readonly toasts = inject(Toasts);
  protected readonly push = inject(PushService);
  private readonly install = inject(InstallService);
  /** The native shells are not on the web's origin: no «Повна версія» there. */
  protected readonly native = Capacitor.isNativePlatform();
  protected readonly scopes = inject(KurinScopes);
  protected readonly user = this.auth.user;
  protected readonly member = signal<MemberDto | null>(null);
  private readonly duesGroups = signal<{ groupKey: string; groupName: string }[]>([]);
  protected readonly leaving = signal(false);
  private readonly savedAccounts = signal<SavedAccount[]>([]);
  /** The account being switched to, while the server answers. */
  protected readonly switching = signal<string | null>(null);
  protected readonly otherAccounts = computed(() =>
    this.savedAccounts().map((account) => {
      const name = account.fullName.trim();
      const [first, ...rest] = name.split(' ');
      return {
        account,
        title: name || account.email,
        subtitle: name ? account.email : null,
        role: accountRoleTag(account),
        monogram: name ? initials(first, rest.join(' ')) : account.email.charAt(0).toUpperCase(),
      };
    }),
  );

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly entries = computed(() =>
    menuEntries(this.user(), {
      // Unknown until the kurins come: the web shows the item rather than hide one the person may use.
      youthKurin: hasYouthProgram(this.scopes.current()?.branch ?? null),
      duesGroups: this.duesGroups(),
    }).filter((entry) => entry.key !== 'profile'),
  );
  protected readonly current = computed(() => {
    const url = this.url();
    const own = ['appearance', 'kurins', 'privacy'].find((page) => url.startsWith(`/tabs/more/${page}`));
    if (own) return own;
    if (url.startsWith('/tabs/more/profile')) return 'profile';
    return currentEntry(this.entries(), url);
  });
  protected readonly name = computed(() => {
    const m = this.member();
    return m ? `${m.firstName} ${m.lastName}`.trim() : '';
  });
  protected readonly monogram = computed(() => {
    const m = this.member();
    return m ? initials(m.firstName, m.lastName) : (this.user()?.email.charAt(0).toUpperCase() ?? '');
  });
  protected readonly currentKurin = computed(() => {
    const current = this.scopes.current();
    return current ? kurinShortLabel(current) : '';
  });
  protected readonly themeLabel = computed(() => THEME_LABELS[this.appearance.choice()]);
  protected readonly pushHint = computed(() => PUSH_HINTS[this.push.state()] ?? '');

  constructor() {
    addIcons({
      book,
      bug,
      calendar,
      checkbox,
      cloudUpload,
      colorPalette,
      desktop,
      eyeOff,
      flag,
      grid,
      home,
      idCard,
      informationCircle,
      key,
      notifications,
      openOutline,
      options,
      people,
      person,
      personAdd,
      ribbon,
      settings,
      shieldCheckmark,
      time,
      trophy,
      wallet,
    });
  }

  ngOnInit(): void {
    const user = this.user();
    if (!user) return;
    void this.scopes.load();
    void this.push.load();
    this.auth.savedAccounts().then(
      (accounts) => this.savedAccounts.set(accounts),
      () => undefined, // The switcher shows only «Додати акаунт».
    );
    if (user.memberKey) {
      this.me.member(user.memberKey).then(
        (member) => this.member.set(member),
        () => undefined, // The row keeps the email.
      );
    }
    // An admin outside a kurin decides on applications (web sidebar-menu): the «Заявки» dot.
    if (user.isAdmin && !user.kurinKey) this.waitlist.refreshPending();
    if (user.kurinKey && kurinAccess(user).seeGroupDues) {
      this.kurinApi.duesGroups(user.kurinKey).then(
        (groups) => this.duesGroups.set(groups),
        () => undefined, // As on the web: a failed read hides the item.
      );
    }
  }

  protected async go(entry: MenuEntry): Promise<void> {
    if (entry.link) {
      await this.open(entry.link);
      return;
    }
    openExternal(entry.help ? helpUrl() : webOnlyPage(entry.web ?? '/'));
    if (this.drawer()) await this.menus.close();
  }

  /** The web instead of this app, remembered (web-links fullVersionUrl): a tab goes there, the home-screen app opens it. */
  protected openFullVersion(): void {
    const url = fullVersionUrl();
    if (this.install.standalone()) openExternal(url);
    else globalThis.location.assign(url);
  }

  protected async open(link: string): Promise<void> {
    if (this.drawer()) await this.menus.close();
    await this.router.navigateByUrl(link);
  }

  /** The switch flips at once; it is set back to what the device really has once the request settles. */
  protected async togglePush(event: Event): Promise<void> {
    const toggle = event.target as HTMLIonToggleElement;
    const wanted = (event as CustomEvent<{ checked: boolean }>).detail.checked;
    try {
      if (wanted) await this.push.enable();
      else await this.push.disable();
    } catch (error) {
      const fallback = wanted ? 'Не вдалося увімкнути сповіщення.' : 'Не вдалося вимкнути сповіщення.';
      await this.toasts.show(apiErrorText(error, fallback), 'danger');
    }
    toggle.checked = this.push.state() === 'on';
  }

  /**
   * The app starts afresh in the other account: every screen and service holds what it read for
   * this one, and a reload is the one way to drop all of it.
   */
  protected async switchTo(account: SavedAccount): Promise<void> {
    if (this.switching()) return;
    this.switching.set(account.userKey);
    try {
      await this.auth.switchAccount(account.userKey);
      globalThis.location.assign(new URL('tabs/home', document.baseURI).href);
    } catch (error) {
      this.switching.set(null);
      await this.toasts.show(apiErrorText(error, 'Не вдалося перейти в цей акаунт. Увійди в нього ще раз.'), 'danger');
      this.auth.savedAccounts().then(
        (accounts) => this.savedAccounts.set(accounts),
        () => undefined,
      );
    }
  }

  /**
   * «Моя картка» (web card-check-dialog and transfer-code-dialog): the QR to be checked by, or the
   * transfer code the person hands to the Звʼязковий of the kurin that takes them in.
   */
  protected async openCard(part: 'check' | 'transfer'): Promise<void> {
    if (this.drawer()) await this.menus.close();
    await this.router.navigate(['/tabs/more/card'], { queryParams: { part } });
  }

  protected async openScan(): Promise<void> {
    if (this.drawer()) await this.menus.close();
    await this.router.navigateByUrl('/tabs/more/scan');
  }

  protected async addAccount(): Promise<void> {
    if (this.drawer()) await this.menus.close();
    await this.router.navigateByUrl('/login?add=1');
  }

  /** As iOS Settings does: a sheet with the destructive action and a way back. */
  protected async confirmLogout(): Promise<void> {
    const sheet = await this.sheets.create({
      header: this.savedAccounts().length
        ? 'Вийти з усіх акаунтів на цьому пристрої?'
        : 'Вийти з акаунта на цьому пристрої?',
      buttons: [
        { text: 'Вийти', role: 'destructive', data: 'leave' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data !== 'leave') return;
    this.leaving.set(true);
    if (this.drawer()) await this.menus.close();
    // While the token still works: the server forgets this device, so the next person here gets none of these pushes.
    await this.push.forgetDevice();
    await this.auth.logout();
    this.leaving.set(false);
    await this.router.navigateByUrl('/login', { replaceUrl: true });
  }
}
