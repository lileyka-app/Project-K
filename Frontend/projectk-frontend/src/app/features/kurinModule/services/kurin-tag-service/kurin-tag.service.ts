import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';
import { KurinTagDto, KurinTagSettingsDto, KurinTagsDto, NO_KURIN_TAGS } from '../../models/kurin-tag.dto';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';

/**
 * Теги куреня, у якому людина зараз діє. Тримає їх одним сигналом, щоб кожен рядок реєстру й
 * картка малювали теги з одного довідника, а не питали сервер кожен окремо. На людях лежать лише
 * ключі (`MemberDto.tagKeys`), назву й колір дає цей довідник.
 */
@Injectable({ providedIn: 'root' })
export class KurinTagService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = environment.apiUrl;

  private readonly state = signal<{ kurinKey: string | null; tags: KurinTagsDto }>({ kurinKey: null, tags: NO_KURIN_TAGS });
  private loadingKurin: string | null = null;

  /** Росте з кожною зміною тегів: сторінки зі списками людей за ним перечитують склад. */
  readonly revision = signal(0);

  /** Теги поточного куреня; порожньо, поки не завантажились або коли вимкнені. */
  readonly current = computed(() => this.state().tags);
  readonly enabled = computed(() => this.current().enabled);
  readonly canManage = computed(() => this.current().enabled && this.current().canManage);
  readonly byKey = computed(() => new Map(this.current().tags.map(tag => [tag.kurinTagKey, tag])));

  /** Теги цих ключів у порядку назв; ключі, яких довідник не знає, пропускає. */
  resolve(keys: readonly string[] | null | undefined): KurinTagDto[] {
    if (!keys?.length) {
      return [];
    }
    const byKey = this.byKey();
    return keys
      .map(key => byKey.get(key))
      .filter((tag): tag is KurinTagDto => !!tag)
      .sort((a, b) => a.name.localeCompare(b.name, 'uk'));
  }

  /** Завантажує теги куреня, у якому людина діє, якщо їх ще немає. */
  ensureCurrent(): void {
    const kurinKey = this.auth.getAuthStateValue()?.kurinKey ?? null;
    if (!kurinKey || this.state().kurinKey === kurinKey || this.loadingKurin === kurinKey) {
      return;
    }
    this.load(kurinKey).subscribe({ error: () => undefined });
  }

  load(kurinKey: string): Observable<KurinTagsDto> {
    this.loadingKurin = kurinKey;
    return this.http.get<KurinTagsDto>(`${this.apiUrl}/kurin/${kurinKey}/tags`, { context: requestFeedback('silent') }).pipe(
      tap({
        next: tags => {
          this.state.set({ kurinKey, tags });
          this.loadingKurin = null;
        },
        error: () => (this.loadingKurin = null)
      })
    );
  }

  createTag(kurinKey: string, name: string, colorHex: string): Observable<KurinTagDto> {
    return this.http.post<KurinTagDto>(`${this.apiUrl}/kurin/${kurinKey}/tags`, { name, colorHex }, this.handled())
      .pipe(tap(() => this.changed(kurinKey)));
  }

  updateTag(kurinKey: string, tagKey: string, name: string, colorHex: string): Observable<KurinTagDto> {
    return this.http.put<KurinTagDto>(`${this.apiUrl}/kurin/${kurinKey}/tags/${tagKey}`, { name, colorHex }, this.handled())
      .pipe(tap(() => this.changed(kurinKey)));
  }

  deleteTag(kurinKey: string, tagKey: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/kurin/${kurinKey}/tags/${tagKey}`)
      .pipe(tap(() => this.changed(kurinKey)));
  }

  /** Робить цих людей саме тими, хто має тег. */
  setTagMembers(kurinKey: string, tagKey: string, memberKeys: string[]): Observable<KurinTagDto> {
    return this.http.put<KurinTagDto>(`${this.apiUrl}/kurin/${kurinKey}/tags/${tagKey}/members`, { memberKeys }, this.handled())
      .pipe(tap(() => this.changed(kurinKey)));
  }

  /** Робить ці теги саме тими, що має людина в курені, де діє той, хто ставить. */
  setMemberTags(memberKey: string, tagKeys: string[]): Observable<string[]> {
    return this.http.put<string[]>(`${this.apiUrl}/member/${memberKey}/tags`, { tagKeys }, this.handled())
      .pipe(tap(() => {
        const kurinKey = this.state().kurinKey;
        if (kurinKey) {
          this.changed(kurinKey);
        }
      }));
  }

  getSettings(kurinKey: string): Observable<KurinTagSettingsDto> {
    return this.http.get<KurinTagSettingsDto>(`${this.apiUrl}/kurin/${kurinKey}/tag-settings`);
  }

  setSettings(kurinKey: string, settings: Pick<KurinTagSettingsDto, 'enabled' | 'ownTagsVisible'>): Observable<KurinTagSettingsDto> {
    return this.http.put<KurinTagSettingsDto>(`${this.apiUrl}/kurin/${kurinKey}/tag-settings`, settings)
      .pipe(tap(() => this.changed(kurinKey)));
  }

  // 400 і 409 пояснює сама сторінка: ліміт, зайнята назва, людина поза куренем.
  private handled() {
    return { context: requestFeedback('auto', [400, 409]) };
  }

  // Теги лежать і на людях у списках, тож разом із довідником застаріває й кеш людей.
  private changed(kurinKey: string): void {
    this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
    this.revision.update(value => value + 1);
    if (this.state().kurinKey === kurinKey) {
      this.load(kurinKey).subscribe({ error: () => undefined });
    }
  }
}
