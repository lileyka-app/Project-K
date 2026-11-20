import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { map, Observable, of } from 'rxjs';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { KurinCardFieldDto } from '../../models/card-fields.dto';

/**
 * Which card fields the kurin leaves off. Only what its край lets go is listed: a field the край
 * requires stays on every card. Saved by the settings page's own «Зберегти».
 */
@Component({
  selector: 'app-kurin-card-fields',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ToggleSwitchModule],
  templateUrl: './kurin-card-fields.html',
  styleUrl: '../agenda-archive-policy/agenda-archive-policy.css'
})
export class KurinCardFieldsComponent implements OnInit {
  private readonly kurins = inject(KurinService);

  readonly kurinKey = input.required<string>();

  protected readonly loaded = signal(false);
  protected readonly fields = signal<KurinCardFieldDto[]>([]);
  /** Which fields the server hides, as it last answered. */
  private readonly saved = signal<KurinCardFieldDto[]>([]);
  /** How many fields are switched differently from what the server has. */
  readonly changeCount = computed(() => {
    const saved = new Map(this.saved().map(field => [field.key, field.hidden]));
    return this.fields().filter(field => saved.get(field.key) !== field.hidden).length;
  });

  ngOnInit(): void {
    this.kurins.getHideableCardFields(this.kurinKey()).subscribe({
      next: fields => {
        this.fields.set(fields);
        this.saved.set(fields);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true)
    });
  }

  protected setShown(key: string, shown: boolean): void {
    this.fields.update(fields => fields.map(field => field.key === key ? { ...field, hidden: !shown } : field));
  }

  /** Saves the choice; with nothing to choose from there is nothing to send. */
  save(): Observable<void> {
    if (!this.loaded() || this.fields().length === 0) {
      return of(undefined);
    }
    const hidden = this.fields().filter(field => field.hidden).map(field => field.key);
    return this.kurins.setHiddenCardFields(this.kurinKey(), hidden).pipe(map(fields => {
      this.fields.set(fields);
      this.saved.set(fields);
    }));
  }
}
