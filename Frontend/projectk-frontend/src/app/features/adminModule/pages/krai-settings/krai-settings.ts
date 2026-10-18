import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SelectModule } from '@openng/optimus-ui/select';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { MessageService } from '@openng/optimus-ui/api';
import { KurinService } from '../../../kurinModule/services/kurin-service/kurin.service';
import { BadgesCatalogService } from '../../../kurinModule/services/probes-and-badges/badges-catalog.service';
import { KraiDto } from '../../../kurinModule/models/krai.dto';
import { BadgeCatalogItemDto } from '../../../kurinModule/models/probes-and-badges/badge-catalog-item.dto';
import { KraiProfileService } from '../../services/krai-profile-service/krai-profile.service';
import { KraiCardFieldsDocument, KraiProfileDocument, KraiProfileEditorDto, KraiProfileRevisionDto } from '../../models/krai-profile.dto';
import { formatUtcDateTime } from '../../../../shared/functions/utc-date-time.function';
import { KraiCardFieldsComponent, sameCardFields } from './krai-card-fields/krai-card-fields';
import { currencySymbol, DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE } from '../../../duesModule/functions/dues-format.function';
import { DUES_UPSTREAM_LABELS, DuesUpstream } from '../../../duesModule/models/dues.enums';

interface SourceOption {
  code: string;
  label: string;
}

const PAGE_SIZE = 40;

/** What a край without its own setting uses: how Lilejka worked before profiles. */
const DEFAULT_TIME_ZONE = 'Europe/Kyiv';

function supported(key: 'currency' | 'timeZone'): string[] {
  try {
    return Intl.supportedValuesOf(key);
  } catch {
    return [];
  }
}

const CURRENCIES = supported('currency');
const TIME_ZONES = supported('timeZone');

/** Number formats a край is likely to want; one saved by hand outside this list is kept as an option. */
const NUMBER_LOCALES = [
  'uk-UA', 'en-US', 'en-GB', 'en-CA', 'fr-CA', 'en-AU', 'de-DE', 'de-AT', 'de-CH', 'cs-CZ', 'sk-SK', 'pl-PL',
  'fr-FR', 'it-IT', 'es-ES', 'pt-BR', 'nl-NL', 'ro-RO', 'hu-HU', 'lt-LT', 'lv-LV', 'et-EE', 'sv-SE', 'nb-NO', 'da-DK'
];

/** «1 234,5» as the locale writes it, so the admin picks by what they see. */
function sample(locale: string): string {
  try {
    return new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(1234.5);
  } catch {
    return '';
  }
}

/**
 * Профіль краю: з яких країв курені бачать вмілості і які з них сховано. Кожне збереження —
 * нова ревізія; історія і є «скасувати», тож відновлення й імпорт теж просто додають ревізію.
 */
