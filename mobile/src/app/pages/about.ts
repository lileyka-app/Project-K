import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { Haptics, ImpactStyle } from '@capacitor/haptics';
import {
  IonBackButton,
  IonBadge,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardTitle,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonTitle,
  IonToolbar,
  getPlatforms,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  book,
  bulb,
  calendar,
  calendarNumber,
  codeSlash,
  colorPalette,
  flag,
  gitNetwork,
  globe,
  home,
  lockClosed,
  logoGithub,
  openOutline,
  pricetag,
  refresh,
  shieldCheckmark,
  star,
} from 'ionicons/icons';
import { environment } from '../../environments/environment';
import { versionLabel } from '../features/account/account.labels';
import { AccountService } from '../features/account/account.service';
import { InstallService } from '../pwa/install.service';
import { apiUrl } from '../runtime-config';

/** A git tag as it is spelled in the repository, plus the code name the release carried, if any. */
interface ProjectRelease {
  tag: string;
  codeName?: string;
}

/** One turn of the project's story. */
interface ProjectMilestone {
  when: string;
  title: string;
  body: string;
  releases?: ProjectRelease[];
  icon: string;
}

/**
 * «Про Лілейку»: who made it and why, the web's about-page word for word (the licence requires it
 * to be shown unchanged, with the attribution), the app's and the API's versions, and under them
 * the technical details of this install, kept for support and for the smoke tests.
 */
