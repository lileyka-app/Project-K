import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { map, Observable, throwError } from 'rxjs';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { countChanges } from '../../../../shared/functions/count-changes.function';

/**
 * How long a kurin's done tasks stay on the board, and how long its archive is kept. Deleting for
 * good is the kurin's call, so each step can be switched off. Saved by the settings page's own
 * «Зберегти», together with the rest of the kurin's settings.
 */
@Component({
  selector: 'app-agenda-archive-policy',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, InputTextModule, ToggleSwitchModule],
  templateUrl: './agenda-archive-policy.html',
  styleUrl: './agenda-archive-policy.css'
})
export class AgendaArchivePolicyComponent implements OnInit {
  private readonly agenda = inject(AgendaService);

  readonly kurinKey = input.required<string>();

  protected readonly loaded = signal(false);
  protected autoArchive = true;
  protected autoArchiveDays = 30;
  protected purge = true;
  protected purgeDays = 365;
  /** The terms as the server last had them. */
  private baseline: object | null = null;

  ngOnInit(): void {
    this.agenda.getArchivePolicy(this.kurinKey()).subscribe({
      next: policy => {
        this.autoArchive = policy.autoArchiveAfterDays !== null;
        this.autoArchiveDays = policy.autoArchiveAfterDays ?? 30;
        this.purge = policy.purgeAfterDays !== null;
        this.purgeDays = policy.purgeAfterDays ?? 365;
        this.baseline = this.onScreen();
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true)
    });
  }

  /** How many terms differ from what the server has. */
  changeCount(): number {
    return this.loaded() ? countChanges(this.baseline, this.onScreen()) : 0;
  }

  private onScreen(): object {
    return { autoArchive: this.autoArchive, autoArchiveDays: this.autoArchiveDays, purge: this.purge, purgeDays: this.purgeDays };
  }

  protected valid(): boolean {
    const inRange = (days: number) => Number.isInteger(days) && days >= 1 && days <= 3650;
    return (!this.autoArchive || inRange(this.autoArchiveDays)) && (!this.purge || inRange(this.purgeDays));
  }

  /** The policy as it would be saved, or null while it is not loaded or a term is out of range. */
  pending(): { autoArchiveAfterDays: number | null; purgeAfterDays: number | null } | null {
    if (!this.loaded() || !this.valid()) {
      return null;
    }
    return {
      autoArchiveAfterDays: this.autoArchive ? this.autoArchiveDays : null,
      purgeAfterDays: this.purge ? this.purgeDays : null
    };
  }

  /** Saves the policy; the page that holds it says how the save went. */
  save(): Observable<void> {
    const policy = this.pending();
    if (!policy) {
      return throwError(() => new Error('Строк — від 1 до 3650 днів'));
    }
    return this.agenda.setArchivePolicy(this.kurinKey(), policy).pipe(map(() => {
      this.baseline = this.onScreen();
    }));
  }
}