@Component({
  selector: 'app-krai-settings',
  imports: [FormsModule, ButtonModule, SelectModule, MultiSelectModule, InputTextModule, ToggleSwitchModule, KraiCardFieldsComponent],
  templateUrl: './krai-settings.html',
  styleUrl: './krai-settings.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KraiSettingsComponent implements OnInit {
  private readonly kurinService = inject(KurinService);
  private readonly badgesCatalog = inject(BadgesCatalogService);
  private readonly profiles = inject(KraiProfileService);
  private readonly messages = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly krais = signal<KraiDto[]>([]);
  readonly kraiCode = signal<string | null>(null);
  readonly editor = signal<KraiProfileEditorDto | null>(null);
  readonly revisions = signal<KraiProfileRevisionDto[]>([]);
  readonly badges = signal<BadgeCatalogItemDto[]>([]);

  readonly sources = signal<string[]>([]);
  readonly hidden = signal<ReadonlySet<string>>(new Set());
  readonly currency = signal<string | null>(null);
  readonly timeZone = signal<string | null>(null);
  readonly numberLocale = signal<string | null>(null);
  readonly upstream = signal<DuesUpstream>(DuesUpstream.Stanytsia);
  readonly upstreamOptions = Object.values(DuesUpstream).map(value => ({ value, label: DUES_UPSTREAM_LABELS[value] }));
  readonly defaultNumberLocale = DEFAULT_NUMBER_LOCALE;
  readonly cardFields = signal<KraiCardFieldsDocument | undefined>(undefined);
  readonly note = signal('');

  readonly defaultCurrency = DEFAULT_CURRENCY;
  readonly defaultTimeZone = DEFAULT_TIME_ZONE;
  readonly currencyOptions = computed<SourceOption[]>(() =>
    withValue(CURRENCIES, this.currency()).map(code => ({ code, label: `${code} · ${currencySymbol(code)}` })));
  readonly timeZoneOptions = computed<SourceOption[]>(() =>
    withValue(TIME_ZONES, this.timeZone()).map(code => ({ code, label: code })));
  readonly numberLocaleOptions = computed<SourceOption[]>(() =>
    withValue(NUMBER_LOCALES, this.numberLocale()).map(code => ({ code, label: `${code} · ${sample(code)}` })));
  readonly search = signal('');
  readonly shown = signal(PAGE_SIZE);

  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly sourceOptions = computed<SourceOption[]>(() => {
    const byCode = new Map<string, SourceOption>();
    for (const badge of this.badges()) {
      const code = badge.kraiCode;
      if (code && !byCode.has(code)) {
        byCode.set(code, { code, label: badge.country ? `${badge.country} (${code})` : code });
      }
    }
    for (const code of this.sources()) {
      if (!byCode.has(code)) {
        byCode.set(code, { code, label: code });
      }
    }
    return [...byCode.values()].sort((a, b) => a.label.localeCompare(b.label, 'uk'));
  });

  /** Порожній вибір — це «свій край», як і на сервері. */
  readonly effectiveSources = computed(() => {
    const chosen = this.sources();
    if (chosen.length) {
      return chosen;
    }
    const own = this.kraiCode();
    return own ? [own] : [];
  });

  readonly sourceBadges = computed(() => {
    const sources = new Set(this.effectiveSources());
    return this.badges()
      .filter(badge => !!badge.kraiCode && sources.has(badge.kraiCode))
      .sort((a, b) => a.title.localeCompare(b.title, 'uk'));
  });

  readonly filteredBadges = computed(() => {
    const query = this.search().trim().toLowerCase();
    const all = this.sourceBadges();
    return query ? all.filter(badge => badge.title.toLowerCase().includes(query)) : all;
  });

  readonly visibleBadges = computed(() => this.filteredBadges().slice(0, this.shown()));

  readonly offeredCount = computed(() => {
    const hidden = this.hidden();
    return this.sourceBadges().filter(badge => !hidden.has(badge.id)).length;
  });

  readonly dirty = computed(() => {
    const editor = this.editor();
    if (!editor) {
      return false;
    }
    const saved = editor.document.catalog;
    const locale = editor.document.locale;
    return !sameSet(saved?.badgeSources ?? [], this.sources())
      || !sameSet(saved?.hiddenBadges ?? [], [...this.hidden()])
      || (locale?.currency ?? null) !== this.currency()
      || (locale?.timeZone ?? null) !== this.timeZone()
      || (locale?.numberLocale ?? null) !== this.numberLocale()
      || (editor.document.dues?.upstream ?? DuesUpstream.Stanytsia) !== this.upstream()
      || !sameCardFields(editor.document.cardFields, this.cardFields());
  });

  ngOnInit(): void {
    this.loading.set(true);
    forkJoin({
      krais: this.kurinService.getKrais(),
      badges: this.badgesCatalog.getAll(1000)
    }).subscribe({
      next: ({ krais, badges }) => {
        this.krais.set(krais);
        this.badges.set(badges);
        const wanted = this.route.snapshot.queryParamMap.get('code');
        const first = krais.find(krai => krai.code === wanted) ?? krais[0];
        if (first) {
          this.select(first.code);
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.loading.set(false);
        this.fail('Не вдалося завантажити краї.');
      }
    });
  }

  back(): void {
    this.router.navigate(['/panel']);
  }

  select(code: string): void {
    this.kraiCode.set(code);
    this.router.navigate([], { relativeTo: this.route, queryParams: { code }, replaceUrl: true });
    this.loading.set(true);
    forkJoin({ editor: this.profiles.get(code), revisions: this.profiles.revisions(code) }).subscribe({
      next: ({ editor, revisions }) => {
        this.apply(editor);
        this.revisions.set(revisions);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.fail('Не вдалося завантажити профіль краю.');
      }
    });
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.shown.set(PAGE_SIZE);
  }

  showMore(): void {
    this.shown.update(count => count + PAGE_SIZE);
  }

  isOffered(badgeId: string): boolean {
    return !this.hidden().has(badgeId);
  }

  setOffered(badgeId: string, offered: boolean): void {
    const next = new Set(this.hidden());
    if (offered) {
      next.delete(badgeId);
    } else {
      next.add(badgeId);
    }
    this.hidden.set(next);
  }

  reset(): void {
    const editor = this.editor();
    if (editor) {
      this.apply(editor);
    }
  }

  save(): void {
    const editor = this.editor();
    if (!editor) {
      return;
    }

    const document: KraiProfileDocument = {
      ...editor.document,
      catalog: {
        badgeSources: this.sources().length ? this.sources() : undefined,
        hiddenBadges: this.hidden().size ? [...this.hidden()] : undefined
      },
      locale: {
        ...editor.document.locale,
        currency: this.currency() ?? undefined,
        timeZone: this.timeZone() ?? undefined,
        numberLocale: this.numberLocale() ?? undefined
      },
      dues: this.upstream() === DuesUpstream.Stanytsia && !editor.document.dues
        ? undefined
        : { ...editor.document.dues, upstream: this.upstream() === DuesUpstream.Stanytsia ? undefined : this.upstream() },
      cardFields: this.cardFields()
    };
    this.write(this.profiles.save(editor.kraiCode, editor.revision, document, this.note().trim() || null), 'Профіль збережено.');
  }

  restore(revision: KraiProfileRevisionDto): void {
    const code = this.kraiCode();
    if (code) {
      this.write(this.profiles.restore(code, revision.revision), `Ревізію ${revision.revision} відновлено.`);
    }
  }

  exportProfile(): void {
    const editor = this.editor();
    if (!editor) {
      return;
    }
    const blob = new Blob([JSON.stringify(editor.document, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `profil-kraiu-${editor.kraiCode}-r${editor.revision}.json`;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  importProfile(input: HTMLInputElement): void {
    const file = input.files?.[0];
    const editor = this.editor();
    input.value = '';
    if (!file || !editor) {
      return;
    }

    file.text().then(text => {
      let parsed: unknown;
      try {
        parsed = JSON.parse(text);
      } catch {
        this.fail('Файл не є JSON.');
        return;
      }
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
        this.fail('У файлі немає профілю краю.');
        return;
      }
      this.write(
        this.profiles.save(editor.kraiCode, editor.revision, parsed as KraiProfileDocument, `Імпорт з файлу ${file.name}`),
        'Профіль імпортовано.'
      );
    }).catch(() => this.fail('Не вдалося прочитати файл.'));
  }

  revisionLabel(revision: KraiProfileRevisionDto): string {
    const when = formatUtcDateTime(revision.createdAtUtc) ?? '';
    return [when, revision.createdByName].filter(Boolean).join(' · ');
  }

  private write(request: ReturnType<KraiProfileService['save']>, success: string): void {
    const code = this.kraiCode();
    this.saving.set(true);
    request.subscribe({
      next: editor => {
        this.apply(editor);
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: 'Збережено', detail: success });
        if (code) {
          this.profiles.revisions(code).subscribe({ next: revisions => this.revisions.set(revisions) });
        }
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.fail(serverMessage(error) ?? 'Не вдалося зберегти профіль.');
      }
    });
  }

  private apply(editor: KraiProfileEditorDto): void {
    this.editor.set(editor);
    this.sources.set([...(editor.document.catalog?.badgeSources ?? [])]);
    this.hidden.set(new Set(editor.document.catalog?.hiddenBadges ?? []));
    this.currency.set(editor.document.locale?.currency ?? null);
    this.timeZone.set(editor.document.locale?.timeZone ?? null);
    this.numberLocale.set(editor.document.locale?.numberLocale ?? null);
    this.upstream.set(editor.document.dues?.upstream ?? DuesUpstream.Stanytsia);
    this.cardFields.set(editor.document.cardFields);
    this.note.set('');
  }

  private fail(detail: string): void {
    this.messages.add({ severity: 'error', summary: 'Помилка', detail });
  }
}

/** The list with the saved value in it, so a value this browser does not know still shows. */
function withValue(values: string[], value: string | null): string[] {
  return value && !values.includes(value) ? [value, ...values] : values;
}

function sameSet(left: readonly string[], right: readonly string[]): boolean {
  const a = new Set(left);
  const b = new Set(right);
  return a.size === b.size && [...a].every(value => b.has(value));
}

/** Сервер пояснює, що не так із профілем (400) чи що його змінили (409), українською. */
function serverMessage(error: unknown): string | null {
  if (error instanceof HttpErrorResponse && (error.status === 400 || error.status === 409)) {
    const message = (error.error as { message?: unknown } | null)?.message;
    return typeof message === 'string' && message ? message : null;
  }
  return null;
}
