import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { TimelineModule } from '@openng/optimus-ui/timeline';
import { environment } from '../../../../../environments/environment';
import { displayCodeName } from '../../../../shared/functions/release-code-name.function';

/** A git tag as it is spelled in the repository, plus the code name the release carried, if any. */
export interface ProjectRelease {
  tag: string;
  codeName?: string;
}

/** One turn of the project's story, as shown on the timeline. */
export interface ProjectMilestone {
  when: string;
  title: string;
  body: string;
  /** Releases that belong to this turn, shown as small tags under the text. */
  releases?: ProjectRelease[];
  icon: string;
}

/** What `/health` says about the running API; the frontend build carries its own copy. */
interface HealthResponse {
  version?: string;
  /** Null when the release was published without a code name. */
  codeName?: string | null;
}

/**
 * Who made this and why. Public: reachable from the welcome page and from the sidebar, with no
 * sign-in. The story is the author's, the dates are the repository's, and the version is the
 * running system's, so nothing here is typed by hand at release time.
 */
@Component({
  selector: 'app-about-page',
  imports: [RouterLink, ButtonModule, TagModule, TimelineModule],
  templateUrl: './about-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './about-page.css'
})
export class AboutPageComponent {
  private readonly http = inject(HttpClient);

  readonly frontendVersion = environment.version;
  readonly frontendCodeName = displayCodeName(environment.codeName);
  readonly apiVersion = signal<string | null>(null);
  readonly apiCodeName = signal<string | null>(null);
  readonly photoMissing = signal(false);

  readonly links = {
    site: 'https://rostyslav-mukha.dev',
    github: 'https://github.com/lileyka-app/Project-K',
    releases: 'https://github.com/lileyka-app/Project-K/releases'
  };

