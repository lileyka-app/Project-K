import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { ToggleSwitchModule, ToggleSwitchChangeEvent } from '@openng/optimus-ui/toggleswitch';
import { ButtonModule } from '@openng/optimus-ui/button';
import { FormsModule } from '@angular/forms';
import { MessageService } from '@openng/optimus-ui/api';

const ENFORCE_PRIVILEGED_MFA_KEY = 'Security__EnforcePrivilegedMFA';

@Component({
  selector: 'app-system-settings',
  imports: [ToggleSwitchModule, ButtonModule, FormsModule],
  templateUrl: './system-settings.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './system-settings.css'
})
export class SystemSettingsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);
  private readonly apiUrl = environment.apiUrl;

  enforcePrivilegedMfa = false;
  loading = false;

  ngOnInit() {
    this.loadSettings();
  }

  loadSettings() {
    this.loading = true;
    this.http.get<Record<string, string>>(`${this.apiUrl}/settings`, { withCredentials: true }).subscribe({
      next: (settings) => {
        this.enforcePrivilegedMfa = settings?.[ENFORCE_PRIVILEGED_MFA_KEY] === 'true';
        this.loading = false;
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося завантажити налаштування' });
        this.loading = false;
      }
    });
  }

  onMfaToggle(event: ToggleSwitchChangeEvent) {
    this.loading = true;
    const value = event.checked ? 'true' : 'false';
    this.http.put(`${this.apiUrl}/settings/${ENFORCE_PRIVILEGED_MFA_KEY}`, { value }, { withCredentials: true }).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Збережено', detail: 'Налаштування оновлено' });
        this.loading = false;
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося оновити налаштування' });
        this.enforcePrivilegedMfa = !event.checked;
        this.loading = false;
      }
    });
  }
}