@Component({
  selector: 'app-about',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardContent,
    IonButton,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonNote,
    IonIcon,
    IonBadge,
    RouterLink,
  ],
  styles: `
    .hero {
      padding: 8px 20px 4px;
      text-align: center;
    }
    .hero img {
      width: 112px;
      height: 112px;
      border-radius: 50%;
      object-fit: cover;
      margin: 8px auto 12px;
      display: block;
    }
    .kicker {
      margin: 8px 0 4px;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: var(--lk-primary);
    }
    .hero h1 {
      margin: 0 0 12px;
      font-size: 24px;
      line-height: 30px;
      font-weight: 800;
      color: var(--lk-ink);
    }
    .hero p,
    .prose p {
      margin: 0 0 12px;
      font-size: 16px;
      line-height: 24px;
      color: var(--lk-ink-soft);
    }
    .hero p strong {
      color: var(--lk-ink);
    }
    .prose {
      padding: 0 20px;
      max-width: 640px;
      margin: 0 auto;
    }
    .prose h2 {
      margin: 24px 0 8px;
      font-size: 20px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .when {
      margin: 0 0 2px;
      font-size: 13px;
      font-weight: 700;
      color: var(--lk-primary);
    }
    .milestone h3 {
      font-weight: 700;
      color: var(--lk-ink);
    }
    .milestone p {
      white-space: normal;
    }
    .releases {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: 8px;
    }
    .releases ion-badge {
      font-weight: 600;
    }
    ion-item > ion-icon[slot='start']:not(.lk-tile) {
      color: var(--lk-primary);
    }
    .external {
      font-size: 18px;
      color: var(--lk-faint);
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/more" text="Меню" /></ion-buttons>
        <ion-title>Про застосунок</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      <section class="hero">
        @if (!photoMissing()) {
          <img src="/assets/images/author-rostyslav.jpg" alt="Ростислав Муха" width="112" height="112" (error)="photoMissing.set(true)" />
        }
        <p class="kicker">Про систему</p>
        <h1>Лілейка: щоб курінь був під рукою, а не в десяти таблицях</h1>
        <p>
          Систему створив <strong>Ростислав Муха</strong>, Full-Stack .NET Software Engineer, член станиці Тернопіль. Один
          автор, відкритий код і три роки від ідеї до першого живого куреня.
        </p>
      </section>

      <ion-list [inset]="true">
        <ion-item-group>
          <ion-item [href]="links.site" target="_blank" rel="noopener" [detail]="false">
            <ion-icon slot="start" name="globe" aria-hidden="true" />
            <ion-label>rostyslav-mukha.dev</ion-label>
            <ion-icon class="external" slot="end" name="open-outline" aria-hidden="true" />
          </ion-item>
          <ion-item [href]="links.github" target="_blank" rel="noopener" [detail]="false">
            <ion-icon slot="start" name="logo-github" aria-hidden="true" />
            <ion-label>GitHub</ion-label>
            <ion-icon class="external" slot="end" name="open-outline" aria-hidden="true" />
          </ion-item>
          <ion-item [href]="links.releases" target="_blank" rel="noopener" [detail]="false">
            <ion-icon slot="start" name="pricetag" aria-hidden="true" />
            <ion-label>Релізи</ion-label>
            <ion-icon class="external" slot="end" name="open-outline" aria-hidden="true" />
          </ion-item>
          <ion-item [button]="true" [detail]="true" routerLink="/tabs/more/privacy">
            <ion-icon slot="start" name="lock-closed" aria-hidden="true" />
            <ion-label>Конфіденційність</ion-label>
          </ion-item>
        </ion-item-group>
      </ion-list>

      <div class="prose">
        <h2>Ідея</h2>
        <p>
          Ідея зʼявилася 2023 року як студентський проєкт для тренування. Але проблема в ньому була справжня: усе
          важливе для станиці, куреня чи гуртка лежить у хаотично розкиданих файлах, таблицях і месенджерах. Склад в
          одному місці, проби в іншому, історія відзначень у чиїйсь памʼяті.
        </p>
        <p>
          Пластова система для прихильника цифровізації одразу стала цікавим челенджем: не ще один сервіс, а
          інструмент, який курінь відкриває на сходинах і не закриває роками.
        </p>

        <h2>Для кого</h2>
        <p>
          Для щоденного використання в гуртку й курені. Для юнака, який бачить свою пробу й вмілості. Для впорядника,
          який підписує точки і веде гурток. Для звʼязкового, який тримає реєстр, календар і планування всього куреня.
        </p>
        <p>
          Зручно і безпечно однаково: двофакторна автентифікація для Звʼязкового й адміністратора, права з ролей у
          курені, а не з налаштувань, і можливість поставити систему на власний сервер.
        </p>

        <h2>Історія</h2>
        <p>
          Розробка йде з вересня 2024 року, спершу як монолітний застосунок на .NET з Razor Pages. Архітектура кілька
          разів переписувалася, і в серпні 2025 року система була переписана з нуля: ASP.NET Core API і Angular. Далі
          альфа, бета, а у вересні 2026 — перша стабільна версія 1.0.
        </p>
      </div>

      <ion-list [inset]="true" data-testid="milestones">
        <ion-item-group>
          @for (milestone of milestones; track milestone.title) {
            <ion-item class="milestone">
              <ion-icon slot="start" [name]="milestone.icon" aria-hidden="true" />
              <ion-label class="ion-text-wrap">
                <p class="when">{{ milestone.when }}</p>
                <h3>{{ milestone.title }}</h3>
                <p>{{ milestone.body }}</p>
                @if (milestone.releases?.length) {
                  <div class="releases">
                    @for (release of milestone.releases; track release.tag) {
                      <ion-badge class="lk-tag--secondary">{{ releaseLabel(release) }}</ion-badge>
                    }
                  </div>
                }
              </ion-label>
            </ion-item>
          }
        </ion-item-group>
      </ion-list>

      <ion-list [inset]="true">
        <ion-list-header><ion-label>Версія</ion-label></ion-list-header>
        <ion-item-group>
          <ion-item>
            <ion-label>Застосунок</ion-label>
            <ion-note slot="end" data-testid="app-version">{{ appVersion }}</ion-note>
          </ion-item>
          @if (apiVersion(); as version) {
            <ion-item>
              <ion-label>API</ion-label>
              <ion-note slot="end" data-testid="api-version">{{ version }}</ion-note>
            </ion-item>
          }
          <ion-item [href]="links.releases" target="_blank" rel="noopener" [detail]="false">
            <ion-label>Що змінилось у кожній версії</ion-label>
            <ion-icon class="external" slot="end" name="open-outline" aria-hidden="true" />
          </ion-item>
        </ion-item-group>
      </ion-list>

      <ion-list [inset]="true">
        <ion-list-header><ion-label>Технічні деталі</ion-label></ion-list-header>
        <ion-item-group>
          <ion-item>
            <ion-label>Версія</ion-label>
            <ion-note slot="end">{{ version }}</ion-note>
          </ion-item>
          <ion-item>
            <ion-label>Відкрито як</ion-label>
            <ion-note slot="end">{{ standalone() ? 'застосунок' : 'вкладка браузера' }}</ion-note>
          </ion-item>
          <ion-item>
            <ion-label>Режим Ionic</ion-label>
            <ion-note slot="end">{{ mode }}</ion-note>
          </ion-item>
          <ion-item>
            <ion-label>Платформа Capacitor</ion-label>
            <ion-note slot="end">{{ nativePlatform }}</ion-note>
          </ion-item>
          <ion-item>
            <ion-label class="ion-text-wrap">
              <h3>Платформи Ionic</h3>
              <p>{{ platforms }}</p>
            </ion-label>
          </ion-item>
          <ion-item>
            <ion-label class="ion-text-wrap">
              <h3>Сервер</h3>
              <p>{{ apiUrl }}</p>
            </ion-label>
          </ion-item>
        </ion-item-group>
      </ion-list>

      <ion-card>
        <ion-card-header>
          <ion-card-title>Перевірка оболонки</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <p>Натиснуто: {{ taps() }} · подвоєно: {{ doubled() }}</p>
          <ion-button expand="block" fill="outline" (click)="tap()">Натиснути</ion-button>
        </ion-card-content>
      </ion-card>
    </ion-content>
  `,
})
export class AboutPage implements OnInit {
  private readonly account = inject(AccountService);