  readonly milestones: ProjectMilestone[] = [
    {
      when: '2023',
      title: 'Ідея',
      body: 'Студентський проєкт для тренування. Проблема вже була видна: документи станиці, куреня '
        + 'й гуртка розкидані по файлах, таблицях і месенджерах, і ніхто не знає, де актуальна версія.',
      icon: 'pi pi-lightbulb'
    },
    {
      when: 'Вересень 2024',
      title: 'Перший коміт',
      body: 'Монолітний застосунок на .NET з Razor Pages: моделі, база даних, перші сторінки реєстру.',
      icon: 'pi pi-code'
    },
    {
      when: 'Травень 2025',
      title: 'Перші DevLog-и',
      body: 'Проєкт починає вести відкритий щоденник розвитку: що зроблено, що далі, і чому саме так.',
      icon: 'pi pi-book'
    },
    {
      when: 'Серпень 2025',
      title: 'Переписано з нуля',
      body: 'Бекенд стає ASP.NET Core API, фронтенд переїздить на Angular. Архітектура шарів, тести, CI.',
      releases: [{ tag: 'v0.1.0-alpha.1' }],
      icon: 'pi pi-refresh'
    },
    {
      when: 'Осінь 2025',
      title: 'Вхід і права',
      body: 'JWT-автентифікація, перевірка доступу до кожної сутності, проби й вмілості, Tailwind.',
      releases: [{ tag: 'v0.3.0-alpha.1' }, { tag: 'v0.4.0-alpha.1' }, { tag: 'v0.5.0-alpha.1' }],
      icon: 'pi pi-lock'
    },
    {
      when: 'Грудень 2025',
      title: 'Планування таборів',
      body: 'Система підбирає дату табору за зайнятістю виховників: перше планування, яке '
        + 'рахує машина, а не Звʼязковий у зошиті.',
      releases: [{ tag: 'v0.6.0-alpha.1' }],
      icon: 'pi pi-calendar'
    },
    {
      when: 'Квітень 2026',
      title: 'Перша бета',
      body: 'Перехід на .NET 10 і Angular 21, нова картка учасника, картковий вигляд гуртка. '
        + 'Релізи отримують мурашині кодові назви.',
      releases: [{ tag: 'v0.8.0-beta' }, { tag: 'v0.10.0-beta', codeName: 'Ant' }],
      icon: 'pi pi-flag'
    },
    {
      when: 'Травень 2026',
      title: 'Безпека',
      body: 'Двофакторна автентифікація для Звʼязкового й адміністратора, налаштування акаунта, Cloudflare, ліміти '
        + 'запитів, кешування, аудит дій.',
      releases: [
        { tag: 'v0.11.0-beta', codeName: 'Orange Ant' },
        { tag: 'v0.12.0-beta', codeName: 'Green Ant' },
        { tag: 'v0.13.0-beta', codeName: 'Queen Ant' }
      ],
      icon: 'pi pi-shield'
    },
    {
      when: 'Липень 2026',
      title: 'Лілейка і self-host',
      body: 'Дизайн-система з власним знаком та імʼям, темна тема, і можливість розгорнути систему '
        + 'на своєму сервері одним compose-файлом.',
      releases: [{ tag: 'v0.14.0-beta', codeName: 'Red Queen Ant' }, { tag: 'v0.15.0-beta', codeName: 'Ant Colony' }],
      icon: 'pi pi-palette'
    },
    {
      when: 'Серпень 2026',
      title: 'Календар і задачі',
      body: 'Спільна адженда куреня: календар і дошка задач над тими самими подіями. Скаутська лілея '
        + 'як знак, перехід на вільний форк UI-бібліотеки.',
      releases: [{ tag: 'v0.17.0-beta', codeName: 'Leafcutter Ant' }, { tag: 'v0.18.0-beta', codeName: 'Harvester Ant' }],
      icon: 'pi pi-calendar-plus'
    },
    {
      when: 'Вересень 2026',
      title: 'Фундамент 1.0',
      body: 'Ролі як джерело прав, сесії на кожен пристрій, реєстр і імпорт складу з Excel, '
        + 'людина в центрі замість куреня: один акаунт, кілька куренів.',
      releases: [{ tag: 'v0.19.0-beta', codeName: 'Carpenter Ant' }, { tag: 'v0.20.0-beta', codeName: 'Honeypot Ant' }],
      icon: 'pi pi-sitemap'
    },
    {
      when: 'Вересень 2026',
      title: '1.0, реліз довіри',
      body: 'Не нові можливості, а надійність: аудит безпеки, довідка для людей, демо '
        + 'і «Повідомити про проблему».',
      releases: [{ tag: 'v1.0' }, { tag: 'v1.0.1' }],
      icon: 'pi pi-star'
    },
    {
      when: 'Жовтень 2026',
      title: '1.1: головна, вкладка, точкування',
      body: 'Головна зі справами людини, вкладка й точкування замість зошитів. Далі задачі для гуртка, '
        + 'підготовчі курені, мобільна версія на телефоні й перемикання акаунтів.',
      releases: [{ tag: 'v1.1' }, { tag: 'v1.1.9' }],
      icon: 'pi pi-home'
    }
  ];

  constructor() {
    this.http.get<HealthResponse>(this.healthUrl(environment.apiUrl)).subscribe({
      next: health => {
        this.apiVersion.set(health.version ?? null);
        this.apiCodeName.set(displayCodeName(health.codeName));
      },
      // No API in reach is not an error on this page; the build's own version is still shown.
      error: () => undefined
    });
  }

  /** One spelling for every release: the tag as git knows it, then the code name when there was one. */
  releaseLabel(release: ProjectRelease): string {
    return release.codeName ? `${release.tag} «${release.codeName}»` : release.tag;
  }

  /** `/health` sits beside `/api`, not under it. */
  private healthUrl(apiUrl: string): string {
    const trimmed = apiUrl.endsWith('/') ? apiUrl.slice(0, -1) : apiUrl;
    return trimmed.endsWith('/api') ? `${trimmed.slice(0, -4)}/health` : `${trimmed}/health`;
  }
}
