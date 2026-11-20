import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { map, Observable, of } from 'rxjs';
import { KurinTagService } from '../../services/kurin-tag-service/kurin-tag.service';
import { KurinTagSettingsDto } from '../../models/kurin-tag.dto';
import { countChanges } from '../../../../shared/functions/count-changes.function';

/**
 * Чи курінь користується тегами й чи бачить людина свої. Хто бачить теги інших — рядок «Бачити
 * теги людей» у «Правах і видимості». Зберігається спільним «Зберегти» сторінки налаштувань.
 */
@Component({
  selector: 'app-kurin-tag-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ToggleSwitchModule],
  templateUrl: './kurin-tag-settings.html',
  styleUrl: '../agenda-archive-policy/agenda-archive-policy.css'
})
export class KurinTagSettingsComponent implements OnInit {
  private readonly tags = inject(KurinTagService);

  readonly kurinKey = input.required<string>();

  protected readonly loaded = signal(false);
  protected readonly settings = signal<KurinTagSettingsDto | null>(null);
  private readonly saved = signal<KurinTagSettingsDto | null>(null);
  /** How many settings differ from what the server has. */
  readonly changeCount = computed(() => countChanges(this.saved(), this.settings()));

  ngOnInit(): void {
    this.tags.getSettings(this.kurinKey()).subscribe({
      next: settings => {
        this.settings.set(settings);
        this.saved.set(settings);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true)
    });
  }

  protected set(change: Partial<KurinTagSettingsDto>): void {
    this.settings.update(settings => settings ? { ...settings, ...change } : settings);
  }

  save(): Observable<void> {
    const settings = this.settings();
    if (!settings) {
      return of(undefined);
    }
    return this.tags.setSettings(this.kurinKey(), settings).pipe(map(saved => {
      this.settings.set(saved);
      this.saved.set(saved);
    }));
  }
}