  protected readonly taps = signal(0);
  protected readonly doubled = computed(() => this.taps() * 2);
  protected readonly photoMissing = signal(false);
  protected readonly apiVersion = signal<string | null>(null);

  protected readonly mode = document.documentElement.getAttribute('mode') ?? '?';
  protected readonly nativePlatform = Capacitor.getPlatform();
  protected readonly platforms = getPlatforms().join(', ');
  protected readonly apiUrl = apiUrl();
  protected readonly version = environment.version;
  protected readonly appVersion = versionLabel(environment.version, environment.codename);
  protected readonly standalone = inject(InstallService).standalone;

  protected readonly links = {
    site: 'https://rostyslav-mukha.dev',
    github: 'https://github.com/lileyka-app/Project-K',
    releases: 'https://github.com/lileyka-app/Project-K/releases',
  };

  /** The web's milestones (about-page.ts), with ionicons for its PrimeIcons. */
  protected readonly milestones: ProjectMilestone[] = [
    {
      when: '2023',
      title: 'Ідея',
      body:
        'Студентський проєкт для тренування. Проблема вже була видна: документи станиці, куреня ' +
        'й гуртка розкидані по файлах, таблицях і месенджерах, і ніхто не знає, де актуальна версія.',
      icon: 'bulb',
    },
    {
      when: 'Вересень 2024',
      title: 'Перший коміт',
      body: 'Монолітний застосунок на .NET з Razor Pages: моделі, база даних, перші сторінки реєстру.',
      icon: 'code-slash',
    },
    {
      when: 'Травень 2025',
      title: 'Перші DevLog-и',
      body: 'Проєкт починає вести відкритий щоденник розвитку: що зроблено, що далі, і чому саме так.',
      icon: 'book',
    },
    {
      when: 'Серпень 2025',
      title: 'Переписано з нуля',
      body: 'Бекенд стає ASP.NET Core API, фронтенд переїздить на Angular. Архітектура шарів, тести, CI.',
      releases: [{ tag: 'v0.1.0-alpha.1' }],
      icon: 'refresh',
    },
    {
      when: 'Осінь 2025',
      title: 'Вхід і права',
      body: 'JWT-автентифікація, перевірка доступу до кожної сутності, проби й вмілості, Tailwind.',
      releases: [{ tag: 'v0.3.0-alpha.1' }, { tag: 'v0.4.0-alpha.1' }, { tag: 'v0.5.0-alpha.1' }],
      icon: 'lock-closed',
    },
    {
      when: 'Грудень 2025',
      title: 'Планування таборів',
      body:
        'Система підбирає дату табору за зайнятістю виховників: перше планування, яке ' +
        'рахує машина, а не Звʼязковий у зошиті.',
      releases: [{ tag: 'v0.6.0-alpha.1' }],
      icon: 'calendar',
    },
    {
      when: 'Квітень 2026',
      title: 'Перша бета',
      body:
        'Перехід на .NET 10 і Angular 21, нова картка учасника, картковий вигляд гуртка. ' +
        'Релізи отримують мурашині кодові назви.',
      releases: [{ tag: 'v0.8.0-beta' }, { tag: 'v0.10.0-beta', codeName: 'Ant' }],
      icon: 'flag',
    },
    {
      when: 'Травень 2026',
      title: 'Безпека',
      body:
        'Двофакторна автентифікація для Звʼязкового й адміністратора, налаштування акаунта, Cloudflare, ліміти ' +
        'запитів, кешування, аудит дій.',
      releases: [
        { tag: 'v0.11.0-beta', codeName: 'Orange Ant' },
        { tag: 'v0.12.0-beta', codeName: 'Green Ant' },
        { tag: 'v0.13.0-beta', codeName: 'Queen Ant' },
      ],
      icon: 'shield-checkmark',
    },
    {
      when: 'Липень 2026',
      title: 'Лілейка і self-host',
      body:
        'Дизайн-система з власним знаком та імʼям, темна тема, і можливість розгорнути систему ' +
        'на своєму сервері одним compose-файлом.',
      releases: [
        { tag: 'v0.14.0-beta', codeName: 'Red Queen Ant' },
        { tag: 'v0.15.0-beta', codeName: 'Ant Colony' },
      ],
      icon: 'color-palette',
    },
    {
      when: 'Серпень 2026',
      title: 'Календар і задачі',
      body:
        'Спільна адженда куреня: календар і дошка задач над тими самими подіями. Скаутська лілея ' +
        'як знак, перехід на вільний форк UI-бібліотеки.',
      releases: [
        { tag: 'v0.17.0-beta', codeName: 'Leafcutter Ant' },
        { tag: 'v0.18.0-beta', codeName: 'Harvester Ant' },
      ],
      icon: 'calendar-number',
    },
    {
      when: 'Вересень 2026',
      title: 'Фундамент 1.0',
      body:
        'Ролі як джерело прав, сесії на кожен пристрій, реєстр і імпорт складу з Excel, ' +
        'людина в центрі замість куреня: один акаунт, кілька куренів.',
      releases: [
        { tag: 'v0.19.0-beta', codeName: 'Carpenter Ant' },
        { tag: 'v0.20.0-beta', codeName: 'Honeypot Ant' },
      ],
      icon: 'git-network',
    },
    {
      when: 'Вересень 2026',
      title: '1.0, реліз довіри',
      body:
        'Не нові можливості, а надійність: аудит безпеки, довідка для людей, демо ' +
        'і «Повідомити про проблему».',
      releases: [{ tag: 'v1.0' }, { tag: 'v1.0.1' }],
      icon: 'star',
    },
    {
      when: 'Жовтень 2026',
      title: '1.1: головна, вкладка, точкування',
      body:
        'Головна зі справами людини, вкладка й точкування замість зошитів. Далі задачі для гуртка, ' +
        'підготовчі курені, мобільна версія на телефоні й перемикання акаунтів.',
      releases: [{ tag: 'v1.1' }, { tag: 'v1.1.9' }],
      icon: 'home',
    },
  ];

  constructor() {
    addIcons({
      globe,
      logoGithub,
      pricetag,
      lockClosed,
      openOutline,
      bulb,
      codeSlash,
      book,
      refresh,
      calendar,
      flag,
      shieldCheckmark,
      colorPalette,
      calendarNumber,
      gitNetwork,
      star,
      home,
    });
  }

  ngOnInit(): void {
    // No API in reach is not an error here; the app's own version still shows.
    this.account.health().then(
      (health) => this.apiVersion.set(health.version ? versionLabel(health.version, health.codeName) : null),
      () => undefined,
    );
  }

  /** One spelling for every release: the tag as git knows it, then the code name when there was one. */
  protected releaseLabel(release: ProjectRelease): string {
    return release.codeName ? `${release.tag} «${release.codeName}»` : release.tag;
  }

  protected async tap(): Promise<void> {
    this.taps.update((n) => n + 1);
    // Native shells use the Taptic engine; on the web Capacitor falls back to navigator.vibrate
    // (Android browsers), and iOS Safari has no vibration API at all.
    try {
      await Haptics.impact({ style: ImpactStyle.Light });
    } catch {
      // No haptics on this device.
    }
  }
}
